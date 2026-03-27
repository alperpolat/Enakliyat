using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Enakliyat.Web.Models;

public class OfferDetailsViewModel
{
    public int MoveRequestId { get; set; }

    public int? FromCityId { get; set; }
    public int? FromDistrictId { get; set; }
    public int? FromNeighborhoodId { get; set; }

    public int? ToCityId { get; set; }
    public int? ToDistrictId { get; set; }
    public int? ToNeighborhoodId { get; set; }

    public string FromAddress { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
    public string MoveType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lütfen adınızı girin.")]
    public string CustomerName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lütfen telefon numaranızı girin.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Lütfen taşınma tarihini seçin.")]
    public DateTime MoveDate { get; set; } = DateTime.Today.AddDays(1);

    public string? RoomType { get; set; }

    public int? FromFloor { get; set; }
    public bool FromHasElevator { get; set; }
    public int? ToFloor { get; set; }
    public bool ToHasElevator { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public IFormFile[]? Photos { get; set; }
}
