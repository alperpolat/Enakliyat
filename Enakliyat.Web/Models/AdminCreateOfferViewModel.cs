using System.ComponentModel.DataAnnotations;

namespace Enakliyat.Web.Models;

public class AdminCreateOfferViewModel
{
    public int MoveRequestId { get; set; }

    [Required]
    public int CarrierId { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    public string? Note { get; set; }
}
