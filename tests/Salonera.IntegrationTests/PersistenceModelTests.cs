using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Salonera.Application.Common.Time;
using Salonera.Domain.Common;
using Salonera.Domain.Entities;
using Salonera.Infrastructure.Persistence;

namespace Salonera.IntegrationTests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void ModelContainsAllRequiredEntitiesAndConcurrencyControls()
    {
        using var context = CreateSqlServerContext();
        var auditableTypes = context.Model.GetEntityTypes()
            .Where(type => typeof(AuditableEntity).IsAssignableFrom(type.ClrType))
            .ToArray();

        Assert.Equal(32, auditableTypes.Length);
        Assert.All(auditableTypes, entityType =>
        {
            Assert.NotEmpty(entityType.GetDeclaredQueryFilters());
            Assert.True(entityType.FindProperty(nameof(AuditableEntity.RowVersion))?.IsConcurrencyToken);
        });
    }

    [Fact]
    public void InitialMigrationProducesExpectedSqlServerSchema()
    {
        using var context = CreateSqlServerContext();
        var migrations = context.Database.GetMigrations().ToArray();
        var script = context.GetService<IMigrator>().GenerateScript();

        Assert.Single(migrations);
        Assert.EndsWith("_InitialCreate", migrations[0], StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [Events]", script, StringComparison.Ordinal);
        Assert.Contains("[RowVersion] rowversion", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IX_Contracts_BranchId_ContractNumber", script, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO [Roles]", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveChangesAppliesAuditFieldsAndSoftDeleteFilter()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 9, 9, 30, 0, TimeSpan.Zero));
        var options = new DbContextOptionsBuilder<SaloneraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new SaloneraDbContext(options, clock);
        var branch = new Branch { Code = "TEST", Name = "Anonim Test Şubesi" };

        context.Branches.Add(branch);
        await context.SaveChangesAsync();

        Assert.Equal(clock.UtcNow, branch.CreatedAt);

        context.Branches.Remove(branch);
        await context.SaveChangesAsync();

        Assert.True(branch.IsDeleted);
        Assert.Equal(clock.UtcNow, branch.UpdatedAt);
        Assert.Empty(await context.Branches.ToListAsync());
        Assert.Single(await context.Branches.IgnoreQueryFilters().ToListAsync());
    }

    private static SaloneraDbContext CreateSqlServerContext()
    {
        var options = new DbContextOptionsBuilder<SaloneraDbContext>()
            .UseSqlServer("Server=localhost;Database=SaloneraMigrationTest;TrustServerCertificate=True")
            .Options;

        return new SaloneraDbContext(options, new FixedClock(DateTimeOffset.UtcNow));
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;

        public DateTimeOffset TurkeyNow => now.ToOffset(TimeSpan.FromHours(3));
    }
}
