using Microsoft.EntityFrameworkCore;
using Salonera.Domain.Entities;

namespace Salonera.Infrastructure.Persistence;

internal static class ModelConfiguration
{
    private const string ActiveFilter = "[IsDeleted] = 0";

    public static void Configure(ModelBuilder modelBuilder)
    {
        ConfigureSecurity(modelBuilder);
        ConfigureOrganization(modelBuilder);
        ConfigureFinance(modelBuilder);
        ConfigureSystem(modelBuilder);

        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(type => type.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }
    }

    private static void ConfigureSecurity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(item => item.Username).HasMaxLength(100).IsRequired();
            entity.Property(item => item.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => new { item.BranchId, item.Username }).IsUnique().HasFilter(ActiveFilter);
            entity.HasOne(item => item.Branch).WithMany().HasForeignKey(item => item.BranchId);
        });
        modelBuilder.Entity<Role>(entity =>
        {
            entity.Property(item => item.Code).HasMaxLength(50).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => item.Code).IsUnique().HasFilter(ActiveFilter);
        });
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasIndex(item => new { item.UserId, item.RoleId }).IsUnique().HasFilter(ActiveFilter);
            entity.HasOne(item => item.User).WithMany(item => item.UserRoles).HasForeignKey(item => item.UserId);
            entity.HasOne(item => item.Role).WithMany(item => item.UserRoles).HasForeignKey(item => item.RoleId);
        });
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(item => item.Action).HasMaxLength(100).IsRequired();
            entity.Property(item => item.EntityName).HasMaxLength(200).IsRequired();
            entity.Property(item => item.OldValuesJson).HasColumnType("nvarchar(max)");
            entity.Property(item => item.NewValuesJson).HasColumnType("nvarchar(max)");
            entity.HasIndex(item => new { item.BranchId, item.CreatedAt });
            entity.HasIndex(item => new { item.UserId, item.CreatedAt });
        });
    }

    private static void ConfigureOrganization(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Branch>(entity =>
        {
            entity.Property(item => item.Code).HasMaxLength(30).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => item.Code).IsUnique().HasFilter(ActiveFilter);
        });
        modelBuilder.Entity<Hall>(entity =>
        {
            entity.Property(item => item.Code).HasMaxLength(30).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => new { item.BranchId, item.Code }).IsUnique().HasFilter(ActiveFilter);
            entity.HasOne(item => item.Branch).WithMany(item => item.Halls).HasForeignKey(item => item.BranchId);
        });
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.Property(item => item.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(item => item.LastName).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Phone).HasMaxLength(30).IsRequired();
            entity.Property(item => item.NationalIdentityNumber).HasMaxLength(11);
            entity.Property(item => item.Notes).HasColumnType("nvarchar(max)");
            entity.HasIndex(item => new { item.BranchId, item.Phone });
            entity.HasIndex(item => item.NationalIdentityNumber).HasFilter("[NationalIdentityNumber] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasOne(item => item.Branch).WithMany().HasForeignKey(item => item.BranchId);
        });
        modelBuilder.Entity<EventType>(entity =>
        {
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => new { item.BranchId, item.Name }).IsUnique().HasFilter(ActiveFilter);
            entity.HasOne(item => item.Branch).WithMany().HasForeignKey(item => item.BranchId);
        });
        modelBuilder.Entity<OrganizationEvent>(entity =>
        {
            entity.ToTable("Events", table => table.HasCheckConstraint("CK_Events_TimeRange", "[EndsAt] > [StartsAt]"));
            entity.Property(item => item.EventNumber).HasMaxLength(50).IsRequired();
            entity.Property(item => item.SpecialRequests).HasColumnType("nvarchar(max)");
            entity.Property(item => item.InternalNotes).HasColumnType("nvarchar(max)");
            entity.HasIndex(item => new { item.BranchId, item.EventNumber }).IsUnique().HasFilter(ActiveFilter);
            entity.HasIndex(item => item.StartsAt);
            entity.HasIndex(item => new { item.HallId, item.StartsAt, item.EndsAt, item.Status });
            entity.HasOne(item => item.Branch).WithMany().HasForeignKey(item => item.BranchId);
            entity.HasOne(item => item.Hall).WithMany().HasForeignKey(item => item.HallId);
            entity.HasOne(item => item.Customer).WithMany().HasForeignKey(item => item.CustomerId);
            entity.HasOne(item => item.EventType).WithMany().HasForeignKey(item => item.EventTypeId);
        });
        modelBuilder.Entity<EventStatusHistory>(entity =>
        {
            entity.HasIndex(item => new { item.EventId, item.CreatedAt });
            entity.HasOne(item => item.Event).WithMany(item => item.StatusHistory).HasForeignKey(item => item.EventId);
        });
        modelBuilder.Entity<Package>(entity =>
        {
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => new { item.BranchId, item.Name }).IsUnique().HasFilter(ActiveFilter);
            entity.HasOne(item => item.Branch).WithMany().HasForeignKey(item => item.BranchId);
        });
        modelBuilder.Entity<PackageItem>(entity =>
        {
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.HasOne(item => item.Package).WithMany(item => item.Items).HasForeignKey(item => item.PackageId);
        });
        modelBuilder.Entity<ExtraService>(entity =>
        {
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => new { item.BranchId, item.Name }).IsUnique().HasFilter(ActiveFilter);
            entity.HasOne(item => item.Branch).WithMany().HasForeignKey(item => item.BranchId);
        });
        modelBuilder.Entity<EventService>(entity =>
        {
            entity.Property(item => item.DescriptionSnapshot).HasMaxLength(500).IsRequired();
            entity.HasOne(item => item.Event).WithMany(item => item.Services).HasForeignKey(item => item.EventId);
            entity.HasOne(item => item.Package).WithMany().HasForeignKey(item => item.PackageId);
            entity.HasOne(item => item.ExtraService).WithMany().HasForeignKey(item => item.ExtraServiceId);
        });
        modelBuilder.Entity<Contract>(entity =>
        {
            entity.Property(item => item.ContractNumber).HasMaxLength(50).IsRequired();
            entity.HasIndex(item => item.EventId).IsUnique().HasFilter(ActiveFilter);
            entity.HasIndex(item => new { item.BranchId, item.ContractNumber }).IsUnique().HasFilter(ActiveFilter);
            entity.ToTable(table => table.HasCheckConstraint("CK_Contracts_ExchangeRate", "[ExchangeRate] > 0"));
            entity.HasOne(item => item.Branch).WithMany().HasForeignKey(item => item.BranchId);
            entity.HasOne(item => item.Event).WithOne(item => item.Contract).HasForeignKey<Contract>(item => item.EventId);
        });
    }

    private static void ConfigureFinance(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PaymentPlan>(entity =>
        {
            entity.HasIndex(item => item.DueDate);
            entity.HasOne(item => item.Contract).WithMany(item => item.PaymentPlans).HasForeignKey(item => item.ContractId);
        });
        modelBuilder.Entity<PaymentReceipt>(entity =>
        {
            entity.ToTable("Collections");
            entity.Property(item => item.ReceiptNumber).HasMaxLength(50).IsRequired();
            entity.HasIndex(item => new { item.BranchId, item.ReceiptNumber }).IsUnique().HasFilter(ActiveFilter);
            entity.HasIndex(item => item.CollectedAt);
            entity.HasIndex(item => item.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
            entity.HasOne(item => item.Contract).WithMany().HasForeignKey(item => item.ContractId);
            entity.HasOne(item => item.PaymentPlan).WithMany(item => item.Collections).HasForeignKey(item => item.PaymentPlanId);
        });
        modelBuilder.Entity<Refund>(entity =>
        {
            entity.HasIndex(item => item.RefundedAt);
            entity.HasOne(item => item.Contract).WithMany().HasForeignKey(item => item.ContractId);
            entity.HasOne(item => item.Collection).WithMany().HasForeignKey(item => item.CollectionId);
        });
        modelBuilder.Entity<IncomeCategory>(entity =>
        {
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => new { item.BranchId, item.Name }).IsUnique().HasFilter(ActiveFilter);
        });
        modelBuilder.Entity<ExpenseCategory>(entity =>
        {
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => new { item.BranchId, item.Name }).IsUnique().HasFilter(ActiveFilter);
        });
        modelBuilder.Entity<Income>(entity =>
        {
            entity.HasIndex(item => item.IncomeDate);
            entity.HasIndex(item => item.DocumentNumber);
            entity.HasIndex(item => item.CollectionId).IsUnique().HasFilter("[CollectionId] IS NOT NULL AND [IsDeleted] = 0");
            entity.HasOne(item => item.IncomeCategory).WithMany().HasForeignKey(item => item.IncomeCategoryId);
            entity.HasOne(item => item.Event).WithMany().HasForeignKey(item => item.EventId);
            entity.HasOne(item => item.Customer).WithMany().HasForeignKey(item => item.CustomerId);
            entity.HasOne(item => item.Collection).WithMany().HasForeignKey(item => item.CollectionId);
        });
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasIndex(item => item.ExpenseDate);
            entity.HasIndex(item => item.DueDate);
            entity.HasIndex(item => item.DocumentNumber);
            entity.HasOne(item => item.ExpenseCategory).WithMany().HasForeignKey(item => item.ExpenseCategoryId);
            entity.HasOne(item => item.Supplier).WithMany().HasForeignKey(item => item.SupplierId);
            entity.HasOne(item => item.Event).WithMany().HasForeignKey(item => item.EventId);
        });
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.Property(item => item.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => new { item.BranchId, item.Name });
            entity.HasIndex(item => item.TaxNumber).HasFilter("[TaxNumber] IS NOT NULL AND [IsDeleted] = 0");
        });
        modelBuilder.Entity<SupplierTransaction>(entity =>
        {
            entity.HasIndex(item => new { item.SupplierId, item.TransactionDate });
            entity.HasIndex(item => item.DocumentNumber);
            entity.HasOne(item => item.Supplier).WithMany(item => item.Transactions).HasForeignKey(item => item.SupplierId);
            entity.HasOne(item => item.Event).WithMany().HasForeignKey(item => item.EventId);
        });
        modelBuilder.Entity<CashAccount>(entity =>
        {
            entity.Property(item => item.Code).HasMaxLength(30).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(item => new { item.BranchId, item.Code }).IsUnique().HasFilter(ActiveFilter);
        });
        modelBuilder.Entity<BankAccount>(entity =>
        {
            entity.Property(item => item.Code).HasMaxLength(30).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Iban).HasMaxLength(34);
            entity.HasIndex(item => new { item.BranchId, item.Code }).IsUnique().HasFilter(ActiveFilter);
            entity.HasIndex(item => item.Iban).HasFilter("[Iban] IS NOT NULL AND [IsDeleted] = 0");
        });
        modelBuilder.Entity<FinancialTransaction>(entity =>
        {
            entity.HasIndex(item => new { item.AccountId, item.TransactionDate });
            entity.HasIndex(item => item.DocumentNumber);
            entity.HasIndex(item => item.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        });
        modelBuilder.Entity<Transfer>(entity =>
        {
            entity.HasIndex(item => item.TransferDate);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Transfers_DifferentAccounts",
                "[FromAccountId] <> [ToAccountId]"));
        });
    }

    private static void ConfigureSystem(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.Property(item => item.FileName).HasMaxLength(260).IsRequired();
            entity.Property(item => item.StoredPath).HasMaxLength(1000).IsRequired();
            entity.HasIndex(item => new { item.EntityName, item.EntityId });
        });
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.Property(item => item.Title).HasMaxLength(200).IsRequired();
            entity.HasIndex(item => new { item.UserId, item.DueAt, item.ReadAt });
        });
        modelBuilder.Entity<ApplicationSetting>(entity =>
        {
            entity.Property(item => item.Key).HasMaxLength(200).IsRequired();
            entity.Property(item => item.Value).HasColumnType("nvarchar(max)").IsRequired();
            entity.HasIndex(item => new { item.BranchId, item.Key }).IsUnique().HasFilter(ActiveFilter);
        });
        modelBuilder.Entity<BackupRecord>(entity =>
        {
            entity.Property(item => item.FilePath).HasMaxLength(1000).IsRequired();
            entity.HasIndex(item => new { item.BranchId, item.StartedAt });
        });
    }
}
