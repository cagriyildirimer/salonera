using Microsoft.AspNetCore.Identity;
using Salonera.Application.Common.Security;
using ApplicationPasswordVerificationResult = Salonera.Application.Common.Security.PasswordVerificationResult;

namespace Salonera.Infrastructure.Security;

public sealed class PasswordService : IPasswordService
{
    private static readonly object PasswordOwner = new();
    private readonly PasswordHasher<object> _passwordHasher = new();

    public string HashPassword(string password)
    {
        ValidatePassword(password);
        return _passwordHasher.HashPassword(PasswordOwner, password);
    }

    public ApplicationPasswordVerificationResult Verify(string passwordHash, string suppliedPassword)
    {
        if (string.IsNullOrWhiteSpace(passwordHash) || string.IsNullOrEmpty(suppliedPassword))
        {
            return ApplicationPasswordVerificationResult.Failed;
        }

        return _passwordHasher.VerifyHashedPassword(PasswordOwner, passwordHash, suppliedPassword) switch
        {
            Microsoft.AspNetCore.Identity.PasswordVerificationResult.Success
                => ApplicationPasswordVerificationResult.Success,
            Microsoft.AspNetCore.Identity.PasswordVerificationResult.SuccessRehashNeeded
                => ApplicationPasswordVerificationResult.SuccessRehashNeeded,
            _ => ApplicationPasswordVerificationResult.Failed,
        };
    }

    private static void ValidatePassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        if (password.Length < 12 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            password.All(char.IsLetterOrDigit))
        {
            throw new ArgumentException(
                "Parola en az 12 karakter olmalı; büyük harf, küçük harf, rakam ve özel karakter içermelidir.",
                nameof(password));
        }
    }
}
