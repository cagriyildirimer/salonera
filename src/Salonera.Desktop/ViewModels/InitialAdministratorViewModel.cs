using Salonera.Application.Common.Security;

namespace Salonera.Desktop.ViewModels;

public sealed class InitialAdministratorViewModel(IUserManagementService userManagementService) : ObservableObject
{
    private string _username = "admin";
    private string _displayName = string.Empty;
    private string _message = string.Empty;
    private bool _isBusy;

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public async Task<bool> CreateAsync(string password, string confirmation)
    {
        if (password != confirmation)
        {
            Message = "Parolalar eşleşmiyor.";
            return false;
        }

        IsBusy = true;
        try
        {
            await userManagementService.CreateInitialAdministratorAsync(Username, DisplayName, password);
            return true;
        }
        catch (ArgumentException exception)
        {
            Message = exception.Message;
            return false;
        }
        catch (InvalidOperationException exception)
        {
            Message = exception.Message;
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
