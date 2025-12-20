using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Enakliyat.Web.Attributes;

public class TurkishPhoneAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            return true; // Optional field

        var phoneNumber = value.ToString()!;
        var cleaned = Regex.Replace(phoneNumber, @"[\s\-\(\)]", "");
        var pattern = @"^(\+90|0)?[5][0-9]{9}$";

        return Regex.IsMatch(cleaned, pattern);
    }

    public override string FormatErrorMessage(string name)
    {
        return $"Geçerli bir telefon numarası giriniz. (Örn: 0532 123 45 67)";
    }
}

