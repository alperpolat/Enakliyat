using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Enakliyat.Web.Attributes;

public class TurkishTaxNumberAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
            return false; // Required field

        var taxNumber = value.ToString()!;
        var cleaned = Regex.Replace(taxNumber, @"[\s\-]", "");

        // TC Kimlik: 11 haneli, Vergi No: 10 haneli
        if (cleaned.Length != 10 && cleaned.Length != 11)
            return false;

        return Regex.IsMatch(cleaned, @"^\d+$");
    }

    public override string FormatErrorMessage(string name)
    {
        return $"Vergi numarası 10 haneli, TC Kimlik numarası 11 haneli olmalıdır.";
    }
}

