using System.Collections.ObjectModel;
using Salonera.Application.Features.Catalogs;

namespace Salonera.Desktop.ViewModels;

public sealed class CatalogManagementViewModel(ICatalogService catalogService) : ObservableObject
{
    private BranchListItem? _selectedBranch;
    private string _branchCode = string.Empty;
    private string _branchName = string.Empty;
    private string _hallCode = string.Empty;
    private string _hallName = string.Empty;
    private string _capacity = "100";
    private string _eventTypeName = string.Empty;
    private string _message = string.Empty;

    public ObservableCollection<BranchListItem> Branches { get; } = [];
    public ObservableCollection<HallListItem> Halls { get; } = [];
    public ObservableCollection<EventTypeListItem> EventTypes { get; } = [];
    public BranchListItem? SelectedBranch { get => _selectedBranch; set => SetProperty(ref _selectedBranch, value); }
    public string BranchCode { get => _branchCode; set => SetProperty(ref _branchCode, value); }
    public string BranchName { get => _branchName; set => SetProperty(ref _branchName, value); }
    public string HallCode { get => _hallCode; set => SetProperty(ref _hallCode, value); }
    public string HallName { get => _hallName; set => SetProperty(ref _hallName, value); }
    public string Capacity { get => _capacity; set => SetProperty(ref _capacity, value); }
    public string EventTypeName { get => _eventTypeName; set => SetProperty(ref _eventTypeName, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }

    public async Task LoadAsync()
    {
        var branches = await catalogService.ListBranchesAsync();
        Branches.Clear();
        foreach (var branch in branches) Branches.Add(branch);
        SelectedBranch ??= Branches.FirstOrDefault();

        var halls = await catalogService.ListHallsAsync(SelectedBranch?.Id);
        Halls.Clear();
        foreach (var hall in halls) Halls.Add(hall);

        var eventTypes = await catalogService.ListEventTypesAsync();
        EventTypes.Clear();
        foreach (var eventType in eventTypes) EventTypes.Add(eventType);
    }

    public async Task CreateBranchAsync()
    {
        await catalogService.CreateBranchAsync(BranchCode, BranchName);
        BranchCode = BranchName = string.Empty;
        Message = "Şube eklendi.";
        await LoadAsync();
    }

    public async Task CreateHallAsync()
    {
        if (SelectedBranch is null || !int.TryParse(Capacity, out var capacity))
        {
            Message = "Şube ve geçerli kapasite seçin.";
            return;
        }

        await catalogService.CreateHallAsync(SelectedBranch.Id, HallCode, HallName, capacity);
        HallCode = HallName = string.Empty;
        Message = "Salon eklendi.";
        await LoadAsync();
    }

    public async Task CreateEventTypeAsync()
    {
        await catalogService.CreateEventTypeAsync(EventTypeName);
        EventTypeName = string.Empty;
        Message = "Organizasyon türü eklendi.";
        await LoadAsync();
    }
}
