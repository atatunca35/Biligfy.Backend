using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Biligfy.Api.Data;

/// <summary>
/// dotnet ef komutları (migrations add / database update) için tasarım zamanı fabrikası.
/// Bağlantı cümlesi dotnet user-secrets içindeki ConnectionStrings:DefaultConnection'dan okunur.
/// Gizli bilgi appsettings.json içine yazılmaz.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    // Yalnızca user-secrets tanımlı değilse kullanılır; kimlik bilgisi içermez.
    private const string FallbackConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=Biligfy;Trusted_Connection=True;TrustServerCertificate=True";

    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<AppDbContextFactory>(optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? FallbackConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
