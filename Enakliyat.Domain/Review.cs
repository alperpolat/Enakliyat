namespace Enakliyat.Domain;

public class Review : BaseEntity
{
    public int MoveRequestId { get; set; }
    public MoveRequest MoveRequest { get; set; } = null!;

    public int CarrierId { get; set; }
    public Carrier Carrier { get; set; } = null!;

    public int? UserId { get; set; }
    public User? User { get; set; }

    public int Rating { get; set; } // 1-5
    public string? Comment { get; set; }
    public string? CarrierReply { get; set; }
}
