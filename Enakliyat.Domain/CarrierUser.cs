namespace Enakliyat.Domain;

public class CarrierUser : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty; // TODO: hash in real app

    public int CarrierId { get; set; }
    public Carrier Carrier { get; set; } = null!;
}
