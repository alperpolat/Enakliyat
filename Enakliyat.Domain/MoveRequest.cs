namespace Enakliyat.Domain;

public class MoveRequest : BaseEntity
{
    public string CustomerName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
    /// <summary>Taşınma penceresinin başlangıcı (veya tek gün).</summary>
    public DateTime MoveDate { get; set; }

    /// <summary>Taşınma penceresinin son günü; null ise yalnızca <see cref="MoveDate"/> kullanılır.</summary>
    public DateTime? MoveDateEnd { get; set; }
    public string? Notes { get; set; }
    public string MoveType { get; set; } = string.Empty;
    public string Status { get; set; } = "Yeni";
    public string? RoomType { get; set; }
    public int? FromFloor { get; set; }
    public bool FromHasElevator { get; set; }
    public int? ToFloor { get; set; }
    public bool ToHasElevator { get; set; }
    public string? AssignedTeam { get; set; }
    public DateTime? EstimatedArrivalTime { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? FromCityId { get; set; }
    public int? FromDistrictId { get; set; }
    public int? FromNeighborhoodId { get; set; }

    public int? ToCityId { get; set; }
    public int? ToDistrictId { get; set; }
    public int? ToNeighborhoodId { get; set; }
    public int? SelectedPackageId { get; set; }
    public ServicePackage? SelectedPackage { get; set; }
    public ICollection<MoveRequestAddOn> AddOns { get; set; } = new List<MoveRequestAddOn>();
    public ICollection<MoveRequestPhoto> Photos { get; set; } = new List<MoveRequestPhoto>();
    public int? AcceptedOfferId { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Misafir taleplerde SMS ile gönderilen gizli takip bağlantısı anahtarı.</summary>
    public string? TrackingToken { get; set; }
}
