namespace Enakliyat.Domain;

public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2,
    Refunded = 3
}

public enum PaymentMethod
{
    Unknown = 0,
    Card = 1,
    BankTransfer = 2,
    Cash = 3
}

public class Payment : BaseEntity
{
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public PaymentMethod Method { get; set; } = PaymentMethod.Unknown;

    public string? ExternalReference { get; set; }
}
