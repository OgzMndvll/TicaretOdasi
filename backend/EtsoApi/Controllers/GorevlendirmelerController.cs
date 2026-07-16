using EtsoApi.Data;
using EtsoApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EtsoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GorevlendirmelerController(EtsoDbContext db) : ControllerBase
{
    private static readonly string[] GecerliDurumlar = ["Aktif", "Tamamlandı", "İptal Edildi"];

    [HttpGet]
    public async Task<IActionResult> Listele(
        [FromQuery] string? durum, [FromQuery] int? gorevliId, [FromQuery] int? grupId, [FromQuery] string? arama,
        [FromQuery] int sayfa = 1, [FromQuery] int sayfaBoyutu = 20)
    {
        // Görevli rolü yalnızca kendi görevlendirmelerini görebilir.
        if (!User.Yonetici()) gorevliId = User.KullaniciId();

        var sorgu = db.Gorevlendirmeler.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(durum)) sorgu = sorgu.Where(g => g.Durum == durum);
        if (gorevliId is not null) sorgu = sorgu.Where(g => g.GorevliId == gorevliId);
        if (grupId is not null) sorgu = sorgu.Where(g => g.GrupId == grupId || (g.Esnaf != null && g.Esnaf.GrupId == grupId));
        if (!string.IsNullOrWhiteSpace(arama))
            sorgu = sorgu.Where(g => g.Esnaf != null && (g.Esnaf.AdSoyad.Contains(arama) || g.Esnaf.Isletme.Contains(arama)));

        var toplam = await sorgu.CountAsync();
        sayfaBoyutu = Math.Clamp(sayfaBoyutu, 1, 100);
        sayfa = Math.Max(sayfa, 1);

        var kayitlar = await sorgu
            .OrderByDescending(g => g.Tarih)
            .Skip((sayfa - 1) * sayfaBoyutu)
            .Take(sayfaBoyutu)
            .Select(g => new
            {
                g.Id, g.Tarih, g.Not, g.Durum,
                g.GorevliId, Gorevli = g.Gorevli!.AdSoyad,
                g.EsnafId, Esnaf = g.Esnaf != null ? g.Esnaf.AdSoyad : null,
                Isletme = g.Esnaf != null ? g.Esnaf.Isletme : null,
                Ilce = g.Esnaf != null ? g.Esnaf.Ilce : null,
                Mahalle = g.Esnaf != null ? g.Esnaf.Mahalle : null,
                Telefon = g.Esnaf != null ? g.Esnaf.Telefon : null,
                g.GrupId, Grup = g.Grup != null ? g.Grup.Ad : null,
            })
            .ToListAsync();

        return Ok(new { toplam, sayfa, sayfaBoyutu, kayitlar });
    }

    [HttpGet("istatistik")]
    public async Task<IActionResult> Istatistik()
    {
        var kaynak = db.Gorevlendirmeler.AsNoTracking();
        if (!User.Yonetici()) kaynak = kaynak.Where(g => g.GorevliId == User.KullaniciId());

        var gruplu = await kaynak.GroupBy(g => g.Durum)
            .Select(g => new { Durum = g.Key, Adet = g.Count() }).ToListAsync();
        int Say(string durum) => gruplu.FirstOrDefault(g => g.Durum == durum)?.Adet ?? 0;
        return Ok(new
        {
            toplam = gruplu.Sum(g => g.Adet),
            aktif = Say("Aktif"),
            tamamlanan = Say("Tamamlandı"),
            iptalEdilen = Say("İptal Edildi"),
        });
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpPost]
    public async Task<IActionResult> Olustur(GorevlendirmeYazDto dto)
    {
        if (!await db.Kullanicilar.AnyAsync(k => k.Id == dto.GorevliId))
            return BadRequest(new { mesaj = "Görevli bulunamadı." });
        if (dto.EsnafId is not null && !await db.Esnaflar.AnyAsync(e => e.Id == dto.EsnafId))
            return BadRequest(new { mesaj = "Üye bulunamadı." });

        var gorevlendirme = new Gorevlendirme
        {
            GorevliId = dto.GorevliId,
            EsnafId = dto.EsnafId,
            GrupId = dto.GrupId,
            Tarih = dto.Tarih == default ? DateTime.UtcNow : dto.Tarih,
            Not = dto.Not,
            // Görevlendirmeler kabul adımı olmadan doğrudan görevliye atanır.
            Durum = "Aktif",
        };
        db.Gorevlendirmeler.Add(gorevlendirme);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Listele), new { gorevliId = gorevlendirme.GorevliId }, new { gorevlendirme.Id });
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Guncelle(int id, GorevlendirmeYazDto dto)
    {
        var gorevlendirme = await db.Gorevlendirmeler.FindAsync(id);
        if (gorevlendirme is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(dto.Durum) && !GecerliDurumlar.Contains(dto.Durum))
            return BadRequest(new { mesaj = $"Geçersiz durum. Geçerli değerler: {string.Join(", ", GecerliDurumlar)}" });

        gorevlendirme.GorevliId = dto.GorevliId;
        gorevlendirme.EsnafId = dto.EsnafId;
        gorevlendirme.GrupId = dto.GrupId;
        if (dto.Tarih != default) gorevlendirme.Tarih = dto.Tarih;
        gorevlendirme.Not = dto.Not;
        if (!string.IsNullOrWhiteSpace(dto.Durum)) gorevlendirme.Durum = dto.Durum;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Sil(int id)
    {
        var gorevlendirme = await db.Gorevlendirmeler.FindAsync(id);
        if (gorevlendirme is null) return NotFound();
        db.Gorevlendirmeler.Remove(gorevlendirme);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
