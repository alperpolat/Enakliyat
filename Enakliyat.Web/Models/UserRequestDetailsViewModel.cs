using Enakliyat.Domain;

namespace Enakliyat.Web.Models;

public class UserRequestDetailsViewModel
{
    /// <summary>Misafir takip linki (<c>?t=</c>) ile gelindiğinde formlarda taşınır.</summary>
    public int? MisafirKullaniciId { get; set; }

    /// <summary>Teklif formunda seçilen ek hizmet adları.</summary>
    public IReadOnlyList<string> SecilenEkHizmetler { get; set; } = Array.Empty<string>();

    public MoveRequest Request { get; set; } = null!;
    public IEnumerable<Offer> Offers { get; set; } = Enumerable.Empty<Offer>();
    public IEnumerable<MoveRequestPhoto> Photos { get; set; } = Enumerable.Empty<MoveRequestPhoto>();

    public bool CanReview { get; set; }
    public int? AcceptedCarrierId { get; set; }
    public int? ReviewRating { get; set; }
    public string? ReviewComment { get; set; }

    public Payment? Payment { get; set; }
}
