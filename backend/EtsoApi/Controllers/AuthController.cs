using System.Security.Claims;
using EtsoApi.Data;
using EtsoApi.Models;
using EtsoApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EtsoApi.Controllers;

public record GirisDto(string KullaniciAdi, string Sifre);
public record SifreDegistirDto(string MevcutSifre, string YeniSifre);

[ApiController]
[Route("api/auth")]
public class AuthController(EtsoDbContext db, TokenServisi tokenServisi, ILogger<AuthController> log) : ControllerBase
{
    private const int KilitEsigi = 5;
    private static readonly TimeSpan KilitSuresi = TimeSpan.FromMinutes(10);
    private static readonly PasswordHasher<Kullanici> Hasher = new();

    public static string? SifrePolitikasiHatasi(string sifre)
    {
        if (sifre.Length < 10) return "Şifre en az 10 karakter olmalıdır.";
        if (sifre.Length > 128) return "Şifre en fazla 128 karakter olabilir.";
        if (!sifre.Any(char.IsUpper)) return "Şifre en az bir büyük harf içermelidir.";
        if (!sifre.Any(char.IsLower)) return "Şifre en az bir küçük harf içermelidir.";
        if (!sifre.Any(char.IsDigit)) return "Şifre en az bir rakam içermelidir.";
        return null;
    }

    public static string Hashle(Kullanici kullanici, string sifre) => Hasher.HashPassword(kullanici, sifre);

    [HttpPost("giris")]
    [AllowAnonymous]
    [EnableRateLimiting("giris")]
    public async Task<IActionResult> Giris(GirisDto dto)
    {
        // Kullanıcı adının var olup olmadığı sızdırılmaz: tüm başarısız durumlarda aynı mesaj döner.
        const string genelHata = "Kullanıcı adı veya şifre hatalı.";

        if (string.IsNullOrWhiteSpace(dto.KullaniciAdi) || string.IsNullOrWhiteSpace(dto.Sifre))
            return Unauthorized(new { mesaj = genelHata });

        // Kullanıcı adları ToLowerInvariant ile kaydedilir; aramada da aynısı kullanılmalı
        // (Türkçe kültürde "I" → "ı" olur ve kayıtla eşleşmezdi).
        var kullaniciAdi = dto.KullaniciAdi.Trim().ToLowerInvariant();
        var kullanici = await db.Kullanicilar
            .FirstOrDefaultAsync(k => k.KullaniciAdi == kullaniciAdi);

        if (kullanici is null || kullanici.SifreHash is null)
        {
            // Zamanlama farkıyla kullanıcı varlığının anlaşılmasını zorlaştırmak için sahte doğrulama yapılır.
            Hasher.VerifyHashedPassword(new Kullanici(), Hashle(new Kullanici(), "zamanlama-esitleme"), dto.Sifre);
            log.LogWarning("Başarısız giriş denemesi (bilinmeyen kullanıcı): {KullaniciAdi}", dto.KullaniciAdi);
            return Unauthorized(new { mesaj = genelHata });
        }

        if (kullanici.KilitBitis is not null && kullanici.KilitBitis > DateTime.UtcNow)
        {
            var kalan = (int)Math.Ceiling((kullanici.KilitBitis.Value - DateTime.UtcNow).TotalMinutes);
            return StatusCode(423, new { mesaj = $"Hesap çok sayıda hatalı deneme nedeniyle kilitlendi. {kalan} dakika sonra tekrar deneyin." });
        }

        var sonuc = Hasher.VerifyHashedPassword(kullanici, kullanici.SifreHash, dto.Sifre);
        if (sonuc == PasswordVerificationResult.Failed)
        {
            kullanici.BasarisizGiris++;
            if (kullanici.BasarisizGiris >= KilitEsigi)
            {
                kullanici.KilitBitis = DateTime.UtcNow.Add(KilitSuresi);
                kullanici.BasarisizGiris = 0;
                log.LogWarning("Hesap kilitlendi: {KullaniciAdi}", kullanici.KullaniciAdi);
            }
            await db.SaveChangesAsync();
            log.LogWarning("Başarısız giriş denemesi: {KullaniciAdi}", kullanici.KullaniciAdi);
            return Unauthorized(new { mesaj = genelHata });
        }

        if (kullanici.Durum != "Aktif")
            return Unauthorized(new { mesaj = "Hesabınız pasif durumda. Yöneticinizle iletişime geçin." });

        // Panele yalnızca yönetici girer. Çalışan kayıtları görüşmelerde seçilmek içindir; şifresi
        // doğru olsa bile oturum açamazlar. Kontrol şifre doğrulandıktan sonra yapılır ki
        // hangi hesabın yönetici olduğu deneme yanılmayla anlaşılmasın.
        if (kullanici.Rol != Kullanici.YoneticiRolu)
        {
            log.LogWarning("Yönetici olmayan hesapla giriş denemesi: {KullaniciAdi}", kullanici.KullaniciAdi);
            return Unauthorized(new { mesaj = "Panele yalnızca yönetici hesapları giriş yapabilir." });
        }

        kullanici.BasarisizGiris = 0;
        kullanici.KilitBitis = null;
        if (sonuc == PasswordVerificationResult.SuccessRehashNeeded)
            kullanici.SifreHash = Hashle(kullanici, dto.Sifre);
        await db.SaveChangesAsync();

        var (token, bitis) = tokenServisi.Uret(kullanici);
        log.LogInformation("Başarılı giriş: {KullaniciAdi}", kullanici.KullaniciAdi);
        return Ok(new
        {
            token,
            gecerlilikBitis = bitis,
            kullanici = new { kullanici.Id, kullanici.AdSoyad, kullanici.KullaniciAdi, kullanici.Rol },
        });
    }

    [HttpGet("ben")]
    public async Task<IActionResult> Ben()
    {
        var kullanici = await AktifKullanici();
        if (kullanici is null) return Unauthorized();
        return Ok(new { kullanici.Id, kullanici.AdSoyad, kullanici.KullaniciAdi, kullanici.Rol, kullanici.Gorev, kullanici.Birim, kullanici.Eposta });
    }

    [HttpPost("sifre-degistir")]
    [EnableRateLimiting("giris")]
    public async Task<IActionResult> SifreDegistir(SifreDegistirDto dto)
    {
        var kullanici = await AktifKullanici();
        if (kullanici is null || kullanici.SifreHash is null) return Unauthorized();

        if (Hasher.VerifyHashedPassword(kullanici, kullanici.SifreHash, dto.MevcutSifre) == PasswordVerificationResult.Failed)
            return BadRequest(new { mesaj = "Mevcut şifre hatalı." });

        var politikaHatasi = SifrePolitikasiHatasi(dto.YeniSifre);
        if (politikaHatasi is not null) return BadRequest(new { mesaj = politikaHatasi });
        if (dto.YeniSifre == dto.MevcutSifre) return BadRequest(new { mesaj = "Yeni şifre mevcut şifreyle aynı olamaz." });

        kullanici.SifreHash = Hashle(kullanici, dto.YeniSifre);
        // Eski token'lar (çalınmış olabilecekler dahil) bu andan itibaren geçersiz.
        kullanici.SifreGuncelleme = DateTime.UtcNow;
        await db.SaveChangesAsync();
        log.LogInformation("Şifre değiştirildi: {KullaniciAdi}", kullanici.KullaniciAdi);

        // Kullanıcının kendi oturumu düşmesin diye taze token verilir; eski token'ı yine de geçersiz.
        var (token, bitis) = tokenServisi.Uret(kullanici);
        return Ok(new { token, gecerlilikBitis = bitis });
    }

    private async Task<Kullanici?> AktifKullanici()
    {
        var idMetni = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(idMetni, out var id) ? await db.Kullanicilar.FindAsync(id) : null;
    }
}
