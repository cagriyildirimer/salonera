using Salonera.Application.Common.Security;

namespace Salonera.Desktop.ViewModels;

public sealed class LoginViewModel(IAuthenticationService authenticationService) : ObservableObject
{
    private string _username = string.Empty;
    private string _message = string.Empty;
    private bool _isBusy;

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
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

    public async Task<bool> LoginAsync(string password)
    {
        if (IsBusy)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            var result = await authenticationService.LoginAsync(Username, password);
            Message = result.UserMessage;
            return result.Succeeded;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
