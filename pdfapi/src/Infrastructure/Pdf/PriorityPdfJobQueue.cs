using System.Threading.Channels;
using PaperAPI.Application.Pdf;
using PaperAPI.Domain.Pdf;

namespace PaperAPI.Infrastructure.Pdf;

public sealed class PriorityPdfJobQueue : IPdfJobQueue
{
    private readonly Channel<PdfJob> _highPriority;
    private readonly Channel<PdfJob> _mediumPriority;
    private readonly Channel<PdfJob> _lowPriority;
    private readonly Channel<PdfJob>[] _schedule;
    private int _scheduleIndex;

    public PriorityPdfJobQueue()
    {
        _highPriority = CreateChannel();
        _mediumPriority = CreateChannel();
        _lowPriority = CreateChannel();

        _schedule = BuildSchedule();
    }

    public ValueTask EnqueueAsync(PdfJob job, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        return GetChannel(job.PriorityWeight).Writer.WriteAsync(job, ct);
    }

    public async ValueTask<PdfJob?> DequeueAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            for (var i = 0; i < _schedule.Length; i++)
            {
                var index = Interlocked.Increment(ref _scheduleIndex);
                var channel = _schedule[Math.Abs(index % _schedule.Length)];

                if (channel.Reader.TryRead(out var job))
                {
                    return job;
                }
            }

            await WaitForAnyAsync(ct);
        }

        return null;
    }

    private static Channel<PdfJob> CreateChannel()
    {
        var options = new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        };

        return Channel.CreateUnbounded<PdfJob>(options);
    }

    private Channel<PdfJob>[] BuildSchedule()
    {
        var schedule = new List<Channel<PdfJob>>();
        schedule.AddRange(Enumerable.Repeat(_highPriority, 5));
        schedule.AddRange(Enumerable.Repeat(_mediumPriority, 3));
        schedule.Add(_lowPriority);
        return schedule.ToArray();
    }

    private Channel<PdfJob> GetChannel(int priorityWeight)
    {
        if (priorityWeight >= 5)
        {
            return _highPriority;
        }

        if (priorityWeight >= 3)
        {
            return _mediumPriority;
        }

        return _lowPriority;
    }

    private async Task WaitForAnyAsync(CancellationToken ct)
    {
        var waitTasks = new[]
        {
            _highPriority.Reader.WaitToReadAsync(ct).AsTask(),
            _mediumPriority.Reader.WaitToReadAsync(ct).AsTask(),
            _lowPriority.Reader.WaitToReadAsync(ct).AsTask()
        };

        await Task.WhenAny(waitTasks);
    }
}
