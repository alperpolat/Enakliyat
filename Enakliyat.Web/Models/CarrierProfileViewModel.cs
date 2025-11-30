using Enakliyat.Domain;

namespace Enakliyat.Web.Models;

public class CarrierProfileViewModel
{
    public Carrier Carrier { get; set; } = null!;
    public List<CarrierDocument> Documents { get; set; } = new();
}
