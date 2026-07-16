namespace EtsoApi.Models;

public class Esnaf
{
    public int Id { get; set; }
    public string AdSoyad { get; set; } = "";
    public string Isletme { get; set; } = "";
    public string? VergiNo { get; set; }
    public int? GrupId { get; set; }
    public Grup? Grup { get; set; }
    public string? Il { get; set; }
    public string? Ilce { get; set; }
    public string? Mahalle { get; set; }
    public string? Adres { get; set; }
    public string? Telefon { get; set; }
    public int? GorevliId { get; set; }
    public Kullanici? Gorevli { get; set; }
    // Onay Verdi | Onay Vermedi | Kararsız | Görüşülmedi
    public string Durum { get; set; } = "Görüşülmedi";
    public DateTime? SonGorusmeTarihi { get; set; }
    public DateTime KayitTarihi { get; set; } = DateTime.UtcNow;

    public List<Gorusme> Gorusmeler { get; set; } = [];
}
