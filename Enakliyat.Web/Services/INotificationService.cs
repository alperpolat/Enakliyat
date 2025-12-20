using Enakliyat.Domain;

namespace Enakliyat.Web.Services;

public interface INotificationService
{
    Task SendEmailAsync(string to, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default);
    Task SendSmsAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
    Task NotifyNewRequestToCarriersAsync(MoveRequest request, CancellationToken cancellationToken = default);
    Task NotifyNewOfferToUserAsync(Offer offer, CancellationToken cancellationToken = default);
    Task NotifyOfferAcceptedToCarrierAsync(Offer offer, CancellationToken cancellationToken = default);
    Task NotifyOfferRejectedToCarrierAsync(Offer offer, CancellationToken cancellationToken = default);
    Task NotifyReservationStatusChangedAsync(MoveRequest request, string oldStatus, string newStatus, CancellationToken cancellationToken = default);
    Task NotifyPaymentReceivedAsync(Payment payment, CancellationToken cancellationToken = default);
}

