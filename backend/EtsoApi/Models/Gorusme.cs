namespace EtsoApi.Models;

public class Gorusme
{
    public int Id { get; set; }
    public int EsnafId { get; set; }
    public Esnaf? Esnaf { get; set; }
    public int GorevliId { get; set; }
    public Kullanici? Gorevli { get; set; }

    /// <summary>Görüşmeye eşlik eden ikinci çalışan ("Görüşecek Kişi"). İsteğe bağlıdır.
    /// Raporlarda görüşme yalnızca birincil <see cref="GorevliId"/> hanesine sayılır; bu alan
    /// gösterim ve süzme içindir, aksi halde sütun toplamları görüşme sayısını aşardı.</summary>
    public int? IkinciGorevliId { get; set; }
    public Kullanici? IkinciGorevli { get; set; }

    public DateTime Tarih { get; set; } = DateTime.UtcNow;

    /// <summary>Bu üyeyle kaçıncı görüşme olduğu (1, 2, 3...). Kayıt anında sunucuda hesaplanır, elle girilmez.</summary>
    public int Sira { get; set; } = 1;

    // Onay Verdi | Onay Vermedi | Kararsız
    public string Sonuc { get; set; } = "Kararsız";
    public string? Not { get; set; }
    public bool TakipGerekli { get; set; }
}
