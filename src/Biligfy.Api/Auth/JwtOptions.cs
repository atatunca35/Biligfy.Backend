namespace Biligfy.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Biligfy.Api";

    public string Audience { get; set; } = "Biligfy.Web";

    public int ExpiresMinutes { get; set; } = 60;

    /// <summary>
    /// Simetrik imza anahtarı. Gizli bilgidir: appsettings.json içine yazılmaz,
    /// dotnet user-secrets ile "Jwt:Key" olarak tanımlanır.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Key) || Key.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Key tanımlı değil veya 32 karakterden kısa. dotnet user-secrets set \"Jwt:Key\" ile tanımlayın.");
        }
    }
}
