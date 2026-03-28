namespace Enakliyat.Web.Models;

public class TalepTakipViewModel
{
    public int Id { get; set; }
    /// <summary>SMS ve paylaşım ile aynı tam URL (Sms:PublicBaseUrl veya mevcut istek hostu).</summary>
    public string PublicTrackingUrl { get; set; } = string.Empty;

    /// <summary>Misafir tek talep görünümünde tüm taleplere dönüş için kullanıcı kimliği.</summary>
    public int? MisafirKullaniciId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
    public string MoveType { get; set; } = string.Empty;
    public DateTime MoveDate { get; set; }
    public DateTime? MoveDateEnd { get; set; }
    public int TeklifSayisi { get; set; }
}
