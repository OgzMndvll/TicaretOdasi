using EtsoApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EtsoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
public class DashboardController(EtsoDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Ozet()
    {
        var durumlar = await db.Esnaflar.GroupBy(e => e.Durum)
            .Select(g => new { Durum = g.Key, Adet = g.Count() }).ToListAsync();
        int Say(string durum) => durumlar.FirstOrDefault(g => g.Durum == durum)?.Adet ?? 0;

        var toplamEsnaf = durumlar.Sum(g => g.Adet);
        var gorusulmemis = Say("Görüşülmedi");

        var bugun = DateTime.UtcNow.Date;
        var seriBaslangic = new DateTime(bugun.Year, bugun.Month, 1).AddMonths(-7);
        var aylikHam = await db.Gorusmeler
            .Where(g => g.Tarih >= seriBaslangic)
            .GroupBy(g => new { g.Tarih.Year, g.Tarih.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Adet = g.Count() })
            .ToListAsync();

        string[] aylar = ["Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara"];
        var aylikGorusmeler = Enumerable.Range(0, 8)
            .Select(i => seriBaslangic.AddMonths(i))
            .Select(ay => new
            {
                ay = aylar[ay.Month - 1],
                adet = aylikHam.FirstOrDefault(h => h.Year == ay.Year && h.Month == ay.Month)?.Adet ?? 0,
            })
            .ToList();

        var gorevliPerformans = await db.Gorusmeler
            .GroupBy(g => g.Gorevli!.AdSoyad)
            .Select(g => new { adSoyad = g.Key, adet = g.Count() })
            .OrderByDescending(g => g.adet)
            .Take(5)
            .ToListAsync();

        var sonGorusmeler = await db.Gorusmeler.AsNoTracking()
            .OrderByDescending(g => g.Tarih)
            .Take(5)
            .Select(g => new
            {
                g.Id, g.Tarih, g.Sonuc,
                Esnaf = g.Esnaf!.AdSoyad,
                Isletme = g.Esnaf.Isletme,
                Grup = g.Esnaf.Grup != null ? g.Esnaf.Grup.Ad : null,
                Gorevli = g.Gorevli!.AdSoyad,
            })
            .ToListAsync();

        return Ok(new
        {
            toplamEsnaf,
            gorusulen = toplamEsnaf - gorusulmemis,
            onayVeren = Say("Onay Verdi"),
            onayVermeyen = Say("Onay Vermedi"),
            kararsiz = Say("Kararsız"),
            gorusulmemis,
            aylikGorusmeler,
            gorevliPerformans,
            sonGorusmeler,
        });
    }
}
