using System.ComponentModel.DataAnnotations;

namespace EtsoApi.Models;

public class Ayar
{
    [Key]
    public string Anahtar { get; set; } = "";
    public string Deger { get; set; } = "";
    public DateTime GuncellemeTarihi { get; set; } = DateTime.UtcNow;
}
