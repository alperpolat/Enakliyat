using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Enakliyat.Web.Helpers;

public static class ValidationHelper
{
    public static ValidationResult? ValidatePhoneNumber(string? phoneNumber, ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return new ValidationResult("Telefon numarası gereklidir.");

        // Türkiye telefon formatı: 05XX XXX XX XX veya +905XX XXX XX XX
        var pattern = @"^(\+90|0)?[5][0-9]{9}$";
        var cleaned = Regex.Replace(phoneNumber, @"[\s\-\(\)]", "");

        if (!Regex.IsMatch(cleaned, pattern))
            return new ValidationResult("Geçerli bir telefon numarası giriniz. (Örn: 0532 123 45 67)");

        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateEmail(string? email, ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(email))
            return ValidationResult.Success; // Email optional in some cases

        var emailAttribute = new EmailAddressAttribute();
        if (!emailAttribute.IsValid(email))
            return new ValidationResult("Geçerli bir e-posta adresi giriniz.");

        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateTurkishTaxNumber(string? taxNumber, ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(taxNumber))
            return new ValidationResult("Vergi numarası gereklidir.");

        // TC Kimlik: 11 haneli, Vergi No: 10 haneli
        var cleaned = Regex.Replace(taxNumber, @"[\s\-]", "");
        
        if (cleaned.Length != 10 && cleaned.Length != 11)
            return new ValidationResult("Vergi numarası 10 haneli, TC Kimlik numarası 11 haneli olmalıdır.");

        if (!Regex.IsMatch(cleaned, @"^\d+$"))
            return new ValidationResult("Vergi numarası sadece rakamlardan oluşmalıdır.");

        return ValidationResult.Success;
    }

    public static string GetUserFriendlyErrorMessage(string errorKey)
    {
        return errorKey switch
        {
            "Required" => "Bu alan zorunludur.",
            "EmailAddress" => "Geçerli bir e-posta adresi giriniz.",
            "StringLength" => "Girilen değer çok uzun.",
            "Range" => "Girilen değer geçerli aralıkta değil.",
            "Compare" => "Değerler eşleşmiyor.",
            "PhoneNumber" => "Geçerli bir telefon numarası giriniz.",
            "TaxNumber" => "Geçerli bir vergi numarası giriniz.",
            _ => "Geçersiz değer."
        };
    }
}

