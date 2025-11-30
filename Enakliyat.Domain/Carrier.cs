namespace Enakliyat.Domain;

public class Carrier : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }

    public string? LicenseNumber { get; set; }
    public string? VehicleInfo { get; set; }
    public string? ServiceAreas { get; set; }
    public string? Description { get; set; }

    public bool IsApproved { get; set; } = false;
    public bool IsRejected { get; set; } = false;
    public bool IsSuspended { get; set; } = false;

    public ICollection<Offer> Offers { get; set; } = new List<Offer>();
    public ICollection<CarrierUser> Users { get; set; } = new List<CarrierUser>();
    public ICollection<CarrierDocument> Documents { get; set; } = new List<CarrierDocument>();
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
}
