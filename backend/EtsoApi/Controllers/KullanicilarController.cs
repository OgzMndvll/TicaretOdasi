using EtsoApi.Data;
using EtsoApi.Models;
using EtsoApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EtsoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KullanicilarController(EtsoDbContext db, CanliBildirim canli) : ControllerBase
{
    private static readonly string[] GecerliRoller = ["Yönetici", "Görevli"];
    private static readonly string[] GecerliDurumlar = ["Aktif", "Pasif"];

    /// <summary>Sistemde en az bir aktif yönetici kalmasını güvence altına alır (kendini dışarı kilitleme koruması).</summary>
    private async Task<bool> BaskaAktifYoneticiVarMi(int haricId) =>
        await db.Kullanicilar.AnyAsync(k => k.Id != haricId && k.Rol == "Yönetici" && k.Durum == "Aktif");

    /// <summary>Hesap açma/şifre belirleme yetkisi olan kullanıcı mı?</summary>
    private bool SistemYetkisiVar => User.SistemYoneticisi();

    /// <summary>Sistemde en az bir aktif sistem yöneticisi kalmalı; aksi halde bir daha
    /// kimse hesap açamaz ve ayarlara giremez.</summary>
    private async Task<bool> BaskaSistemYoneticisiVarMi(int haricId) =>
        await db.Kullanicilar.AnyAsync(k => k.Id != haricId && k.SistemYoneticisi && k.Durum == "Aktif");

    private static ObjectResult Yasak(string mesaj) =>
        new(new { mesaj }) { StatusCode = StatusCodes.Status403Forbidden };

    private const string YetkiMesaji =
        "Panele giriş yapabilen hesap açmak, şifre belirlemek ve hesapları değiştirmek yalnızca "
        + "sistem yöneticisine açıktır. Şifresiz görevli kaydını herkes ekleyebilir.";

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpGet]
    public async Task<IActionResult> Listele(
        [FromQuery] string? rol, [FromQuery] string? durum, [FromQuery] List<int>? grupId)
    {
        var sorgu = db.Kullanicilar.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(rol)) sorgu = sorgu.Where(k => k.Rol == rol);
        if (!string.IsNullOrWhiteSpace(durum)) sorgu = sorgu.Where(k => k.Durum == durum);
        // Meslek grubu süzgeci çoklu seçime açıktır: seçilen gruplardan herhangi birine bakan çalışanlar.
        if (grupId is { Count: > 0 }) sorgu = sorgu.Where(k => k.Gruplar.Any(b => grupId.Contains(b.GrupId)));
        return Ok(await Projeksiyon(sorgu.OrderBy(k => k.AdSoyad)).ToListAsync());
    }

    /// <summary>
    /// Kullanıcı kaydının API görünümü. Ham varlık döndürülmez: <see cref="Kullanici.Gruplar"/>
    /// gezinme özelliği yüklenmediğinde boş dizi görünüp yanıltır, yüklendiğinde ise
    /// Grup → Esnaf → Grup döngüsüyle serileştirmeyi kilitler.
    /// </summary>
    private static IQueryable<object> Projeksiyon(IQueryable<Kullanici> sorgu) => sorgu.Select(k => new
    {
        k.Id, k.AdSoyad, k.KullaniciAdi, k.Rol, k.Gorev, k.Birim, k.Eposta, k.Telefon, k.Durum,
        k.SistemYoneticisi, k.OlusturmaTarihi,
        Gruplar = k.Gruplar
            // Gruplar ekranda numarasıyla anıldığı için sıralama da numaraya göre;
            // numarasız gruplar sona alfabetik gelir.
            .OrderBy(b => b.Grup!.No ?? int.MaxValue).ThenBy(b => b.Grup!.Ad)
            .Select(b => new { Id = b.GrupId, b.Grup!.No, b.Grup.Ad, b.Sira })
            .ToList(),
    });

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
        var kullanici = await Projeksiyon(db.Kullanicilar.AsNoTracking().Where(k => k.Id == id)).FirstOrDefaultAsync();
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

        // Rol ve durum yalnızca bilinen değerlerden olabilir; serbest metin kabul edilmez.
        var rol = string.IsNullOrWhiteSpace(dto.Rol) ? Kullanici.CalisanRolu : dto.Rol;

        // Şifresiz görevli kaydını her yönetici ekleyebilir (görüşme listelerinde seçilmek için).
        // Panele giriş yapabilen hesap açmak, şifre belirlemek ve yetki vermek sistem yöneticisine
        // özeldir; aksi halde herkes kendine yeni bir giriş hesabı açabilirdi.
        if (!SistemYetkisiVar
            && (rol == Kullanici.YoneticiRolu || !string.IsNullOrWhiteSpace(dto.Sifre) || dto.SistemYoneticisi == true))
            return Yasak(YetkiMesaji);
        if (!GecerliRoller.Contains(rol))
            return BadRequest(new { mesaj = "Geçersiz rol. 'Yönetici' veya 'Görevli' olmalıdır." });
        var durum = string.IsNullOrWhiteSpace(dto.Durum) ? "Aktif" : dto.Durum;
        if (!GecerliDurumlar.Contains(durum))
            return BadRequest(new { mesaj = "Geçersiz durum. 'Aktif' veya 'Pasif' olmalıdır." });

        // Panele yalnızca yönetici girer; şifre de yalnızca yönetici hesabı için anlamlıdır.
        // Çalışan kaydı görüşmelerde seçilmek içindir, şifresiz açılır ve giriş yapamaz.
        if (rol == Kullanici.YoneticiRolu)
        {
            if (string.IsNullOrWhiteSpace(dto.Sifre))
                return BadRequest(new { mesaj = "Yönetici hesabı için geçici şifre zorunludur." });
            var politikaHatasi = AuthController.SifrePolitikasiHatasi(dto.Sifre);
            if (politikaHatasi is not null) return BadRequest(new { mesaj = politikaHatasi });
        }

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
            SistemYoneticisi = dto.SistemYoneticisi == true,
        };
        // Şifresiz çalışan kaydında da hash rastgele üretilir: alan boş kalırsa açılışta
        // "şifresiz kullanıcı" olarak görülüp yeniden şifre atanmaya çalışılırdı.
        kullanici.SifreHash = AuthController.Hashle(kullanici,
            string.IsNullOrWhiteSpace(dto.Sifre)
                ? Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
                : dto.Sifre);
        // Grup listesi kullanıcı yazılmadan önce doğrulanır; aksi halde hatalı bir seçim
        // yüzünden gruplarsız bir kullanıcı kaydı geride kalırdı.
        var grupHatasi = await GruplariDogrula(dto.GrupIdler);
        if (grupHatasi is not null) return BadRequest(new { mesaj = grupHatasi });

        db.Kullanicilar.Add(kullanici);
        await db.SaveChangesAsync();

        await GruplariEsitle(kullanici, dto.GrupIdler);
        await db.SaveChangesAsync();

        // Kayıt değişti: bağlı paneller listeyi kendiliğinden tazeler (bkz. Services/CanliBildirim.cs).
        await canli.DegistiAsync("kullanici", kullanici.Id);
        return CreatedAtAction(nameof(Getir), new { id = kullanici.Id }, new { kullanici.Id, kullanici.KullaniciAdi });
    }

    /// <summary>Bir kullanıcının şifresini sıfırlar; kilidi de açar. Yalnızca sistem yöneticisi.</summary>
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = TokenServisi.SistemYonetimiPolitikasi)]
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

        // Giriş yapabilen hesaplara yalnızca sistem yöneticisi dokunabilir: kendi hesabını ya da
        // başkasınınkini yönetici yapmak, yetki vermek/almak bu kilidin arkasındadır. Şifresiz
        // görevli kayıtları (ad, görev, birim, grup) her yönetici tarafından düzenlenebilir.
        var yetkiDegisiyor = dto.SistemYoneticisi is not null && dto.SistemYoneticisi != kullanici.SistemYoneticisi;
        if (!SistemYetkisiVar
            && (kullanici.Rol == Kullanici.YoneticiRolu || dto.Rol == Kullanici.YoneticiRolu || yetkiDegisiyor))
            return Yasak(YetkiMesaji);

        // Son aktif sistem yöneticisinin yetkisi alınamaz/pasife alınamaz; aksi halde bir daha
        // kimse hesap açamaz, şifre veremez ve ayarlara giremez.
        var sistemYetkisiBitiyor = kullanici.SistemYoneticisi && kullanici.Durum == "Aktif"
            && ((dto.SistemYoneticisi == false) || (!string.IsNullOrWhiteSpace(dto.Durum) && dto.Durum != "Aktif"));
        if (sistemYetkisiBitiyor && !await BaskaSistemYoneticisiVarMi(id))
            return Conflict(new { mesaj = "Sistemdeki son sistem yöneticisi bu kullanıcı; yetkisini veya durumunu değiştiremezsiniz. Önce başka bir sistem yöneticisi tanımlayın." });

        // Son aktif yöneticinin yetkisi alınamaz/pasife alınamaz; aksi halde sisteme kimse giremez.
        var yoneticilikBitiyor = kullanici.Rol == "Yönetici" && kullanici.Durum == "Aktif"
            && ((!string.IsNullOrWhiteSpace(dto.Rol) && dto.Rol != "Yönetici")
                || (!string.IsNullOrWhiteSpace(dto.Durum) && dto.Durum != "Aktif"));
        if (yoneticilikBitiyor && !await BaskaAktifYoneticiVarMi(id))
            return Conflict(new { mesaj = "Sistemdeki son aktif yönetici bu kullanıcı; rolünü veya durumunu değiştiremezsiniz. Önce başka bir yönetici tanımlayın." });

        // Kullanıcı adı tekil indekslidir; çakışma denetlenmezse istek 500 ile düşerdi.
        var yeniKullaniciAdi = string.IsNullOrWhiteSpace(dto.KullaniciAdi)
            ? kullanici.KullaniciAdi
            : dto.KullaniciAdi.Trim().ToLowerInvariant();
        if (yeniKullaniciAdi != kullanici.KullaniciAdi
            && await db.Kullanicilar.AnyAsync(k => k.KullaniciAdi == yeniKullaniciAdi && k.Id != id))
            return Conflict(new { mesaj = "Bu kullanıcı adı zaten kullanımda." });

        kullanici.AdSoyad = dto.AdSoyad.Trim();
        kullanici.KullaniciAdi = yeniKullaniciAdi;
        if (!string.IsNullOrWhiteSpace(dto.Rol)) kullanici.Rol = dto.Rol;
        kullanici.Gorev = dto.Gorev;
        kullanici.Birim = dto.Birim;
        kullanici.Eposta = dto.Eposta;
        kullanici.Telefon = dto.Telefon;
        if (!string.IsNullOrWhiteSpace(dto.Durum)) kullanici.Durum = dto.Durum;
        // Alan gönderilmezse (null) mevcut yetkiye dokunulmaz; yetki alanını taşımayan
        // eski istemciler kullanıcının sistem yöneticiliğini sessizce düşürmesin.
        if (dto.SistemYoneticisi is not null) kullanici.SistemYoneticisi = dto.SistemYoneticisi.Value;

        var grupHatasi = await GruplariDogrula(dto.GrupIdler);
        if (grupHatasi is not null) return BadRequest(new { mesaj = grupHatasi });
        await GruplariEsitle(kullanici, dto.GrupIdler);

        await db.SaveChangesAsync();
        await canli.DegistiAsync("kullanici", kullanici.Id);
        return NoContent();
    }

    /// <summary>Gönderilen grup kimliklerinin gerçekten var olduğunu doğrular. Hata varsa mesajı döner.</summary>
    private async Task<string?> GruplariDogrula(int[]? grupIdler)
    {
        var istenen = TemizGrupIdler(grupIdler);
        if (istenen is null or { Count: 0 }) return null;
        var bulunan = await db.Gruplar.Where(g => istenen.Contains(g.Id)).Select(g => g.Id).ToListAsync();
        var eksik = istenen.Except(bulunan).ToList();
        return eksik.Count == 0
            ? null
            : $"Seçilen meslek gruplarından bazıları bulunamadı (#{string.Join(", #", eksik)}).";
    }

    private static List<int>? TemizGrupIdler(int[]? grupIdler) =>
        grupIdler is null ? null : grupIdler.Where(x => x > 0).Distinct().ToList();

    /// <summary>
    /// Çalışanın meslek grubu bağlarını gelen listeyle eşitler: listede olmayanlar silinir,
    /// yeni olanlar eklenir, kalanların dosyadan gelen sırası korunur. <paramref name="grupIdler"/>
    /// null ise (alanı hiç göndermeyen istemci) mevcut bağlara dokunulmaz.
    /// Çağıranın ardından <c>SaveChangesAsync</c> çağırması gerekir.
    /// </summary>
    private async Task GruplariEsitle(Kullanici kullanici, int[]? grupIdler)
    {
        var istenen = TemizGrupIdler(grupIdler);
        if (istenen is null) return;

        var mevcut = await db.KullaniciGruplari.Where(b => b.KullaniciId == kullanici.Id).ToListAsync();
        foreach (var bag in mevcut.Where(b => !istenen.Contains(b.GrupId)))
            db.KullaniciGruplari.Remove(bag);
        foreach (var grupId in istenen.Where(g => mevcut.All(b => b.GrupId != g)))
            db.KullaniciGruplari.Add(new KullaniciGrup { KullaniciId = kullanici.Id, GrupId = grupId });
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Sil(int id)
    {
        var kullanici = await db.Kullanicilar.FindAsync(id);
        if (kullanici is null) return NotFound();
        if (!SistemYetkisiVar && kullanici.Rol == Kullanici.YoneticiRolu)
            return Yasak(YetkiMesaji);
        if (id == User.KullaniciId())
            return Conflict(new { mesaj = "Kendi hesabınızı silemezsiniz." });
        if (kullanici.SistemYoneticisi && !await BaskaSistemYoneticisiVarMi(id))
            return Conflict(new { mesaj = "Sistemdeki son sistem yöneticisi silinemez. Önce başka bir sistem yöneticisi tanımlayın." });
        if (kullanici.Rol == "Yönetici" && !await BaskaAktifYoneticiVarMi(id))
            return Conflict(new { mesaj = "Sistemdeki son aktif yönetici silinemez. Önce başka bir yönetici tanımlayın." });
        if (await db.Gorusmeler.AnyAsync(g => g.GorevliId == id) || await db.Gorevlendirmeler.AnyAsync(g => g.GorevliId == id))
            return Conflict(new { mesaj = "Bu kullanıcının görüşme/görevlendirme kayıtları var; silmek yerine pasife alın." });
        db.Kullanicilar.Remove(kullanici);
        await db.SaveChangesAsync();
        await canli.DegistiAsync("kullanici", id);
        return NoContent();
    }

    private static readonly string[] ExcelBasliklari =
        ["Ad Soyad", "Kullanıcı Adı", "Rol", "Görev", "Birim", "E-posta", "Telefon", "Durum", "Meslek Grupları"];

    /// <summary>Şablondaki "Meslek Grupları" sütununda birden çok grup bu işaretle ayrılır.</summary>
    private const char GrupAyraci = ';';

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpGet("sablon")]
    public IActionResult Sablon()
    {
        var ornek = new object?[][]
        {
            ["Örnek Kullanıcı", "ornek.kullanici", "Görevli", "Üye Temsilcisi", "Saha",
             "ornek@erzto.org.tr", "0532 000 00 00", "Aktif", "1; 5. GRUP; SİGORTA FAALİYETLERİ"],
        };
        var dosya = ExcelServisi.Olustur("Kullanıcılar", ExcelBasliklari, ornek);
        return File(dosya, ExcelServisi.IcerikTipi, "kullanici-ice-aktarma-sablonu.xlsx");
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpGet("disa-aktar")]
    public async Task<IActionResult> DisaAktar()
    {
        var kayitlar = await db.Kullanicilar.AsNoTracking()
            .OrderBy(k => k.AdSoyad)
            .Select(k => new
            {
                k.AdSoyad, k.KullaniciAdi, k.Rol, k.Gorev, k.Birim, k.Eposta, k.Telefon, k.Durum,
                Gruplar = k.Gruplar
                    .OrderBy(b => b.Sira ?? int.MaxValue).ThenBy(b => b.Grup!.No ?? int.MaxValue)
                    .Select(b => b.Grup!.No != null ? b.Grup.No + ". GRUP" : b.Grup.Ad)
                    .ToList(),
            })
            .ToListAsync();
        var satirlar = kayitlar.Select(k => new object?[]
        {
            k.AdSoyad, k.KullaniciAdi, k.Rol, k.Gorev, k.Birim, k.Eposta, k.Telefon, k.Durum,
            string.Join($"{GrupAyraci} ", k.Gruplar),
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
        catch (InvalidOperationException e)
        {
            // Satır tavanı gibi anlaşılır sınır hataları kullanıcıya olduğu gibi iletilir.
            return BadRequest(new { mesaj = e.Message });
        }
        catch
        {
            return BadRequest(new { mesaj = "Dosya okunamadı. Geçerli bir Excel (.xlsx) dosyası olduğundan emin olun." });
        }
        if (satirlar.Count == 0)
            return BadRequest(new { mesaj = "Dosyada veri satırı bulunamadı. Şablonu indirip doldurun." });

        // Odanın "ETSO GRUPLAR" listesi (satır = grup, sütun = o gruba bakan kişiler) doğrudan
        // yüklenebilir; başlıklarından tanınır ve kendi aktarıcısına verilir.
        if (GrupCalisanIceAktarmaServisi.DosyaBuDuzendeMi(satirlar))
        {
            var grupSonucu = await GrupCalisanIceAktarmaServisi.AktarAsync(db, satirlar);
            await canli.DegistiAsync("kullanici");
            await canli.DegistiAsync("grup");
            return Ok(grupSonucu);
        }

        return Ok(await SablonDuzeniniAktar(satirlar));
    }

    /// <summary>
    /// Panelin kendi şablonundaki düzeni (satır = kişi) aktarır. Var olan kullanıcı kullanıcı
    /// adından bulunur ve üzerine yazılır; böylece aynı dosya yeniden yüklenerek yalnızca
    /// meslek grubu dağılımı güncellenebilir.
    /// </summary>
    private async Task<IceAktarmaSonucu> SablonDuzeniniAktar(
        List<(int SatirNo, Dictionary<string, string> Degerler)> satirlar)
    {
        var hatalar = new List<string>();
        var eklenen = 0;
        var guncellenen = 0;
        var atlanan = 0;

        var kullanicilar = await db.Kullanicilar.ToListAsync();
        var adIndeksi = new Dictionary<string, Kullanici>();
        foreach (var k in kullanicilar) adIndeksi.TryAdd(k.KullaniciAdi, k);

        var gruplar = await db.Gruplar.AsNoTracking().Select(g => new { g.Id, g.No, g.Ad }).ToListAsync();
        var grupNoIndeksi = new Dictionary<int, int>();
        var grupAdIndeksi = new Dictionary<string, int>();
        foreach (var g in gruplar)
        {
            if (g.No is not null) grupNoIndeksi.TryAdd(g.No.Value, g.Id);
            grupAdIndeksi.TryAdd(ExcelServisi.Normalize(g.Ad), g.Id);
        }

        var mevcutBaglar = (await db.KullaniciGruplari.ToListAsync())
            .GroupBy(b => b.KullaniciId)
            .ToDictionary(g => g.Key, g => g.ToList());

        string? Al(Dictionary<string, string> d, string baslik) =>
            d.TryGetValue(ExcelServisi.Normalize(baslik), out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

        foreach (var (satirNo, degerler) in satirlar)
        {
            var adSoyad = Al(degerler, "Ad Soyad");
            if (adSoyad is null)
            {
                hatalar.Add($"Satır {satirNo}: 'Ad Soyad' zorunludur.");
                atlanan++;
                continue;
            }
            var kullaniciAdi = Al(degerler, "Kullanıcı Adı")?.ToLowerInvariant() ?? UretKullaniciAdi(adSoyad);

            var rol = Al(degerler, "Rol");
            if (rol is not null && !GecerliRoller.Contains(rol))
            {
                hatalar.Add($"Satır {satirNo}: '{rol}' geçersiz rol, 'Görevli' olarak kaydedildi.");
                rol = Kullanici.CalisanRolu;
            }
            // Dosyayla panele giriş yapabilen hesap açılamaz: içe aktarma herkese açıktır ve
            // şifre taşımaz. Yönetici hesabı yalnızca Kullanıcılar ekranından, sistem
            // yöneticisi tarafından açılır.
            if (rol == Kullanici.YoneticiRolu && !SistemYetkisiVar)
            {
                hatalar.Add($"Satır {satirNo}: Dosyayla yönetici hesabı açılamaz, 'Görevli' olarak kaydedildi.");
                rol = Kullanici.CalisanRolu;
            }
            var durum = Al(degerler, "Durum");
            if (durum is not null && !GecerliDurumlar.Contains(durum)) durum = "Aktif";

            // ---- Meslek grupları ----
            List<int>? grupIdler = null;
            var grupMetni = Al(degerler, "Meslek Grupları");
            if (grupMetni is not null)
            {
                grupIdler = [];
                foreach (var parca in grupMetni.Split(GrupAyraci, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var id = GrubuCoz(parca, grupNoIndeksi, grupAdIndeksi);
                    if (id is null) hatalar.Add($"Satır {satirNo}: \"{parca}\" adlı/numaralı meslek grubu bulunamadı, atlandı.");
                    else if (!grupIdler.Contains(id.Value)) grupIdler.Add(id.Value);
                }
            }

            if (adIndeksi.TryGetValue(kullaniciAdi, out var mevcut))
            {
                var degisti = false;
                // Dosyada boş bırakılan hücre "değiştirme" demektir; yalnızca dolu alanlar yazılır.
                void Yaz(string? eski, string? yeni, Action<string> ata)
                {
                    if (yeni is null || eski == yeni) return;
                    ata(yeni);
                    degisti = true;
                }
                Yaz(mevcut.AdSoyad, adSoyad, v => mevcut.AdSoyad = v);
                Yaz(mevcut.Gorev, Al(degerler, "Görev"), v => mevcut.Gorev = v);
                Yaz(mevcut.Birim, Al(degerler, "Birim"), v => mevcut.Birim = v);
                Yaz(mevcut.Eposta, Al(degerler, "E-posta"), v => mevcut.Eposta = v);
                Yaz(mevcut.Telefon, Al(degerler, "Telefon"), v => mevcut.Telefon = v);

                // Son aktif yöneticinin yetkisi/durumu dosyayla düşürülemez; aksi halde
                // bir içe aktarma herkesi paneldan kilitleyebilirdi.
                var yoneticilikBitiyor = mevcut.Rol == Kullanici.YoneticiRolu && mevcut.Durum == "Aktif"
                    && ((rol is not null && rol != Kullanici.YoneticiRolu) || (durum is not null && durum != "Aktif"));
                if (yoneticilikBitiyor && !await BaskaAktifYoneticiVarMi(mevcut.Id))
                    hatalar.Add($"Satır {satirNo}: \"{mevcut.AdSoyad}\" sistemdeki son aktif yönetici; " +
                                "rolü ve durumu dosyadaki değerlerle değiştirilmedi.");
                else
                {
                    Yaz(mevcut.Rol, rol, v => mevcut.Rol = v);
                    Yaz(mevcut.Durum, durum, v => mevcut.Durum = v);
                }

                if (GruplariUygula(mevcut.Id, grupIdler, mevcutBaglar)) degisti = true;
                if (degisti) guncellenen++; else atlanan++;
                continue;
            }

            var kullanici = new Kullanici
            {
                AdSoyad = adSoyad,
                KullaniciAdi = kullaniciAdi,
                Rol = rol ?? Kullanici.CalisanRolu,
                Gorev = Al(degerler, "Görev"),
                Birim = Al(degerler, "Birim"),
                Eposta = Al(degerler, "E-posta"),
                Telefon = Al(degerler, "Telefon"),
                Durum = durum ?? "Aktif",
            };
            // Şifresiz çalışan kaydında da hash rastgele üretilir (bkz. Olustur).
            kullanici.SifreHash = AuthController.Hashle(kullanici,
                Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
            db.Kullanicilar.Add(kullanici);
            adIndeksi[kullaniciAdi] = kullanici;
            eklenen++;
            // Grup bağları için Id gerekiyor.
            await db.SaveChangesAsync();
            GruplariUygula(kullanici.Id, grupIdler, mevcutBaglar);
        }

        await db.SaveChangesAsync();
        await canli.DegistiAsync("kullanici");
        return new IceAktarmaSonucu(eklenen, atlanan, hatalar, guncellenen);
    }

    /// <summary>Bir kullanıcının grup bağlarını dosyadaki listeyle eşitler. Değişiklik oldu mu döner.</summary>
    private bool GruplariUygula(int kullaniciId, List<int>? grupIdler, Dictionary<int, List<KullaniciGrup>> mevcutBaglar)
    {
        // Sütun hiç doldurulmamışsa mevcut dağılım korunur; boş bırakmak "hepsini sil" demek değildir.
        if (grupIdler is null) return false;
        if (!mevcutBaglar.TryGetValue(kullaniciId, out var baglar)) baglar = mevcutBaglar[kullaniciId] = [];

        var degisti = false;
        foreach (var bag in baglar.Where(b => !grupIdler.Contains(b.GrupId)).ToList())
        {
            db.KullaniciGruplari.Remove(bag);
            baglar.Remove(bag);
            degisti = true;
        }
        foreach (var grupId in grupIdler.Where(g => baglar.All(b => b.GrupId != g)))
        {
            var yeni = new KullaniciGrup { KullaniciId = kullaniciId, GrupId = grupId };
            db.KullaniciGruplari.Add(yeni);
            baglar.Add(yeni);
            degisti = true;
        }
        return degisti;
    }

    /// <summary>"5", "5. GRUP" veya grubun tam adı → grup kimliği.</summary>
    private static int? GrubuCoz(string parca, Dictionary<int, int> noIndeksi, Dictionary<string, int> adIndeksi)
    {
        var rakamlar = new string(parca.TakeWhile(char.IsDigit).ToArray());
        if (rakamlar.Length > 0 && int.TryParse(rakamlar, out var no)
            && ExcelServisi.Normalize(parca[rakamlar.Length..]) is "" or "grup"
            && noIndeksi.TryGetValue(no, out var idNo))
            return idNo;
        return adIndeksi.TryGetValue(ExcelServisi.Normalize(parca), out var idAd) ? idAd : null;
    }

    /// <summary>
    /// "İdris Akdemir" → "idris.akdemir". Türkçe 'İ' harfinin küçültme tuzağı için
    /// bkz. <see cref="GrupCalisanIceAktarmaServisi.KullaniciAdiUret"/>.
    /// </summary>
    private static string UretKullaniciAdi(string adSoyad) =>
        GrupCalisanIceAktarmaServisi.KullaniciAdiUret(adSoyad);
}
