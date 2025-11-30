using Enakliyat.Domain;

namespace Enakliyat.Web.Models;

public class AdminRequestDetailsViewModel
{
    public MoveRequest Request { get; set; } = null!;
    public List<Offer> Offers { get; set; } = new();
}
