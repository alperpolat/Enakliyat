namespace Enakliyat.Web.Helpers;

/// <summary>İletimX / SMS ile uyumlu: 10 hane 5xxxxxxxxx (ülke kodu ve baştaki 0 yok).</summary>
public static class PhoneNumberHelper
{
    public static string? NormalizeTurkishMobile(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var cleaned = new string(phone.Where(char.IsDigit).ToArray());

        if (cleaned.StartsWith('0'))
        {
            cleaned = cleaned[1..];
        }

        if (cleaned.StartsWith("90", StringComparison.Ordinal))
        {
            cleaned = cleaned[2..];
        }

        if (cleaned.Length != 10 || cleaned[0] != '5')
        {
            return null;
        }

        return cleaned;
    }
}
