using EtsoApi.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EtsoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
public class DashboardController(EtsoDbContext db) : ControllerBase
{
    private const int VarsayilanGunSayisi = 30;
    private const int EnFazlaGunSayisi = 366;

    /// <summary>
    /// Panel özeti. Meslek grubu ve tarih aralığı süzgeçleri kartlara, grafiklere ve son görüşmeler
    /// listesine birlikte uygulanır; aksi halde kartlarla grafik birbiriyle çelişirdi.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Ozet(
        [FromQuery] int? grupId, [FromQuery] DateTime? baslangic, [FromQuery] DateTime? bitis)
    {
        // Tarih aralığı: verilmezse son 30 gün. Aralık, sunucuyu koruyacak şekilde sınırlandırılır.
        var bugun = DateTime.UtcNow.Date;
        var bitisGunu = (bitis?.Date ?? bugun).AddDays(1);
        var baslangicGunu = baslangic?.Date ?? bitisGunu.AddDays(-VarsayilanGunSayisi);
        if (baslangicGunu >= bitisGunu) baslangicGunu = bitisGunu.AddDays(-1);
        if ((bitisGunu - baslangicGunu).TotalDays > EnFazlaGunSayisi)
            baslangicGunu = bitisGunu.AddDays(-EnFazlaGunSayisi);

        var uyeler = db.Esnaflar.AsNoTracking();
        var gorusmeler = db.Gorusmeler.AsNoTracking();
        if (grupId is not null)
        {
            uyeler = uyeler.Where(e => e.GrupId == grupId);
            gorusmeler = gorusmeler.Where(g => g.Esnaf!.GrupId == grupId);
        }

        // Üye kartları üyenin güncel onay durumunu gösterir; tarih aralığından etkilenmez.
        var durumlar = await uyeler.GroupBy(e => e.Durum)
            .Select(g => new { Durum = g.Key, Adet = g.Count() }).ToListAsync();
        int Say(string durum) => durumlar.FirstOrDefault(g => g.Durum == durum)?.Adet ?? 0;

        var toplamEsnaf = durumlar.Sum(g => g.Adet);
        var gorusulmemis = Say("Görüşülmedi");

        var aralik = gorusmeler.Where(g => g.Tarih >= baslangicGunu && g.Tarih < bitisGunu);

        var gunlukHam = await aralik
            .GroupBy(g => g.Tarih.Date)
            .Select(g => new { Gun = g.Key, Adet = g.Count() })
            .ToListAsync();

        // Kayıt olmayan günler de seriye 0 olarak girer; aksi halde grafikte boşluklar oluşur.
        var gunSayisi = (int)(bitisGunu - baslangicGunu).TotalDays;
        var gunlukGorusmeler = Enumerable.Range(0, gunSayisi)
            .Select(i => baslangicGunu.AddDays(i))
            .Select(gun => new
            {
                tarih = gun.ToString("yyyy-MM-dd"),
                ay = gun.ToString("dd.MM"),
                adet = gunlukHam.FirstOrDefault(h => h.Gun == gun)?.Adet ?? 0,
            })
            .ToList();

        // Ada göre gruplamak, aynı adı taşıyan iki çalışanı tek satırda birleştirirdi; anahtar Id'dir.
        var gorevliPerformans = await aralik
            .GroupBy(g => new { g.GorevliId, g.Gorevli!.AdSoyad })
            .Select(g => new { g.Key.GorevliId, adSoyad = g.Key.AdSoyad, adet = g.Count() })
            .OrderByDescending(g => g.adet)
            .Take(5)
            .ToListAsync();

        var sonGorusmeler = await aralik
            .OrderByDescending(g => g.Tarih)
            .Take(8)
            .Select(g => new
            {
                g.Id, g.Tarih, g.Sonuc, g.Sira,
                Esnaf = g.Esnaf!.AdSoyad,
                Isletme = g.Esnaf.Isletme,
                Grup = g.Esnaf.Grup != null ? g.Esnaf.Grup.Ad : null,
                GrupNo = g.Esnaf.Grup != null ? g.Esnaf.Grup.No : null,
                EsnafDurum = g.Esnaf.Durum,
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
            takipEdilecek = Say("Takip Edilecek"),
            gelmeyecek = Say("Gelmeyecek"),
            gorusulmemis,
            aralikGorusme = gunlukHam.Sum(g => g.Adet),
            baslangic = baslangicGunu.ToString("yyyy-MM-dd"),
            bitis = bitisGunu.AddDays(-1).ToString("yyyy-MM-dd"),
            gunlukGorusmeler,
            gorevliPerformans,
            sonGorusmeler,
        });
    }
}
