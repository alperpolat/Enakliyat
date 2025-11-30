using Enakliyat.Domain;

namespace Enakliyat.Web.Models;

public class UserRequestDetailsViewModel
{
    public MoveRequest Request { get; set; } = null!;
    public IEnumerable<Offer> Offers { get; set; } = Enumerable.Empty<Offer>();
    public IEnumerable<MoveRequestPhoto> Photos { get; set; } = Enumerable.Empty<MoveRequestPhoto>();

    public bool CanReview { get; set; }
    public int? AcceptedCarrierId { get; set; }
    public int? ReviewRating { get; set; }
    public string? ReviewComment { get; set; }
}
