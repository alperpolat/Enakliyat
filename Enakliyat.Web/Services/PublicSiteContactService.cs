using Enakliyat.Infrastructure;
using Enakliyat.Web.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Enakliyat.Web.Services;

public class PublicSiteContactService : IPublicSiteContact
{
    public PublicSiteContactService(EnakliyatDbContext db)
    {
        var raw = db.SystemSettings.AsNoTracking()
            .FirstOrDefault(s => s.Key == "QuoteCallHotline")?.Value?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            raw = db.SystemSettings.AsNoTracking()
                .FirstOrDefault(s => s.Key == "SupportPhone")?.Value?.Trim();
        }

        PhoneDisplay = raw;
        QuoteCallTelHref = PhoneDialUri.BuildTelHref(raw);
        WhatsAppUrl = PhoneDialUri.BuildWhatsAppMeUrl(raw);
    }

    public string? PhoneDisplay { get; }
    public string? QuoteCallTelHref { get; }
    public string? WhatsAppUrl { get; }
}
