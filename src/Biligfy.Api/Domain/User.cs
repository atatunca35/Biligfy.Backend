namespace Biligfy.Api.Domain;

public sealed class User
{
    public Guid Id { get; set; }

    /// <summary>Küçük harfe çevrilmiş e-posta adresi (benzersiz).</summary>
    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Arayüz dili: "tr" veya "en".</summary>
    public string PreferredLanguage { get; set; } = "tr";

    public AccountType AccountType { get; set; } = AccountType.Individual;

    public DateTime CreatedAtUtc { get; set; }
}

public enum AccountType
{
    Individual,
    Company
}
