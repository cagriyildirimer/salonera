using Microsoft.EntityFrameworkCore;
using Salonera.Application.Common.Security;
using Salonera.Application.Features.Finance;
using Salonera.Domain.Entities;

namespace Salonera.Infrastructure.Features;

public sealed partial class FinanceService
{
    public async Task<IReadOnlyList<SupplierItem>> ListSuppliersAsync(CancellationToken ct = default)
    {
        var user = User(Permissions.FinanceView);
        var (scope, db) = Context();
        await using var disposal = scope;
        var suppliers = await db.Suppliers.AsNoTracking().Where(x => x.BranchId == user.BranchId)
            .OrderBy(x => x.Name).ToListAsync(ct);
        var ledger = await db.SupplierTransactions.AsNoTracking()
            .Where(x => x.BranchId == user.BranchId && !x.IsCancelled).ToListAsync(ct);
        var balances = ledger.GroupBy(x => x.SupplierId)
            .ToDictionary(x => x.Key, x => x.Sum(y =>
                decimal.Round((y.Credit - y.Debit) * y.ExchangeRate, 2)));
        return suppliers.Select(x => new SupplierItem(x.Id, x.Name, x.Phone,
            x.TaxNumber, balances.GetValueOrDefault(x.Id), x.IsActive)).ToArray();
    }

    public async Task<IReadOnlyList<SupplierMovementItem>> ListSupplierMovementsAsync(
        Guid supplierId, CancellationToken ct = default)
    {
        var user = User(Permissions.FinanceView);
        var (scope, db) = Context();
        await using var disposal = scope;
        if (!await db.Suppliers.AnyAsync(x => x.Id == supplierId && x.BranchId == user.BranchId, ct))
            throw new InvalidOperationException("Tedarikçi bulunamadı.");
        return await db.SupplierTransactions.AsNoTracking()
            .Where(x => x.SupplierId == supplierId && x.BranchId == user.BranchId && !x.IsCancelled)
            .OrderByDescending(x => x.TransactionDate)
            .Select(x => new SupplierMovementItem(x.Id, x.TransactionDate,
                x.Debit, x.Credit, x.Currency, x.Description)).ToListAsync(ct);
    }

    public async Task<Guid> CreateSupplierAsync(NewSupplier request, CancellationToken ct = default)
    {
        var user = User(Permissions.FinanceManage);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        var name = request.Name.Trim();
        if (name.Length > 200) throw new ArgumentException("Tedarikçi adı çok uzun.");
        var (scope, db) = Context();
        await using var disposal = scope;
        if (await db.Suppliers.AnyAsync(x => x.BranchId == user.BranchId && x.Name == name, ct))
            throw new InvalidOperationException("Bu tedarikçi zaten kayıtlı.");
        var supplier = new Supplier
        {
            BranchId = user.BranchId,
            Name = name,
            ContactPerson = request.ContactPerson?.Trim(),
            Phone = request.Phone?.Trim(),
            Email = request.Email?.Trim(),
            TaxOffice = request.TaxOffice?.Trim(),
            TaxNumber = request.TaxNumber?.Trim(),
            Address = request.Address?.Trim(),
            BankInformation = request.BankInformation?.Trim(),
        };
        db.Suppliers.Add(supplier);
        db.AuditLogs.Add(Audit(user, nameof(Supplier), supplier.Id, "SUPPLIER_CREATED"));
        await db.SaveChangesAsync(ct);
        return supplier.Id;
    }
}
