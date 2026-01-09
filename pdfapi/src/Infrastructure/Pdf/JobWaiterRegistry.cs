using System.Collections.Concurrent;
using PaperAPI.Application.Pdf;
using PaperAPI.Domain.Pdf;

namespace PaperAPI.Infrastructure.Pdf;

public sealed class JobWaiterRegistry : IJobWaiterRegistry
{
    private sealed record Waiter(TaskCompletionSource<PdfJob> Tcs, CancellationTokenRegistration Registration);

    private readonly ConcurrentDictionary<Guid, Waiter> _waiters = new();

    public Task<PdfJob> WaitForCompletionAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var waiter = _waiters.GetOrAdd(
            jobId,
            key =>
            {
                var tcs = new TaskCompletionSource<PdfJob>(TaskCreationOptions.RunContinuationsAsynchronously);
                var registration = cancellationToken.Register(() => CancelWaiter(key, cancellationToken));
                return new Waiter(tcs, registration);
            });

        return waiter.Tcs.Task;
    }

    public void NotifyCompleted(PdfJob job)
    {
        if (!_waiters.TryRemove(job.Id, out var waiter))
        {
            return;
        }

        waiter.Registration.Dispose();
        waiter.Tcs.TrySetResult(job);
    }

    private void CancelWaiter(Guid jobId, CancellationToken cancellationToken)
    {
        if (!_waiters.TryRemove(jobId, out var waiter))
        {
            return;
        }

        waiter.Registration.Dispose();
        waiter.Tcs.TrySetCanceled(cancellationToken);
    }
}
