using System.Text.RegularExpressions;

namespace Enakliyat.Web.Helpers;

public static class PasswordPolicyHelper
{
    public static (bool IsValid, string ErrorMessage) ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return (false, "Şifre gereklidir.");

        if (password.Length < 6)
            return (false, "Şifre en az 6 karakter olmalıdır.");

        if (password.Length > 100)
            return (false, "Şifre en fazla 100 karakter olabilir.");

        // Optional: Add more complex requirements
        // if (!Regex.IsMatch(password, @"[A-Z]"))
        //     return (false, "Şifre en az bir büyük harf içermelidir.");
        
        // if (!Regex.IsMatch(password, @"[a-z]"))
        //     return (false, "Şifre en az bir küçük harf içermelidir.");
        
        // if (!Regex.IsMatch(password, @"[0-9]"))
        //     return (false, "Şifre en az bir rakam içermelidir.");

        return (true, string.Empty);
    }
}

