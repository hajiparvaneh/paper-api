using PaperAPI.Domain.Pdf;
using PaperAPI.PdfApi.Models.Responses;

namespace PaperAPI.PdfApi.Endpoints;

internal static class PdfJobResponseFactory
{
    public static PdfJobStatusResponse Create(PdfJob job, bool includeDownloadUrl, bool includePendingDownloadUrl = false)
    {
        var jobStatusUrl = $"/jobs/{job.Id}";
        var downloadUrl = includeDownloadUrl
            ? $"/jobs/{job.Id}/result"
            : null;

        if (!includePendingDownloadUrl && job.Status != PdfJobStatus.Succeeded)
        {
            downloadUrl = null;
        }

        return new PdfJobStatusResponse
        {
            Id = job.Id,
            Status = job.Status.ToString(),
            ErrorMessage = job.ErrorMessage,
            DownloadUrl = downloadUrl,
            JobStatusUrl = jobStatusUrl,
            CreatedAt = job.CreatedAt,
            ExpiresAt = job.ExpiresAt,
            Links = new PdfJobLinks
            {
                Self = jobStatusUrl,
                Result = downloadUrl
            }
        };
    }
}
