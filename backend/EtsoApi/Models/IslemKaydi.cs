namespace EtsoApi.Models;

/// <summary>
/// Denetim kaydı: hangi kullanıcı, hangi üyede, ne zaman, hangi işlemi yaptı.
///
/// Kullanıcı adı ve üye unvanı kimlik yanında **kopya olarak** da tutulur. Kayıt sonradan
/// silinse bile (üye silme işleminin kendisi de loglanır) geçmiş okunabilir kalmalı; ilişkisel
/// bağ kurulsaydı silinen üyenin satırları ya kaybolur ya da boş görünürdü.
/// </summary>
public class IslemKaydi
{
    public int Id { get; set; }

    /// <summary>İşlemi yapan kullanıcı. Kullanıcı silinirse kayıt kalır, alan boşalır.</summary>
    public int? KullaniciId { get; set; }
    public string KullaniciAdi { get; set; } = "";
    public string KullaniciAdSoyad { get; set; } = "";

    /// <summary>İlgili üye. Üye silindiğinde kimlik boşa düşer; unvan kopyası okunur kalır.</summary>
    public int? EsnafId { get; set; }
    public string? EsnafUnvan { get; set; }

    /// <summary>İşlemin türü (bkz. <see cref="Islemler"/>): "Görüşme eklendi" gibi.</summary>
    public string Islem { get; set; } = "";

    /// <summary>İnsan tarafından okunacak ayrıntı: "1. görüşme, Onay Verdi" / "Askı → Faal".</summary>
    public string? Detay { get; set; }

    public DateTime Tarih { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Log'a yazılan işlem adları. Süzgeç açılır listesi de bu listeden beslendiği için
/// serbest metin yerine sabitler kullanılır.
/// </summary>
public static class Islemler
{
    public const string GorusmeEklendi = "Görüşme eklendi";
    public const string GorusmeDuzenlendi = "Görüşme düzenlendi";
    public const string GorusmeSilindi = "Görüşme silindi";
    public const string UyelikDurumuDegisti = "Üyelik durumu değişti";
    public const string UyeEklendi = "Üye eklendi";
    public const string UyeDuzenlendi = "Üye düzenlendi";
    public const string UyeSilindi = "Üye silindi";

    /// <summary>Süzgeç seçenekleri için tam liste.</summary>
    public static readonly string[] Tumu =
    [
        GorusmeEklendi, GorusmeDuzenlendi, GorusmeSilindi,
        UyelikDurumuDegisti, UyeEklendi, UyeDuzenlendi, UyeSilindi,
    ];
}
