namespace Enakliyat.Domain;

public class MoveRequestPhoto : BaseEntity
{
    public int MoveRequestId { get; set; }
    public MoveRequest MoveRequest { get; set; } = null!;

    public string FilePath { get; set; } = string.Empty; // relative path under wwwroot
}
