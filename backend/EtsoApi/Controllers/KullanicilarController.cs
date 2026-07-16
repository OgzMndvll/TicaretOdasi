using EtsoApi.Data;
using EtsoApi.Models;
using EtsoApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EtsoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KullanicilarController(EtsoDbContext db) : ControllerBase
{
    private static readonly string[] GecerliRoller = ["Yönetici", "Görevli"];
    private static readonly string[] GecerliDurumlar = ["Aktif", "Pasif"];

    /// <summary>Sistemde en az bir aktif yönetici kalmasını güvence altına alır (kendini dışarı kilitleme koruması).</summary>
    private async Task<bool> BaskaAktifYoneticiVarMi(int haricId) =>
        await db.Kullanicilar.AnyAsync(k => k.Id != haricId && k.Rol == "Yönetici" && k.Durum == "Aktif");

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpGet]
    public async Task<IActionResult> Listele([FromQuery] string? rol, [FromQuery] string? durum)
    {
        var sorgu = db.Kullanicilar.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(rol)) sorgu = sorgu.Where(k => k.Rol == rol);
        if (!string.IsNullOrWhiteSpace(durum)) sorgu = sorgu.Where(k => k.Durum == durum);
        return Ok(await sorgu.OrderBy(k => k.AdSoyad).ToListAsync());
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpGet("istatistik")]
    public async Task<IActionResult> Istatistik()
    {
        var toplam = await db.Kullanicilar.CountAsync();
        var aktif = await db.Kullanicilar.CountAsync(k => k.Durum == "Aktif");
        var yonetici = await db.Kullanicilar.CountAsync(k => k.Rol == "Yönetici");
        return Ok(new { toplam, aktif, pasif = toplam - aktif, yonetici, gorevli = toplam - yonetici });
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Getir(int id)
    {
        var kullanici = await db.Kullanicilar.FindAsync(id);
        return kullanici is null ? NotFound() : Ok(kullanici);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpPost]
    public async Task<IActionResult> Olustur(KullaniciYazDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.AdSoyad)) return BadRequest(new { mesaj = "Ad soyad zorunludur." });

        var kullaniciAdi = string.IsNullOrWhiteSpace(dto.KullaniciAdi)
            ? UretKullaniciAdi(dto.AdSoyad)
            : dto.KullaniciAdi.Trim().ToLowerInvariant();
        if (await db.Kullanicilar.AnyAsync(k => k.KullaniciAdi == kullaniciAdi))
            return Conflict(new { mesaj = "Bu kullanıcı adı zaten kullanımda." });

        if (string.IsNullOrWhiteSpace(dto.Sifre))
            return BadRequest(new { mesaj = "Geçici şifre zorunludur." });
        var politikaHatasi = AuthController.SifrePolitikasiHatasi(dto.Sifre);
        if (politikaHatasi is not null) return BadRequest(new { mesaj = politikaHatasi });

        // Rol ve durum yalnızca bilinen değerlerden olabilir; serbest metin kabul edilmez.
        var rol = string.IsNullOrWhiteSpace(dto.Rol) ? "Görevli" : dto.Rol;
        if (!GecerliRoller.Contains(rol))
            return BadRequest(new { mesaj = "Geçersiz rol. 'Yönetici' veya 'Görevli' olmalıdır." });
        var durum = string.IsNullOrWhiteSpace(dto.Durum) ? "Aktif" : dto.Durum;
        if (!GecerliDurumlar.Contains(durum))
            return BadRequest(new { mesaj = "Geçersiz durum. 'Aktif' veya 'Pasif' olmalıdır." });

        var kullanici = new Kullanici
        {
            AdSoyad = dto.AdSoyad.Trim(),
            KullaniciAdi = kullaniciAdi,
            Rol = rol,
            Gorev = dto.Gorev,
            Birim = dto.Birim,
            Eposta = dto.Eposta,
            Telefon = dto.Telefon,
            Durum = durum,
        };
        kullanici.SifreHash = AuthController.Hashle(kullanici, dto.Sifre);
        db.Kullanicilar.Add(kullanici);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Getir), new { id = kullanici.Id }, new { kullanici.Id, kullanici.KullaniciAdi });
    }

    /// <summary>Yöneticinin bir kullanıcının şifresini sıfırlaması. Kilidi de açar.</summary>
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpPut("{id:int}/sifre")]
    public async Task<IActionResult> SifreSifirla(int id, SifreSifirlaDto dto)
    {
        var kullanici = await db.Kullanicilar.FindAsync(id);
        if (kullanici is null) return NotFound();

        var politikaHatasi = AuthController.SifrePolitikasiHatasi(dto.YeniSifre);
        if (politikaHatasi is not null) return BadRequest(new { mesaj = politikaHatasi });

        kullanici.SifreHash = AuthController.Hashle(kullanici, dto.YeniSifre);
        kullanici.BasarisizGiris = 0;
        kullanici.KilitBitis = null;
        // Sıfırlama, o kullanıcının açık tüm oturumlarını (ele geçirilmiş olabilecekler dahil) anında düşürür.
        kullanici.SifreGuncelleme = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Guncelle(int id, KullaniciYazDto dto)
    {
        var kullanici = await db.Kullanicilar.FindAsync(id);
        if (kullanici is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(dto.Rol) && !GecerliRoller.Contains(dto.Rol))
            return BadRequest(new { mesaj = "Geçersiz rol. 'Yönetici' veya 'Görevli' olmalıdır." });
        if (!string.IsNullOrWhiteSpace(dto.Durum) && !GecerliDurumlar.Contains(dto.Durum))
            return BadRequest(new { mesaj = "Geçersiz durum. 'Aktif' veya 'Pasif' olmalıdır." });

        // Son aktif yöneticinin yetkisi alınamaz/pasife alınamaz; aksi halde sisteme kimse giremez.
        var yoneticilikBitiyor = kullanici.Rol == "Yönetici" && kullanici.Durum == "Aktif"
            && ((!string.IsNullOrWhiteSpace(dto.Rol) && dto.Rol != "Yönetici")
                || (!string.IsNullOrWhiteSpace(dto.Durum) && dto.Durum != "Aktif"));
        if (yoneticilikBitiyor && !await BaskaAktifYoneticiVarMi(id))
            return Conflict(new { mesaj = "Sistemdeki son aktif yönetici bu kullanıcı; rolünü veya durumunu değiştiremezsiniz. Önce başka bir yönetici tanımlayın." });

        kullanici.AdSoyad = dto.AdSoyad.Trim();
        if (!string.IsNullOrWhiteSpace(dto.KullaniciAdi)) kullanici.KullaniciAdi = dto.KullaniciAdi.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(dto.Rol)) kullanici.Rol = dto.Rol;
        kullanici.Gorev = dto.Gorev;
        kullanici.Birim = dto.Birim;
        kullanici.Eposta = dto.Eposta;
        kullanici.Telefon = dto.Telefon;
        if (!string.IsNullOrWhiteSpace(dto.Durum)) kullanici.Durum = dto.Durum;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Sil(int id)
    {
        var kullanici = await db.Kullanicilar.FindAsync(id);
        if (kullanici is null) return NotFound();
        if (id == User.KullaniciId())
            return Conflict(new { mesaj = "Kendi hesabınızı silemezsiniz." });
        if (kullanici.Rol == "Yönetici" && !await BaskaAktifYoneticiVarMi(id))
            return Conflict(new { mesaj = "Sistemdeki son aktif yönetici silinemez. Önce başka bir yönetici tanımlayın." });
        if (await db.Gorusmeler.AnyAsync(g => g.GorevliId == id) || await db.Gorevlendirmeler.AnyAsync(g => g.GorevliId == id))
            return Conflict(new { mesaj = "Bu kullanıcının görüşme/görevlendirme kayıtları var; silmek yerine pasife alın." });
        db.Kullanicilar.Remove(kullanici);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static readonly string[] ExcelBasliklari =
        ["Ad Soyad", "Kullanıcı Adı", "Rol", "Görev", "Birim", "E-posta", "Telefon", "Durum"];

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpGet("sablon")]
    public IActionResult Sablon()
    {
        var ornek = new object?[][]
        {
            ["Örnek Kullanıcı", "ornek.kullanici", "Görevli", "Üye Temsilcisi", "Saha", "ornek@erzto.org.tr", "0532 000 00 00", "Aktif"],
        };
        var dosya = ExcelServisi.Olustur("Kullanıcılar", ExcelBasliklari, ornek);
        return File(dosya, ExcelServisi.IcerikTipi, "kullanici-ice-aktarma-sablonu.xlsx");
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpGet("disa-aktar")]
    public async Task<IActionResult> DisaAktar()
    {
        var kayitlar = await db.Kullanicilar.AsNoTracking().OrderBy(k => k.AdSoyad).ToListAsync();
        var satirlar = kayitlar.Select(k => new object?[]
        {
            k.AdSoyad, k.KullaniciAdi, k.Rol, k.Gorev, k.Birim, k.Eposta, k.Telefon, k.Durum,
        });
        var dosya = ExcelServisi.Olustur("Kullanıcılar", ExcelBasliklari, satirlar);
        return File(dosya, ExcelServisi.IcerikTipi, $"kullanici-raporu-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
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

        var mevcutlar = (await db.Kullanicilar.Select(k => k.KullaniciAdi).ToListAsync()).ToHashSet();
        var hatalar = new List<string>();
        var eklenen = 0;
        var atlanan = 0;

        string? Al(Dictionary<string, string> d, string baslik) =>
            d.TryGetValue(ExcelServisi.Normalize(baslik), out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

        foreach (var (satirNo, degerler) in satirlar)
        {
            var adSoyad = Al(degerler, "Ad Soyad");
            if (adSoyad is null)
            {
                hatalar.Add($"Satır {satirNo}: 'Ad Soyad' zorunludur.");
                continue;
            }
            var kullaniciAdi = Al(degerler, "Kullanıcı Adı")?.ToLowerInvariant() ?? UretKullaniciAdi(adSoyad);
            if (!mevcutlar.Add(kullaniciAdi))
            {
                atlanan++;
                continue;
            }
            var rol = Al(degerler, "Rol") ?? "Görevli";
            if (rol != "Yönetici" && rol != "Görevli")
            {
                hatalar.Add($"Satır {satirNo}: '{rol}' geçersiz rol, 'Görevli' olarak kaydedildi.");
                rol = "Görevli";
            }
            var durum = Al(degerler, "Durum") ?? "Aktif";
            if (durum != "Aktif" && durum != "Pasif") durum = "Aktif";

            db.Kullanicilar.Add(new Kullanici
            {
                AdSoyad = adSoyad,
                KullaniciAdi = kullaniciAdi,
                Rol = rol,
                Gorev = Al(degerler, "Görev"),
                Birim = Al(degerler, "Birim"),
                Eposta = Al(degerler, "E-posta"),
                Telefon = Al(degerler, "Telefon"),
                Durum = durum,
            });
            eklenen++;
        }

        await db.SaveChangesAsync();
        return Ok(new IceAktarmaSonucu(eklenen, atlanan, hatalar));
    }

    private static string UretKullaniciAdi(string adSoyad)
    {
        var normal = adSoyad.Trim().ToLowerInvariant()
            .Replace("ç", "c").Replace("ğ", "g").Replace("ı", "i")
            .Replace("ö", "o").Replace("ş", "s").Replace("ü", "u");
        return string.Join(".", normal.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
