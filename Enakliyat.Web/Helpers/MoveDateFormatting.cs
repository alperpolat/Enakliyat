namespace Enakliyat.Web.Helpers;

public static class MoveDateFormatting
{
    /// <summary>Tek gün veya başlangıç–bitiş aralığını Türkçe tarih metni olarak döner.</summary>
    public static string ToTurkishRangeDisplay(DateTime moveDate, DateTime? moveDateEnd)
    {
        var start = moveDate.Date;
        var end = moveDateEnd?.Date;
        if (!end.HasValue || end.Value <= start)
            return start.ToString("dd.MM.yyyy");
        return $"{start:dd.MM.yyyy} – {end.Value:dd.MM.yyyy}";
    }
}
