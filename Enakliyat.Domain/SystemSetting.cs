namespace Enakliyat.Domain;

public class SystemSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Description { get; set; }
    public string Category { get; set; } = "General"; // General, Email, SMS, Commission, etc.
    public bool IsEncrypted { get; set; } = false;
}

