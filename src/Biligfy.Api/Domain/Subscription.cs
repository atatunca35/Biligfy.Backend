namespace Biligfy.Api.Domain;

public sealed class Subscription
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Vendor { get; set; }

    public decimal Amount { get; set; }

    /// <summary>ISO 4217, büyük harf (TRY, USD, EUR, GBP).</summary>
    public string Currency { get; set; } = string.Empty;

    public BillingCycle BillingCycle { get; set; }

    public DateOnly NextRenewalDate { get; set; }

    public int? CancellationNoticeDays { get; set; }
    public string? Category { get; set; }
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public enum BillingCycle
{
    Monthly,
    Yearly,
    Weekly
}
