namespace Enakliyat.Domain;

public class NotificationTemplate : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // Email, SMS
    public string EventType { get; set; } = string.Empty; // NewRequest, NewOffer, OfferAccepted, etc.
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? Variables { get; set; } // JSON: Available variables for template
}

