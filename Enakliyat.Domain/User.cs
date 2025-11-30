namespace Enakliyat.Domain;

public class User : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty; // TODO: Replace with hashed password in real app
    public ICollection<MoveRequest> MoveRequests { get; set; } = new List<MoveRequest>();
    public bool IsAdmin { get; set; } = false;
    public bool IsBanned { get; set; } = false;
    public bool IsSuspended { get; set; } = false;
}
