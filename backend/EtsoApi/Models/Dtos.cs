namespace EtsoApi.Models;

public record EsnafYazDto(
    string AdSoyad, string Isletme, string? VergiNo, int? GrupId,
    string? Il, string? Ilce, string? Mahalle, string? Adres, string? Telefon, int? GorevliId, string? Durum);

public record GrupYazDto(string Ad, string? Aciklama, string? Tur, int? UstGrupId, string? Durum);

public record KullaniciYazDto(
    string AdSoyad, string KullaniciAdi, string Rol, string? Gorev,
    string? Birim, string? Eposta, string? Telefon, string? Durum, string? Sifre = null);

public record SifreSifirlaDto(string YeniSifre);

public record GorusmeYazDto(int EsnafId, int GorevliId, DateTime Tarih, string Sonuc, string? Not, bool TakipGerekli);

public record GorevlendirmeYazDto(int GorevliId, int? EsnafId, int? GrupId, DateTime Tarih, string? Not, string? Durum);

public record OnayYazDto(int EsnafId, int? GorevliId, string IslemTuru);

public record OnayKararDto(string Durum);
