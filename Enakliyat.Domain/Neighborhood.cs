namespace Enakliyat.Domain;

public class Neighborhood : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public int DistrictId { get; set; }
    public District District { get; set; } = null!;
}
