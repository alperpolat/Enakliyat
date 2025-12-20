namespace Enakliyat.Domain;

public class OfferTemplate : BaseEntity
{
    public int CarrierId { get; set; }
    public Carrier Carrier { get; set; } = null!;
    
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // Fiyat hesaplama parametreleri
    public decimal? BasePrice { get; set; }
    public decimal? PricePerKm { get; set; }
    public decimal? PricePerRoom { get; set; }
    public decimal? PricePerFloor { get; set; }
    
    public string? NoteTemplate { get; set; }
    public bool IsDefault { get; set; } = false;
}

