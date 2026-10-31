using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Biligfy.Api.Auth;
using Biligfy.Api.Data;
using Biligfy.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Biligfy.Api.Tests;

internal static class TestSetup
{
    // Yalnızca testler için; gerçek ortamda kullanılmaz.
    public const string TestKey = "test-only-signing-key-0123456789abcdef";

    public static JwtOptions Jwt() => new() { Key = TestKey };

    public static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public static AuthService NewAuth(AppDbContext db) =>
        new(db, new PasswordHasher<User>(), new JwtTokenService(Options.Create(Jwt())));
}

public class JwtOptionsTests
{
    [Fact]
    public void Validate_Throws_WhenKeyMissing()
    {
        var options = new JwtOptions { Key = string.Empty };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Validate_Throws_WhenKeyShorterThan32Characters()
    {
        var options = new JwtOptions { Key = "short-key" };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Fact]
    public void Validate_Passes_WhenKeyIsLongEnough()
    {
        var options = TestSetup.Jwt();

        options.Validate();
    }
}

public class JwtTokenServiceTests
{
    [Fact]
    public void Create_ProducesToken_ThatValidatesWithConfiguredIssuerAudienceAndKey()
    {
        var jwt = TestSetup.Jwt();
        var service = new JwtTokenService(Options.Create(jwt));
        var user = new User { Id = Guid.NewGuid(), Email = "ali@example.com", DisplayName = "Ali", PreferredLanguage = "tr" };

        var (token, expiresAtUtc) = service.Create(user);

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true
        };

        new JwtSecurityTokenHandler().ValidateToken(token, parameters, out var validated);
        var jwtToken = Assert.IsType<JwtSecurityToken>(validated);

        Assert.Equal(user.Id.ToString(), jwtToken.Subject);
        Assert.Equal("ali@example.com", jwtToken.Claims.First(c => c.Type == "email").Value);
        Assert.True(expiresAtUtc > DateTime.UtcNow);
    }
}

public class AuthServiceTests
{
    private const string Password = "GucluSifre123";

    [Fact]
    public async Task Register_NormalizesEmail_AndReturnsToken()
    {
        using var db = TestSetup.NewDb();
        var auth = TestSetup.NewAuth(db);

        var response = await auth.RegisterAsync(new RegisterRequest("  Ali@Example.COM ", Password, "Ali Veli"));

        Assert.Equal("ali@example.com", response.User.Email);
        Assert.Equal("Bearer", response.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.Equal(AccountType.Individual, response.User.AccountType);
        Assert.Equal("tr", response.User.PreferredLanguage);
    }

    [Fact]
    public async Task Register_Stores_PasswordHash_NotPlainText()
    {
        using var db = TestSetup.NewDb();
        var auth = TestSetup.NewAuth(db);

        await auth.RegisterAsync(new RegisterRequest("ali@example.com", Password, "Ali"));

        var stored = await db.Users.SingleAsync();
        Assert.NotEqual(Password, stored.PasswordHash);
        Assert.DoesNotContain(Password, stored.PasswordHash);
    }

    [Fact]
    public async Task Register_Throws_WhenEmailAlreadyExists_CaseInsensitive()
    {
        using var db = TestSetup.NewDb();
        var auth = TestSetup.NewAuth(db);
        await auth.RegisterAsync(new RegisterRequest("ali@example.com", Password, "Ali"));

        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(() =>
            auth.RegisterAsync(new RegisterRequest("ALI@example.com", Password, "Ali 2")));
    }

    [Fact]
    public async Task Register_SupportsCompanyAccountAndEnglish()
    {
        using var db = TestSetup.NewDb();
        var auth = TestSetup.NewAuth(db);

        var response = await auth.RegisterAsync(
            new RegisterRequest("ops@acme.com", Password, "Acme Ops", AccountType.Company, "en"));

        Assert.Equal(AccountType.Company, response.User.AccountType);
        Assert.Equal("en", response.User.PreferredLanguage);
    }

    [Fact]
    public async Task Login_ReturnsToken_WhenCredentialsAreCorrect_RegardlessOfEmailCase()
    {
        using var db = TestSetup.NewDb();
        var auth = TestSetup.NewAuth(db);
        await auth.RegisterAsync(new RegisterRequest("ali@example.com", Password, "Ali"));

        var response = await auth.LoginAsync(new LoginRequest("ALI@example.com", Password));

        Assert.NotNull(response);
        Assert.False(string.IsNullOrWhiteSpace(response!.AccessToken));
    }

    [Fact]
    public async Task Login_ReturnsNull_WhenPasswordIsWrong()
    {
        using var db = TestSetup.NewDb();
        var auth = TestSetup.NewAuth(db);
        await auth.RegisterAsync(new RegisterRequest("ali@example.com", Password, "Ali"));

        var response = await auth.LoginAsync(new LoginRequest("ali@example.com", "yanlis-sifre"));

        Assert.Null(response);
    }

    [Fact]
    public async Task Login_ReturnsNull_WhenUserDoesNotExist()
    {
        using var db = TestSetup.NewDb();
        var auth = TestSetup.NewAuth(db);

        var response = await auth.LoginAsync(new LoginRequest("yok@example.com", Password));

        Assert.Null(response);
    }
}
