using Enakliyat.Domain;

namespace Enakliyat.Web.Models;

public class AdminUserDetailsViewModel
{
    public User User { get; set; } = null!;
    public List<MoveRequest> Requests { get; set; } = new();
}
