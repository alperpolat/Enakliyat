namespace Enakliyat.Web.Models;

public enum MoveType
{
    Home = 0,
    Partial = 1,
    Office = 2,
    Storage = 3,
    International = 4
}

public class MoveRequestViewModel
{
    public string FromAddress { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;

    public int? FromCityId { get; set; }
    public int? FromDistrictId { get; set; }
    public int? FromNeighborhoodId { get; set; }

    public int? ToCityId { get; set; }
    public int? ToDistrictId { get; set; }
    public int? ToNeighborhoodId { get; set; }
    public MoveType MoveType { get; set; } = MoveType.Home;
}
