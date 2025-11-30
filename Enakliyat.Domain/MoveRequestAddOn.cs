namespace Enakliyat.Domain;

public class MoveRequestAddOn : BaseEntity
{
    public int MoveRequestId { get; set; }
    public MoveRequest MoveRequest { get; set; } = null!;

    public int AddOnServiceId { get; set; }
    public AddOnService AddOnService { get; set; } = null!;
}
