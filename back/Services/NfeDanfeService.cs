using NFEDanfe;
using NFEDanfe.Options;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;
using back.Models;

namespace back.Services;

public static class NfeDanfeService
{
    private static readonly object PdfGate = new();

    public static byte[] Gerar(NotaFiscal nota)
    {
        if (nota.Tipo != "NFe" || nota.Situacao is not ("Autorizada" or "Cancelada") || nota.ChaveAcesso.Length != 44)
            throw new ArgumentException("O DANFE exige uma NF-e autorizada com chave de acesso valida.");
        // Valida o XML protocolado salvo, incluindo emitente, chave, ambiente e protocolo.
        // Não usa dados atuais do cadastro nem transmite novamente a nota.
        var original = NotaFiscalXml.Importar(nota.Xml, nota.EmpresaId, nota.ChaveAcesso.Substring(6, 14));
        if (original.ChaveAcesso != nota.ChaveAcesso || original.Ambiente != nota.Ambiente || original.Protocolo != nota.Protocolo || original.Tipo != "NFe")
            throw new ArgumentException("O XML autorizado diverge da chave, ambiente ou protocolo da nota salva.");
        lock (PdfGate)
        {
            using var generated = new MemoryStream();
            DanfeGenerator.GenerateFromXmlContent(nota.Xml, generated, new DanfeOptions { UseDefaultLogo = false, EmitFooter = false, Font = DanfeFont.Roboto });
            generated.Position = 0;
            using var document = PdfReader.Open(generated, PdfDocumentOpenMode.Modify);
            document.Info.Title = nota.Situacao == "Cancelada" ? "DANFE - NF-e CANCELADA" : "DANFE - NF-e " + nota.Numero;
            document.Info.Subject = "DANFE - Chave: " + nota.ChaveAcesso;
            document.Info.Creator = "UniFlow AutoParts";
            if (nota.Situacao == "Cancelada")
            {
                foreach (var page in document.Pages)
                {
                    using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                    var state = graphics.Save();
                    graphics.TranslateTransform(page.Width.Point / 2, page.Height.Point / 2);
                    graphics.RotateTransform(-35);
                    graphics.DrawString("NF-e CANCELADA", new XFont("DanfeFont", 42, XFontStyleEx.Bold),
                        new XSolidBrush(XColor.FromArgb(130, 190, 35, 35)), new XPoint(0, 0), XStringFormats.Center);
                    graphics.Restore(state);
                }
            }
            using var output = new MemoryStream();
            document.Save(output, false);
            return output.ToArray();
        }
    }
}
