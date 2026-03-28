namespace Enakliyat.Web.Models;

public class MisafirTalepOzetItem
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
    public string MoveType { get; set; } = string.Empty;
    public DateTime MoveDate { get; set; }
    public DateTime? MoveDateEnd { get; set; }
    public int TeklifSayisi { get; set; }
}
