using System.Net;
using System.Net.Mail;
using Enakliyat.Domain;
using Enakliyat.Infrastructure;
using Enakliyat.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Enakliyat.Web.Services;

public class NotificationService : INotificationService
{
    private readonly EmailSettings _emailSettings;
    private readonly EnakliyatDbContext _context;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IOptions<EmailSettings> emailSettings,
        EnakliyatDbContext context,
        ILogger<NotificationService> logger)
    {
        _emailSettings = emailSettings.Value;
        _context = context;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_emailSettings.Host) || string.IsNullOrWhiteSpace(_emailSettings.From))
        {
            _logger.LogWarning("Email ayarları yapılandırılmamış. Email gönderilemedi: {To}", to);
            return;
        }

        try
        {
            using var client = new SmtpClient(_emailSettings.Host, _emailSettings.Port)
            {
                EnableSsl = _emailSettings.EnableSsl,
                Credentials = new NetworkCredential(_emailSettings.User, _emailSettings.Password)
            };

            using var message = new MailMessage(_emailSettings.From, to)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };

            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Email gönderildi: {To}, Konu: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email gönderilirken hata: {To}", to);
        }
    }

    public async Task SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        // SMS servisi entegrasyonu buraya eklenecek (örn: Netgsm, İleti Merkezi vb.)
        // Şimdilik log olarak kaydediyoruz
        _logger.LogInformation("SMS gönderilecek: {PhoneNumber}, Mesaj: {Message}", phoneNumber, message);
        
        // TODO: Gerçek SMS servisi entegrasyonu
        await Task.CompletedTask;
    }

    public async Task NotifyNewRequestToCarriersAsync(MoveRequest request, CancellationToken cancellationToken = default)
    {
        // İlgili bölgedeki onaylı firmalara bildirim gönder
        var relevantCarriers = await _context.Carriers
            .Where(c => c.IsApproved && !c.IsRejected && !c.IsSuspended)
            .Where(c => string.IsNullOrEmpty(c.ServiceAreas) || 
                       (request.FromCityId.HasValue && c.ServiceAreas.Contains(request.FromCityId.Value.ToString())) ||
                       (request.ToCityId.HasValue && c.ServiceAreas.Contains(request.ToCityId.Value.ToString())) ||
                       c.ServiceAreas.Contains("Türkiye geneli"))
            .ToListAsync(cancellationToken);

        var subject = $"Yeni Taşınma Talebi - Talep #{request.Id}";
        var body = $@"
<h3>Yeni Taşınma Talebi</h3>
<p><strong>Talep No:</strong> {request.Id}</p>
<p>
    <strong>Nereden:</strong> {request.FromAddress}<br/>
    <strong>Nereye:</strong> {request.ToAddress}<br/>
    <strong>Taşınma Tipi:</strong> {request.MoveType}<br/>
    <strong>Taşınma Tarihi:</strong> {request.MoveDate:dd.MM.yyyy}
</p>
<p><a href='https://nakliye360.com/Carrier/Leads'>Teklif vermek için tıklayın</a></p>";

        foreach (var carrier in relevantCarriers)
        {
            if (!string.IsNullOrWhiteSpace(carrier.Email))
            {
                await SendEmailAsync(carrier.Email, subject, body, cancellationToken: cancellationToken);
            }
        }
    }

    public async Task NotifyNewOfferToUserAsync(Offer offer, CancellationToken cancellationToken = default)
    {
        var request = await _context.MoveRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == offer.MoveRequestId, cancellationToken);

        if (request == null || string.IsNullOrWhiteSpace(request.Email)) return;

        var subject = $"Yeni Teklif Aldınız - Talep #{request.Id}";
        var body = $@"
<h3>Yeni Teklif</h3>
<p><strong>Talep No:</strong> {request.Id}</p>
<p>
    <strong>Firma:</strong> {offer.Carrier?.Name ?? "-"}<br/>
    <strong>Fiyat:</strong> {offer.Price:N2} TL<br/>
    <strong>Not:</strong> {offer.Note ?? "-"}
</p>
<p><a href='https://nakliye360.com/Home/Details/{request.Id}'>Teklifleri görüntülemek için tıklayın</a></p>";

        await SendEmailAsync(request.Email, subject, body, cancellationToken: cancellationToken);
    }

    public async Task NotifyOfferAcceptedToCarrierAsync(Offer offer, CancellationToken cancellationToken = default)
    {
        var carrier = await _context.Carriers.FirstOrDefaultAsync(c => c.Id == offer.CarrierId, cancellationToken);
        if (carrier == null || string.IsNullOrWhiteSpace(carrier.Email)) return;

        var request = await _context.MoveRequests.FirstOrDefaultAsync(r => r.Id == offer.MoveRequestId, cancellationToken);
        if (request == null) return;

        var subject = $"Teklifiniz Kabul Edildi - Talep #{request.Id}";
        var body = $@"
<h3>Tebrikler! Teklifiniz Kabul Edildi</h3>
<p><strong>Talep No:</strong> {request.Id}</p>
<p>
    <strong>Nereden:</strong> {request.FromAddress}<br/>
    <strong>Nereye:</strong> {request.ToAddress}<br/>
    <strong>Fiyat:</strong> {offer.Price:N2} TL<br/>
    <strong>Taşınma Tarihi:</strong> {request.MoveDate:dd.MM.yyyy}
</p>
<p><a href='https://nakliye360.com/Carrier/Reservations'>Rezervasyonları görüntülemek için tıklayın</a></p>";

        await SendEmailAsync(carrier.Email, subject, body, cancellationToken: cancellationToken);
    }

    public async Task NotifyOfferRejectedToCarrierAsync(Offer offer, CancellationToken cancellationToken = default)
    {
        var carrier = await _context.Carriers.FirstOrDefaultAsync(c => c.Id == offer.CarrierId, cancellationToken);
        if (carrier == null || string.IsNullOrWhiteSpace(carrier.Email)) return;

        var request = await _context.MoveRequests.FirstOrDefaultAsync(r => r.Id == offer.MoveRequestId, cancellationToken);
        if (request == null) return;

        var subject = $"Teklif Durumu - Talep #{request.Id}";
        var body = $@"
<h3>Teklif Durumu Güncellendi</h3>
<p><strong>Talep No:</strong> {request.Id}</p>
<p>Maalesef bu talep için teklifiniz kabul edilmedi. Başka talepler için teklif vermeye devam edebilirsiniz.</p>
<p><a href='https://nakliye360.com/Carrier/Leads'>Yeni talepleri görüntülemek için tıklayın</a></p>";

        await SendEmailAsync(carrier.Email, subject, body, cancellationToken: cancellationToken);
    }

    public async Task NotifyReservationStatusChangedAsync(MoveRequest request, string oldStatus, string newStatus, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email)) return;

        var subject = $"Rezervasyon Durumu Güncellendi - Talep #{request.Id}";
        var body = $@"
<h3>Rezervasyon Durumu Güncellendi</h3>
<p><strong>Talep No:</strong> {request.Id}</p>
<p>
    <strong>Eski Durum:</strong> {oldStatus}<br/>
    <strong>Yeni Durum:</strong> {newStatus}
</p>
<p><a href='https://nakliye360.com/Home/Details/{request.Id}'>Detayları görüntülemek için tıklayın</a></p>";

        await SendEmailAsync(request.Email, subject, body, cancellationToken: cancellationToken);
    }

    public async Task NotifyPaymentReceivedAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        var contract = await _context.Contracts
            .Include(c => c.Offer)
            .ThenInclude(o => o.MoveRequest)
            .FirstOrDefaultAsync(c => c.Id == payment.ContractId, cancellationToken);

        if (contract?.Offer?.MoveRequest == null) return;

        var request = contract.Offer.MoveRequest;
        if (string.IsNullOrWhiteSpace(request.Email)) return;

        var subject = $"Ödeme Alındı - Talep #{request.Id}";
        var body = $@"
<h3>Ödeme Alındı</h3>
<p><strong>Talep No:</strong> {request.Id}</p>
<p>
    <strong>Tutar:</strong> {payment.Amount:N2} {payment.Currency}<br/>
    <strong>Ödeme Tarihi:</strong> {payment.CreatedAt:dd.MM.yyyy HH:mm}<br/>
    <strong>Durum:</strong> {payment.Status}
</p>
<p>Ödemeniz başarıyla alınmıştır. Rezervasyonunuz onaylanmıştır.</p>
<p><a href='https://nakliye360.com/Home/Reservation/{request.Id}'>Rezervasyon detaylarını görüntülemek için tıklayın</a></p>";

        await SendEmailAsync(request.Email, subject, body, cancellationToken: cancellationToken);
    }
}

