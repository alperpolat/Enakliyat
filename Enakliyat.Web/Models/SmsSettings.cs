namespace Enakliyat.Web.Models;

/// <summary>
/// NakliyeCrm ile aynı İletimX ayarları + uygulama anahtarları.
/// appsettings: <c>"Sms": { ... }</c>
/// </summary>
public class SmsSettings
{
    public bool Enabled { get; set; }

    /// <summary>
    /// Takip linki kökü (örn. https://www.site.com). Boş bırakılırsa SMS gönderildiği istekteki <c>Host</c> kullanılır (canlıda otomatik doğru alan adı).
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>İletimX SMS API Gateway URL</summary>
    public string ApiGatewayUrl { get; set; } = "http://g.iletimx.com/?anabayi=1";

    public string KullaniciAdi { get; set; } = string.Empty;
    public string Sifre { get; set; } = string.Empty;
    public string BayiKodu { get; set; } = string.Empty;

    /// <summary>Gönderen başlık (Originator), panelde tanımlı olmalı.</summary>
    public string Baslik { get; set; } = string.Empty;
}
