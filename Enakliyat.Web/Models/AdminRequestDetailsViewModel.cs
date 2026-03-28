using Enakliyat.Domain;

namespace Enakliyat.Web.Models;

public class AdminRequestDetailsViewModel
{
    public MoveRequest Request { get; set; } = null!;
    public List<Offer> Offers { get; set; } = new();

    public IReadOnlyList<MoveRequestPhoto> Photos { get; set; } = Array.Empty<MoveRequestPhoto>();

    public IReadOnlyList<string> SecilenEkHizmetler { get; set; } = Array.Empty<string>();
}
