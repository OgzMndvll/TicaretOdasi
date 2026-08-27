using EtsoApi.Data;
using EtsoApi.Models;
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
                TakipEdilecek = g.Esnaflar.Count(e => e.Durum == "Takip Edilecek"),
                Gelmeyecek = g.Esnaflar.Count(e => e.Durum == "Gelmeyecek"),
                Gorusulmeyen = g.Esnaflar.Count(e => e.Durum == "Görüşülmedi"),
            })
            .OrderByDescending(g => g.ToplamEsnaf)
            .ToListAsync();

        return Ok(rapor.Select(g => new
        {
            g.Id, g.No, g.Ad, g.ToplamEsnaf, g.Gorusme, g.Onaylayan, g.Reddedilen, g.Kararsiz,
            g.TakipEdilecek, g.Gelmeyecek, g.Gorusulmeyen,
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
                TakipEdilecek = g.Esnaflar.Count(e => e.Durum == "Takip Edilecek"),
                Gelmeyecek = g.Esnaflar.Count(e => e.Durum == "Gelmeyecek"),
                Gorusulmeyen = g.Esnaflar.Count(e => e.Durum == "Görüşülmedi"),
            })
            .OrderByDescending(g => g.ToplamEsnaf)
            .ToListAsync();

        var satirlar = rapor.Select(g => new object?[]
        {
            g.No, g.Ad, g.ToplamEsnaf, g.Gorusme, g.Onaylayan, g.Reddedilen, g.Kararsiz,
            g.TakipEdilecek, g.Gelmeyecek, g.Gorusulmeyen,
            g.ToplamEsnaf == 0 ? 0.0 : Math.Round(g.Onaylayan * 100.0 / g.ToplamEsnaf, 1),
        });
        var dosya = ExcelServisi.Olustur("Grup Raporu",
            ["Grup No", "Grup", "Toplam Üye", "Görüşme", "Onaylayan", "Reddedilen", "Kararsız",
             "Takip Edilecek", "Gelmeyecek", "Görüşülmeyen", "Onay Oranı (%)"],
            satirlar);
        return File(dosya, ExcelServisi.IcerikTipi, $"grup-raporu-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }

    // ---- Çalışan (görüşen görevli) raporu ----

    /// <summary>
    /// Süzgeçleri hem özet hem detay hem de Excel çıktısı aynı biçimde kullanır.
    /// <c>Durum</c> çalışanın kendi durumudur ("Aktif"/"Pasif"), diğerleri görüşmeye aittir.
    /// Tüm alanların varsayılanı boştur = süzme yok: kayıt varsayılanı, boş gelen bir sorgu
    /// değerinin yerine de konduğu için "Aktif" varsayılanı "Tümü" seçimini sessizce ezerdi.
    /// Panel, aktif çalışan görünümünü <c>durum=Aktif</c> göndererek açar.
    /// </summary>
    public record CalisanRaporFiltresi(
        string? Durum = null, DateTime? Baslangic = null, DateTime? Bitis = null,
        string? Sonuc = null, int? GrupId = null, int? GorevliId = null);

    /// <summary>Süzgeçlerden geçmiş görüşme sorgusu. Sayımlar yalnızca birincil görevliye yazılır.</summary>
    private IQueryable<Gorusme> SuzulmusGorusmeler(CalisanRaporFiltresi f)
    {
        var sorgu = db.Gorusmeler.AsNoTracking();
        if (f.Baslangic is not null) sorgu = sorgu.Where(g => g.Tarih >= f.Baslangic.Value.Date);
        // Bitiş günü dahil olsun diye ertesi günün başına kadar bakılır.
        if (f.Bitis is not null)
        {
            var bitisUstu = f.Bitis.Value.Date.AddDays(1);
            sorgu = sorgu.Where(g => g.Tarih < bitisUstu);
        }
        if (!string.IsNullOrWhiteSpace(f.Sonuc)) sorgu = sorgu.Where(g => g.Sonuc == f.Sonuc);
        if (f.GrupId is not null) sorgu = sorgu.Where(g => g.Esnaf!.GrupId == f.GrupId);
        if (f.GorevliId is not null) sorgu = sorgu.Where(g => g.GorevliId == f.GorevliId);
        return sorgu;
    }

    private async Task<List<Kullanici>> RaporCalisanlari(CalisanRaporFiltresi f)
    {
        var sorgu = db.Kullanicilar.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(f.Durum)) sorgu = sorgu.Where(k => k.Durum == f.Durum);
        if (f.GorevliId is not null) sorgu = sorgu.Where(k => k.Id == f.GorevliId);
        return await sorgu.OrderBy(k => k.AdSoyad).ToListAsync();
    }

    private record CalisanSatiri(
        int Id, string AdSoyad, string? Gorev, string? Birim, string Durum, string Rol,
        int Gorusme, int UyeSayisi, int OnayVerdi, int OnayVermedi, int Kararsiz,
        // Sonuç sütunlarının toplamı "Gorusme" ile eşleşsin diye takip/gelmeyecek de ayrı sayılır.
        int TakipEdilecek, int Gelmeyecek,
        int OnayliUyeSayisi, int IkinciKatilim, DateTime? SonGorusme, double OnayOrani);

    /// <summary>
    /// Çalışan başına görüşme özetini kurar. Dağıtık (distinct) üye sayıları ayrı sorgularla
    /// alınır; gruplama içinde Distinct().Count() her sağlayıcıda SQL'e çevrilemiyor.
    /// </summary>
    private async Task<List<CalisanSatiri>> CalisanOzeti(CalisanRaporFiltresi f)
    {
        var gorusmeler = SuzulmusGorusmeler(f);

        var sayimlar = await gorusmeler
            .GroupBy(g => g.GorevliId)
            .Select(g => new
            {
                GorevliId = g.Key,
                Toplam = g.Count(),
                OnayVerdi = g.Count(x => x.Sonuc == "Onay Verdi"),
                OnayVermedi = g.Count(x => x.Sonuc == "Onay Vermedi"),
                Kararsiz = g.Count(x => x.Sonuc == "Kararsız"),
                TakipEdilecek = g.Count(x => x.Sonuc == "Takip Edilecek"),
                Gelmeyecek = g.Count(x => x.Sonuc == "Gelmeyecek"),
                SonGorusme = (DateTime?)g.Max(x => x.Tarih),
            })
            .ToDictionaryAsync(x => x.GorevliId);

        var uyeSayilari = await gorusmeler
            .Select(g => new { g.GorevliId, g.EsnafId }).Distinct()
            .GroupBy(x => x.GorevliId)
            .Select(x => new { GorevliId = x.Key, Adet = x.Count() })
            .ToDictionaryAsync(x => x.GorevliId, x => x.Adet);

        var onayliUyeSayilari = await gorusmeler
            .Where(g => g.Sonuc == "Onay Verdi")
            .Select(g => new { g.GorevliId, g.EsnafId }).Distinct()
            .GroupBy(x => x.GorevliId)
            .Select(x => new { GorevliId = x.Key, Adet = x.Count() })
            .ToDictionaryAsync(x => x.GorevliId, x => x.Adet);

        // İkinci çalışan olarak katılım ayrı sütunda gösterilir; toplamlara eklenmez.
        var ikinciKatilimlar = await SuzulmusGorusmeler(f with { GorevliId = null })
            .Where(g => g.IkinciGorevliId != null)
            .GroupBy(g => g.IkinciGorevliId)
            .Select(x => new { GorevliId = x.Key, Adet = x.Count() })
            .ToDictionaryAsync(x => x.GorevliId!.Value, x => x.Adet);

        return (await RaporCalisanlari(f))
            .Select(k =>
            {
                sayimlar.TryGetValue(k.Id, out var s);
                var toplam = s?.Toplam ?? 0;
                return new CalisanSatiri(
                    k.Id, k.AdSoyad, k.Gorev, k.Birim, k.Durum, k.Rol,
                    toplam,
                    uyeSayilari.GetValueOrDefault(k.Id),
                    s?.OnayVerdi ?? 0, s?.OnayVermedi ?? 0, s?.Kararsiz ?? 0,
                    s?.TakipEdilecek ?? 0, s?.Gelmeyecek ?? 0,
                    onayliUyeSayilari.GetValueOrDefault(k.Id),
                    ikinciKatilimlar.GetValueOrDefault(k.Id),
                    s?.SonGorusme,
                    toplam == 0 ? 0 : Math.Round((s?.OnayVerdi ?? 0) * 100.0 / toplam, 1));
            })
            .OrderByDescending(x => x.Gorusme).ThenBy(x => x.AdSoyad)
            .ToList();
    }

    [HttpGet("calisanlar")]
    public async Task<IActionResult> CalisanRaporu([FromQuery] CalisanRaporFiltresi filtre)
    {
        var satirlar = await CalisanOzeti(filtre);
        return Ok(new
        {
            satirlar,
            // Toplamlar tablodaki satırlardan türetilir; süzgeç dışında kalan çalışanların
            // görüşmeleri tabloda görünmediği gibi toplamlara da girmez.
            toplam = new
            {
                calisan = satirlar.Count,
                gorusme = satirlar.Sum(s => s.Gorusme),
                onayVerdi = satirlar.Sum(s => s.OnayVerdi),
                onayVermedi = satirlar.Sum(s => s.OnayVermedi),
                kararsiz = satirlar.Sum(s => s.Kararsiz),
                takipEdilecek = satirlar.Sum(s => s.TakipEdilecek),
                gelmeyecek = satirlar.Sum(s => s.Gelmeyecek),
            },
        });
    }

    /// <summary>Bir çalışanın kimlerle görüştüğü: rapor satırının açılabilir detayı.</summary>
    [HttpGet("calisanlar/{id:int}/gorusmeler")]
    public async Task<IActionResult> CalisanGorusmeleri(int id, [FromQuery] CalisanRaporFiltresi filtre)
    {
        var kayitlar = await CalisanGorusmeSorgusu(filtre with { GorevliId = id }).ToListAsync();
        return Ok(kayitlar);
    }

    private IQueryable<CalisanGorusmeSatiri> CalisanGorusmeSorgusu(CalisanRaporFiltresi f) =>
        SuzulmusGorusmeler(f)
            .OrderByDescending(g => g.Tarih)
            .Select(g => new CalisanGorusmeSatiri(
                g.Id, g.GorevliId, g.Gorevli!.AdSoyad,
                g.IkinciGorevli != null ? g.IkinciGorevli.AdSoyad : null,
                g.EsnafId, g.Esnaf!.AdSoyad, g.Esnaf.Isletme,
                g.Esnaf.Grup != null ? g.Esnaf.Grup.Ad : null,
                g.Esnaf.Grup != null ? g.Esnaf.Grup.No : null,
                g.Esnaf.Ilce, g.Esnaf.Telefon,
                g.Sira, g.Tarih, g.Sonuc, g.TakipGerekli, g.Not));

    private record CalisanGorusmeSatiri(
        int Id, int GorevliId, string Gorevli, string? IkinciGorevli,
        int EsnafId, string Esnaf, string Isletme, string? Grup, int? GrupNo,
        string? Ilce, string? Telefon, int Sira, DateTime Tarih, string Sonuc, bool TakipGerekli, string? Not);

    [HttpGet("calisanlar/disa-aktar")]
    public async Task<IActionResult> CalisanRaporuIndir([FromQuery] CalisanRaporFiltresi filtre)
    {
        var ozet = await CalisanOzeti(filtre);
        var detay = await CalisanGorusmeSorgusu(filtre).ToListAsync();

        var dosya = ExcelServisi.OlusturCok(
            ("Çalışan Özeti",
                ["Çalışan", "Görev", "Birim", "Rol", "Durum", "Görüşme", "Görüşülen Üye", "Onay Verdi",
                 "Onay Vermedi", "Kararsız", "Takip Edilecek", "Gelmeyecek",
                 "Onay Alınan Üye", "İkinci Kişi Katılımı", "Onay Oranı (%)", "Son Görüşme"],
                ozet.Select(s => new object?[]
                {
                    s.AdSoyad, s.Gorev, s.Birim, s.Rol, s.Durum, s.Gorusme, s.UyeSayisi, s.OnayVerdi,
                    s.OnayVermedi, s.Kararsiz, s.TakipEdilecek, s.Gelmeyecek,
                    s.OnayliUyeSayisi, s.IkinciKatilim, s.OnayOrani, s.SonGorusme,
                })),
            ("Görüşme Detayı",
                // Ayrı "Takip" sütunu yok: takip bilgisi artık "Sonuç" seçeneklerinin içinde.
                // Eski kayıtlarda TakipGerekli sonuçtan bağımsız işaretlenmiş olabiliyor ve iki
                // sütun yan yana çelişkili görünürdü; kolon geçmişi bozmamak için silinmedi.
                ["Görüşen Çalışan", "Görüşecek Kişi", "Üye Yetkilisi", "Unvan", "Meslek Grubu No", "Meslek Grubu",
                 "İlçe", "Telefon", "Kaçıncı Görüşme", "Tarih", "Sonuç", "Not"],
                detay.Select(g => new object?[]
                {
                    g.Gorevli, g.IkinciGorevli, g.Esnaf, g.Isletme, g.GrupNo, g.Grup,
                    g.Ilce, g.Telefon, g.Sira, g.Tarih, g.Sonuc, g.Not,
                })));

        return File(dosya, ExcelServisi.IcerikTipi, $"calisan-raporu-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }
}
