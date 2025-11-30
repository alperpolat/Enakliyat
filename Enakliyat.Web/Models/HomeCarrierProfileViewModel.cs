using Enakliyat.Domain;

namespace Enakliyat.Web.Models;

public class HomeCarrierProfileViewModel
{
    public Carrier Carrier { get; set; } = null!;
    public List<Review> Reviews { get; set; } = new();
}
