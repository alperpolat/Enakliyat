using System.Net;
using System.Net.Mail;
using Enakliyat.Domain;
using Enakliyat.Web.Helpers;
using Enakliyat.Web.Models;
using Microsoft.Extensions.Options;

namespace Enakliyat.Web.Services;

public class SmtpReservationNotificationService : IReservationNotificationService
{
    private readonly EmailSettings _settings;

    public SmtpReservationNotificationService(IOptions<EmailSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task SendReservationConfirmationAsync(MoveRequest request, Offer? acceptedOffer, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.Host) || string.IsNullOrWhiteSpace(_settings.From))
        {
            // Konfigürasyon eksikse sessizce çık.
            return;
        }

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.EnableSsl,
            Credentials = new NetworkCredential(_settings.User, _settings.Password)
        };

        var subject = $"Enakliyat Rezervasyon Onayı - Talep #{request.Id}";
        var offerInfo = acceptedOffer != null
            ? $"<p><strong>Seçilen Nakliyeci:</strong> {acceptedOffer.Carrier?.Name ?? "-"}<br/>" +
              $"<strong>Fiyat:</strong> {acceptedOffer.Price:N2} TL</p>"
            : string.Empty;

        var bodyHtml = $@"<h3>Rezervasyon Onayınız</h3>
<p><strong>Talep Numaranız:</strong> {request.Id}</p>
<p>
  <strong>Nereden:</strong> {request.FromAddress}<br/>
  <strong>Nereye:</strong> {request.ToAddress}<br/>
  <strong>Taşınma Tarihi:</strong> {MoveDateFormatting.ToTurkishRangeDisplay(request.MoveDate, request.MoveDateEnd)}<br/>
  <strong>Durum:</strong> {request.Status}
</p>
{offerInfo}
<p>Enakliyat'ı tercih ettiğiniz için teşekkür ederiz.</p>";

        // Müşteri maili
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            using var userMessage = new MailMessage(_settings.From, request.Email)
            {
                Subject = subject,
                Body = bodyHtml,
                IsBodyHtml = true
            };

            await client.SendMailAsync(userMessage, cancellationToken);
        }

        // Admin bilgilendirme maili
        if (!string.IsNullOrWhiteSpace(_settings.AdminTo))
        {
            using var adminMessage = new MailMessage(_settings.From, _settings.AdminTo)
            {
                Subject = $"Yeni Rezervasyon - Talep #{request.Id}",
                Body = bodyHtml,
                IsBodyHtml = true
            };

            await client.SendMailAsync(adminMessage, cancellationToken);
        }
    }
}
