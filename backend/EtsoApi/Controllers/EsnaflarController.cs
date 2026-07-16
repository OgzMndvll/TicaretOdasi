using EtsoApi.Data;
using EtsoApi.Models;
using EtsoApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EtsoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EsnaflarController(EtsoDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listele(
        [FromQuery] string? durum, [FromQuery] int? grupId, [FromQuery] int? gorevliId,
        [FromQuery] string? ilce, [FromQuery] string? arama, [FromQuery] string? sirala,
        [FromQuery] bool gorevlendirilmis = false,
        [FromQuery] int sayfa = 1, [FromQuery] int sayfaBoyutu = 20)
    {
        var sorgu = db.Esnaflar.AsNoTracking();

        // Görevli rolü esnaf rehberinin tamamını göremez; yalnızca kendi görevlendirmelerindekileri görür.
        if (!User.Yonetici()) gorevlendirilmis = true;

        // gorevlendirilmis=true → yalnızca oturumdaki kullanıcıya atanmış aktif üyeler
        if (gorevlendirilmis)
        {
            var benimId = User.KullaniciId();
            sorgu = sorgu.Where(e => db.Gorevlendirmeler.Any(g =>
                g.EsnafId == e.Id && g.GorevliId == benimId && g.Durum == "Aktif"));
        }

        if (!string.IsNullOrWhiteSpace(durum)) sorgu = sorgu.Where(e => e.Durum == durum);
        if (grupId is not null) sorgu = sorgu.Where(e => e.GrupId == grupId);
        if (gorevliId is not null) sorgu = sorgu.Where(e => e.GorevliId == gorevliId);
        if (!string.IsNullOrWhiteSpace(ilce)) sorgu = sorgu.Where(e => e.Ilce == ilce);
        if (!string.IsNullOrWhiteSpace(arama))
            sorgu = sorgu.Where(e => e.AdSoyad.Contains(arama) || e.Isletme.Contains(arama) || (e.Telefon != null && e.Telefon.Contains(arama)));

        var toplam = await sorgu.CountAsync();
        sayfaBoyutu = Math.Clamp(sayfaBoyutu, 1, 100);
        sayfa = Math.Max(sayfa, 1);

        // sirala=ad → alfabetik (A-Z); varsayılan: en son görüşülen üstte
        var sirali = sirala == "ad"
            ? sorgu.OrderBy(e => e.AdSoyad).ThenBy(e => e.Isletme)
            : sorgu.OrderByDescending(e => e.SonGorusmeTarihi ?? DateTime.MinValue)
                .ThenByDescending(e => e.KayitTarihi);

        var kayitlar = await sirali
            .Skip((sayfa - 1) * sayfaBoyutu)
            .Take(sayfaBoyutu)
            .Select(e => new
            {
                e.Id, e.AdSoyad, e.Isletme, e.VergiNo,
                e.GrupId, Grup = e.Grup != null ? e.Grup.Ad : null,
                e.Il, e.Ilce, e.Mahalle, e.Adres, e.Telefon,
                e.GorevliId, Gorevli = e.Gorevli != null ? e.Gorevli.AdSoyad : null,
                e.Durum, e.SonGorusmeTarihi, e.KayitTarihi,
            })
            .ToListAsync();

        return Ok(new { toplam, sayfa, sayfaBoyutu, kayitlar });
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpGet("istatistik")]
    public async Task<IActionResult> Istatistik()
    {
        var gruplu = await db.Esnaflar.GroupBy(e => e.Durum)
            .Select(g => new { Durum = g.Key, Adet = g.Count() }).ToListAsync();
        var toplam = gruplu.Sum(g => g.Adet);
        int Say(string durum) => gruplu.FirstOrDefault(g => g.Durum == durum)?.Adet ?? 0;

        return Ok(new
        {
            toplam,
            onayVeren = Say("Onay Verdi"),
            onayVermeyen = Say("Onay Vermedi"),
            kararsiz = Say("Kararsız"),
            gorusulmemis = Say("Görüşülmedi"),
            gorusulen = toplam - Say("Görüşülmedi"),
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Getir(int id)
    {
        // Görevli, yalnızca kendisine görevlendirilmiş esnafın detayını görebilir.
        if (!User.Yonetici() && !await db.Gorevlendirmeler.AnyAsync(g =>
                g.EsnafId == id && g.GorevliId == User.KullaniciId()))
            return Forbid();

        var esnaf = await db.Esnaflar.AsNoTracking()
            .Include(e => e.Grup).Include(e => e.Gorevli)
            .Include(e => e.Gorusmeler.OrderByDescending(g => g.Tarih)).ThenInclude(g => g.Gorevli)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (esnaf is null) return NotFound();

        return Ok(new
        {
            esnaf.Id, esnaf.AdSoyad, esnaf.Isletme, esnaf.VergiNo,
            esnaf.GrupId, Grup = esnaf.Grup?.Ad,
            esnaf.Il, esnaf.Ilce, esnaf.Mahalle, esnaf.Adres, esnaf.Telefon,
            esnaf.GorevliId, Gorevli = esnaf.Gorevli?.AdSoyad,
            esnaf.Durum, esnaf.SonGorusmeTarihi, esnaf.KayitTarihi,
            Gorusmeler = esnaf.Gorusmeler.Select(g => new
            {
                g.Id, g.Tarih, g.Sonuc, g.Not, g.TakipGerekli,
                Gorevli = g.Gorevli?.AdSoyad,
            }),
        });
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpPost]
    public async Task<IActionResult> Olustur(EsnafYazDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.AdSoyad) || string.IsNullOrWhiteSpace(dto.Isletme))
            return BadRequest(new { mesaj = "Ad soyad ve işletme adı zorunludur." });

        var esnaf = new Esnaf
        {
            AdSoyad = dto.AdSoyad.Trim(),
            Isletme = dto.Isletme.Trim(),
            VergiNo = dto.VergiNo,
            GrupId = dto.GrupId,
            Il = dto.Il,
            Ilce = dto.Ilce,
            Mahalle = dto.Mahalle,
            Adres = dto.Adres,
            Telefon = dto.Telefon,
            GorevliId = dto.GorevliId,
            Durum = string.IsNullOrWhiteSpace(dto.Durum) ? "Görüşülmedi" : dto.Durum,
        };
        db.Esnaflar.Add(esnaf);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Getir), new { id = esnaf.Id }, new { esnaf.Id });
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Guncelle(int id, EsnafYazDto dto)
    {
        var esnaf = await db.Esnaflar.FindAsync(id);
        if (esnaf is null) return NotFound();

        esnaf.AdSoyad = dto.AdSoyad.Trim();
        esnaf.Isletme = dto.Isletme.Trim();
        esnaf.VergiNo = dto.VergiNo;
        esnaf.GrupId = dto.GrupId;
        esnaf.Il = dto.Il;
        esnaf.Ilce = dto.Ilce;
        esnaf.Mahalle = dto.Mahalle;
        esnaf.Adres = dto.Adres;
        esnaf.Telefon = dto.Telefon;
        esnaf.GorevliId = dto.GorevliId;
        if (!string.IsNullOrWhiteSpace(dto.Durum)) esnaf.Durum = dto.Durum;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Sil(int id)
    {
        var esnaf = await db.Esnaflar.FindAsync(id);
        if (esnaf is null) return NotFound();
        db.Esnaflar.Remove(esnaf);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static readonly string[] ExcelBasliklari =
        ["Ad Soyad", "İşletme", "Telefon", "Grup", "Görevli", "İl", "İlçe", "Mahalle", "Adres", "Vergi No", "Durum"];

    private static readonly string[] GecerliDurumlar = ["Onay Verdi", "Onay Vermedi", "Kararsız", "Görüşülmedi"];

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpGet("sablon")]
    public IActionResult Sablon()
    {
        var ornek = new object?[][]
        {
            ["Örnek ÜYE", "Örnek Ticaret", "0532 000 00 00", "Otomotiv", "Ahmet Yılmaz", "Erzurum", "Yakutiye", "Lalapaşa", "Örnek Mah. Örnek Cad. No: 1", "1234567890", "Görüşülmedi"],
        };
        var dosya = ExcelServisi.Olustur("Üyeler", ExcelBasliklari, ornek);
        return File(dosya, ExcelServisi.IcerikTipi, "uye-ice-aktarma-sablonu.xlsx");
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpGet("disa-aktar")]
    public async Task<IActionResult> DisaAktar([FromQuery] string? durum, [FromQuery] int? grupId, [FromQuery] int? gorevliId, [FromQuery] string? ilce, [FromQuery] string? arama)
    {
        var sorgu = db.Esnaflar.AsNoTracking().Include(e => e.Grup).Include(e => e.Gorevli).AsQueryable();
        if (!string.IsNullOrWhiteSpace(durum)) sorgu = sorgu.Where(e => e.Durum == durum);
        if (grupId is not null) sorgu = sorgu.Where(e => e.GrupId == grupId);
        if (gorevliId is not null) sorgu = sorgu.Where(e => e.GorevliId == gorevliId);
        if (!string.IsNullOrWhiteSpace(ilce)) sorgu = sorgu.Where(e => e.Ilce == ilce);
        if (!string.IsNullOrWhiteSpace(arama))
            sorgu = sorgu.Where(e => e.AdSoyad.Contains(arama) || e.Isletme.Contains(arama) || (e.Telefon != null && e.Telefon.Contains(arama)));

        var kayitlar = await sorgu.OrderBy(e => e.AdSoyad).ToListAsync();
        var satirlar = kayitlar.Select(e => new object?[]
        {
            e.AdSoyad, e.Isletme, e.Telefon, e.Grup?.Ad, e.Gorevli?.AdSoyad, e.Il, e.Ilce, e.Mahalle, e.Adres, e.VergiNo, e.Durum,
        });
        var dosya = ExcelServisi.Olustur("Üyeler", ExcelBasliklari, satirlar);
        return File(dosya, ExcelServisi.IcerikTipi, $"uyeler-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpPost("ice-aktar")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> IceAktar(IFormFile? dosya)
    {
        if (dosya is null || dosya.Length == 0)
            return BadRequest(new { mesaj = "Excel dosyası (.xlsx) yükleyin." });
        if (!dosya.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { mesaj = "Yalnızca .xlsx uzantılı dosyalar destekleniyor." });

        List<(int SatirNo, Dictionary<string, string> Degerler)> satirlar;
        try
        {
            await using var akis = dosya.OpenReadStream();
            satirlar = ExcelServisi.Oku(akis);
        }
        catch
        {
            return BadRequest(new { mesaj = "Dosya okunamadı. Geçerli bir Excel (.xlsx) dosyası olduğundan emin olun." });
        }
        if (satirlar.Count == 0)
            return BadRequest(new { mesaj = "Dosyada veri satırı bulunamadı. Şablonu indirip doldurun." });

        var gruplar = await db.Gruplar.ToListAsync();
        var gorevliler = await db.Kullanicilar.ToListAsync();
        var mevcutlar = (await db.Esnaflar.Select(e => new { e.AdSoyad, e.Isletme }).ToListAsync())
            .Select(e => $"{e.AdSoyad}|{e.Isletme}".ToLowerInvariant()).ToHashSet();

        var hatalar = new List<string>();
        var eklenen = 0;
        var atlanan = 0;

        string? Al(Dictionary<string, string> d, params string[] basliklar)
        {
            foreach (var baslik in basliklar)
                if (d.TryGetValue(ExcelServisi.Normalize(baslik), out var deger) && !string.IsNullOrWhiteSpace(deger))
                    return deger.Trim();
            return null;
        }

        static string? AdrestenIlce(string? adres)
        {
            if (string.IsNullOrWhiteSpace(adres)) return null;
            string[] ilceler =
            [
                "Yakutiye", "Palandöken", "Aziziye", "Aşkale", "Çat", "Hınıs", "Horasan", "İspir",
                "Karaçoban", "Karayazı", "Köprüköy", "Narman", "Oltu", "Olur", "Pasinler",
                "Pazaryolu", "Şenkaya", "Tekman", "Tortum", "Uzundere"
            ];
            return ilceler.FirstOrDefault(ilce => adres.Contains(ilce, StringComparison.CurrentCultureIgnoreCase));
        }

        static string? AdrestenMahalle(string? adres)
        {
            if (string.IsNullOrWhiteSpace(adres)) return null;
            var eslesme = System.Text.RegularExpressions.Regex.Match(adres,
                @"(?:^|\s)([\p{L}\s]+?)\s+MAHALLES[İI]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!eslesme.Success) return null;
            var mahalle = eslesme.Groups[1].Value.Trim();
            var sonAyirac = mahalle.LastIndexOfAny(['/', ',']);
            return (sonAyirac >= 0 ? mahalle[(sonAyirac + 1)..] : mahalle).Trim();
        }

        // "YUSUF KARATAŞ , ÖMER KARATAŞ" gibi çoklu yetkili listelerinden ilk kişiyi alır.
        static string? IlkYetkili(string? ham) =>
            string.IsNullOrWhiteSpace(ham) ? null : ham.Split(',', ';')[0].Trim();

        static string? BaslikYap(string? metin)
        {
            if (string.IsNullOrWhiteSpace(metin)) return metin;
            var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
            return tr.TextInfo.ToTitleCase(metin.ToLower(tr));
        }

        // "4422341515,4422349750" → ilk numara; 10-11 haneliler "0442 234 15 15" biçimine getirilir.
        static string? TelefonDuzenle(string? ham)
        {
            if (string.IsNullOrWhiteSpace(ham)) return null;
            var ilk = ham.Split(',', ';')[0].Trim();
            if (ilk.Length == 0) return null;
            var rakamlar = new string(ilk.Where(char.IsDigit).ToArray());
            if (rakamlar.StartsWith("90") && rakamlar.Length == 12) rakamlar = rakamlar[2..];
            if (rakamlar.Length == 10) rakamlar = "0" + rakamlar;
            if (rakamlar.Length == 11 && rakamlar[0] == '0')
                return $"{rakamlar[..4]} {rakamlar[4..7]} {rakamlar[7..9]} {rakamlar[9..]}";
            return ilk.Length <= 30 ? ilk : ilk[..30];
        }

        static string? Kirp(string? metin, int enFazla) =>
            metin is null ? null : (metin.Length <= enFazla ? metin : metin[..enFazla]);

        foreach (var (satirNo, degerler) in satirlar)
        {
            var isletme = Kirp(Al(degerler, "İşletme", "Unvan", "Ünvan", "Firma Unvanı", "Ticaret Unvanı"), 160);
            var adSoyad = BaslikYap(IlkYetkili(Al(degerler, "Ad Soyad", "Yetkili Adı Soyadı", "Yetkili", "İsim Soyisim")));
            if (isletme is null)
            {
                hatalar.Add($"Satır {satirNo}: İşletme/unvan bilgisi bulunamadı.");
                continue;
            }
            // Bazı oda raporlarında yetkili alanı boş bırakılır. Kaydı kaybetmemek için unvanı kullanırız.
            adSoyad = Kirp(adSoyad ?? isletme, 120)!;
            if (!mevcutlar.Add($"{adSoyad}|{isletme}".ToLowerInvariant()))
            {
                atlanan++;
                continue;
            }

            var grupAdi = Al(degerler, "Grup", "Meslek Grubu", "Meslek Komitesi");
            Grup? grup = null;
            if (grupAdi is not null)
            {
                grup = gruplar.FirstOrDefault(g => string.Equals(g.Ad, grupAdi, StringComparison.OrdinalIgnoreCase));
                if (grup is null)
                {
                    grup = new Grup { Ad = grupAdi, Aciklama = "Excel içe aktarmayla oluşturuldu" };
                    db.Gruplar.Add(grup);
                    gruplar.Add(grup);
                }
            }

            var gorevliAdi = Al(degerler, "Görevli", "Atanan Görevli");
            Kullanici? gorevli = null;
            if (gorevliAdi is not null)
            {
                gorevli = gorevliler.FirstOrDefault(k => string.Equals(k.AdSoyad, gorevliAdi, StringComparison.OrdinalIgnoreCase));
                if (gorevli is null) hatalar.Add($"Satır {satirNo}: '{gorevliAdi}' adlı görevli bulunamadı, görevli boş bırakıldı.");
            }

            var durum = Al(degerler, "Durum", "Onay Durumu", "Görüşme Durumu") ?? "Görüşülmedi";
            if (!GecerliDurumlar.Contains(durum))
            {
                hatalar.Add($"Satır {satirNo}: '{durum}' geçersiz durum, 'Görüşülmedi' olarak kaydedildi.");
                durum = "Görüşülmedi";
            }

            var adres = Al(degerler, "Adres", "Tescil Adresi");
            var telefon = TelefonDuzenle(Al(degerler, "Telefon", "Cep Telefonu (GSM)", "Cep Telefonu", "GSM", "İş Telefonu"));
            db.Esnaflar.Add(new Esnaf
            {
                AdSoyad = adSoyad,
                Isletme = isletme,
                Telefon = telefon,
                Grup = grup,
                Gorevli = gorevli,
                Il = Al(degerler, "İl") ?? "Erzurum",
                Ilce = Al(degerler, "İlçe") ?? AdrestenIlce(adres),
                Mahalle = Kirp(BaslikYap(Al(degerler, "Mahalle") ?? AdrestenMahalle(adres)), 80),
                Adres = Kirp(adres, 500),
                VergiNo = Kirp(Al(degerler, "Vergi No", "Vergi Numarası"), 20),
                Durum = durum,
            });
            eklenen++;
        }

        await db.SaveChangesAsync();
        return Ok(new IceAktarmaSonucu(eklenen, atlanan, hatalar));
    }
}
