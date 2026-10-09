using System.ComponentModel.DataAnnotations;
using organaizer.Infrastructure;

namespace organaizer.Pages.Operations.Transfers;

public sealed class TransferForm
{
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }
    [Range(typeof(decimal),"0.00000001","9999999999999999")]
    public decimal Amount { get; set; }
    public DateTime OccurredAt { get; set; } = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(5)).Date;
    [StringLength(500)] public string? Note { get; set; }
    public int Revision { get; set; }
}
