namespace EtsoApi.Models;

public class Esnaf
{
    public int Id { get; set; }

    // ---- Oda kayıt bilgileri (ÜYE LİSTE DETAY RAPORU kolonları) ----
    public string? UyeSicilNo { get; set; }
    public string? TicaretSicilNo { get; set; }
    public string? SirketTipi { get; set; }
    public string? TabelaUnvani { get; set; }
    public string? Uyruk { get; set; }
    public string? Sermaye { get; set; }
    public string? Derece { get; set; }
    public string? VergiDairesi { get; set; }
    public DateTime? VergiTerkTarihi { get; set; }
    public DateTime? KurulusTarihi { get; set; }
    public DateTime? OdaKararTarihi { get; set; }

    // Odadaki üyelik durumu: Faal | Askı | Pasif. (Esnaf.Durum ile karıştırılmamalı; o, görüşme onay sonucudur.)
    public string UyelikDurumu { get; set; } = "Faal";
    public DateTime? DurumDegisimTarihi { get; set; }
    public string? DurumDegisimNedeni { get; set; }

    /// <summary>
    /// Üyenin borcunu ödeyip ödemediği. Üyelik durumuyla birlikte, üye kartındaki "Düzenle"
    /// ekranından işaretlenir: askıdaki bir üye ödemesini yapınca hem "Faal"a alınır hem de
    /// burası işaretlenir. Üyelik durumundan bağımsız tutulur; ödeme yapmış ama henüz faale
    /// alınmamış (ya da tersi) üyeler de kaydedilebilsin.
    /// </summary>
    public bool Odendi { get; set; }
    /// <summary>Ödemenin işaretlendiği an. Elle girilmez; işaretle birlikte damgalanır.</summary>
    public DateTime? OdemeTarihi { get; set; }

    public string? FaaliyetDetayi { get; set; }
    public string? NaceKodu { get; set; }
    public string? NaceAdi { get; set; }

    // ---- Kimlik ve iletişim ----
    // AdSoyad = birincil yetkili, Isletme = unvan.
    public string AdSoyad { get; set; } = "";
    public string Isletme { get; set; } = "";
    public string? Gorevi { get; set; }
    public string? VergiNo { get; set; }
    public int? GrupId { get; set; }
    public Grup? Grup { get; set; }
    public string? Il { get; set; }
    public string? Ilce { get; set; }
    public string? Mahalle { get; set; }
    public string? Adres { get; set; }
    public string? Telefon { get; set; }
    public string? IsTelefonu { get; set; }

    // ---- Saha takibi ----
    public int? GorevliId { get; set; }
    public Kullanici? Gorevli { get; set; }
    // Görüşme sonucu: Onay Verdi | Onay Vermedi | Kararsız | Görüşülmedi
    public string Durum { get; set; } = "Görüşülmedi";
    public DateTime? SonGorusmeTarihi { get; set; }
    public DateTime KayitTarihi { get; set; } = DateTime.UtcNow;

    public List<Gorusme> Gorusmeler { get; set; } = [];
    public List<EsnafYetkili> Yetkililer { get; set; } = [];
}

/// <summary>Bir üyenin oda kaydındaki yetkili kişileri. Kaynak raporda her yetkili ayrı satırdır.</summary>
public class EsnafYetkili
{
    public int Id { get; set; }
    public int EsnafId { get; set; }
    public Esnaf? Esnaf { get; set; }
    public string AdSoyad { get; set; } = "";
    public string? Gorevi { get; set; }
    public DateTime? YetkiBaslangic { get; set; }
    public DateTime? YetkiBitis { get; set; }
}
