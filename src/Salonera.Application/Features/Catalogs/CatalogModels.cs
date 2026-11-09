namespace Salonera.Application.Features.Catalogs;

public sealed record BranchListItem(Guid Id, string Code, string Name, bool IsActive);

public sealed record HallListItem(Guid Id, Guid BranchId, string Code, string Name, int Capacity, bool IsActive);

public sealed record EventTypeListItem(Guid Id, string Name, bool IsActive);

public interface ICatalogService
{
    Task<IReadOnlyList<BranchListItem>> ListBranchesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HallListItem>> ListHallsAsync(Guid? branchId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EventTypeListItem>> ListEventTypesAsync(CancellationToken cancellationToken = default);

    Task<Guid> CreateBranchAsync(string code, string name, CancellationToken cancellationToken = default);

    Task<Guid> CreateHallAsync(
        Guid branchId,
        string code,
        string name,
        int capacity,
        CancellationToken cancellationToken = default);

    Task UpdateHallAsync(
        Guid id,
        string code,
        string name,
        int capacity,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task DeleteHallAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> CreateEventTypeAsync(string name, CancellationToken cancellationToken = default);
}
