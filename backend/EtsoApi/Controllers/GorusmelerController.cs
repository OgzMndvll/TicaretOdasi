using EtsoApi.Data;
using EtsoApi.Models;
using EtsoApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EtsoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GorusmelerController(EtsoDbContext db, CanliBildirim canli) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listele(
        [FromQuery] int? esnafId, [FromQuery] int? gorevliId, [FromQuery] int? grupId, [FromQuery] string? sonuc,
        [FromQuery] bool? takipGerekli, [FromQuery] DateTime? baslangic, [FromQuery] DateTime? bitis,
        [FromQuery] int sayfa = 1, [FromQuery] int sayfaBoyutu = 20)
    {
        var sorgu = db.Gorusmeler.AsNoTracking();
        if (esnafId is not null) sorgu = sorgu.Where(g => g.EsnafId == esnafId);
        // Yalnızca birincil görevli: üye listesi ve çalışan raporu da görüşmeyi ona sayar,
        // üç uç farklı sayı vermemeli.
        if (gorevliId is not null) sorgu = sorgu.Where(g => g.GorevliId == gorevliId);
        if (grupId is not null) sorgu = sorgu.Where(g => g.Esnaf!.GrupId == grupId);
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
                g.Id, g.Tarih, g.Sonuc, g.Not, g.TakipGerekli, g.Sira,
                g.EsnafId,
                Esnaf = g.Esnaf!.AdSoyad,
                Isletme = g.Esnaf.Isletme,
                Grup = g.Esnaf.Grup != null ? g.Esnaf.Grup.Ad : null,
                GrupNo = g.Esnaf.Grup != null ? g.Esnaf.Grup.No : null,
                Ilce = g.Esnaf.Ilce,
                Mahalle = g.Esnaf.Mahalle,
                Telefon = g.Esnaf.Telefon,
                EsnafDurum = g.Esnaf.Durum,
                g.GorevliId,
                Gorevli = g.Gorevli!.AdSoyad,
                g.IkinciGorevliId,
                IkinciGorevli = g.IkinciGorevli != null ? g.IkinciGorevli.AdSoyad : null,
            })
            .ToListAsync();

        return Ok(new { toplam, sayfa, sayfaBoyutu, kayitlar });
    }

    [HttpGet("istatistik")]
    public async Task<IActionResult> Istatistik()
    {
        var kaynak = db.Gorusmeler.AsNoTracking();

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

        // Ada göre gruplamak, aynı adı taşıyan iki çalışanı tek satırda birleştirirdi; anahtar Id'dir.
        var gorevliPerformans = await kaynak
            .GroupBy(g => new { g.GorevliId, g.Gorevli!.AdSoyad })
            .Select(g => new { g.Key.GorevliId, adSoyad = g.Key.AdSoyad, adet = g.Count() })
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
        var gorevliId = dto.GorevliId;

        var esnaf = await db.Esnaflar.FindAsync(dto.EsnafId);
        if (esnaf is null) return BadRequest(new { mesaj = "Üye bulunamadı." });
        if (!await db.Kullanicilar.AnyAsync(k => k.Id == gorevliId))
            return BadRequest(new { mesaj = "Görevli bulunamadı." });

        var ikinciHata = await IkinciGorevliyiDogrula(dto.IkinciGorevliId, gorevliId);
        if (ikinciHata is not null) return BadRequest(new { mesaj = ikinciHata });

        // Kaçıncı görüşme olduğu formda seçilebilir; seçilmezse sıradaki numara verilir.
        // Üst sınır mevcut görüşme sayısının bir fazlasıdır: numara atlanarak boşluk bırakılamaz.
        var mevcutSayi = await db.Gorusmeler.CountAsync(g => g.EsnafId == dto.EsnafId);
        var sira = dto.Sira ?? mevcutSayi + 1;
        if (sira < 1 || sira > mevcutSayi + 1)
            return BadRequest(new { mesaj = $"Görüşme sırası 1 ile {mevcutSayi + 1} arasında olmalıdır." });

        var gorusme = new Gorusme
        {
            EsnafId = dto.EsnafId,
            GorevliId = gorevliId,
            IkinciGorevliId = dto.IkinciGorevliId,
            Sira = sira,
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

        await db.SaveChangesAsync();
        // Kayıt değişti: bağlı paneller listeyi kendiliğinden tazeler (bkz. Services/CanliBildirim.cs).
        await canli.DegistiAsync("gorusme", gorusme.Id);
        return CreatedAtAction(nameof(Listele), new { esnafId = gorusme.EsnafId }, new { gorusme.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Guncelle(int id, GorusmeYazDto dto)
    {
        var gorusme = await db.Gorusmeler.FindAsync(id);
        if (gorusme is null) return NotFound();

        if (!await db.Kullanicilar.AnyAsync(k => k.Id == dto.GorevliId))
            return BadRequest(new { mesaj = "Görevli bulunamadı." });

        var ikinciHata = await IkinciGorevliyiDogrula(dto.IkinciGorevliId, dto.GorevliId);
        if (ikinciHata is not null) return BadRequest(new { mesaj = ikinciHata });

        if (dto.Sira is not null)
        {
            var esnafGorusmeSayisi = await db.Gorusmeler.CountAsync(g => g.EsnafId == gorusme.EsnafId);
            if (dto.Sira < 1 || dto.Sira > esnafGorusmeSayisi)
                return BadRequest(new { mesaj = $"Görüşme sırası 1 ile {esnafGorusmeSayisi} arasında olmalıdır." });
            gorusme.Sira = dto.Sira.Value;
        }

        gorusme.GorevliId = dto.GorevliId;
        gorusme.IkinciGorevliId = dto.IkinciGorevliId;
        if (dto.Tarih != default) gorusme.Tarih = dto.Tarih;
        if (!string.IsNullOrWhiteSpace(dto.Sonuc)) gorusme.Sonuc = dto.Sonuc;
        gorusme.Not = dto.Not;
        gorusme.TakipGerekli = dto.TakipGerekli;
        await db.SaveChangesAsync();
        await EsnafDurumunuEsitle(gorusme.EsnafId);
        await canli.DegistiAsync("gorusme", gorusme.Id);
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
        await canli.DegistiAsync("gorusme", id);
        return NoContent();
    }

    /// <summary>İkinci çalışan doğrulaması. Hata varsa mesajı, yoksa null döner.</summary>
    private async Task<string?> IkinciGorevliyiDogrula(int? ikinciGorevliId, int gorevliId)
    {
        if (ikinciGorevliId is null) return null;
        if (ikinciGorevliId == gorevliId) return "Görüşecek kişi, görüşen çalışanla aynı olamaz.";
        return await db.Kullanicilar.AnyAsync(k => k.Id == ikinciGorevliId)
            ? null : "Görüşecek kişi bulunamadı.";
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
