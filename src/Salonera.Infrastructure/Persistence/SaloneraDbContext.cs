using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Salonera.Application.Common.Persistence;
using Salonera.Application.Common.Time;
using Salonera.Domain.Common;
using Salonera.Domain.Entities;

namespace Salonera.Infrastructure.Persistence;

public sealed class SaloneraDbContext(
    DbContextOptions<SaloneraDbContext> options,
    IClock clock) : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Hall> Halls => Set<Hall>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<EventType> EventTypes => Set<EventType>();
    public DbSet<OrganizationEvent> Events => Set<OrganizationEvent>();
    public DbSet<EventStatusHistory> EventStatusHistories => Set<EventStatusHistory>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<PackageItem> PackageItems => Set<PackageItem>();
    public DbSet<ExtraService> ExtraServices => Set<ExtraService>();
    public DbSet<EventService> EventServices => Set<EventService>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<PaymentPlan> PaymentPlans => Set<PaymentPlan>();
    public DbSet<PaymentReceipt> Collections => Set<PaymentReceipt>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<IncomeCategory> IncomeCategories => Set<IncomeCategory>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierTransaction> SupplierTransactions => Set<SupplierTransaction>();
    public DbSet<CashAccount> CashAccounts => Set<CashAccount>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<FinancialTransaction> FinancialTransactions => Set<FinancialTransaction>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ApplicationSetting> ApplicationSettings => Set<ApplicationSetting>();
    public DbSet<BackupRecord> BackupRecords => Set<BackupRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ModelConfiguration.Configure(modelBuilder);
        ConfigureSharedConventions(modelBuilder);
        SeedData.Configure(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditFields();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditFields();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await operation(cancellationToken);
            await SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static void ConfigureSharedConventions(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(type => typeof(AuditableEntity).IsAssignableFrom(type.ClrType)))
        {
            modelBuilder.Entity(entityType.ClrType).HasKey(nameof(AuditableEntity.Id));
            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(AuditableEntity.RowVersion))
                .IsRowVersion();

            var parameter = Expression.Parameter(entityType.ClrType, "entity");
            var isDeleted = Expression.Property(parameter, nameof(AuditableEntity.IsDeleted));
            var filter = Expression.Lambda(Expression.Not(isDeleted), parameter);
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);

            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                {
                    property.SetPrecision(18);
                    property.SetScale(2);
                }

                if (property.ClrType == typeof(string) && property.GetMaxLength() is null)
                {
                    property.SetMaxLength(500);
                }
            }
        }
    }

    private void ApplyAuditFields()
    {
        var now = clock.UtcNow;
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
