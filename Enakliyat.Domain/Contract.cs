namespace Enakliyat.Domain;

public class Contract : BaseEntity
{
    public int MoveRequestId { get; set; }
    public MoveRequest MoveRequest { get; set; } = null!;

    public int OfferId { get; set; }
    public Offer Offer { get; set; } = null!;

    public string ContractNumber { get; set; } = string.Empty;

    public bool IsInsuranceIncluded { get; set; }
    public string? PolicyNumber { get; set; }
    public string? InsuranceCompany { get; set; }
    public string? CoverageDescription { get; set; }
    public decimal? CoverageAmount { get; set; }
}
