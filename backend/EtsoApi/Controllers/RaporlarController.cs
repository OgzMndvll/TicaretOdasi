using EtsoApi.Data;
using EtsoApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EtsoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
public class RaporlarController(EtsoDbContext db) : ControllerBase
{
    [HttpGet("gruplar")]
    public async Task<IActionResult> GrupRaporu()
    {
        var rapor = await db.Gruplar.AsNoTracking()
            .Select(g => new
            {
                g.Id,
                g.No,
                g.Ad,
                ToplamEsnaf = g.Esnaflar.Count,
                Gorusme = g.Esnaflar.SelectMany(e => e.Gorusmeler).Count(),
                Onaylayan = g.Esnaflar.Count(e => e.Durum == "Onay Verdi"),
                Reddedilen = g.Esnaflar.Count(e => e.Durum == "Onay Vermedi"),
                Kararsiz = g.Esnaflar.Count(e => e.Durum == "Kararsız"),
                Gorusulmeyen = g.Esnaflar.Count(e => e.Durum == "Görüşülmedi"),
            })
            .OrderByDescending(g => g.ToplamEsnaf)
            .ToListAsync();

        return Ok(rapor.Select(g => new
        {
            g.Id, g.No, g.Ad, g.ToplamEsnaf, g.Gorusme, g.Onaylayan, g.Reddedilen, g.Kararsiz, g.Gorusulmeyen,
            OnayOrani = g.ToplamEsnaf == 0 ? 0 : Math.Round(g.Onaylayan * 100.0 / g.ToplamEsnaf, 1),
        }));
    }

    [HttpGet("gruplar/disa-aktar")]
    public async Task<IActionResult> GrupRaporuIndir()
    {
        var rapor = await db.Gruplar.AsNoTracking()
            .Select(g => new
            {
                g.No,
                g.Ad,
                ToplamEsnaf = g.Esnaflar.Count,
                Gorusme = g.Esnaflar.SelectMany(e => e.Gorusmeler).Count(),
                Onaylayan = g.Esnaflar.Count(e => e.Durum == "Onay Verdi"),
                Reddedilen = g.Esnaflar.Count(e => e.Durum == "Onay Vermedi"),
                Kararsiz = g.Esnaflar.Count(e => e.Durum == "Kararsız"),
                Gorusulmeyen = g.Esnaflar.Count(e => e.Durum == "Görüşülmedi"),
            })
            .OrderByDescending(g => g.ToplamEsnaf)
            .ToListAsync();

        var satirlar = rapor.Select(g => new object?[]
        {
            g.No, g.Ad, g.ToplamEsnaf, g.Gorusme, g.Onaylayan, g.Reddedilen, g.Kararsiz, g.Gorusulmeyen,
            g.ToplamEsnaf == 0 ? 0.0 : Math.Round(g.Onaylayan * 100.0 / g.ToplamEsnaf, 1),
        });
        var dosya = ExcelServisi.Olustur("Grup Raporu",
            ["Grup No", "Grup", "Toplam Üye", "Görüşme", "Onaylayan", "Reddedilen", "Kararsız", "Görüşülmeyen", "Onay Oranı (%)"],
            satirlar);
        return File(dosya, ExcelServisi.IcerikTipi, $"grup-raporu-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }
}
