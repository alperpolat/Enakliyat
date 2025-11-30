using System.Security.Cryptography;
using System.Text;

namespace Enakliyat.Web.Services;

public static class PasswordHasher
{
    public static string Hash(string password)
    {
        if (string.IsNullOrEmpty(password)) return string.Empty;

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash); // .NET 5+
    }

    public static bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(hash)) return false;
        return string.Equals(Hash(password), hash, StringComparison.OrdinalIgnoreCase);
    }
}
