using Enakliyat.Domain;

namespace Enakliyat.Web.Models;

public class AdminCarrierDetailsViewModel
{
    public Carrier Carrier { get; set; } = null!;
    public List<Offer> Offers { get; set; } = new();
    public List<Review> Reviews { get; set; } = new();
}
