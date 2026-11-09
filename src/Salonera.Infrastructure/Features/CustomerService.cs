using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Salonera.Application.Common.Security;
using Salonera.Application.Features.Customers;
using Salonera.Domain.Entities;
using Salonera.Infrastructure.Persistence;

namespace Salonera.Infrastructure.Features;

public sealed class CustomerService(
    IServiceScopeFactory scopeFactory,
    ICurrentUser currentUser,
    IAuthorizationService authorizationService) : ICustomerService
{
    public async Task<IReadOnlyList<CustomerListItem>> ListAsync(
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.DashboardView);
        var branchId = RequiredBranchId();
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var query = context.Customers.AsNoTracking().Where(customer => customer.BranchId == branchId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(customer =>
                customer.FirstName.Contains(term) ||
                customer.LastName.Contains(term) ||
                customer.Phone.Contains(term));
        }

        return await query
            .OrderBy(customer => customer.FirstName)
            .ThenBy(customer => customer.LastName)
            .Select(customer => new CustomerListItem(
                customer.Id,
                customer.FirstName + " " + customer.LastName,
                customer.Phone,
                customer.Email,
                customer.BrideName,
                customer.GroomName))
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerDetail> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.DashboardView);
        var branchId = RequiredBranchId();
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        return await context.Customers
            .AsNoTracking()
            .Where(customer => customer.Id == id && customer.BranchId == branchId)
            .Select(customer => new CustomerDetail(
                customer.Id,
                customer.FirstName,
                customer.LastName,
                customer.Phone,
                customer.SecondaryPhone,
                customer.Email,
                customer.Address,
                customer.NationalIdentityNumber,
                customer.BrideName,
                customer.GroomName,
                customer.ContactPerson,
                customer.Notes,
                customer.KvkkConsentNote,
                customer.RowVersion))
            .SingleAsync(cancellationToken);
    }

    public async Task<Guid> CreateAsync(
        SaveCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.CustomersManage);
        Validate(request);
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var customer = new Customer { BranchId = user.BranchId };
        Apply(customer, request);
        context.Customers.Add(customer);
        context.AuditLogs.Add(Audit(user, customer.Id, "CUSTOMER_CREATED"));
        await context.SaveChangesAsync(cancellationToken);
        return customer.Id;
    }

    public async Task UpdateAsync(
        Guid id,
        SaveCustomerRequest request,
        byte[] rowVersion,
        CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.CustomersManage);
        Validate(request);
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var customer = await context.Customers.SingleAsync(
            item => item.Id == id && item.BranchId == user.BranchId,
            cancellationToken);
        context.Entry(customer).Property(item => item.RowVersion).OriginalValue = rowVersion;
        Apply(customer, request);
        context.AuditLogs.Add(Audit(user, customer.Id, "CUSTOMER_UPDATED"));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        authorizationService.EnsurePermission(Permissions.CustomersManage);
        var user = RequiredUser();
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SaloneraDbContext>();
        var customer = await context.Customers.SingleAsync(
            item => item.Id == id && item.BranchId == user.BranchId,
            cancellationToken);
        context.Customers.Remove(customer);
        context.AuditLogs.Add(Audit(user, customer.Id, "CUSTOMER_DELETED"));
        await context.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(SaveCustomerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FirstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.LastName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Phone);
        var digitCount = request.Phone.Count(char.IsDigit);
        if (digitCount < 10)
        {
            throw new ArgumentException("Telefon numarası en az 10 rakam içermelidir.", nameof(request));
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && !MailAddress.TryCreate(request.Email, out _))
        {
            throw new ArgumentException("E-posta adresi geçerli değil.", nameof(request));
        }
    }

    private static void Apply(Customer customer, SaveCustomerRequest request)
    {
        customer.FirstName = request.FirstName.Trim();
        customer.LastName = request.LastName.Trim();
        customer.Phone = request.Phone.Trim();
        customer.SecondaryPhone = request.SecondaryPhone?.Trim();
        customer.Email = request.Email?.Trim();
        customer.Address = request.Address?.Trim();
        customer.NationalIdentityNumber = request.NationalIdentityNumber?.Trim();
        customer.BrideName = request.BrideName?.Trim();
        customer.GroomName = request.GroomName?.Trim();
        customer.ContactPerson = request.ContactPerson?.Trim();
        customer.Notes = request.Notes?.Trim();
        customer.KvkkConsentNote = request.KvkkConsentNote?.Trim();
    }

    private AuthenticatedUser RequiredUser() => currentUser.User
        ?? throw new AuthorizationException("Bu işlem için giriş yapmalısınız.");

    private Guid RequiredBranchId() => RequiredUser().BranchId;

    private static AuditLog Audit(AuthenticatedUser user, Guid entityId, string action) => new()
    {
        BranchId = user.BranchId,
        UserId = user.Id,
        Action = action,
        EntityName = nameof(Customer),
        EntityId = entityId.ToString(),
        Succeeded = true,
    };
}
