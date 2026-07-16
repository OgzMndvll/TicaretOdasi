using EtsoApi.Data;
using EtsoApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EtsoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
public class AyarlarController(EtsoDbContext db) : ControllerBase
{
    // Kurum bilgileri ve güvenlik ayarları yalnızca Yönetici'ye açıktır (Görevli okuyamaz).
    [HttpGet]
    public async Task<IActionResult> Listele()
    {
        var ayarlar = await db.Ayarlar.AsNoTracking().ToDictionaryAsync(a => a.Anahtar, a => a.Deger);
        return Ok(ayarlar);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpPut]
    public async Task<IActionResult> Kaydet([FromBody] Dictionary<string, string> gelen)
    {
        if (gelen.Count == 0) return BadRequest(new { mesaj = "Kaydedilecek ayar bulunamadı." });
        if (gelen.Keys.Any(k => k.Length > 80) || gelen.Values.Any(v => v.Length > 2000))
            return BadRequest(new { mesaj = "Ayar anahtarı veya değeri çok uzun." });

        var mevcutlar = await db.Ayarlar.Where(a => gelen.Keys.Contains(a.Anahtar)).ToListAsync();
        foreach (var (anahtar, deger) in gelen)
        {
            var ayar = mevcutlar.FirstOrDefault(a => a.Anahtar == anahtar);
            if (ayar is null) db.Ayarlar.Add(new Ayar { Anahtar = anahtar, Deger = deger });
            else { ayar.Deger = deger; ayar.GuncellemeTarihi = DateTime.UtcNow; }
        }
        await db.SaveChangesAsync();
        return NoContent();
    }
}
