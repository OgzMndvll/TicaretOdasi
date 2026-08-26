using EtsoApi.Data;
using EtsoApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EtsoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GruplarController(EtsoDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listele([FromQuery] string? durum, [FromQuery] string? tur)
    {
        var sorgu = db.Gruplar.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(durum)) sorgu = sorgu.Where(g => g.Durum == durum);
        if (!string.IsNullOrWhiteSpace(tur)) sorgu = sorgu.Where(g => g.Tur == tur);

        var kayitlar = await sorgu
            // Meslek grupları oda numarasına göre sıralanır; numarasız gruplar sona alfabetik gelir.
            .OrderBy(g => g.No == null).ThenBy(g => g.No).ThenBy(g => g.Ad)
            .Select(g => new
            {
                g.Id, g.No, g.Ad, g.Aciklama, g.Tur,
                g.UstGrupId, UstGrup = g.UstGrup != null ? g.UstGrup.Ad : null,
                EsnafSayisi = g.Esnaflar.Count,
                AktifGorevli = g.Esnaflar.Where(e => e.GorevliId != null).Select(e => e.GorevliId).Distinct().Count(),
                g.Durum, g.GuncellemeTarihi,
            })
            .ToListAsync();

        return Ok(kayitlar);
    }

    [HttpGet("istatistik")]
    public async Task<IActionResult> Istatistik()
    {
        var toplamGrup = await db.Gruplar.CountAsync();
        var aktifGrup = await db.Gruplar.CountAsync(g => g.Durum == "Aktif");
        var toplamEsnaf = await db.Esnaflar.CountAsync();
        return Ok(new { toplamGrup, aktifGrup, pasifGrup = toplamGrup - aktifGrup, toplamEsnaf });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Getir(int id)
    {
        var grup = await db.Gruplar.AsNoTracking().Include(g => g.UstGrup).FirstOrDefaultAsync(g => g.Id == id);
        if (grup is null) return NotFound();
        return Ok(new { grup.Id, grup.No, grup.Ad, grup.Aciklama, grup.Tur, grup.UstGrupId, UstGrup = grup.UstGrup?.Ad, grup.Durum, grup.GuncellemeTarihi });
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpPost]
    public async Task<IActionResult> Olustur(GrupYazDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Ad)) return BadRequest(new { mesaj = "Grup adı zorunludur." });
        if (await db.Gruplar.AnyAsync(g => g.Ad == dto.Ad.Trim()))
            return Conflict(new { mesaj = "Bu adla bir grup zaten var." });
        if (dto.No is not null && await db.Gruplar.AnyAsync(g => g.No == dto.No))
            return Conflict(new { mesaj = $"{dto.No} numaralı meslek grubu zaten var." });

        var grup = new Grup
        {
            No = dto.No,
            Ad = dto.Ad.Trim(),
            Aciklama = dto.Aciklama,
            Tur = string.IsNullOrWhiteSpace(dto.Tur) ? "Sektörel" : dto.Tur,
            UstGrupId = dto.UstGrupId,
            Durum = string.IsNullOrWhiteSpace(dto.Durum) ? "Aktif" : dto.Durum,
        };
        db.Gruplar.Add(grup);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Getir), new { id = grup.Id }, new { grup.Id });
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Guncelle(int id, GrupYazDto dto)
    {
        var grup = await db.Gruplar.FindAsync(id);
        if (grup is null) return NotFound();
        if (dto.No is not null && await db.Gruplar.AnyAsync(g => g.No == dto.No && g.Id != id))
            return Conflict(new { mesaj = $"{dto.No} numaralı meslek grubu başka bir kayıtta var." });

        grup.No = dto.No;
        grup.Ad = dto.Ad.Trim();
        grup.Aciklama = dto.Aciklama;
        if (!string.IsNullOrWhiteSpace(dto.Tur)) grup.Tur = dto.Tur;
        grup.UstGrupId = dto.UstGrupId;
        if (!string.IsNullOrWhiteSpace(dto.Durum)) grup.Durum = dto.Durum;
        grup.GuncellemeTarihi = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Sil(int id)
    {
        var grup = await db.Gruplar.Include(g => g.Esnaflar).FirstOrDefaultAsync(g => g.Id == id);
        if (grup is null) return NotFound();
        if (grup.Esnaflar.Count > 0)
            return Conflict(new { mesaj = "Bu gruba kayıtlı üyeler var; önce üyeleri başka gruba taşıyın." });
        db.Gruplar.Remove(grup);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
