namespace Enakliyat.Domain;

public class Message : BaseEntity
{
    public int MoveRequestId { get; set; }
    public MoveRequest MoveRequest { get; set; } = null!;
    
    public int? FromUserId { get; set; }
    public User? FromUser { get; set; }
    
    public int? FromCarrierId { get; set; }
    public Carrier? FromCarrier { get; set; }
    
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public string? AttachmentPath { get; set; }
}

