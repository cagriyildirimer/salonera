using Salonera.Application.Common.Security;

namespace Salonera.Desktop.ViewModels;

public sealed class ChangePasswordViewModel(
    IUserManagementService userManagementService,
    ICurrentUser currentUser) : ObservableObject
{
    private string _message = string.Empty;

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public async Task<bool> ChangeAsync(string currentPassword, string newPassword, string confirmation)
    {
        if (newPassword != confirmation)
        {
            Message = "Yeni parolalar eşleşmiyor.";
            return false;
        }

        try
        {
            var userId = currentUser.User?.Id
                ?? throw new AuthorizationException("Aktif kullanıcı bulunamadı.");
            await userManagementService.ChangePasswordAsync(userId, currentPassword, newPassword);
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
    }
}
