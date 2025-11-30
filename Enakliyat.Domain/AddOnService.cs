namespace Enakliyat.Domain;

public enum AddOnPricingType
{
    Fixed = 0,
    Percentage = 1,
    Included = 2
}

public class AddOnService : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public AddOnPricingType PricingType { get; set; } = AddOnPricingType.Fixed;
    public decimal? DefaultPrice { get; set; }

    public ICollection<ServicePackageItem> PackageItems { get; set; } = new List<ServicePackageItem>();
}
