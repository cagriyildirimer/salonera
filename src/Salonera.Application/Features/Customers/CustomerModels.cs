namespace Salonera.Application.Features.Customers;

public sealed record CustomerListItem(
    Guid Id,
    string FullName,
    string Phone,
    string? Email,
    string? BrideName,
    string? GroomName);

public sealed record CustomerDetail(
    Guid Id,
    string FirstName,
    string LastName,
    string Phone,
    string? SecondaryPhone,
    string? Email,
    string? Address,
    string? NationalIdentityNumber,
    string? BrideName,
    string? GroomName,
    string? ContactPerson,
    string? Notes,
    string? KvkkConsentNote,
    byte[] RowVersion);

public sealed record SaveCustomerRequest(
    string FirstName,
    string LastName,
    string Phone,
    string? SecondaryPhone = null,
    string? Email = null,
    string? Address = null,
    string? NationalIdentityNumber = null,
    string? BrideName = null,
    string? GroomName = null,
    string? ContactPerson = null,
    string? Notes = null,
    string? KvkkConsentNote = null);

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerListItem>> ListAsync(
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<CustomerDetail> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(SaveCustomerRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Guid id,
        SaveCustomerRequest request,
        byte[] rowVersion,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
