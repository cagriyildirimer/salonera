using System.Collections.ObjectModel;
using Salonera.Application.Common.Security;

namespace Salonera.Desktop.ViewModels;

public sealed class UserManagementViewModel(
    IUserManagementService userManagementService,
    ICurrentUser currentUser) : ObservableObject
{
    private string _username = string.Empty;
    private string _displayName = string.Empty;
    private string _selectedRoleCode = RoleCodes.Sales;
    private string _message = string.Empty;
    private UserListItem? _selectedUser;

    public ObservableCollection<UserListItem> Users { get; } = [];

    public IReadOnlyList<RoleOption> RoleOptions { get; } =
    [
        new(RoleCodes.Administrator, "Yönetici"),
        new(RoleCodes.Sales, "Satış"),
        new(RoleCodes.Accounting, "Muhasebe"),
        new(RoleCodes.Viewer, "Görüntüleme"),
    ];

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

    public string SelectedRoleCode
    {
        get => _selectedRoleCode;
        set => SetProperty(ref _selectedRoleCode, value);
    }

    public string Message
    {
        get => _message;
        private set => SetProperty(ref _message, value);
    }

    public UserListItem? SelectedUser
    {
        get => _selectedUser;
        set => SetProperty(ref _selectedUser, value);
    }

    public async Task LoadAsync()
    {
        var users = await userManagementService.ListUsersAsync();
        Users.Clear();
        foreach (var user in users)
        {
            Users.Add(user);
        }
    }

    public async Task<bool> CreateAsync(string password)
    {
        try
        {
            var branchId = currentUser.User?.BranchId
                ?? throw new AuthorizationException("Aktif kullanıcı bulunamadı.");
            await userManagementService.CreateUserAsync(new CreateUserRequest(
                branchId,
                Username,
                DisplayName,
                password,
                [SelectedRoleCode]));
            Username = string.Empty;
            DisplayName = string.Empty;
            Message = "Kullanıcı oluşturuldu. İlk girişte parola değişikliği istenecek.";
            await LoadAsync();
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

    public async Task ToggleSelectedUserAsync()
    {
        if (SelectedUser is null)
        {
            Message = "Önce bir kullanıcı seçin.";
            return;
        }

        try
        {
            await userManagementService.SetUserActiveAsync(SelectedUser.Id, !SelectedUser.IsActive);
            Message = SelectedUser.IsActive ? "Kullanıcı pasif yapıldı." : "Kullanıcı aktifleştirildi.";
            await LoadAsync();
        }
        catch (InvalidOperationException exception)
        {
            Message = exception.Message;
        }
    }

    public sealed record RoleOption(string Code, string Name);
}
