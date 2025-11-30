namespace Enakliyat.Domain;

public class CarrierDocument : BaseEntity
{
    public int CarrierId { get; set; }
    public Carrier Carrier { get; set; } = null!;

    public string DocumentType { get; set; } = string.Empty; // e.g. Yetki Belgesi, Sigorta Poliçesi
    public string FilePath { get; set; } = string.Empty; // relative path under wwwroot
    public bool IsApproved { get; set; }
}
