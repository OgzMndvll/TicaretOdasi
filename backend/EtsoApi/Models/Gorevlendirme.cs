namespace EtsoApi.Models;

public class Gorevlendirme
{
    public int Id { get; set; }
    public int GorevliId { get; set; }
    public Kullanici? Gorevli { get; set; }
    public int? EsnafId { get; set; }
    public Esnaf? Esnaf { get; set; }
    public int? GrupId { get; set; }
    public Grup? Grup { get; set; }
    public DateTime Tarih { get; set; } = DateTime.UtcNow;
    public string? Not { get; set; }
    public string? RedMazereti { get; set; }
    public DateTime? KararTarihi { get; set; }
    // Bekleyen | Aktif | Reddedildi | Tamamlandı | İptal Edildi
    public string Durum { get; set; } = "Bekleyen";
}
