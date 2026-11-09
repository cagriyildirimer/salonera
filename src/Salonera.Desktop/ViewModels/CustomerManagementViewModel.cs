using System.Collections.ObjectModel;
using Salonera.Application.Features.Customers;

namespace Salonera.Desktop.ViewModels;

public sealed class CustomerManagementViewModel(ICustomerService customerService) : ObservableObject
{
    private CustomerListItem? _selectedCustomer;
    private string _search = string.Empty;
    private string _firstName = string.Empty;
    private string _lastName = string.Empty;
    private string _phone = string.Empty;
    private string _email = string.Empty;
    private string _brideName = string.Empty;
    private string _groomName = string.Empty;
    private string _message = string.Empty;

    public ObservableCollection<CustomerListItem> Customers { get; } = [];

    public CustomerListItem? SelectedCustomer { get => _selectedCustomer; set => SetProperty(ref _selectedCustomer, value); }
    public string Search { get => _search; set => SetProperty(ref _search, value); }
    public string FirstName { get => _firstName; set => SetProperty(ref _firstName, value); }
    public string LastName { get => _lastName; set => SetProperty(ref _lastName, value); }
    public string Phone { get => _phone; set => SetProperty(ref _phone, value); }
    public string Email { get => _email; set => SetProperty(ref _email, value); }
    public string BrideName { get => _brideName; set => SetProperty(ref _brideName, value); }
    public string GroomName { get => _groomName; set => SetProperty(ref _groomName, value); }
    public string Message { get => _message; private set => SetProperty(ref _message, value); }

    public async Task LoadAsync()
    {
        var customers = await customerService.ListAsync(Search);
        Customers.Clear();
        foreach (var customer in customers)
        {
            Customers.Add(customer);
        }
    }

    public async Task CreateAsync()
    {
        try
        {
            await customerService.CreateAsync(new SaveCustomerRequest(
                FirstName,
                LastName,
                Phone,
                Email: Email,
                BrideName: BrideName,
                GroomName: GroomName));
            FirstName = LastName = Phone = Email = BrideName = GroomName = string.Empty;
            Message = "Müşteri kaydedildi.";
            await LoadAsync();
        }
        catch (ArgumentException exception)
        {
            Message = exception.Message;
        }
    }

    public async Task DeleteSelectedAsync()
    {
        if (SelectedCustomer is null)
        {
            Message = "Önce bir müşteri seçin.";
            return;
        }

        await customerService.DeleteAsync(SelectedCustomer.Id);
        Message = "Müşteri kaydı arşivlendi.";
        await LoadAsync();
    }
}
