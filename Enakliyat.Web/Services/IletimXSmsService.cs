using System.Net.Http;
using System.Security;
using System.Text;
using Enakliyat.Web.Models;
using Microsoft.Extensions.Options;

namespace Enakliyat.Web.Services;

/// <summary>İletimX gateway — NakliyeCrm projesindeki SMS ile aynı XML/POST sözleşmesi.</summary>
public class IletimXSmsService : ISmsService
{
    private readonly HttpClient _http;
    private readonly SmsSettings _settings;
    private readonly ILogger<IletimXSmsService> _logger;

    public IletimXSmsService(HttpClient http, IOptions<SmsSettings> options, ILogger<IletimXSmsService> logger)
    {
        _http = http;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<SmsSendResult> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("SMS gönderilmedi: Sms.Enabled=false (appsettings içinde true yapın)");
            return new SmsSendResult(false, "disabled");
        }

        var cleanPhone = CleanPhoneNumber(phoneNumber);
        if (string.IsNullOrEmpty(cleanPhone))
        {
            _logger.LogWarning("SMS gönderilmedi: geçersiz telefon: {Phone}", phoneNumber);
            return new SmsSendResult(false, "invalid_phone");
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiGatewayUrl) ||
            string.IsNullOrWhiteSpace(_settings.KullaniciAdi) ||
            string.IsNullOrWhiteSpace(_settings.Sifre) ||
            string.IsNullOrWhiteSpace(_settings.BayiKodu) ||
            string.IsNullOrWhiteSpace(_settings.Baslik))
        {
            _logger.LogWarning("SMS yapılandırması eksik (ApiGatewayUrl, KullaniciAdi, Sifre, BayiKodu veya Baslik)");
            return new SmsSendResult(false, "not_configured");
        }

        var escaped = XmlEscape(message);
        var xml = $@"<MainmsgBody>
    <UserName>{_settings.KullaniciAdi}-{_settings.BayiKodu}</UserName>
    <PassWord>{_settings.Sifre}</PassWord>
    <Action>12</Action>
    <Mesgbody>{escaped}</Mesgbody>
    <Numbers>[{cleanPhone}]</Numbers>
    <Originator>{_settings.Baslik}</Originator>
</MainmsgBody>";

        try
        {
            using var content = new StringContent(xml, Encoding.UTF8, "application/xml");
            using var response = await _http.PostAsync(_settings.ApiGatewayUrl, content, cancellationToken);
            var responseString = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();

            _logger.LogInformation(
                "İletimX yanıtı. Phone={Phone}, Http={Code}, Body={Body}",
                cleanPhone, (int)response.StatusCode, responseString);

            if (!response.IsSuccessStatusCode)
            {
                return new SmsSendResult(false, $"http_{(int)response.StatusCode}:{responseString}");
            }

            if (responseString.StartsWith("ID:", StringComparison.OrdinalIgnoreCase))
            {
                return new SmsSendResult(true, responseString);
            }

            var err = GetSmsErrorMessage(responseString);
            _logger.LogWarning("İletimX hata: {Code} — {Message}", responseString, err);
            return new SmsSendResult(false, $"{responseString}:{err}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "İletimX SMS istisnası");
            return new SmsSendResult(false, ex.Message);
        }
    }

    private static string XmlEscape(string text) =>
        SecurityElement.Escape(text) ?? string.Empty;

    /// <summary>NakliyeCrm ile aynı: 10 hane 5xxxxxxxxx (ülke kodu ve baştaki 0 yok).</summary>
    private static string CleanPhoneNumber(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        var cleaned = new string(phone.Where(char.IsDigit).ToArray());

        if (cleaned.StartsWith('0'))
        {
            cleaned = cleaned[1..];
        }

        if (cleaned.StartsWith("90", StringComparison.Ordinal))
        {
            cleaned = cleaned[2..];
        }

        if (cleaned.Length != 10 || cleaned[0] != '5')
        {
            return string.Empty;
        }

        return cleaned;
    }

    private static string GetSmsErrorMessage(string errorCode) =>
        errorCode switch
        {
            "01" => "Hatalı Kullanıcı Adı, Şifre ya da Bayi Kodu",
            "02" => "Yetersiz Kredi / Ödenmemiş Fatura Borcu",
            "03" => "Tanımsız Action Parametresi",
            "05" => "Xml Düğümü Eksik ya da Hatalı",
            "06" => "Tanımsız Originator",
            "07" => "Mesaj Kodu (ID) yok",
            "09" => "Tarih alanları hatalı",
            "10" => "SMS Gönderilemedi",
            _ => $"Bilinmeyen SMS yanıtı: {errorCode}"
        };
}
