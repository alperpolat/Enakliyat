namespace Enakliyat.Web.Models;

public class MisafirTaleplerimViewModel
{
    public int KullaniciId { get; set; }
    public string MusteriAdi { get; set; } = string.Empty;
    public string PublicListeUrl { get; set; } = string.Empty;
    public IReadOnlyList<MisafirTalepOzetItem> Talepler { get; set; } = Array.Empty<MisafirTalepOzetItem>();
}
