namespace Enakliyat.Web.Services;

public interface ISmsService
{
    /// <summary>Tek SMS gönderir. Kapalı / hatalı yapılandırmada loglar ve false dönebilir.</summary>
    Task<SmsSendResult> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
}

public sealed record SmsSendResult(bool Ok, string? Detail);
