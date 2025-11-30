using Enakliyat.Domain;

namespace Enakliyat.Web.Models;

public class ReservationViewModel
{
    public MoveRequest Request { get; set; } = null!;
    public Offer? AcceptedOffer { get; set; }
    public Contract? Contract { get; set; }
    public Payment? Payment { get; set; }
}
