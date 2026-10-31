using Biligfy.Api.Data;
using Biligfy.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Biligfy.Api.Auth;

public sealed class EmailAlreadyRegisteredException : Exception
{
    public EmailAlreadyRegisteredException() : base("Bu e-posta adresi zaten kayıtlı.") { }
}

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    /// <summary>Kimlik bilgileri geçersizse null döner.</summary>
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

public sealed class AuthService(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    IJwtTokenService tokenService) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw new EmailAlreadyRegisteredException();
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            PreferredLanguage = request.PreferredLanguage,
            AccountType = request.AccountType,
            CreatedAtUtc = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Eşzamanlı kayıtta benzersiz indeks ihlali olabilir; e-posta gerçekten kayıtlıysa bunu bildir.
            if (await db.Users.AnyAsync(u => u.Email == email, ct))
            {
                throw new EmailAlreadyRegisteredException();
            }

            throw;
        }

        return ToResponse(user);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
        {
            return null;
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        return ToResponse(user);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private AuthResponse ToResponse(User user)
    {
        var (token, expiresAtUtc) = tokenService.Create(user);

        return new AuthResponse(
            AccessToken: token,
            TokenType: "Bearer",
            ExpiresAtUtc: expiresAtUtc,
            User: new UserResponse(user.Id, user.Email, user.DisplayName, user.PreferredLanguage, user.AccountType));
    }
}
