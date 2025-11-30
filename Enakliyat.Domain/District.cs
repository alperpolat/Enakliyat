namespace Enakliyat.Domain;

public class District : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public int CityId { get; set; }
    public City City { get; set; } = null!;

    public ICollection<Neighborhood> Neighborhoods { get; set; } = new List<Neighborhood>();
}
