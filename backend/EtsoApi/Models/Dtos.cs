namespace EtsoApi.Models;

public record EsnafYazDto(
    string AdSoyad, string Isletme, string? VergiNo, int? GrupId,
    string? Il, string? Ilce, string? Mahalle, string? Adres, string? Telefon, int? GorevliId, string? Durum,
    // Oda kayıt bilgileri (ÜYE LİSTE DETAY RAPORU kolonları)
    string? UyeSicilNo = null, string? TicaretSicilNo = null, string? SirketTipi = null, string? TabelaUnvani = null,
    string? Uyruk = null, string? Sermaye = null, string? Derece = null, string? VergiDairesi = null,
    DateTime? VergiTerkTarihi = null, DateTime? KurulusTarihi = null, DateTime? OdaKararTarihi = null,
    string? UyelikDurumu = null, DateTime? DurumDegisimTarihi = null, string? DurumDegisimNedeni = null,
    string? FaaliyetDetayi = null, string? NaceKodu = null, string? NaceAdi = null,
    string? Gorevi = null, string? IsTelefonu = null, DateTime? KayitTarihi = null);

/// <summary>Üye kartındaki "Düzenle" düğmesi: yalnızca üyelik durumu (Faal / Askı / Pasif).</summary>
public record EsnafUyelikDurumuDto(string UyelikDurumu);

public record GrupYazDto(string Ad, string? Aciklama, string? Tur, int? UstGrupId, string? Durum, int? No = null);

public record KullaniciYazDto(
    string AdSoyad, string KullaniciAdi, string Rol, string? Gorev,
    string? Birim, string? Eposta, string? Telefon, string? Durum, string? Sifre = null,
    /// <summary>Çalışanın sorumlu olduğu meslek grupları. Gönderilen liste kaydın tam
    /// karşılığıdır: listede olmayan bağlar silinir. Alan hiç gönderilmezse (null) mevcut
    /// bağlara dokunulmaz — böylece grup alanını taşımayan eski istemciler bağları silmez.</summary>
    int[]? GrupIdler = null);

public record SifreSifirlaDto(string YeniSifre);

public record GorusmeYazDto(int EsnafId, int GorevliId,
    /// <summary>Görüşme tarihi. Zorunlu değildir: boş gelirse yeni kayıtta bugünün tarihi
    /// yazılır, güncellemede kaydın mevcut tarihi korunur.</summary>
    DateTime? Tarih, string Sonuc, string? Not, bool TakipGerekli,
    /// <summary>Üyeyle kaçıncı görüşme. Boş bırakılırsa sunucu sıradaki numarayı verir.</summary>
    int? Sira = null,
    /// <summary>Görüşmeye eşlik eden ikinci çalışan. İsteğe bağlı; birincil görevliyle aynı olamaz.</summary>
    int? IkinciGorevliId = null);

public record GorevlendirmeYazDto(int GorevliId, int? EsnafId, int? GrupId, DateTime Tarih, string? Not, string? Durum);

public record GorevlendirmeKararDto(string Durum, string? RedMazereti);

public record OnayYazDto(int EsnafId, int? GorevliId, string IslemTuru);

public record OnayKararDto(string Durum);
