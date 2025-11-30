namespace Enakliyat.Domain;

public class Offer : BaseEntity
{
    public int MoveRequestId { get; set; }
    public MoveRequest MoveRequest { get; set; } = null!;

    public int CarrierId { get; set; }
    public Carrier Carrier { get; set; } = null!;

    public decimal Price { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "Beklemede"; // Beklemede, Kabul Edildi, Reddedildi
}
