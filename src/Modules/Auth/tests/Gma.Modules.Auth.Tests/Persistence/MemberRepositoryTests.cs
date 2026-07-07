namespace Gma.Modules.Auth.Tests;

using Gma.Modules.Auth.Domain.Aggregates;
using Gma.Modules.Auth.Domain.Enums;
using Gma.Modules.Auth.Domain.ValueObjects;
using Gma.Modules.Auth.Persistence;
using Gma.Modules.Auth.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Gma.Framework.Tenancy;
using Xunit;

[Trait("Category", "Unit")]
public sealed class MemberRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Username_exists_reserves_inactive_username_history_while_login_uses_active_username()
    {
        await using AuthDbContext dbContext = CreateDbContext();
        MemberRepository repository = new(dbContext);
        Member member = CreateMember("member@example.com");

        var changed = member.AddUsername(
            new MemberUsernameId(Guid.NewGuid()),
            "other@example.com",
            MemberUsernameType.Email);
        Assert.True(changed.IsSuccess);

        await repository.AddAsync(member, CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        Assert.True(await repository.UsernameExistsAsync("member@example.com", CancellationToken.None));
        Assert.Null(await repository.GetByUsernameAsync("member@example.com", CancellationToken.None));
        Assert.NotNull(await repository.GetByUsernameAsync("other@example.com", CancellationToken.None));
    }

    private static AuthDbContext CreateDbContext()
    {
        DbContextOptions<AuthDbContext> options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseInMemoryDatabase($"auth-repository-{Guid.NewGuid():N}")
            .Options;

        return new AuthDbContext(options, new TestTenantContext());
    }

    private static Member CreateMember(string username) =>
        Member.Create(
            new MemberId(Guid.NewGuid()),
            "tenant-a",
            username,
            MemberUsernameType.Email,
            "password-hash",
            new MemberUsernameId(Guid.NewGuid()),
            Guid.NewGuid(),
            Now).Value;

    private sealed class TestTenantContext : ITenantContext
    {
        public bool IsEnabled => true;
        public string? TenantId => "tenant-a";
    }
}
