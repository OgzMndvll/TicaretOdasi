using EtsoApi.Data;
using EtsoApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EtsoApi.Services;

/// <summary>
/// Odanın "ÜYE LİSTE DETAY RAPORU" Excel çıktısını üye kayıtlarına dönüştürür.
/// Hem API'nin içe aktarma ucu hem de toplu yükleme komutu bu servisi kullanır.
/// </summary>
public static class UyeIceAktarmaServisi
{
    public static readonly string[] OnayDurumlari = ["Onay Verdi", "Onay Vermedi", "Kararsız", "Görüşülmedi"];

    // Odadaki üyelik durumu. "Faal" ve "Askı" oda raporundan gelir; "Pasif" kaydı elde tutulan ayrılmış üyeler içindir.
    public static readonly string[] UyelikDurumlari = ["Faal", "Askı", "Pasif"];

    public static async Task<IceAktarmaSonucu> AktarAsync(
        EtsoDbContext db, List<(int SatirNo, Dictionary<string, string> Degerler)> satirlar)
    {
        var gruplar = await db.Gruplar.ToListAsync();
        var gorevliler = await db.Kullanicilar.ToListAsync();
        var sicilIndeksi = await db.Esnaflar.Where(e => e.UyeSicilNo != null)
            .ToDictionaryAsync(e => e.UyeSicilNo!, e => e);
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

        // "YUSUF KARATAŞ , ÖMER KARATAŞ" gibi çoklu yetkili listeleri ayrı kişi kayıtlarına dönüştürür.
        static List<string> YetkilileriAyir(string? ham) =>
            string.IsNullOrWhiteSpace(ham)
                ? []
                : ham.Split(',', ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

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

        // Oda raporunda iki tarih biçimi bir arada geçer: "20/12/1978" ve "09 05 1979". Boşlar null döner.
        static DateTime? TarihOku(string? ham)
        {
            if (string.IsNullOrWhiteSpace(ham)) return null;
            // "dd/MM/yyyy" ve "dd MM yyyy" kaynak oda raporundan; ISO biçimler panelin kendi dışa
            // aktarımından geri yüklemede gelir (bkz. ExcelServisi.HucreMetni).
            string[] bicimler =
            [
                "dd/MM/yyyy", "dd MM yyyy", "dd.MM.yyyy", "d/M/yyyy", "d M yyyy", "d.M.yyyy",
                "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm",
            ];
            return DateTime.TryParseExact(ham.Trim(), bicimler, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var tarih) ? tarih : null;
        }

        // Meslek grubu iki biçimde gelir:
        //   "13.HIRDAVAT ÜRÜNLERİNİN..."
        //   "--- (Askı durumundaki üyenin mevcut meslek grubu:15.TEKSTİL ÜRÜNLERİNİN...)"
        // İkisi de aynı gruba (numara + ad) indirgenir.
        static (int? No, string? Ad) MeslekGrubuCoz(string? ham)
        {
            if (string.IsNullOrWhiteSpace(ham)) return (null, null);
            var metin = ham.Trim();
            var askili = System.Text.RegularExpressions.Regex.Match(metin, @"mevcut meslek grubu:\s*(.*?)\)\s*$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
            if (askili.Success) metin = askili.Groups[1].Value.Trim();
            if (metin.Length == 0 || metin.StartsWith("---")) return (null, null);

            var numarali = System.Text.RegularExpressions.Regex.Match(metin, @"^(\d{1,3})\s*\.\s*(.+)$",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            if (!numarali.Success) return (null, metin);
            var no = int.Parse(numarali.Groups[1].Value);
            var ad = numarali.Groups[2].Value.Trim();
            // Yeni kısa listede grup hücresi yalnızca "1. GRUP" biçiminde; burada "GRUP" bir ad
            // değil etikettir. Mevcut açıklayıcı grup adı korunur, yoksa numaralı benzersiz ad üretilir.
            if (string.Equals(ad, "GRUP", StringComparison.CurrentCultureIgnoreCase)) ad = "";
            return (no, string.IsNullOrWhiteSpace(ad) ? null : ad);
        }

        Esnaf? sonEsnaf = null;

        foreach (var (satirNo, degerler) in satirlar)
        {
            var isletme = Kirp(Al(degerler, "İşletme", "Unvan", "Ünvan", "Firma Unvanı", "Ticaret Unvanı"), 250);
            var yetkiliHam = Al(degerler, "Yetkili Adı Soyadı/Unvan", "Yetkili Adı Soyadı", "Ad Soyad", "Yetkili", "İsim Soyisim");
            var yetkiliAdlari = YetkilileriAyir(yetkiliHam)
                .Select(x => Kirp(BaslikYap(x), 120))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .ToList();
            var yetkiliAdi = yetkiliAdlari.FirstOrDefault();
            var sicilNo = Kirp(Al(degerler, "Üye Sicil No", "Uye Sicil No", "Sicil No"), 20);
            var kaynakSiraNo = Al(degerler, "S.N.", "Sıra No", "Sira No");

            // Oda raporunda bir üyenin ikinci ve sonraki yetkilileri, üye alanları boş bırakılmış
            // ve sıra numarası taşımayan devam satırları olarak gelir. Bu satırlar hata değildir;
            // önceki üyenin yetkilisi olarak eklenir.
            if (isletme is null && sicilNo is null && kaynakSiraNo is null)
            {
                if (sonEsnaf is not null && yetkiliAdi is not null)
                    sonEsnaf.Yetkililer.Add(new EsnafYetkili
                    {
                        AdSoyad = yetkiliAdi,
                        Gorevi = Kirp(BaslikYap(Al(degerler, "Görevi", "Gorevi", "Yetkili Görevi")), 80),
                        YetkiBaslangic = TarihOku(Al(degerler, "Yetki Başlangıç Tarihi")),
                        YetkiBitis = TarihOku(Al(degerler, "Yetki Bitiş Tarihi")),
                    });
                else
                    atlanan++;
                continue;
            }

            // Kısa oda listesinde tekil bir satırın unvanı boş bırakılmış olabilir. Sıra numarası
            // bulunan böyle bir satırı kaybetmemek için şahıs yetkilisinin adı şirket/unvan olarak kullanılır.
            if (isletme is null && kaynakSiraNo is not null && yetkiliAdi is not null)
                isletme = yetkiliAdi;

            if (isletme is null)
            {
                hatalar.Add($"Satır {satirNo}: İşletme/unvan bilgisi bulunamadı.");
                continue;
            }

            // Üye sicil no oda kayıtlarında tekildir; tekrar yüklemede kayıt ikizlenmez.
            if (sicilNo is not null && sicilIndeksi.ContainsKey(sicilNo))
            {
                sonEsnaf = sicilIndeksi[sicilNo];
                atlanan++;
                continue;
            }

            // Sicil no yoksa ad+unvan ikilisi tekrar denetimi yapar (elle hazırlanmış listeler için).
            var adSoyad = Kirp(yetkiliAdi ?? isletme, 120)!;
            if (sicilNo is null && !mevcutlar.Add($"{adSoyad}|{isletme}".ToLowerInvariant()))
            {
                atlanan++;
                continue;
            }

            var (grupNo, grupAdi) = MeslekGrubuCoz(Al(degerler, "Meslek Grubu", "Grup", "Meslek Komitesi"));
            Grup? grup = null;
            if (grupNo is not null || grupAdi is not null)
            {
                grup = grupNo is not null
                    ? gruplar.FirstOrDefault(g => g.No == grupNo)
                    : gruplar.FirstOrDefault(g => string.Equals(g.Ad, grupAdi, StringComparison.OrdinalIgnoreCase));
                if (grup is null)
                {
                    grup = new Grup
                    {
                        No = grupNo,
                        Ad = Kirp(grupAdi ?? $"{grupNo}. Meslek Grubu", 180)!,
                        Aciklama = "Oda üye raporundan oluşturuldu",
                    };
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

            var durum = Al(degerler, "Onay Durumu", "Durum", "Görüşme Durumu") ?? "Görüşülmedi";
            if (!OnayDurumlari.Contains(durum))
            {
                hatalar.Add($"Satır {satirNo}: '{durum}' geçersiz onay durumu, 'Görüşülmedi' olarak kaydedildi.");
                durum = "Görüşülmedi";
            }

            var uyelikHam = Al(degerler, "Üyelik Durumu", "Durum Tanımı", "Uyelik Durumu", "Durumu") ?? "Faal";
            var uyelikDurumu = UyelikDurumlari.FirstOrDefault(x =>
                string.Equals(x, uyelikHam, StringComparison.CurrentCultureIgnoreCase));
            if (uyelikDurumu is null)
            {
                hatalar.Add($"Satır {satirNo}: '{uyelikHam}' geçersiz üyelik durumu, 'Faal' olarak kaydedildi.");
                uyelikDurumu = "Faal";
            }

            var adres = Al(degerler, "Adres", "Tescil Adresi");
            var esnaf = new Esnaf
            {
                UyeSicilNo = sicilNo,
                TicaretSicilNo = Kirp(Al(degerler, "Ticaret Sicil No"), 30),
                SirketTipi = Kirp(Al(degerler, "Şirket Tipi", "Sirket Tipi"), 60),
                TabelaUnvani = Kirp(Al(degerler, "Tabela Unvanı"), 160),
                Uyruk = Kirp(Al(degerler, "Uyruk"), 40),
                Sermaye = Kirp(Al(degerler, "Sermaye"), 30),
                Derece = Kirp(Al(degerler, "Derece"), 20),
                VergiDairesi = Kirp(Al(degerler, "Vergi Dairesi"), 80),
                VergiTerkTarihi = TarihOku(Al(degerler, "Vergi Terk Tarihi")),
                KurulusTarihi = TarihOku(Al(degerler, "Kuruluş Tarihi", "Kurulus Tarihi")),
                OdaKararTarihi = TarihOku(Al(degerler, "Üye Oda Karar Tarihi", "Uye Oda Karar Tarihi")),
                UyelikDurumu = uyelikDurumu,
                DurumDegisimTarihi = TarihOku(Al(degerler, "Durum Değişim Tarihi", "Durum Degisim Tarihi")),
                DurumDegisimNedeni = Kirp(Al(degerler, "Durum Değişim Nedeni", "Durum Degisim Nedeni"), 200),
                FaaliyetDetayi = Kirp(Al(degerler, "Faaliyet Detayı", "Faaliyet Detayi"), 2000),
                NaceKodu = Kirp(Al(degerler, "NACE Faaliyet Kodu", "NACE Kodu"), 20),
                NaceAdi = Kirp(Al(degerler, "NACE Faaliyet Adı", "NACE Faaliyet Adi"), 500),
                AdSoyad = adSoyad,
                Isletme = isletme,
                Gorevi = Kirp(BaslikYap(Al(degerler, "Görevi", "Gorevi", "Yetkili Görevi")), 80),
                Telefon = TelefonDuzenle(Al(degerler, "Cep Telefonu (GSM)", "Cep Telefonu", "GSM", "Telefon")),
                IsTelefonu = TelefonDuzenle(Al(degerler, "İş Telefonu", "Is Telefonu")),
                Grup = grup,
                Gorevli = gorevli,
                Il = Al(degerler, "İl") ?? "Erzurum",
                Ilce = BaslikYap(Al(degerler, "İlçe") ?? AdrestenIlce(adres)),
                Mahalle = Kirp(BaslikYap(Al(degerler, "Mahalle") ?? AdrestenMahalle(adres)), 80),
                Adres = Kirp(adres, 500),
                VergiNo = Kirp(Al(degerler, "Vergi No", "Vergi Numarası"), 20),
                Durum = durum,
                KayitTarihi = TarihOku(Al(degerler, "Üye Kayıt Tarihi", "Uye Kayit Tarihi", "Kayıt Tarihi")) ?? DateTime.UtcNow,
            };

            // Kaynak satırdaki bütün yetkililer şirket kartındaki yetkili listesine yazılır.
            foreach (var ad in yetkiliAdlari)
                esnaf.Yetkililer.Add(new EsnafYetkili
                {
                    AdSoyad = ad,
                    Gorevi = esnaf.Gorevi,
                    YetkiBaslangic = TarihOku(Al(degerler, "Yetki Başlangıç Tarihi")),
                    YetkiBitis = TarihOku(Al(degerler, "Yetki Bitiş Tarihi")),
                });

            db.Esnaflar.Add(esnaf);
            if (sicilNo is not null) sicilIndeksi[sicilNo] = esnaf;
            sonEsnaf = esnaf;
            eklenen++;
        }
        await db.SaveChangesAsync();
        return new IceAktarmaSonucu(eklenen, atlanan, hatalar);
    }
}
