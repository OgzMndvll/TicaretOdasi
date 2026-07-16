using System.Text;
using System.Text.Json;
using EtsoApi.Data;
using EtsoApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EtsoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
public class SistemController(EtsoDbContext db, IWebHostEnvironment ortam) : ControllerBase
{
    /// <summary>Tüm tabloların tam JSON yedeğini indirir.</summary>
    [HttpGet("yedek")]
    public async Task<IActionResult> Yedek()
    {
        var yedek = new
        {
            olusturmaTarihi = DateTime.UtcNow,
            gruplar = await db.Gruplar.AsNoTracking().ToListAsync(),
            kullanicilar = await db.Kullanicilar.AsNoTracking().ToListAsync(),
            esnaflar = await db.Esnaflar.AsNoTracking().Select(e => new
            {
                e.Id, e.AdSoyad, e.Isletme, e.VergiNo, e.GrupId, e.Ilce, e.Mahalle,
                e.Telefon, e.GorevliId, e.Durum, e.SonGorusmeTarihi, e.KayitTarihi,
            }).ToListAsync(),
            gorusmeler = await db.Gorusmeler.AsNoTracking().Select(g => new
            {
                g.Id, g.EsnafId, g.GorevliId, g.Tarih, g.Sonuc, g.Not, g.TakipGerekli,
            }).ToListAsync(),
            gorevlendirmeler = await db.Gorevlendirmeler.AsNoTracking().Select(g => new
            {
                g.Id, g.GorevliId, g.EsnafId, g.GrupId, g.Tarih, g.Not, g.Durum,
            }).ToListAsync(),
            onaylar = await db.Onaylar.AsNoTracking().Select(o => new
            {
                o.Id, o.EsnafId, o.GorevliId, o.IslemTuru, o.Tarih, o.Durum,
            }).ToListAsync(),
            ayarlar = await db.Ayarlar.AsNoTracking().ToListAsync(),
        };
        var json = JsonSerializer.SerializeToUtf8Bytes(yedek, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        return File(json, "application/json", $"etso-yedek-{DateTime.Now:yyyyMMdd-HHmm}.json");
    }

    /// <summary>Tüm verileri tek Excel dosyasında (her tablo ayrı sayfa) dışa aktarır.</summary>
    [HttpGet("disa-aktar")]
    public async Task<IActionResult> DisaAktar()
    {
        using var kitap = new ClosedXML.Excel.XLWorkbook();

        void SayfaEkle(string ad, string[] basliklar, IEnumerable<object?[]> satirlar)
        {
            var sayfa = kitap.Worksheets.Add(ad);
            for (var i = 0; i < basliklar.Length; i++)
            {
                var hucre = sayfa.Cell(1, i + 1);
                hucre.Value = basliklar[i];
                hucre.Style.Font.Bold = true;
            }
            var satirNo = 2;
            foreach (var satir in satirlar)
            {
                for (var i = 0; i < satir.Length; i++)
                {
                    var deger = satir[i];
                    var hucre = sayfa.Cell(satirNo, i + 1);
                    if (deger is DateTime t) { hucre.Value = t; hucre.Style.DateFormat.Format = "dd.mm.yyyy hh:mm"; }
                    else if (deger is int s) hucre.Value = s;
                    else if (deger is bool b) hucre.Value = b ? "Evet" : "Hayır";
                    else hucre.Value = deger?.ToString() ?? "";
                }
                satirNo++;
            }
            sayfa.Columns().AdjustToContents(1, Math.Min(satirNo, 50));
        }

        var esnaflar = await db.Esnaflar.AsNoTracking().Include(e => e.Grup).Include(e => e.Gorevli).OrderBy(e => e.AdSoyad).ToListAsync();
        SayfaEkle("Üyeler",
            ["Ad Soyad", "İşletme", "Telefon", "Grup", "Görevli", "İlçe", "Mahalle", "Vergi No", "Durum", "Son Görüşme"],
            esnaflar.Select(e => new object?[] { e.AdSoyad, e.Isletme, e.Telefon, e.Grup?.Ad, e.Gorevli?.AdSoyad, e.Ilce, e.Mahalle, e.VergiNo, e.Durum, e.SonGorusmeTarihi }));

        var gorusmeler = await db.Gorusmeler.AsNoTracking().Include(g => g.Esnaf).Include(g => g.Gorevli).OrderByDescending(g => g.Tarih).ToListAsync();
        SayfaEkle("Görüşmeler",
            ["Üye", "Görevli", "Tarih", "Sonuç", "Not", "Takip Gerekli"],
            gorusmeler.Select(g => new object?[] { g.Esnaf?.AdSoyad, g.Gorevli?.AdSoyad, g.Tarih, g.Sonuc, g.Not, g.TakipGerekli }));

        var gorevlendirmeler = await db.Gorevlendirmeler.AsNoTracking().Include(g => g.Esnaf).Include(g => g.Gorevli).Include(g => g.Grup).OrderByDescending(g => g.Tarih).ToListAsync();
        SayfaEkle("Görevlendirmeler",
            ["Görevli", "Üye", "Grup", "Tarih", "Durum", "Not"],
            gorevlendirmeler.Select(g => new object?[] { g.Gorevli?.AdSoyad, g.Esnaf?.AdSoyad, g.Grup?.Ad, g.Tarih, g.Durum, g.Not }));

        var onaylar = await db.Onaylar.AsNoTracking().Include(o => o.Esnaf).Include(o => o.Gorevli).OrderByDescending(o => o.Tarih).ToListAsync();
        SayfaEkle("Onaylar",
            ["Üye", "İşlem Türü", "Görevli", "Tarih", "Durum"],
            onaylar.Select(o => new object?[] { o.Esnaf?.AdSoyad, o.IslemTuru, o.Gorevli?.AdSoyad, o.Tarih, o.Durum }));

        var gruplar = await db.Gruplar.AsNoTracking().OrderBy(g => g.Ad).ToListAsync();
        SayfaEkle("Gruplar",
            ["Ad", "Açıklama", "Tür", "Durum", "Güncelleme"],
            gruplar.Select(g => new object?[] { g.Ad, g.Aciklama, g.Tur, g.Durum, g.GuncellemeTarihi }));

        var kullanicilar = await db.Kullanicilar.AsNoTracking().OrderBy(k => k.AdSoyad).ToListAsync();
        SayfaEkle("Kullanıcılar",
            ["Ad Soyad", "Kullanıcı Adı", "Rol", "Görev", "Birim", "E-posta", "Telefon", "Durum"],
            kullanicilar.Select(k => new object?[] { k.AdSoyad, k.KullaniciAdi, k.Rol, k.Gorev, k.Birim, k.Eposta, k.Telefon, k.Durum }));

        using var akis = new MemoryStream();
        kitap.SaveAs(akis);
        return File(akis.ToArray(), ExcelServisi.IcerikTipi, $"etso-tum-veriler-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }

    /// <summary>İstek günlüğü dosyasını indirir.</summary>
    [HttpGet("loglar")]
    public IActionResult Loglar()
    {
        var yol = Path.Combine(ortam.ContentRootPath, "logs", "istekler.log");
        if (!System.IO.File.Exists(yol))
            return File(Encoding.UTF8.GetBytes("Henüz log kaydı yok.\n"), "text/plain", "sistem-loglari.txt");
        // Dosya API tarafından yazılmaya devam ettiği için paylaşımlı okuma gerekir.
        using var okuyucu = new FileStream(yol, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var bellek = new MemoryStream();
        okuyucu.CopyTo(bellek);
        return File(bellek.ToArray(), "text/plain", $"sistem-loglari-{DateTime.Now:yyyyMMdd-HHmm}.txt");
    }
}
