using EtsoApi.Data;
using EtsoApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EtsoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GorusmelerController(EtsoDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listele(
        [FromQuery] int? esnafId, [FromQuery] int? gorevliId, [FromQuery] string? sonuc,
        [FromQuery] bool? takipGerekli, [FromQuery] DateTime? baslangic, [FromQuery] DateTime? bitis,
        [FromQuery] int sayfa = 1, [FromQuery] int sayfaBoyutu = 20)
    {
        // Görevli rolü yalnızca kendi görüşmelerini görebilir.
        if (!User.Yonetici()) gorevliId = User.KullaniciId();

        var sorgu = db.Gorusmeler.AsNoTracking();
        if (esnafId is not null) sorgu = sorgu.Where(g => g.EsnafId == esnafId);
        if (gorevliId is not null) sorgu = sorgu.Where(g => g.GorevliId == gorevliId);
        if (!string.IsNullOrWhiteSpace(sonuc)) sorgu = sorgu.Where(g => g.Sonuc == sonuc);
        if (takipGerekli is not null) sorgu = sorgu.Where(g => g.TakipGerekli == takipGerekli);
        if (baslangic is not null) sorgu = sorgu.Where(g => g.Tarih >= baslangic);
        if (bitis is not null) sorgu = sorgu.Where(g => g.Tarih < bitis);

        var toplam = await sorgu.CountAsync();
        sayfaBoyutu = Math.Clamp(sayfaBoyutu, 1, 100);
        sayfa = Math.Max(sayfa, 1);

        var kayitlar = await sorgu
            .OrderByDescending(g => g.Tarih)
            .Skip((sayfa - 1) * sayfaBoyutu)
            .Take(sayfaBoyutu)
            .Select(g => new
            {
                g.Id, g.Tarih, g.Sonuc, g.Not, g.TakipGerekli,
                g.EsnafId,
                Esnaf = g.Esnaf!.AdSoyad,
                Isletme = g.Esnaf.Isletme,
                Grup = g.Esnaf.Grup != null ? g.Esnaf.Grup.Ad : null,
                Ilce = g.Esnaf.Ilce,
                Mahalle = g.Esnaf.Mahalle,
                Telefon = g.Esnaf.Telefon,
                EsnafDurum = g.Esnaf.Durum,
                g.GorevliId,
                Gorevli = g.Gorevli!.AdSoyad,
            })
            .ToListAsync();

        return Ok(new { toplam, sayfa, sayfaBoyutu, kayitlar });
    }

    [HttpGet("istatistik")]
    public async Task<IActionResult> Istatistik()
    {
        var kaynak = db.Gorusmeler.AsNoTracking();
        if (!User.Yonetici()) kaynak = kaynak.Where(g => g.GorevliId == User.KullaniciId());

        var toplam = await kaynak.CountAsync();
        var bugun = DateTime.UtcNow.Date;
        var bugunku = await kaynak.CountAsync(g => g.Tarih >= bugun);
        var takipGereken = await kaynak.CountAsync(g => g.TakipGerekli);
        var sonuclar = await kaynak.GroupBy(g => g.Sonuc)
            .Select(g => new { Sonuc = g.Key, Adet = g.Count() }).ToListAsync();

        var alti_ay_once = new DateTime(bugun.Year, bugun.Month, 1).AddMonths(-7);
        var aylikHam = await kaynak
            .Where(g => g.Tarih >= alti_ay_once)
            .GroupBy(g => new { g.Tarih.Year, g.Tarih.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Adet = g.Count() })
            .ToListAsync();

        string[] aylar = ["Oca", "Şub", "Mar", "Nis", "May", "Haz", "Tem", "Ağu", "Eyl", "Eki", "Kas", "Ara"];
        var aylik = Enumerable.Range(0, 8)
            .Select(i => alti_ay_once.AddMonths(i))
            .Select(ay => new
            {
                ay = aylar[ay.Month - 1],
                adet = aylikHam.FirstOrDefault(h => h.Year == ay.Year && h.Month == ay.Month)?.Adet ?? 0,
            })
            .ToList();

        var gorevliPerformans = await kaynak
            .GroupBy(g => g.Gorevli!.AdSoyad)
            .Select(g => new { adSoyad = g.Key, adet = g.Count() })
            .OrderByDescending(g => g.adet)
            .Take(5)
            .ToListAsync();

        return Ok(new
        {
            toplam, bugunku, takipGereken,
            onayVerdi = sonuclar.FirstOrDefault(s => s.Sonuc == "Onay Verdi")?.Adet ?? 0,
            onayVermedi = sonuclar.FirstOrDefault(s => s.Sonuc == "Onay Vermedi")?.Adet ?? 0,
            kararsiz = sonuclar.FirstOrDefault(s => s.Sonuc == "Kararsız")?.Adet ?? 0,
            aylik, gorevliPerformans,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Olustur(GorusmeYazDto dto)
    {
        // Görevli, yalnızca kendi adına görüşme kaydedebilir.
        var gorevliId = User.Yonetici() ? dto.GorevliId : User.KullaniciId() ?? dto.GorevliId;

        var esnaf = await db.Esnaflar.FindAsync(dto.EsnafId);
        if (esnaf is null) return BadRequest(new { mesaj = "Üye bulunamadı." });
        if (!await db.Kullanicilar.AnyAsync(k => k.Id == gorevliId))
            return BadRequest(new { mesaj = "Görevli bulunamadı." });

        // Görevli, yalnızca kendisine atanmış aktif bir üye için görüşme kaydedebilir.
        if (!User.Yonetici() && !await db.Gorevlendirmeler.AnyAsync(g =>
                g.EsnafId == dto.EsnafId && g.GorevliId == gorevliId && g.Durum == "Aktif"))
            return BadRequest(new { mesaj = "Bu üye için aktif bir görevlendirmeniz bulunmuyor." });

        var gorusme = new Gorusme
        {
            EsnafId = dto.EsnafId,
            GorevliId = gorevliId,
            Tarih = dto.Tarih == default ? DateTime.UtcNow : dto.Tarih,
            Sonuc = string.IsNullOrWhiteSpace(dto.Sonuc) ? "Kararsız" : dto.Sonuc,
            Not = dto.Not,
            TakipGerekli = dto.TakipGerekli,
        };
        db.Gorusmeler.Add(gorusme);

        // Esnafın son görüşme bilgisi ve onay durumu, en güncel görüşmeyle senkron tutulur.
        if (esnaf.SonGorusmeTarihi is null || gorusme.Tarih >= esnaf.SonGorusmeTarihi)
        {
            esnaf.SonGorusmeTarihi = gorusme.Tarih;
            esnaf.Durum = gorusme.Sonuc;
        }

        // Görüşme yapıldığında bu üye için aktif görevlendirme otomatik tamamlanır.
        var acikGorevlendirme = await db.Gorevlendirmeler
            .FirstOrDefaultAsync(g => g.EsnafId == dto.EsnafId && g.GorevliId == gorevliId && g.Durum == "Aktif");
        if (acikGorevlendirme is not null) acikGorevlendirme.Durum = "Tamamlandı";

        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Listele), new { esnafId = gorusme.EsnafId }, new { gorusme.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Guncelle(int id, GorusmeYazDto dto)
    {
        var gorusme = await db.Gorusmeler.FindAsync(id);
        if (gorusme is null) return NotFound();

        // Görevli yalnızca kendi görüşmesini düzenleyebilir ve görüşmeyi başkasına devredemez.
        if (!User.Yonetici())
        {
            if (gorusme.GorevliId != User.KullaniciId()) return Forbid();
            dto = dto with { GorevliId = gorusme.GorevliId };
        }
        if (!await db.Kullanicilar.AnyAsync(k => k.Id == dto.GorevliId))
            return BadRequest(new { mesaj = "Görevli bulunamadı." });

        gorusme.GorevliId = dto.GorevliId;
        if (dto.Tarih != default) gorusme.Tarih = dto.Tarih;
        if (!string.IsNullOrWhiteSpace(dto.Sonuc)) gorusme.Sonuc = dto.Sonuc;
        gorusme.Not = dto.Not;
        gorusme.TakipGerekli = dto.TakipGerekli;
        await db.SaveChangesAsync();
        await EsnafDurumunuEsitle(gorusme.EsnafId);
        return NoContent();
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Sil(int id)
    {
        var gorusme = await db.Gorusmeler.FindAsync(id);
        if (gorusme is null) return NotFound();
        var esnafId = gorusme.EsnafId;
        db.Gorusmeler.Remove(gorusme);
        await db.SaveChangesAsync();
        await EsnafDurumunuEsitle(esnafId);
        return NoContent();
    }

    /// <summary>Esnafın durumunu ve son görüşme tarihini, kalan en güncel görüşmesiyle eşitler.</summary>
    private async Task EsnafDurumunuEsitle(int esnafId)
    {
        var esnaf = await db.Esnaflar.FindAsync(esnafId);
        if (esnaf is null) return;
        var sonGorusme = await db.Gorusmeler
            .Where(g => g.EsnafId == esnafId)
            .OrderByDescending(g => g.Tarih)
            .FirstOrDefaultAsync();
        esnaf.SonGorusmeTarihi = sonGorusme?.Tarih;
        esnaf.Durum = sonGorusme?.Sonuc ?? "Görüşülmedi";
        await db.SaveChangesAsync();
    }
}
