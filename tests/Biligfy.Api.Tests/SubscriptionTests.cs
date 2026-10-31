using System.ComponentModel.DataAnnotations;
using Biligfy.Api.Domain;
using Biligfy.Api.Subscriptions;
using Xunit;

namespace Biligfy.Api.Tests;

public class SubscriptionServiceTests
{
    private static readonly Guid UserA = Guid.NewGuid();
    private static readonly Guid UserB = Guid.NewGuid();

    private static SubscriptionRequest Req(string name = "Netflix", string date = "2026-11-15", bool active = true) => new()
    {
        Name = name,
        Vendor = " Netflix ",
        Amount = 8.99m,
        Currency = "USD",
        BillingCycle = "Monthly",
        NextRenewalDate = DateOnly.Parse(date),
        IsActive = active
    };

    /// <summary>Hatalı özellik adlarını döner (özellik bazında Data Annotations doğrulaması).</summary>
    private static List<string> Validate(SubscriptionRequest r)
    {
        var bad = new List<string>();
        foreach (var p in typeof(SubscriptionRequest).GetProperties())
        {
            var ctx = new ValidationContext(r) { MemberName = p.Name };
            if (!Validator.TryValidateProperty(p.GetValue(r), ctx, new List<ValidationResult>())) bad.Add(p.Name);
        }
        return bad;
    }

    [Fact]
    public async Task Create_AssignsUserAndNormalizesFields()
    {
        var svc = new SubscriptionService(TestSetup.NewDb());
        var req = Req(" Netflix ");
        req.Notes = "   ";

        var created = await svc.CreateAsync(UserA, req, default);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Netflix", created.Name);
        Assert.Equal("Netflix", created.Vendor);
        Assert.Null(created.Notes);
        Assert.Equal(BillingCycle.Monthly, created.BillingCycle);
        Assert.Equal(created.CreatedAtUtc, created.UpdatedAtUtc);
    }

    [Fact]
    public async Task List_PagesAndSortsByRenewalDateThenName()
    {
        var svc = new SubscriptionService(TestSetup.NewDb());
        await svc.CreateAsync(UserA, Req("B", "2026-12-01"), default);
        await svc.CreateAsync(UserA, Req("A", "2026-12-01"), default);
        await svc.CreateAsync(UserA, Req("C", "2026-11-01"), default);

        var page1 = await svc.ListAsync(UserA, 1, 2, null, default);
        var page2 = await svc.ListAsync(UserA, 2, 2, null, default);
        var page3 = await svc.ListAsync(UserA, 3, 2, null, default);

        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(["C", "A"], page1.Items.Select(i => i.Name));
        Assert.Equal(["B"], page2.Items.Select(i => i.Name));
        Assert.Empty(page3.Items);
        Assert.Equal(2, page1.PageSize);
    }

    [Fact]
    public async Task List_FiltersByIsActive()
    {
        var svc = new SubscriptionService(TestSetup.NewDb());
        await svc.CreateAsync(UserA, Req("On"), default);
        await svc.CreateAsync(UserA, Req("Off", active: false), default);

        var active = await svc.ListAsync(UserA, 1, 20, true, default);
        var inactive = await svc.ListAsync(UserA, 1, 20, false, default);
        var all = await svc.ListAsync(UserA, 1, 20, null, default);

        Assert.Equal(["On"], active.Items.Select(i => i.Name));
        Assert.Equal(["Off"], inactive.Items.Select(i => i.Name));
        Assert.Equal(2, all.TotalCount);
    }

    [Fact]
    public async Task List_OnlyReturnsOwnRecords()
    {
        var svc = new SubscriptionService(TestSetup.NewDb());
        await svc.CreateAsync(UserA, Req("Mine"), default);
        await svc.CreateAsync(UserB, Req("Theirs"), default);

        var list = await svc.ListAsync(UserA, 1, 20, null, default);

        Assert.Equal(1, list.TotalCount);
        Assert.Equal("Mine", list.Items[0].Name);
    }

    [Fact]
    public async Task Get_Update_Delete_ReturnNotFound_ForOtherUsersRecord()
    {
        var svc = new SubscriptionService(TestSetup.NewDb());
        var created = await svc.CreateAsync(UserA, Req(), default);

        Assert.Null(await svc.GetAsync(UserB, created.Id, default));
        Assert.Null(await svc.UpdateAsync(UserB, created.Id, Req("Hacked"), default));
        Assert.False(await svc.DeleteAsync(UserB, created.Id, default));
        Assert.Equal("Netflix", (await svc.GetAsync(UserA, created.Id, default))!.Name);
    }

    [Fact]
    public async Task Update_ReplacesFields_KeepsCreatedAt_RefreshesUpdatedAt()
    {
        var svc = new SubscriptionService(TestSetup.NewDb());
        var created = await svc.CreateAsync(UserA, Req(), default);
        var req = Req("Spotify");
        req.Vendor = null;
        req.Currency = "EUR";
        req.BillingCycle = "Yearly";

        var updated = await svc.UpdateAsync(UserA, created.Id, req, default);

        Assert.NotNull(updated);
        Assert.Equal("Spotify", updated!.Name);
        Assert.Null(updated.Vendor);
        Assert.Equal("EUR", updated.Currency);
        Assert.Equal(BillingCycle.Yearly, updated.BillingCycle);
        Assert.Equal(created.CreatedAtUtc, updated.CreatedAtUtc);
        Assert.True(updated.UpdatedAtUtc >= created.UpdatedAtUtc);
    }

    [Fact]
    public async Task Delete_RemovesRecord()
    {
        var svc = new SubscriptionService(TestSetup.NewDb());
        var created = await svc.CreateAsync(UserA, Req(), default);

        Assert.True(await svc.DeleteAsync(UserA, created.Id, default));
        Assert.Null(await svc.GetAsync(UserA, created.Id, default));
        Assert.False(await svc.DeleteAsync(UserA, created.Id, default));
    }

    [Fact]
    public async Task Create_AllowsUpTo100_Then_Rejects101st()
    {
        var svc = new SubscriptionService(TestSetup.NewDb());
        for (var i = 1; i <= 100; i++)
            await svc.CreateAsync(UserA, Req($"Sub{i}"), default); // 100. kayıt başarılı

        await Assert.ThrowsAsync<SubscriptionLimitExceededException>(
            () => svc.CreateAsync(UserA, Req("Sub101"), default));

        var list = await svc.ListAsync(UserA, 1, 20, null, default);
        Assert.Equal(100, list.TotalCount);
    }

    [Fact]
    public async Task Create_Limit_IsPerUser_AndFreesUpAfterDelete()
    {
        var svc = new SubscriptionService(TestSetup.NewDb());
        SubscriptionResponse? first = null;
        for (var i = 1; i <= 100; i++)
        {
            var c = await svc.CreateAsync(UserA, Req($"Sub{i}"), default);
            first ??= c;
        }

        // Başka kullanıcı etkilenmez
        await svc.CreateAsync(UserB, Req("Other"), default);

        // Silince yeniden eklenebilir
        Assert.True(await svc.DeleteAsync(UserA, first!.Id, default));
        await svc.CreateAsync(UserA, Req("Again"), default);
        await Assert.ThrowsAsync<SubscriptionLimitExceededException>(
            () => svc.CreateAsync(UserA, Req("TooMany"), default));
    }

    [Fact]
    public void Validation_AcceptsValidRequest() => Assert.Empty(Validate(Req()));

    [Theory]
    [InlineData("usd")]
    [InlineData("JPY")]
    [InlineData("US")]
    [InlineData("")]
    public void Validation_RejectsInvalidCurrency(string currency)
    {
        var r = Req();
        r.Currency = currency;

        Assert.Contains("Currency", Validate(r));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1.234")]
    public void Validation_RejectsInvalidAmount(string amount)
    {
        var r = Req();
        r.Amount = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Contains("Amount", Validate(r));
    }

    [Fact]
    public void Validation_RejectsInvalidBillingCycleAndBlankName()
    {
        var r = Req("   ");
        r.BillingCycle = "Daily";

        var errors = Validate(r);
        Assert.Contains("BillingCycle", errors);
        Assert.Contains("Name", errors);
    }
}
