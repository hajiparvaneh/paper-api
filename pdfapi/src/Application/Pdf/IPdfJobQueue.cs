using PaperAPI.Domain.Pdf;

namespace PaperAPI.Application.Pdf;

public interface IPdfJobQueue
{
    ValueTask EnqueueAsync(PdfJob job, CancellationToken ct = default);
    ValueTask<PdfJob?> DequeueAsync(CancellationToken ct = default);
}
