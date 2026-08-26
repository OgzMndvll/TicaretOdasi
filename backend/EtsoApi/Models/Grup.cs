namespace EtsoApi.Models;

public class Grup
{
    public int Id { get; set; }
    // Odanın meslek grubu numarası (1-35). Üye raporunda grup adının başında bu numara yer alır.
    public int? No { get; set; }
    public string Ad { get; set; } = "";
    public string? Aciklama { get; set; }
    public string Tur { get; set; } = "Sektörel";
    public int? UstGrupId { get; set; }
    public Grup? UstGrup { get; set; }
    public string Durum { get; set; } = "Aktif";
    public DateTime GuncellemeTarihi { get; set; } = DateTime.UtcNow;

    public List<Esnaf> Esnaflar { get; set; } = [];
}
