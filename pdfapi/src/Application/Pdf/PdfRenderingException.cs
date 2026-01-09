namespace PaperAPI.Application.Pdf;

public sealed class PdfRenderingException : Exception
{
    public PdfRenderingException(string message, string? details = null)
        : base(message)
    {
        Details = details;
    }

    public string? Details { get; }
}
