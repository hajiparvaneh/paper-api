namespace PaperAPI.Application.Pdf;

public interface IPdfRenderer
{
    Task RenderAsync(string html, PdfOptions options, Stream output, CancellationToken cancellationToken);
}
