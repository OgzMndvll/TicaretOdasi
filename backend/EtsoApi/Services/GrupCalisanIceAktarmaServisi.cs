using EtsoApi.Data;
using EtsoApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EtsoApi.Services;

/// <summary>
/// Odanın "ETSO GRUPLAR" listesini (her satır bir meslek grubu, sütunlarda o gruba bakan
/// 1–3 kişi) aktif çalışan kayıtlarına ve çalışan–grup bağlarına çevirir.
///
/// Dosya düzeni:
///   | 1. Üye | 2. Üye | 3. Üye | Grup      | Faaliyet Alanı              |
///   | AD SOYAD | AD SOYAD |     | 1. GRUP | ET, SÜT, MEYVE, SEBZE …    |
///
/// Aktarım tekrar edilebilir: aynı dosya ikinci kez yüklendiğinde yeni kayıt açılmaz,
/// yalnızca eksik bağlar tamamlanır.
/// </summary>
public static class GrupCalisanIceAktarmaServisi
{
    /// <summary>Kişi sütunları. Sıra bilgisi <see cref="KullaniciGrup.Sira"/> alanına yazılır.</summary>
    private static readonly string[] UyeSutunlari = ["1. Üye", "2. Üye", "3. Üye"];

    /// <summary>Bu düzendeki bir dosya mı? En az bir kişi sütunu ve "Grup" sütunu aranır.</summary>
    public static bool DosyaBuDuzendeMi(IReadOnlyList<(int SatirNo, Dictionary<string, string> Degerler)> satirlar)
    {
        if (satirlar.Count == 0) return false;
        var basliklar = satirlar[0].Degerler.Keys.ToHashSet();
        return basliklar.Contains(ExcelServisi.Normalize("Grup"))
            && UyeSutunlari.Any(s => basliklar.Contains(ExcelServisi.Normalize(s)));
    }

    public static async Task<IceAktarmaSonucu> AktarAsync(
        EtsoDbContext db, IReadOnlyList<(int SatirNo, Dictionary<string, string> Degerler)> satirlar)
    {
        var hatalar = new List<string>();
        var eklenen = 0;
        // Sayaçlar kişi başına tutulur: bir kişi iki gruba birden eklense de "1 güncellenen"dir.
        var degisenKisiler = new HashSet<int>();
        var degismeyenKisiler = new HashSet<int>();
        var atlananSatir = 0;

        // ---- Referans veriler ----
        var gruplar = await db.Gruplar.ToListAsync();
        // Eşleştirmenin birincil anahtarı odanın grup numarasıdır ("12. GRUP" → Gruplar.No = 12).
        var grupNoIndeksi = new Dictionary<int, Grup>();
        foreach (var g in gruplar) if (g.No is not null) grupNoIndeksi.TryAdd(g.No.Value, g);
        // Numara okunamazsa ada göre yedek eşleşme denenir. Adlar tekil indekslidir ama
        // sadeleştirme sonrası iki farklı ad aynı anahtara düşebilir; ilk kayıt kazanır.
        var grupAdIndeksi = new Dictionary<string, Grup>();
        foreach (var g in gruplar) grupAdIndeksi.TryAdd(Anahtar(g.Ad), g);

        var kullanicilar = await db.Kullanicilar.ToListAsync();
        var kisiIndeksi = new Dictionary<string, Kullanici>();
        foreach (var k in kullanicilar) kisiIndeksi.TryAdd(Anahtar(k.AdSoyad), k);
        var kullanilanAdlar = kullanicilar.Select(k => k.KullaniciAdi).ToHashSet();

        var mevcutBaglar = (await db.KullaniciGruplari.ToListAsync())
            .ToDictionary(b => (b.KullaniciId, b.GrupId), b => b);
        // Dosya, içinde geçen kişiler için grup dağılımının tamamıdır: aktarım sonunda bu
        // kişilerin dosyada yer almayan eski bağları silinir. Aksi halde hatalı bir aktarımın
        // bıraktığı bağlar, dosya düzeltilip yeniden yüklense bile kalıcı olurdu.
        var dosyadakiBaglar = new HashSet<(int KullaniciId, int GrupId)>();

        foreach (var (satirNo, degerler) in satirlar)
        {
            var grupEtiketi = Al(degerler, "Grup");
            var faaliyet = Al(degerler, "Faaliyet Alanı");
            var grupNo = GrupNumarasi(grupEtiketi);

            var kisiler = UyeSutunlari
                .Select((sutun, i) => (Sira: i + 1, Ad: Al(degerler, sutun)))
                .Where(x => x.Ad is not null)
                .ToList();

            if (kisiler.Count == 0)
            {
                // Kişisiz satır (yalnızca grup tanımı) veri kaybı değildir; sessizce geçilir.
                atlananSatir++;
                continue;
            }
            if (faaliyet is null && grupEtiketi is null)
            {
                hatalar.Add($"Satır {satirNo}: 'Grup' ve 'Faaliyet Alanı' sütunlarının ikisi de boş; satır atlandı.");
                atlananSatir++;
                continue;
            }

            // ---- Grubu çöz ----
            // Kişiler daima sistemdeki mevcut meslek gruplarına bağlanır; dosyadan yeni grup
            // AÇILMAZ. Anahtar dosyadaki numaradır ("12. GRUP" → Gruplar.No = 12); numara yoksa
            // ya da o numarada grup yoksa faaliyet alanı adıyla yedek eşleşme denenir.
            var grup = grupNo is not null && grupNoIndeksi.TryGetValue(grupNo.Value, out var noyaGore)
                ? noyaGore
                : GrubuBul(grupAdIndeksi, faaliyet);
            if (grup is null)
            {
                hatalar.Add($"Satır {satirNo}: \"{grupEtiketi ?? faaliyet}\" sistemdeki meslek gruplarıyla " +
                            "eşleşmedi (numara da ad da bulunamadı); bu satırdaki kişiler gruba bağlanmadı.");
                atlananSatir++;
                continue;
            }

            // ---- Kişileri çöz ----
            foreach (var (sira, ad) in kisiler)
            {
                var adSoyad = Kisalt(ad!, 120);
                var kisiAnahtari = Anahtar(adSoyad);
                var yeniKisi = false;
                var degisti = false;
                if (!kisiIndeksi.TryGetValue(kisiAnahtari, out var kullanici))
                {
                    kullanici = new Kullanici
                    {
                        AdSoyad = adSoyad,
                        KullaniciAdi = BenzersizKullaniciAdi(adSoyad, kullanilanAdlar),
                        // Dosya odanın grup sorumlularını listeler: hepsi saha çalışanı olarak açılır,
                        // panele giriş yetkisi verilmez.
                        Rol = Kullanici.CalisanRolu,
                        Durum = "Aktif",
                    };
                    // Giriş yapamayacak kayıtlarda da özet rastgele üretilir; boş bırakılırsa
                    // "şifresiz kullanıcı" sayılıp açılışta yeniden şifre atanmaya çalışılırdı.
                    kullanici.SifreHash = Controllers.AuthController.Hashle(kullanici,
                        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
                    db.Kullanicilar.Add(kullanici);
                    kisiIndeksi[kisiAnahtari] = kullanici;
                    eklenen++;
                    yeniKisi = true;
                    // Bağ kurabilmek için Id gerekiyor.
                    await db.SaveChangesAsync();
                }
                else if (kullanici.Durum != "Aktif")
                {
                    // Dosya "aktif çalışanlar" listesidir: pasife alınmış bir kişi yeniden listedeyse
                    // kaydı yeniden etkinleştirilir.
                    kullanici.Durum = "Aktif";
                    degisti = true;
                }

                // ---- Bağı kur ----
                var anahtar = (kullanici.Id, grup.Id);
                dosyadakiBaglar.Add(anahtar);
                if (mevcutBaglar.TryGetValue(anahtar, out var bag))
                {
                    if (bag.Sira != sira) { bag.Sira = sira; degisti = true; }
                }
                else
                {
                    var yeni = new KullaniciGrup { KullaniciId = kullanici.Id, GrupId = grup.Id, Sira = sira };
                    db.KullaniciGruplari.Add(yeni);
                    mevcutBaglar[anahtar] = yeni;
                    degisti = true;
                }

                if (yeniKisi) continue;
                if (degisti) { degisenKisiler.Add(kullanici.Id); degismeyenKisiler.Remove(kullanici.Id); }
                else if (!degisenKisiler.Contains(kullanici.Id)) degismeyenKisiler.Add(kullanici.Id);
            }
        }

        // ---- Dosyadaki kişilerin fazla bağlarını temizle ----
        var dosyadakiKisiler = dosyadakiBaglar.Select(x => x.KullaniciId).ToHashSet();
        foreach (var bag in mevcutBaglar.Values
                     .Where(b => dosyadakiKisiler.Contains(b.KullaniciId)
                                 && !dosyadakiBaglar.Contains((b.KullaniciId, b.GrupId)))
                     .ToList())
        {
            db.KullaniciGruplari.Remove(bag);
            degisenKisiler.Add(bag.KullaniciId);
            degismeyenKisiler.Remove(bag.KullaniciId);
        }

        await db.SaveChangesAsync();
        return new IceAktarmaSonucu(eklenen, atlananSatir + degismeyenKisiler.Count, hatalar, degisenKisiler.Count);
    }

    private static string? Al(Dictionary<string, string> degerler, string baslik) =>
        degerler.TryGetValue(ExcelServisi.Normalize(baslik), out var v) && !string.IsNullOrWhiteSpace(v)
            ? v.Trim()
            : null;

    /// <summary>"12. GRUP" → 12. Grup eşleştirmesinin birincil anahtarı.</summary>
    private static int? GrupNumarasi(string? etiket)
    {
        if (etiket is null) return null;
        var rakamlar = new string(etiket.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(rakamlar, out var no) ? no : null;
    }

    /// <summary>
    /// Yedek eşleştirme: faaliyet alanı metnini odadaki grup adlarıyla eşler. Dosyadaki adlar
    /// odanın uzun adlarının kısaltılmış hâli olabildiği için (ör. "ET,SÜT,MEYVE,SEBZE, TAHIL V.B."
    /// → "TARIM … (ET,SÜT,MEYVE,SEBZE, TAHIL V.B.)") önce birebir, sonra içerme aranır.
    /// Yalnızca grup numarası okunamadığında devreye girer.
    /// </summary>
    private static Grup? GrubuBul(Dictionary<string, Grup> adIndeksi, string? faaliyet)
    {
        if (faaliyet is null) return null;
        var anahtar = Anahtar(faaliyet);
        if (anahtar.Length == 0) return null;
        if (adIndeksi.TryGetValue(anahtar, out var tam)) return tam;

        // İçerme eşleşmesi yalnızca anlamlı uzunlukta metinlerde güvenlidir; çok kısa bir
        // parça ("CAM") birçok grup adının içinde geçip yanlış eşleşme üretirdi.
        if (anahtar.Length < 12) return null;
        var adaylar = adIndeksi
            .Where(x => x.Key.Contains(anahtar) || anahtar.Contains(x.Key))
            .Select(x => x.Value)
            .ToList();
        // Birden çok aday varsa hangisinin kastedildiği belirsizdir; eşleşme sayılmaz.
        return adaylar.Count == 1 ? adaylar[0] : null;
    }

    /// <summary>Ad/başlık karşılaştırma anahtarı: Türkçe harfler sadeleşir, noktalama ve boşluk düşer.</summary>
    private static string Anahtar(string metin) => ExcelServisi.Normalize(metin)
        .Replace(",", "").Replace("(", "").Replace(")", "").Replace("'", "").Replace("\"", "");

    private static string Kisalt(string metin, int uzunluk) =>
        metin.Length <= uzunluk ? metin : metin[..uzunluk];

    /// <summary>
    /// "İDRİS AKDEMİR" → "idris.akdemir". <c>ToLowerInvariant</c> tek başına yetmez:
    /// 'İ' (U+0130) küçültülürken 'i' + birleşen nokta (U+0307) üretir ve bu görünmez
    /// karakter tekil indeksli kullanıcı adına sızardı. Türkçe harfler bu yüzden önce
    /// tek tek sadeleştirilir.
    /// </summary>
    public static string KullaniciAdiUret(string adSoyad)
    {
        var sade = adSoyad.Trim()
            .Replace('İ', 'i').Replace('I', 'i').Replace('ı', 'i')
            .Replace('Ç', 'c').Replace('Ğ', 'g').Replace('Ö', 'o').Replace('Ş', 's').Replace('Ü', 'u')
            .ToLowerInvariant()
            .Replace("ç", "c").Replace("ğ", "g").Replace("\u0307", "")
            .Replace("ö", "o").Replace("ş", "s").Replace("ü", "u");
        var parcalar = sade.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => new string(p.Where(c => char.IsLetterOrDigit(c)).ToArray()))
            .Where(p => p.Length > 0);
        var ad = string.Join(".", parcalar);
        return ad.Length == 0 ? "kullanici" : Kisalt(ad, 55);
    }

    /// <summary>Aynı ada sahip iki kişi varsa kullanıcı adının sonuna sıra eklenir (ad.soyad2).</summary>
    private static string BenzersizKullaniciAdi(string adSoyad, HashSet<string> kullanilan)
    {
        var taban = KullaniciAdiUret(adSoyad);
        var aday = taban;
        var sayac = 1;
        while (!kullanilan.Add(aday))
        {
            sayac++;
            aday = $"{taban}{sayac}";
        }
        return aday;
    }
}
