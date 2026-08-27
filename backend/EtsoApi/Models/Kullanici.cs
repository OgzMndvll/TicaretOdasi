using System.Text.Json.Serialization;

namespace EtsoApi.Models;

public class Kullanici
{
    /// <summary>Panele giriş yapabilen tek rol. "Görevli" kayıtları görüşmelerde seçilen çalışanlardır, giriş yapmazlar.</summary>
    public const string YoneticiRolu = "Yönetici";
    public const string CalisanRolu = "Görevli";

    public int Id { get; set; }
    public string AdSoyad { get; set; } = "";
    public string KullaniciAdi { get; set; } = "";
    public string Rol { get; set; } = "Görevli"; // Yönetici | Görevli
    public string? Gorev { get; set; }
    public string? Birim { get; set; }
    public string? Eposta { get; set; }
    public string? Telefon { get; set; }
    public string Durum { get; set; } = "Aktif";
    public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Çalışanın sorumlu olduğu meslek grupları. Gezinme özelliği API yanıtlarına doğrudan
    /// çıkmaz (yükleme yapılmadığında boş dizi görünüp yanıltırdı); uçlar bu listeyi
    /// açıkça projeksiyonla döndürür.
    /// </summary>
    [JsonIgnore]
    public List<KullaniciGrup> Gruplar { get; set; } = [];

    /// <summary>PBKDF2 (ASP.NET Identity PasswordHasher) ile üretilen şifre özeti. API yanıtlarına asla dahil edilmez.</summary>
    [JsonIgnore]
    public string? SifreHash { get; set; }
    [JsonIgnore]
    public int BasarisizGiris { get; set; }
    [JsonIgnore]
    public DateTime? KilitBitis { get; set; }

    /// <summary>
    /// Şifrenin en son değiştirildiği an. Bu andan önce üretilmiş JWT'ler geçersiz sayılır:
    /// şifre değiştirildiğinde/sıfırlandığında çalınmış token'lar da anında geçersiz olur.
    /// </summary>
    [JsonIgnore]
    public DateTime SifreGuncelleme { get; set; } = DateTime.UtcNow;
}
