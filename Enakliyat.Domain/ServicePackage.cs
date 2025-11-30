namespace Enakliyat.Domain;

public class ServicePackage : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    // Comma-separated move types for now, e.g. "Home,Office"
    public string? ApplicableMoveTypes { get; set; }

    public decimal? BasePrice { get; set; }

    public ICollection<ServicePackageItem> Items { get; set; } = new List<ServicePackageItem>();
}

public class ServicePackageItem : BaseEntity
{
    public int ServicePackageId { get; set; }
    public ServicePackage ServicePackage { get; set; } = null!;

    public int AddOnServiceId { get; set; }
    public AddOnService AddOnService { get; set; } = null!;

    public int? Quantity { get; set; }
    public decimal? ExtraPrice { get; set; }
}
