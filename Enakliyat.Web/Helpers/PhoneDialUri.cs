namespace Enakliyat.Web.Helpers;

public static class PhoneDialUri
{
    /// <summary>Boşluk/metinden arama için tel: URI üretir (Türkiye cep: 05xx veya 5xx).</summary>
    public static string? BuildTelHref(string? displayPhone)
    {
        if (string.IsNullOrWhiteSpace(displayPhone))
            return null;

        var digits = new string(displayPhone.Where(char.IsDigit).ToArray());
        if (digits.Length < 10)
            return null;

        if (digits.StartsWith("90", StringComparison.Ordinal) && digits.Length >= 12)
            return "tel:+" + digits;

        if (digits[0] == '0' && digits.Length >= 11)
            return "tel:+90" + digits[1..];

        if (digits.Length == 10 && digits[0] == '5')
            return "tel:+90" + digits;

        return "tel:+" + digits;
    }

    /// <summary>WhatsApp web/app için wa.me bağlantısı (ülke kodu + numara, sadece rakam).</summary>
    public static string? BuildWhatsAppMeUrl(string? displayPhone)
    {
        if (string.IsNullOrWhiteSpace(displayPhone))
            return null;

        var digits = new string(displayPhone.Where(char.IsDigit).ToArray());
        if (digits.Length < 10)
            return null;

        string n;
        if (digits.StartsWith("90", StringComparison.Ordinal) && digits.Length >= 12)
            n = digits;
        else if (digits[0] == '0' && digits.Length >= 11)
            n = "90" + digits[1..];
        else if (digits.Length == 10 && digits[0] == '5')
            n = "90" + digits;
        else
            n = digits;

        return "https://wa.me/" + n;
    }
}
