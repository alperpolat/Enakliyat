using System.IO;
using Microsoft.AspNetCore.Http;

namespace Enakliyat.Web.Helpers;

public static class FileUploadHelper
{
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private static readonly string[] AllowedDocumentExtensions = { ".pdf", ".doc", ".docx", ".xls", ".xlsx" };
    private static readonly long MaxFileSize = 10 * 1024 * 1024; // 10 MB
    private static readonly long MaxImageSize = 5 * 1024 * 1024; // 5 MB

    public static bool IsValidImageFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return false;

        if (file.Length > MaxImageSize)
            return false;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        return AllowedImageExtensions.Contains(extension);
    }

    public static bool IsValidDocumentFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return false;

        if (file.Length > MaxFileSize)
            return false;

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        return AllowedDocumentExtensions.Contains(extension);
    }

    public static string GetFileValidationError(IFormFile file, bool isImage = false)
    {
        if (file == null || file.Length == 0)
            return "Dosya seçilmedi.";

        if (isImage && file.Length > MaxImageSize)
            return $"Resim dosyası en fazla {MaxImageSize / 1024 / 1024} MB olabilir.";

        if (!isImage && file.Length > MaxFileSize)
            return $"Dosya en fazla {MaxFileSize / 1024 / 1024} MB olabilir.";

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtensions = isImage ? AllowedImageExtensions : AllowedDocumentExtensions;

        if (!allowedExtensions.Contains(extension))
        {
            var allowed = string.Join(", ", allowedExtensions);
            return $"Geçersiz dosya tipi. İzin verilen formatlar: {allowed}";
        }

        return string.Empty;
    }

    public static string GenerateSafeFileName(string originalFileName)
    {
        var safeName = Path.GetFileNameWithoutExtension(originalFileName);
        var ext = Path.GetExtension(originalFileName);
        
        // Remove dangerous characters
        safeName = string.Join("_", safeName.Split(Path.GetInvalidFileNameChars()));
        
        // Limit length
        if (safeName.Length > 50)
            safeName = safeName.Substring(0, 50);

        return $"{safeName}_{Guid.NewGuid():N}{ext}";
    }
}

