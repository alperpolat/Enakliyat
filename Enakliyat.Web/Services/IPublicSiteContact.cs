namespace Enakliyat.Web.Services;

/// <summary>
/// Admin Sistem Ayarlarındaki teklif/destek hattı (<c>QuoteCallHotline</c>, yoksa <c>SupportPhone</c>).
/// </summary>
public interface IPublicSiteContact
{
    /// <summary>Orijinal metin (footer vb.).</summary>
    string? PhoneDisplay { get; }

    /// <summary>Ana sayfa «Hemen Ara Teklif Al» için tel: bağlantısı.</summary>
    string? QuoteCallTelHref { get; }

    /// <summary>Sabit WhatsApp düğmesi ve footer için wa.me bağlantısı.</summary>
    string? WhatsAppUrl { get; }
}
