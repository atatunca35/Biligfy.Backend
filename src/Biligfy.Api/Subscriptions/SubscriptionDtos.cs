using System.ComponentModel.DataAnnotations;
using Biligfy.Api.Domain;

namespace Biligfy.Api.Subscriptions;

public static class SubscriptionRules
{
    public static readonly string[] SupportedCurrencies = ["TRY", "USD", "EUR", "GBP"];
    public const decimal MaxAmount = 999999999999999.99m;
}

/// <summary>Kırpıldıktan sonra uzunluk kontrolü.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class TrimmedLengthAttribute(int min, int max) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(value as string)) return ValidationResult.Success; // [Required] kontrol eder
        var len = ((string)value!).Trim().Length;
        return len >= min && len <= max
            ? ValidationResult.Success
            : new ValidationResult($"Ad {min} ile {max} karakter arasında olmalıdır.");
    }
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class SupportedCurrencyAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext context) =>
        value is string s && SubscriptionRules.SupportedCurrencies.Contains(s, StringComparer.Ordinal)
            ? ValidationResult.Success
            : new ValidationResult(
                $"Desteklenmeyen para birimi. Desteklenenler: {string.Join(", ", SubscriptionRules.SupportedCurrencies)}.");
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class BillingCycleValueAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext context) =>
        value is string s && SubscriptionMapper.TryParseCycle(s, out _)
            ? ValidationResult.Success
            : new ValidationResult("Geçersiz faturalandırma döngüsü. Değerler: Monthly, Yearly, Weekly.");
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class MoneyAmountAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext context)
    {
        if (value is not decimal d) return ValidationResult.Success; // [Required] ayrıca kontrol eder
        if (d <= 0) return new ValidationResult("Tutar 0'dan büyük olmalıdır.");
        if (d > SubscriptionRules.MaxAmount) return new ValidationResult("Tutar çok büyük.");
        if (decimal.Round(d, 2) != d) return new ValidationResult("Tutar en fazla 2 ondalık basamak içerebilir.");
        return ValidationResult.Success;
    }
}

public sealed class SubscriptionRequest
{
    [Required(ErrorMessage = "Ad zorunludur.")]
    [TrimmedLength(1, 100)]
    public string? Name { get; set; }

    [StringLength(100, ErrorMessage = "Sağlayıcı en fazla 100 karakter olabilir.")]
    public string? Vendor { get; set; }

    [Required(ErrorMessage = "Tutar zorunludur.")]
    [MoneyAmount]
    public decimal? Amount { get; set; }

    [Required(ErrorMessage = "Para birimi zorunludur.")]
    [SupportedCurrency]
    public string? Currency { get; set; }

    [Required(ErrorMessage = "Faturalandırma döngüsü zorunludur.")]
    [BillingCycleValue]
    public string? BillingCycle { get; set; }

    [Required(ErrorMessage = "Yenileme tarihi zorunludur.")]
    public DateOnly? NextRenewalDate { get; set; }

    [Range(0, 365, ErrorMessage = "İptal bildirim süresi 0 ile 365 gün arasında olmalıdır.")]
    public int? CancellationNoticeDays { get; set; }

    [StringLength(50, ErrorMessage = "Kategori en fazla 50 karakter olabilir.")]
    public string? Category { get; set; }

    [StringLength(500, ErrorMessage = "Not en fazla 500 karakter olabilir.")]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed record SubscriptionResponse(
    Guid Id,
    string Name,
    string? Vendor,
    decimal Amount,
    string Currency,
    BillingCycle BillingCycle,
    DateOnly NextRenewalDate,
    int? CancellationNoticeDays,
    string? Category,
    string? Notes,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public static class SubscriptionMapper
{
    public static bool TryParseCycle(string value, out BillingCycle cycle) =>
        Enum.TryParse(value, ignoreCase: false, out cycle) && Enum.IsDefined(cycle) && !int.TryParse(value, out _);

    public static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public static void Apply(Subscription s, SubscriptionRequest r)
    {
        s.Name = r.Name!.Trim();
        s.Vendor = NullIfBlank(r.Vendor);
        s.Amount = r.Amount!.Value;
        s.Currency = r.Currency!;
        TryParseCycle(r.BillingCycle!, out var cycle);
        s.BillingCycle = cycle;
        s.NextRenewalDate = r.NextRenewalDate!.Value;
        s.CancellationNoticeDays = r.CancellationNoticeDays;
        s.Category = NullIfBlank(r.Category);
        s.Notes = NullIfBlank(r.Notes);
        s.IsActive = r.IsActive;
    }

    public static SubscriptionResponse ToResponse(Subscription s) => new(
        s.Id, s.Name, s.Vendor, s.Amount, s.Currency, s.BillingCycle, s.NextRenewalDate,
        s.CancellationNoticeDays, s.Category, s.Notes, s.IsActive,
        DateTime.SpecifyKind(s.CreatedAtUtc, DateTimeKind.Utc),
        DateTime.SpecifyKind(s.UpdatedAtUtc, DateTimeKind.Utc));
}
