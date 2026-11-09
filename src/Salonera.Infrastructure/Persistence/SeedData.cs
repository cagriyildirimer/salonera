using Microsoft.EntityFrameworkCore;
using Salonera.Domain.Entities;

namespace Salonera.Infrastructure.Persistence;

internal static class SeedData
{
    public static readonly Guid DefaultBranchId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset SeedDate = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Branch>().HasData(new Branch
        {
            Id = DefaultBranchId,
            Code = "MRKZ",
            Name = "Merkez Şube",
            CreatedAt = SeedDate,
            IsActive = true,
        });

        modelBuilder.Entity<Role>().HasData(
            CreateRole("20000000-0000-0000-0000-000000000001", "ADMIN", "Yönetici"),
            CreateRole("20000000-0000-0000-0000-000000000002", "SALES", "Satış"),
            CreateRole("20000000-0000-0000-0000-000000000003", "ACCOUNTING", "Muhasebe"),
            CreateRole("20000000-0000-0000-0000-000000000004", "VIEWER", "Görüntüleme"));

        modelBuilder.Entity<IncomeCategory>().HasData(new IncomeCategory
        {
            Id = Guid.Parse("30000000-0000-0000-0000-000000000001"),
            BranchId = DefaultBranchId,
            Name = "Diğer Gelir",
            CreatedAt = SeedDate,
        });
        modelBuilder.Entity<ExpenseCategory>().HasData(new ExpenseCategory
        {
            Id = Guid.Parse("40000000-0000-0000-0000-000000000001"),
            BranchId = DefaultBranchId,
            Name = "Diğer Gider",
            CreatedAt = SeedDate,
        });
    }

    private static Role CreateRole(string id, string code, string name) => new()
    {
        Id = Guid.Parse(id),
        Code = code,
        Name = name,
        CreatedAt = SeedDate,
    };
}
