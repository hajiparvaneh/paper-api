using PaperAPI.Domain.Pdf;

namespace PaperAPI.Application.Pdf;

public interface IJobWaiterRegistry
{
    Task<PdfJob> WaitForCompletionAsync(Guid jobId, CancellationToken cancellationToken);
    void NotifyCompleted(PdfJob job);
}
