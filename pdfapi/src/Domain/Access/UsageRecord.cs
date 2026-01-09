namespace PaperAPI.Domain.Access;

public sealed class UsageRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? ApiKeyId { get; set; }
    public DateOnly Date { get; set; }
    public int RequestsCount { get; set; }
    public int PdfCount { get; set; }
    public long BytesGenerated { get; set; }
}
