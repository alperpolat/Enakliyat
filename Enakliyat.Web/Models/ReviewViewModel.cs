using System.ComponentModel.DataAnnotations;

namespace Enakliyat.Web.Models;

public class ReviewViewModel
{
    public int MoveRequestId { get; set; }
    public int CarrierId { get; set; }

    [Range(1,5)]
    [Required]
    public int Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }
}
