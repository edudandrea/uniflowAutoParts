using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace back.Services;

public interface ICloudflareR2StorageService
{
    Task<StoredDocument> UploadAsync(long tenantId, string category, IFormFile file, CancellationToken cancellationToken);
    Task<DownloadedDocument?> DownloadAsync(long tenantId, string key, CancellationToken cancellationToken);
}

public record StoredDocument(string Key, string Url, string ContentType, long Size);

public sealed class DownloadedDocument : IAsyncDisposable
{
    private readonly GetObjectResponse response;

    public DownloadedDocument(GetObjectResponse response)
    {
        this.response = response;
        Stream = response.ResponseStream;
        ContentType = response.Headers.ContentType ?? "application/octet-stream";
        FileName = Path.GetFileName(response.Key);
    }

    public Stream Stream { get; }
    public string ContentType { get; }
    public string FileName { get; }

    public ValueTask DisposeAsync()
    {
        response.Dispose();
        return ValueTask.CompletedTask;
    }
}

public class CloudflareR2StorageService : ICloudflareR2StorageService
{
    private readonly CloudflareR2Options options;
    private readonly IAmazonS3 client;

    public CloudflareR2StorageService(IOptions<CloudflareR2Options> options)
    {
        this.options = options.Value;
        EnsureConfigured(this.options);

        var endpoint = string.IsNullOrWhiteSpace(this.options.Endpoint)
            ? $"https://{this.options.AccountId}.r2.cloudflarestorage.com"
            : this.options.Endpoint;

        var credentials = new BasicAWSCredentials(this.options.AccessKeyId, this.options.SecretAccessKey);
        var config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "auto",
            RegionEndpoint = RegionEndpoint.USEast1
        };

        client = new AmazonS3Client(credentials, config);
    }

    public async Task<StoredDocument> UploadAsync(long tenantId, string category, IFormFile file, CancellationToken cancellationToken)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentException("TenantId e obrigatorio.", nameof(tenantId));
        }

        if (file.Length == 0)
        {
            throw new ArgumentException("Arquivo vazio.", nameof(file));
        }

        var extension = Path.GetExtension(file.FileName);
        var safeExtension = string.IsNullOrWhiteSpace(extension) ? "" : extension.ToLowerInvariant();
        var safeCategory = SanitizePath(category);
        var folderPrefix = $"empresas/{tenantId}";
        var key = $"{folderPrefix}/{safeCategory}/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{safeExtension}";

        await EnsureCompanyFolderAsync(folderPrefix, cancellationToken);

        await using var stream = file.OpenReadStream();
        var request = new PutObjectRequest
        {
            BucketName = options.BucketName,
            Key = key,
            InputStream = stream,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType
        };

        await client.PutObjectAsync(request, cancellationToken);

        return new StoredDocument(key, $"/api/documentos/empresas/{tenantId}/arquivo?key={Uri.EscapeDataString(key)}", request.ContentType, file.Length);
    }

    public async Task<DownloadedDocument?> DownloadAsync(long tenantId, string key, CancellationToken cancellationToken)
    {
        var expectedPrefix = $"empresas/{tenantId}/";
        if (tenantId <= 0 || string.IsNullOrWhiteSpace(key) || !key.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var response = await client.GetObjectAsync(options.BucketName, key, cancellationToken);
            return new DownloadedDocument(response);
        }
        catch (AmazonS3Exception error) when (error.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task EnsureCompanyFolderAsync(string folderPrefix, CancellationToken cancellationToken)
    {
        var markerKey = $"{folderPrefix}/.keep";
        var request = new PutObjectRequest
        {
            BucketName = options.BucketName,
            Key = markerKey,
            ContentBody = "",
            ContentType = "text/plain"
        };

        await client.PutObjectAsync(request, cancellationToken);
    }

    private static string SanitizePath(string value)
    {
        var parts = value
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => new string(part.Select(character =>
                char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-').ToArray()))
            .Where(part => !string.IsNullOrWhiteSpace(part));

        return string.Join('/', parts);
    }

    private static void EnsureConfigured(CloudflareR2Options options)
    {
        if (string.IsNullOrWhiteSpace(options.AccountId) ||
            string.IsNullOrWhiteSpace(options.AccessKeyId) ||
            string.IsNullOrWhiteSpace(options.SecretAccessKey) ||
            string.IsNullOrWhiteSpace(options.BucketName))
        {
            throw new InvalidOperationException("Cloudflare R2 nao esta configurado. Preencha CloudflareR2:AccountId, AccessKeyId, SecretAccessKey e BucketName.");
        }
    }
}
