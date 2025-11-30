using Enakliyat.Domain;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Enakliyat.Web.Models;

public class AdminOffersViewModel
{
    public MoveRequest Request { get; set; } = null!;
    public IEnumerable<Offer> Offers { get; set; } = Enumerable.Empty<Offer>();
    public AdminCreateOfferViewModel NewOffer { get; set; } = new();
    public IEnumerable<SelectListItem> CarrierOptions { get; set; } = Enumerable.Empty<SelectListItem>();
}
