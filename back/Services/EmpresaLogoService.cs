using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.WebUtilities;

namespace back.Services;

public sealed class EmpresaLogoService(IServiceProvider services, ILogger<EmpresaLogoService> logger)
{
    private const int MaxBytes = 5 * 1024 * 1024;
    private static readonly HttpClient Client = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        ConnectCallback = async (context, token) =>
        {
            // Conecta ao IP validado, evitando redirecionamentos e DNS rebinding.
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token);
            var address = addresses.FirstOrDefault(IsPublicAddress)
                ?? throw new HttpRequestException("A logo deve estar em um endereco publico.");
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(address, context.DnsEndPoint.Port, token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch { socket.Dispose(); throw; }
        }
    }) { Timeout = TimeSpan.FromSeconds(5) };

    public async Task<byte[]?> CarregarAsync(int empresaId, string? logoUrl, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(logoUrl)) return null;
        try
        {
            if (logoUrl.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase) ||
                logoUrl.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase))
            {
                if (logoUrl.Length > MaxBytes * 4 / 3 + 100) return null;
                return ImageOrNull(Convert.FromBase64String(logoUrl[(logoUrl.IndexOf(',') + 1)..]));
            }
            var localPath = $"/api/documentos/empresas/{empresaId}/arquivo";
            if (logoUrl.StartsWith(localPath + "?", StringComparison.Ordinal))
            {
                var query = QueryHelpers.ParseQuery(logoUrl[localPath.Length..]);
                if (!query.TryGetValue("key", out var key) || !key.ToString().StartsWith($"empresas/{empresaId}/", StringComparison.Ordinal)) return null;
                var storage = services.GetRequiredService<ICloudflareR2StorageService>();
                await using var file = await storage.DownloadAsync(empresaId, key.ToString(), token);
                return file is null ? null : await ReadAsync(file.Stream, token);
            }
            if (!Uri.TryCreate(logoUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)) return null;
            using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxBytes) return null;
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            return await ReadAsync(stream, token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            logger.LogWarning("Tempo esgotado ao carregar logo da empresa {EmpresaId} para o DANFE.", empresaId);
            return null;
        }
        catch (Exception error) when (error is HttpRequestException or IOException or FormatException or InvalidOperationException or Amazon.S3.AmazonS3Exception)
        {
            logger.LogWarning("Logo indisponivel para o DANFE da empresa {EmpresaId}. Tipo: {Tipo}", empresaId, error.GetType().Name);
            return null;
        }
    }

    private static async Task<byte[]?> ReadAsync(Stream stream, CancellationToken token)
    {
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, token)) > 0)
        {
            if (output.Length + read > MaxBytes) return null;
            output.Write(buffer, 0, read);
        }
        return ImageOrNull(output.ToArray());
    }

    private static byte[]? ImageOrNull(byte[] bytes) => bytes.Length <= MaxBytes &&
        (bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
         bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 })) ? bytes : null;

    private static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return (bytes[0] & 0xe0) == 0x20 && !(bytes[0] == 0x20 && bytes[1] == 0x02);
        return bytes[0] is not (0 or 10 or 127) && bytes[0] < 224 &&
            !(bytes[0] == 169 && bytes[1] == 254) && !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31) &&
            !(bytes[0] == 192 && bytes[1] == 168) && !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127);
    }
}
