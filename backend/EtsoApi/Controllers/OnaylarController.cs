using EtsoApi.Data;
using EtsoApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EtsoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
public class OnaylarController(EtsoDbContext db) : ControllerBase
{
    private static readonly string[] GecerliDurumlar = ["Bekliyor", "Onaylandı", "Reddedildi", "İptal Edildi"];

    [HttpGet]
    public async Task<IActionResult> Listele(
        [FromQuery] string? durum, [FromQuery] string? islemTuru, [FromQuery] int? gorevliId,
        [FromQuery] DateTime? baslangic, [FromQuery] DateTime? bitis,
        [FromQuery] int sayfa = 1, [FromQuery] int sayfaBoyutu = 20)
    {
        var sorgu = db.Onaylar.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(durum)) sorgu = sorgu.Where(o => o.Durum == durum);
        if (!string.IsNullOrWhiteSpace(islemTuru)) sorgu = sorgu.Where(o => o.IslemTuru == islemTuru);
        if (gorevliId is not null) sorgu = sorgu.Where(o => o.GorevliId == gorevliId);
        if (baslangic is not null) sorgu = sorgu.Where(o => o.Tarih >= baslangic);
        if (bitis is not null) sorgu = sorgu.Where(o => o.Tarih < bitis);

        var toplam = await sorgu.CountAsync();
        sayfaBoyutu = Math.Clamp(sayfaBoyutu, 1, 100);
        sayfa = Math.Max(sayfa, 1);

        var kayitlar = await sorgu
            .OrderByDescending(o => o.Tarih)
            .Skip((sayfa - 1) * sayfaBoyutu)
            .Take(sayfaBoyutu)
            .Select(o => new
            {
                o.Id, o.IslemTuru, o.Tarih, o.Durum,
                o.EsnafId,
                Esnaf = o.Esnaf!.AdSoyad,
                Isletme = o.Esnaf.Isletme,
                Grup = o.Esnaf.Grup != null ? o.Esnaf.Grup.Ad : null,
                Ilce = o.Esnaf.Ilce,
                Mahalle = o.Esnaf.Mahalle,
                Telefon = o.Esnaf.Telefon,
                o.GorevliId,
                Gorevli = o.Gorevli != null ? o.Gorevli.AdSoyad : null,
            })
            .ToListAsync();

        return Ok(new { toplam, sayfa, sayfaBoyutu, kayitlar });
    }

    [HttpGet("istatistik")]
    public async Task<IActionResult> Istatistik()
    {
        var gruplu = await db.Onaylar.GroupBy(o => o.Durum)
            .Select(g => new { Durum = g.Key, Adet = g.Count() }).ToListAsync();
        int Say(string durum) => gruplu.FirstOrDefault(g => g.Durum == durum)?.Adet ?? 0;
        return Ok(new
        {
            toplam = gruplu.Sum(g => g.Adet),
            bekleyen = Say("Bekliyor"),
            onaylanan = Say("Onaylandı"),
            reddedilen = Say("Reddedildi"),
            iptalEdilen = Say("İptal Edildi"),
        });
    }

    [HttpPost]
    public async Task<IActionResult> Olustur(OnayYazDto dto)
    {
        if (!await db.Esnaflar.AnyAsync(e => e.Id == dto.EsnafId))
            return BadRequest(new { mesaj = "Üye bulunamadı." });

        var onay = new Onay
        {
            EsnafId = dto.EsnafId,
            GorevliId = dto.GorevliId,
            IslemTuru = string.IsNullOrWhiteSpace(dto.IslemTuru) ? "Üyelik Onayı" : dto.IslemTuru,
        };
        db.Onaylar.Add(onay);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Listele), new { durum = onay.Durum }, new { onay.Id });
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpPut("{id:int}/karar")]
    public async Task<IActionResult> Karar(int id, OnayKararDto dto)
    {
        if (!GecerliDurumlar.Contains(dto.Durum))
            return BadRequest(new { mesaj = $"Geçersiz durum. Geçerli değerler: {string.Join(", ", GecerliDurumlar)}" });

        var onay = await db.Onaylar.FindAsync(id);
        if (onay is null) return NotFound();

        onay.Durum = dto.Durum;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Sil(int id)
    {
        var onay = await db.Onaylar.FindAsync(id);
        if (onay is null) return NotFound();
        db.Onaylar.Remove(onay);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
