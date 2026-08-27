using System.Text.Json.Serialization;

namespace EtsoApi.Models;

/// <summary>
/// Aktif çalışan ile meslek grubu arasındaki bağ. Odanın grup listesinde her grubun
/// 1–3 sorumlu üyesi bulunduğu için ilişki çoka-çoktur: bir çalışan birden çok gruba
/// bakabilir, bir grupta birden çok çalışan olabilir.
/// </summary>
public class KullaniciGrup
{
    public int Id { get; set; }

    public int KullaniciId { get; set; }
    [JsonIgnore]
    public Kullanici? Kullanici { get; set; }

    public int GrupId { get; set; }
    [JsonIgnore]
    public Grup? Grup { get; set; }

    /// <summary>
    /// Grup listesindeki sütun sırası ("1. Üye", "2. Üye", "3. Üye"). Yalnızca Excel'den
    /// içe aktarmada dolar; panelden elle eklenen bağlarda boş kalır ve liste sonuna yazılır.
    /// </summary>
    public int? Sira { get; set; }
}
