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

    /// <summary>
    /// Görüşme sonucu: Onay Verdi | Onay Vermedi | Kararsız | Takip Edilecek | Gelmeyecek.
    /// Üyenin <c>Esnaf.Durum</c> alanı en güncel görüşmenin bu değerini yansıtır.
    /// </summary>
    public string Sonuc { get; set; } = "Kararsız";
    public string? Not { get; set; }

    /// <summary>
    /// Sonuçtan türetilir: yalnızca "Takip Edilecek" sonucunda true olur. Ayrı bir form alanı
    /// değildir; takip bilgisi görüşme sonucunun içine alındı, kolon sorgular ve raporlar
    /// (takip gereken görüşme sayısı) için korunuyor.
    /// </summary>
    public bool TakipGerekli { get; set; }
}
