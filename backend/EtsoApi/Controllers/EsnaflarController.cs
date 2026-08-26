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
        // Çoklu seçime açık süzgeçler dizi olarak gelir: ?durum=Onay Verdi&durum=Kararsız
        [FromQuery] List<string>? durum, [FromQuery] List<string>? uyelikDurumu, [FromQuery] List<int>? grupId,
        [FromQuery] List<int>? gorevliId, [FromQuery] List<int>? gorusenId, [FromQuery] List<string>? ilce,
        [FromQuery] bool? takipGerekli, [FromQuery] bool? gorusuldu,
        [FromQuery] string? arama, [FromQuery] string? sirala,
        [FromQuery] int sayfa = 1, [FromQuery] int sayfaBoyutu = 20)
    {
        var sorgu = db.Esnaflar.AsNoTracking();

        // Aynı süzgeçte birden çok değer VEYA, farklı süzgeçler VE ile birleşir:
        // "(2. veya 5. grup) VE (onay verdi veya kararsız)".
        if (Dolu(durum)) sorgu = sorgu.Where(e => durum!.Contains(e.Durum));
        if (Dolu(uyelikDurumu)) sorgu = sorgu.Where(e => uyelikDurumu!.Contains(e.UyelikDurumu));
        if (Dolu(grupId)) sorgu = sorgu.Where(e => e.GrupId != null && grupId!.Contains(e.GrupId.Value));
        if (Dolu(gorevliId)) sorgu = sorgu.Where(e => e.GorevliId != null && gorevliId!.Contains(e.GorevliId.Value));
        // "Görüşen çalışan": üyeye atanan kişi değil, üyeyle fiilen görüşme yapmış çalışan.
        if (Dolu(gorusenId)) sorgu = sorgu.Where(e => e.Gorusmeler.Any(g => gorusenId!.Contains(g.GorevliId)));
        if (takipGerekli is not null)
            sorgu = takipGerekli.Value
                ? sorgu.Where(e => e.Gorusmeler.Any(g => g.TakipGerekli))
                : sorgu.Where(e => !e.Gorusmeler.Any(g => g.TakipGerekli));
        if (gorusuldu is not null)
            sorgu = gorusuldu.Value ? sorgu.Where(e => e.Gorusmeler.Any()) : sorgu.Where(e => !e.Gorusmeler.Any());
        if (Dolu(ilce)) sorgu = sorgu.Where(e => e.Ilce != null && ilce!.Contains(e.Ilce));
        if (!string.IsNullOrWhiteSpace(arama))
            sorgu = sorgu.Where(e => e.AdSoyad.Contains(arama) || e.Isletme.Contains(arama)
                || (e.Telefon != null && e.Telefon.Contains(arama))
                || (e.UyeSicilNo != null && e.UyeSicilNo.Contains(arama))
                || (e.TicaretSicilNo != null && e.TicaretSicilNo.Contains(arama)));

        // Liste toplamı ve üstteki durum kartları aynı sorgudan, aynı anda hesaplanır.
        // Böylece grup/çalışan/ilçe gibi bir süzgeç değiştiğinde tablo ile kartlar ayrışamaz.
        var durumSayilari = await sorgu
            .GroupBy(e => e.Durum)
            .Select(g => new { Durum = g.Key, Adet = g.Count() })
            .ToListAsync();
        var toplam = durumSayilari.Sum(g => g.Adet);
        int DurumSayisi(string deger) => durumSayilari.FirstOrDefault(g => g.Durum == deger)?.Adet ?? 0;
        var genelToplam = await db.Esnaflar.AsNoTracking().CountAsync();
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
                e.GrupId, Grup = e.Grup != null ? e.Grup.Ad : null, GrupNo = e.Grup != null ? e.Grup.No : null,
                e.Il, e.Ilce, e.Mahalle, e.Adres, e.Telefon, e.IsTelefonu,
                e.GorevliId, Gorevli = e.Gorevli != null ? e.Gorevli.AdSoyad : null,
                e.Durum, e.SonGorusmeTarihi, e.KayitTarihi,
                e.UyeSicilNo, e.TicaretSicilNo, e.SirketTipi, e.Gorevi,
                e.UyelikDurumu, e.DurumDegisimTarihi, e.DurumDegisimNedeni, e.NaceKodu,
            })
            .ToListAsync();

        return Ok(new
        {
            toplam,
            sayfa,
            sayfaBoyutu,
            kayitlar,
            istatistik = new
            {
                toplam,
                genelToplam,
                // Grup kartı tüm diğer aktif süzgeçleri de dikkate alan gerçek liste toplamıdır.
                grupToplam = Dolu(grupId) ? toplam : 0,
                onayVeren = DurumSayisi("Onay Verdi"),
                onayVermeyen = DurumSayisi("Onay Vermedi"),
                kararsiz = DurumSayisi("Kararsız"),
                gorusulmemis = DurumSayisi("Görüşülmedi"),
            },
        });
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpGet("istatistik")]
    public async Task<IActionResult> Istatistik(
        [FromQuery] List<string>? durum, [FromQuery] List<string>? uyelikDurumu, [FromQuery] List<int>? grupId,
        [FromQuery] List<int>? gorevliId, [FromQuery] List<int>? gorusenId, [FromQuery] List<string>? ilce,
        [FromQuery] bool? takipGerekli, [FromQuery] bool? gorusuldu, [FromQuery] string? arama)
    {
        // Kartlar, listeyle birebir aynı süzgeç kapsamını kullanır.
        var sorgu = db.Esnaflar.AsNoTracking();
        if (Dolu(durum)) sorgu = sorgu.Where(e => durum!.Contains(e.Durum));
        if (Dolu(uyelikDurumu)) sorgu = sorgu.Where(e => uyelikDurumu!.Contains(e.UyelikDurumu));
        if (Dolu(grupId)) sorgu = sorgu.Where(e => e.GrupId != null && grupId!.Contains(e.GrupId.Value));
        if (Dolu(gorevliId)) sorgu = sorgu.Where(e => e.GorevliId != null && gorevliId!.Contains(e.GorevliId.Value));
        if (Dolu(gorusenId)) sorgu = sorgu.Where(e => e.Gorusmeler.Any(g => gorusenId!.Contains(g.GorevliId)));
        if (takipGerekli is not null)
            sorgu = takipGerekli.Value
                ? sorgu.Where(e => e.Gorusmeler.Any(g => g.TakipGerekli))
                : sorgu.Where(e => !e.Gorusmeler.Any(g => g.TakipGerekli));
        if (gorusuldu is not null)
            sorgu = gorusuldu.Value ? sorgu.Where(e => e.Gorusmeler.Any()) : sorgu.Where(e => !e.Gorusmeler.Any());
        if (Dolu(ilce)) sorgu = sorgu.Where(e => e.Ilce != null && ilce!.Contains(e.Ilce));
        if (!string.IsNullOrWhiteSpace(arama))
            sorgu = sorgu.Where(e => e.AdSoyad.Contains(arama) || e.Isletme.Contains(arama)
                || (e.Telefon != null && e.Telefon.Contains(arama))
                || (e.UyeSicilNo != null && e.UyeSicilNo.Contains(arama))
                || (e.TicaretSicilNo != null && e.TicaretSicilNo.Contains(arama)));

        var gruplu = await sorgu.GroupBy(e => e.Durum)
            .Select(g => new { Durum = g.Key, Adet = g.Count() }).ToListAsync();
        var toplam = gruplu.Sum(g => g.Adet);
        int Say(string durum) => gruplu.FirstOrDefault(g => g.Durum == durum)?.Adet ?? 0;
        var uyelik = await sorgu.GroupBy(e => e.UyelikDurumu)
            .Select(g => new { Durum = g.Key, Adet = g.Count() }).ToListAsync();
        var genelToplam = await db.Esnaflar.CountAsync();
        int? grupToplam = Dolu(grupId)
            ? await db.Esnaflar.CountAsync(e => e.GrupId != null && grupId!.Contains(e.GrupId.Value))
            : null;

        return Ok(new
        {
            toplam,
            genelToplam,
            grupToplam,
            onayVeren = Say("Onay Verdi"),
            onayVermeyen = Say("Onay Vermedi"),
            kararsiz = Say("Kararsız"),
            gorusulmemis = Say("Görüşülmedi"),
            gorusulen = toplam - Say("Görüşülmedi"),
            faal = uyelik.FirstOrDefault(u => u.Durum == "Faal")?.Adet ?? 0,
            askida = uyelik.FirstOrDefault(u => u.Durum == "Askı")?.Adet ?? 0,
            pasif = uyelik.FirstOrDefault(u => u.Durum == "Pasif")?.Adet ?? 0,
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Getir(int id)
    {
        var esnaf = await db.Esnaflar.AsNoTracking()
            .Include(e => e.Grup).Include(e => e.Gorevli).Include(e => e.Yetkililer)
            .Include(e => e.Gorusmeler.OrderBy(g => g.Sira).ThenBy(g => g.Tarih)).ThenInclude(g => g.Gorevli)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (esnaf is null) return NotFound();

        return Ok(new
        {
            esnaf.Id, esnaf.AdSoyad, esnaf.Isletme, esnaf.VergiNo,
            esnaf.GrupId, Grup = esnaf.Grup?.Ad, GrupNo = esnaf.Grup?.No,
            esnaf.Il, esnaf.Ilce, esnaf.Mahalle, esnaf.Adres, esnaf.Telefon, esnaf.IsTelefonu,
            esnaf.GorevliId, Gorevli = esnaf.Gorevli?.AdSoyad,
            esnaf.Durum, esnaf.SonGorusmeTarihi, esnaf.KayitTarihi,
            esnaf.UyeSicilNo, esnaf.TicaretSicilNo, esnaf.SirketTipi, esnaf.TabelaUnvani,
            esnaf.Uyruk, esnaf.Sermaye, esnaf.Derece, esnaf.VergiDairesi, esnaf.VergiTerkTarihi,
            esnaf.KurulusTarihi, esnaf.OdaKararTarihi, esnaf.Gorevi,
            esnaf.UyelikDurumu, esnaf.DurumDegisimTarihi, esnaf.DurumDegisimNedeni,
            esnaf.FaaliyetDetayi, esnaf.NaceKodu, esnaf.NaceAdi,
            Yetkililer = esnaf.Yetkililer.Select(y => new { y.Id, y.AdSoyad, y.Gorevi, y.YetkiBaslangic, y.YetkiBitis }),
            Gorusmeler = esnaf.Gorusmeler.Select(g => new
            {
                g.Id, g.Sira, g.Tarih, g.Sonuc, g.Not, g.TakipGerekli,
                g.GorevliId, Gorevli = g.Gorevli?.AdSoyad,
            }),
        });
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpPost]
    public async Task<IActionResult> Olustur(EsnafYazDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.AdSoyad) || string.IsNullOrWhiteSpace(dto.Isletme))
            return BadRequest(new { mesaj = "Ad soyad ve işletme adı zorunludur." });

        if (!string.IsNullOrWhiteSpace(dto.UyelikDurumu) && !UyelikDurumlari.Contains(dto.UyelikDurumu))
            return BadRequest(new { mesaj = $"Geçersiz üyelik durumu. Geçerli değerler: {string.Join(", ", UyelikDurumlari)}" });
        if (!string.IsNullOrWhiteSpace(dto.UyeSicilNo) && await db.Esnaflar.AnyAsync(e => e.UyeSicilNo == dto.UyeSicilNo))
            return BadRequest(new { mesaj = $"'{dto.UyeSicilNo}' üye sicil numarası zaten kayıtlı." });

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
        OdaBilgileriniYaz(esnaf, dto);
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
        if (!string.IsNullOrWhiteSpace(dto.UyelikDurumu) && !UyelikDurumlari.Contains(dto.UyelikDurumu))
            return BadRequest(new { mesaj = $"Geçersiz üyelik durumu. Geçerli değerler: {string.Join(", ", UyelikDurumlari)}" });
        if (!string.IsNullOrWhiteSpace(dto.UyeSicilNo) && await db.Esnaflar.AnyAsync(e => e.UyeSicilNo == dto.UyeSicilNo && e.Id != id))
            return BadRequest(new { mesaj = $"'{dto.UyeSicilNo}' üye sicil numarası başka bir üyede kayıtlı." });

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
        OdaBilgileriniYaz(esnaf, dto);
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

    // Kolon düzeni odanın "ÜYE LİSTE DETAY RAPORU" çıktısıyla hizalıdır; içe aktarma da bu başlıkları okur.
    private static readonly string[] ExcelBasliklari =
    [
        "Üye Sicil No", "Unvan", "Yetkili Adı Soyadı", "Görevi", "Şirket Tipi", "Ticaret Sicil No",
        "Meslek Grubu", "Üyelik Durumu", "Durum Değişim Tarihi", "Durum Değişim Nedeni",
        "Vergi Dairesi", "Vergi No", "Kuruluş Tarihi", "Üye Kayıt Tarihi",
        "İş Telefonu", "Cep Telefonu (GSM)", "İl", "İlçe", "Mahalle", "Adres",
        "NACE Faaliyet Kodu", "NACE Faaliyet Adı", "Faaliyet Detayı", "Görevli", "Onay Durumu",
    ];

    /// <summary>Süzgeç dizisi gerçekten değer taşıyor mu (boş dizi "süzme yok" demektir).</summary>
    private static bool Dolu<T>(List<T>? deger) => deger is { Count: > 0 };

    private static readonly string[] GecerliDurumlar = ["Onay Verdi", "Onay Vermedi", "Kararsız", "Görüşülmedi"];

    private static string[] UyelikDurumlari => UyeIceAktarmaServisi.UyelikDurumlari;

    /// <summary>Oda kayıt bilgilerini (sicil, şirket tipi, üyelik durumu, NACE vb.) DTO'dan kayda geçirir.</summary>
    private static void OdaBilgileriniYaz(Esnaf esnaf, EsnafYazDto dto)
    {
        esnaf.UyeSicilNo = dto.UyeSicilNo;
        esnaf.TicaretSicilNo = dto.TicaretSicilNo;
        esnaf.SirketTipi = dto.SirketTipi;
        esnaf.TabelaUnvani = dto.TabelaUnvani;
        esnaf.Uyruk = dto.Uyruk;
        esnaf.Sermaye = dto.Sermaye;
        esnaf.Derece = dto.Derece;
        esnaf.VergiDairesi = dto.VergiDairesi;
        esnaf.VergiTerkTarihi = dto.VergiTerkTarihi;
        esnaf.KurulusTarihi = dto.KurulusTarihi;
        esnaf.OdaKararTarihi = dto.OdaKararTarihi;
        esnaf.DurumDegisimTarihi = dto.DurumDegisimTarihi;
        esnaf.DurumDegisimNedeni = dto.DurumDegisimNedeni;
        esnaf.FaaliyetDetayi = dto.FaaliyetDetayi;
        esnaf.NaceKodu = dto.NaceKodu;
        esnaf.NaceAdi = dto.NaceAdi;
        esnaf.Gorevi = dto.Gorevi;
        esnaf.IsTelefonu = dto.IsTelefonu;
        if (!string.IsNullOrWhiteSpace(dto.UyelikDurumu)) esnaf.UyelikDurumu = dto.UyelikDurumu;
        if (dto.KayitTarihi is not null) esnaf.KayitTarihi = dto.KayitTarihi.Value;
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpGet("sablon")]
    public IActionResult Sablon()
    {
        var ornek = new object?[][]
        {
            [
                "10036", "ÖRNEK TİCARET LİMİTED ŞİRKETİ", "Örnek Yetkili", "MÜDÜR", "LİMİTED ŞİRKET", "5735",
                "13.HIRDAVAT ÜRÜNLERİNİN TOPTAN VE PERAKENDE TİCARETİ", "Faal", "09/05/1979", "",
                "Aziziye V.D.", "1234567890", "20/12/1978", "09/05/1979",
                "4422130092", "5426442075", "Erzurum", "Yakutiye", "Lalapaşa", "Örnek Mah. Örnek Cad. No: 1",
                "47.52.02", "Hırdavat (nalburiye) ve el aletleri perakende ticareti", "İNŞAAT MALZEMELERİ TİCARETİ.",
                "Ahmet Yılmaz", "Görüşülmedi",
            ],
        };
        var dosya = ExcelServisi.Olustur("Üyeler", ExcelBasliklari, ornek);
        return File(dosya, ExcelServisi.IcerikTipi, "uye-ice-aktarma-sablonu.xlsx");
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Yönetici")]
    [HttpGet("disa-aktar")]
    public async Task<IActionResult> DisaAktar(
        [FromQuery] List<string>? durum, [FromQuery] List<string>? uyelikDurumu, [FromQuery] List<int>? grupId,
        [FromQuery] List<int>? gorevliId, [FromQuery] List<int>? gorusenId, [FromQuery] List<string>? ilce,
        [FromQuery] bool? takipGerekli, [FromQuery] bool? gorusuldu, [FromQuery] string? arama)
    {
        var sorgu = db.Esnaflar.AsNoTracking().Include(e => e.Grup).Include(e => e.Gorevli).AsQueryable();
        // Aynı süzgeçte birden çok değer VEYA, farklı süzgeçler VE ile birleşir:
        // "(2. veya 5. grup) VE (onay verdi veya kararsız)".
        if (Dolu(durum)) sorgu = sorgu.Where(e => durum!.Contains(e.Durum));
        if (Dolu(uyelikDurumu)) sorgu = sorgu.Where(e => uyelikDurumu!.Contains(e.UyelikDurumu));
        if (Dolu(grupId)) sorgu = sorgu.Where(e => e.GrupId != null && grupId!.Contains(e.GrupId.Value));
        if (Dolu(gorevliId)) sorgu = sorgu.Where(e => e.GorevliId != null && gorevliId!.Contains(e.GorevliId.Value));
        // "Görüşen çalışan": üyeye atanan kişi değil, üyeyle fiilen görüşme yapmış çalışan.
        if (Dolu(gorusenId)) sorgu = sorgu.Where(e => e.Gorusmeler.Any(g => gorusenId!.Contains(g.GorevliId)));
        if (takipGerekli is not null)
            sorgu = takipGerekli.Value
                ? sorgu.Where(e => e.Gorusmeler.Any(g => g.TakipGerekli))
                : sorgu.Where(e => !e.Gorusmeler.Any(g => g.TakipGerekli));
        if (gorusuldu is not null)
            sorgu = gorusuldu.Value ? sorgu.Where(e => e.Gorusmeler.Any()) : sorgu.Where(e => !e.Gorusmeler.Any());
        if (Dolu(ilce)) sorgu = sorgu.Where(e => e.Ilce != null && ilce!.Contains(e.Ilce));
        if (!string.IsNullOrWhiteSpace(arama))
            sorgu = sorgu.Where(e => e.AdSoyad.Contains(arama) || e.Isletme.Contains(arama)
                || (e.Telefon != null && e.Telefon.Contains(arama))
                || (e.UyeSicilNo != null && e.UyeSicilNo.Contains(arama))
                || (e.TicaretSicilNo != null && e.TicaretSicilNo.Contains(arama)));

        var kayitlar = await sorgu.OrderBy(e => e.Isletme).ToListAsync();
        var satirlar = kayitlar.Select(e => new object?[]
        {
            e.UyeSicilNo, e.Isletme, e.AdSoyad, e.Gorevi, e.SirketTipi, e.TicaretSicilNo,
            e.Grup is null ? null : (e.Grup.No is null ? e.Grup.Ad : $"{e.Grup.No}.{e.Grup.Ad}"),
            e.UyelikDurumu, e.DurumDegisimTarihi, e.DurumDegisimNedeni,
            e.VergiDairesi, e.VergiNo, e.KurulusTarihi, e.KayitTarihi,
            e.IsTelefonu, e.Telefon, e.Il, e.Ilce, e.Mahalle, e.Adres,
            e.NaceKodu, e.NaceAdi, e.FaaliyetDetayi, e.Gorevli?.AdSoyad, e.Durum,
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

        return Ok(await UyeIceAktarmaServisi.AktarAsync(db, satirlar));
    }
}
