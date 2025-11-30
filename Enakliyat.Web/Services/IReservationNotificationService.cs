using Enakliyat.Domain;

namespace Enakliyat.Web.Services;

public interface IReservationNotificationService
{
    Task SendReservationConfirmationAsync(MoveRequest request, Offer? acceptedOffer, CancellationToken cancellationToken = default);
}
