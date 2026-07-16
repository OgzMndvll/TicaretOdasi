namespace EtsoApi.Models;

public class Onay
{
    public int Id { get; set; }
    public int EsnafId { get; set; }
    public Esnaf? Esnaf { get; set; }
    public int? GorevliId { get; set; }
    public Kullanici? Gorevli { get; set; }
    public string IslemTuru { get; set; } = "Üyelik Onayı";
    public DateTime Tarih { get; set; } = DateTime.UtcNow;
    // Bekliyor | Onaylandı | Reddedildi | İptal Edildi
    public string Durum { get; set; } = "Bekliyor";
}
