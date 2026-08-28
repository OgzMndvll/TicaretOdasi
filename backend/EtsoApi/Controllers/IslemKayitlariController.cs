using EtsoApi.Data;
using EtsoApi.Models;
using EtsoApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EtsoApi.Controllers;

/// <summary>
/// Denetim kayıtları: kim, hangi üyede, ne zaman, hangi işlemi yaptı. Yalnızca okunur —
/// kayıt yazma işi <see cref="IslemGunlugu"/>'nde, düzenleme/silme ucu bilerek yok
/// (denetim kaydı sonradan değiştirilebiliyorsa denetim değildir).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = TokenServisi.SistemYonetimiPolitikasi)]
public class IslemKayitlariController(EtsoDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listele(
        [FromQuery] List<string>? islem, [FromQuery] List<int>? kullaniciId, [FromQuery] int? esnafId,
        [FromQuery] DateTime? baslangic, [FromQuery] DateTime? bitis, [FromQuery] string? arama,
        [FromQuery] int sayfa = 1, [FromQuery] int sayfaBoyutu = 50)
    {
        var sorgu = db.IslemKayitlari.AsNoTracking();
        if (islem is { Count: > 0 }) sorgu = sorgu.Where(k => islem.Contains(k.Islem));
        if (kullaniciId is { Count: > 0 }) sorgu = sorgu.Where(k => k.KullaniciId != null && kullaniciId.Contains(k.KullaniciId.Value));
        if (esnafId is not null) sorgu = sorgu.Where(k => k.EsnafId == esnafId);
        if (baslangic is not null) sorgu = sorgu.Where(k => k.Tarih >= baslangic);
        // Bitiş tarihi kullanıcı için "o günün sonu"dur; gün başlangıcı gelirse o gün dışarıda kalırdı.
        if (bitis is not null) sorgu = sorgu.Where(k => k.Tarih < bitis.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(arama))
            sorgu = sorgu.Where(k => k.EsnafUnvan!.Contains(arama)
                || k.KullaniciAdSoyad.Contains(arama) || k.KullaniciAdi.Contains(arama));

        var toplam = await sorgu.CountAsync();
        sayfaBoyutu = Math.Clamp(sayfaBoyutu, 1, 200);
        sayfa = Math.Max(sayfa, 1);

        var kayitlar = await sorgu
            .OrderByDescending(k => k.Tarih).ThenByDescending(k => k.Id)
            .Skip((sayfa - 1) * sayfaBoyutu)
            .Take(sayfaBoyutu)
            .ToListAsync();

        return Ok(new { toplam, sayfa, sayfaBoyutu, kayitlar });
    }

    /// <summary>Süzgeç seçenekleri: log'da fiilen geçen kullanıcılar ve işlem türleri.</summary>
    [HttpGet("secenekler")]
    public async Task<IActionResult> Secenekler()
    {
        var kullanicilar = await db.IslemKayitlari.AsNoTracking()
            .Where(k => k.KullaniciId != null)
            .GroupBy(k => new { k.KullaniciId, k.KullaniciAdSoyad })
            .Select(g => new { id = g.Key.KullaniciId!.Value, adSoyad = g.Key.KullaniciAdSoyad, adet = g.Count() })
            .OrderByDescending(g => g.adet)
            .ToListAsync();
        return Ok(new { kullanicilar, islemler = Islemler.Tumu });
    }

    [HttpGet("istatistik")]
    public async Task<IActionResult> Istatistik()
    {
        var bugun = DateTime.UtcNow.Date;
        var toplam = await db.IslemKayitlari.CountAsync();
        var bugunku = await db.IslemKayitlari.CountAsync(k => k.Tarih >= bugun);
        var haftalik = await db.IslemKayitlari.CountAsync(k => k.Tarih >= bugun.AddDays(-7));
        var kullaniciSayisi = await db.IslemKayitlari.Where(k => k.KullaniciId != null)
            .Select(k => k.KullaniciId).Distinct().CountAsync();
        return Ok(new { toplam, bugunku, haftalik, kullaniciSayisi });
    }

    [HttpGet("disa-aktar")]
    public async Task<IActionResult> DisaAktar(
        [FromQuery] List<string>? islem, [FromQuery] List<int>? kullaniciId, [FromQuery] int? esnafId,
        [FromQuery] DateTime? baslangic, [FromQuery] DateTime? bitis, [FromQuery] string? arama)
    {
        var sorgu = db.IslemKayitlari.AsNoTracking();
        if (islem is { Count: > 0 }) sorgu = sorgu.Where(k => islem.Contains(k.Islem));
        if (kullaniciId is { Count: > 0 }) sorgu = sorgu.Where(k => k.KullaniciId != null && kullaniciId.Contains(k.KullaniciId.Value));
        if (esnafId is not null) sorgu = sorgu.Where(k => k.EsnafId == esnafId);
        if (baslangic is not null) sorgu = sorgu.Where(k => k.Tarih >= baslangic);
        if (bitis is not null) sorgu = sorgu.Where(k => k.Tarih < bitis.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(arama))
            sorgu = sorgu.Where(k => k.EsnafUnvan!.Contains(arama)
                || k.KullaniciAdSoyad.Contains(arama) || k.KullaniciAdi.Contains(arama));

        var kayitlar = await sorgu.OrderByDescending(k => k.Tarih).Take(ExcelServisi.EnFazlaSatir).ToListAsync();
        var dosya = ExcelServisi.Olustur("İşlem Kayıtları",
            ["Tarih", "Kullanıcı", "Kullanıcı Adı", "Üye", "İşlem", "Ayrıntı"],
            kayitlar.Select(k => new object?[]
            {
                k.Tarih, k.KullaniciAdSoyad, k.KullaniciAdi, k.EsnafUnvan, k.Islem, k.Detay,
            }));
        return File(dosya, ExcelServisi.IcerikTipi, $"islem-kayitlari-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }
}
