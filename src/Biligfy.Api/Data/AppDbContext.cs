using Biligfy.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Biligfy.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(u => u.Id);

            entity.Property(u => u.Email).HasMaxLength(256).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();

            entity.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(u => u.PreferredLanguage).HasMaxLength(5).IsRequired();
            entity.Property(u => u.AccountType).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(u => u.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("Subscriptions");
            entity.HasKey(s => s.Id);

            entity.Property(s => s.Name).HasMaxLength(100).IsRequired();
            entity.Property(s => s.Vendor).HasMaxLength(100);
            entity.Property(s => s.Amount).HasPrecision(18, 2);
            entity.Property(s => s.Currency).HasMaxLength(3).IsRequired();
            entity.Property(s => s.BillingCycle).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(s => s.NextRenewalDate).HasColumnType("date");
            entity.Property(s => s.Category).HasMaxLength(50);
            entity.Property(s => s.Notes).HasMaxLength(500);

            entity.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(s => s.UserId);
            entity.HasIndex(s => new { s.UserId, s.IsActive, s.NextRenewalDate });
        });
    }
}
