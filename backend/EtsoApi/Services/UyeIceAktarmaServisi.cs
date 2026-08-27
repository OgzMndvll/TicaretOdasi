using System.Text;
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
    // Görüşme sonucu seçenekleri + hiç görüşülmemiş üyeler için "Görüşülmedi".
    public static readonly string[] OnayDurumlari =
        [.. Controllers.GorusmelerController.GecerliSonuclar, "Görüşülmedi"];

    // Odadaki üyelik durumu. "Faal" ve "Askı" oda raporundan gelir; "Pasif" kaydı elde tutulan ayrılmış üyeler içindir.
    public static readonly string[] UyelikDurumlari = ["Faal", "Askı", "Pasif"];

    public static async Task<IceAktarmaSonucu> AktarAsync(
        EtsoDbContext db, List<(int SatirNo, Dictionary<string, string> Degerler)> satirlar)
    {
        var gruplar = await db.Gruplar.ToListAsync();
        var gorevliler = await db.Kullanicilar.ToListAsync();
        // Mevcut üyeler kayıt nesnesiyle indekslenir: eşleşen satır artık yalnızca atlanmıyor,
        // değişen alanları güncelleniyor. Yetkililer de yükleniyor; liste değişmişse yenileniyor.
        // TryAdd: aynı anahtara düşen iki kayıt varsa aktarım çökmek yerine ilkini kullanır.
        var tumEsnaflar = await db.Esnaflar.Include(e => e.Yetkililer).ToListAsync();
        var sicilIndeksi = new Dictionary<string, Esnaf>();
        var adIndeksi = new Dictionary<string, Esnaf>();
        foreach (var kayit in tumEsnaflar)
        {
            if (!string.IsNullOrWhiteSpace(kayit.UyeSicilNo)) sicilIndeksi.TryAdd(kayit.UyeSicilNo!, kayit);
            adIndeksi.TryAdd(TekrarAnahtari(kayit.AdSoyad, kayit.Isletme), kayit);
        }

        var hatalar = new List<string>();
        var eklenen = 0;
        // Yalnızca sahipsiz/kullanılamayan satırları sayar; mevcut üyelerin dökümü sonda hesaplanır.
        var atlanan = 0;
        var dokunulan = new HashSet<int>();  // dosyada karşılaşılan mevcut üyeler
        var degisen = new HashSet<int>();    // en az bir alanı gerçekten değişenler
        // Mevcut üyeler için dosyadan derlenen yetkili listesi; döngü bitince mevcutla karşılaştırılır.
        var yeniYetkililer = new Dictionary<int, List<EsnafYetkili>>();

        // Kaynak satırda değer varsa ve mevcuttan farklıysa yazar. Boş bırakılmış bir sütun
        // mevcut veriyi SİLMEZ: oda raporları çoğu zaman kolonların bir kısmını taşımıyor.
        static bool Metin(string? yeni, string? simdiki, Action<string> yaz)
        {
            if (string.IsNullOrWhiteSpace(yeni) || string.Equals(yeni, simdiki, StringComparison.Ordinal)) return false;
            yaz(yeni);
            return true;
        }

        static bool Zaman(DateTime? yeni, DateTime? simdiki, Action<DateTime> yaz)
        {
            // Kaynak dosya yalnızca günü taşır; saat farkı değişiklik sayılmaz. Aksi halde
            // (ör. KayitTarihi'nde saat var) her aktarım tüm üyeleri "güncellendi" gösterirdi.
            if (yeni is null || yeni.Value.Date == simdiki?.Date) return false;
            yaz(yeni.Value);
            return true;
        }

        // Ad ve unvan için: yalnızca gerçekten farklı bir metinse yazılır. Büyük/küçük harf ve
        // Türkçe "İ" farkı değişiklik sayılmaz — dosya panelin kendi dışa aktarımı olabilir ve
        // başlık biçimlendirmesi metni birebir geri getirmez.
        static bool MetinNormal(string? yeni, string? simdiki, Action<string> yaz)
        {
            if (string.IsNullOrWhiteSpace(yeni) || Anahtarla(yeni) == Anahtarla(simdiki)) return false;
            yaz(yeni);
            return true;
        }

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
            // Hem üye satırı hem devam satırı bu üç alanı kullanır.
            var gorevAdi = Kirp(BaslikYap(Al(degerler, "Görevi", "Gorevi", "Yetkili Görevi")), 80);
            var yetkiBaslangic = TarihOku(Al(degerler, "Yetki Başlangıç Tarihi"));
            var yetkiBitis = TarihOku(Al(degerler, "Yetki Bitiş Tarihi"));

            // Oda raporunda bir üyenin ikinci ve sonraki yetkilileri, üye alanları boş bırakılmış
            // ve sıra numarası taşımayan devam satırları olarak gelir. Bu satırlar hata değildir;
            // önceki üyenin yetkilisi olarak eklenir.
            if (isletme is null && sicilNo is null && kaynakSiraNo is null)
            {
                if (sonEsnaf is not null && yetkiliAdi is not null)
                {
                    var yetkili = new EsnafYetkili
                    {
                        AdSoyad = yetkiliAdi, Gorevi = gorevAdi,
                        YetkiBaslangic = yetkiBaslangic, YetkiBitis = yetkiBitis,
                    };
                    // Yeni kayıtta doğrudan listeye; mevcut kayıtta ise ayrı derlenir ve döngü
                    // sonunda karşılaştırılır. Aksi halde her aktarımda yetkililer üst üste binerdi.
                    if (sonEsnaf.Id == 0) sonEsnaf.Yetkililer.Add(yetkili);
                    else yeniYetkililer[sonEsnaf.Id].Add(yetkili);
                }
                else atlanan++;
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

            // Mevcut üye aranır: önce oda kayıtlarında tekil olan sicil no, o yoksa ad+unvan.
            var adSoyad = Kirp(yetkiliAdi ?? isletme, 120)!;
            var adAnahtari = TekrarAnahtari(adSoyad, isletme);
            Esnaf? mevcut = null;
            if (sicilNo is not null) sicilIndeksi.TryGetValue(sicilNo, out mevcut);
            if (mevcut is null) adIndeksi.TryGetValue(adAnahtari, out mevcut);

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

            // Onay durumu YALNIZCA yeni kayıtta kullanılır. Mevcut üyede bu alan görüşmelerden
            // türetiliyor; oda raporunda böyle bir kolon olmadığı için güncellemede yazılsaydı
            // her aktarım tüm üyeleri "Görüşülmedi"ye çevirip görüşme geçmişiyle çelişirdi.
            var durum = Al(degerler, "Onay Durumu", "Durum", "Görüşme Durumu") ?? "Görüşülmedi";
            if (!OnayDurumlari.Contains(durum))
            {
                hatalar.Add($"Satır {satirNo}: '{durum}' geçersiz onay durumu, 'Görüşülmedi' olarak kaydedildi.");
                durum = "Görüşülmedi";
            }

            var uyelikHam = Al(degerler, "Üyelik Durumu", "Durum Tanımı", "Uyelik Durumu", "Durumu");
            string? uyelikDurumu = null;
            if (uyelikHam is not null)
            {
                uyelikDurumu = UyelikDurumlari.FirstOrDefault(x =>
                    string.Equals(x, uyelikHam, StringComparison.CurrentCultureIgnoreCase));
                if (uyelikDurumu is null)
                    hatalar.Add($"Satır {satirNo}: '{uyelikHam}' geçersiz üyelik durumu, alan değiştirilmedi.");
            }

            var adres = Al(degerler, "Adres", "Tescil Adresi");
            var ilce = BaslikYap(Al(degerler, "İlçe") ?? AdrestenIlce(adres));
            var mahalle = Kirp(BaslikYap(Al(degerler, "Mahalle") ?? AdrestenMahalle(adres)), 80);

            // ---- Mevcut üye: ikizlenmez, değişen alanları güncellenir ----
            if (mevcut is not null)
            {
                dokunulan.Add(mevcut.Id);
                var d = false;
                d |= MetinNormal(isletme, mevcut.Isletme, v => mevcut.Isletme = v);
                d |= MetinNormal(adSoyad, mevcut.AdSoyad, v => mevcut.AdSoyad = v);
                d |= Metin(gorevAdi, mevcut.Gorevi, v => mevcut.Gorevi = v);
                d |= Metin(sicilNo, mevcut.UyeSicilNo, v => mevcut.UyeSicilNo = v);
                d |= Metin(Kirp(Al(degerler, "Ticaret Sicil No"), 30), mevcut.TicaretSicilNo, v => mevcut.TicaretSicilNo = v);
                d |= Metin(Kirp(Al(degerler, "Şirket Tipi", "Sirket Tipi"), 60), mevcut.SirketTipi, v => mevcut.SirketTipi = v);
                d |= Metin(Kirp(Al(degerler, "Tabela Unvanı"), 160), mevcut.TabelaUnvani, v => mevcut.TabelaUnvani = v);
                d |= Metin(Kirp(Al(degerler, "Uyruk"), 40), mevcut.Uyruk, v => mevcut.Uyruk = v);
                d |= Metin(Kirp(Al(degerler, "Sermaye"), 30), mevcut.Sermaye, v => mevcut.Sermaye = v);
                d |= Metin(Kirp(Al(degerler, "Derece"), 20), mevcut.Derece, v => mevcut.Derece = v);
                d |= Metin(Kirp(Al(degerler, "Vergi Dairesi"), 80), mevcut.VergiDairesi, v => mevcut.VergiDairesi = v);
                d |= Metin(Kirp(Al(degerler, "Vergi No", "Vergi Numarası"), 20), mevcut.VergiNo, v => mevcut.VergiNo = v);
                d |= Metin(uyelikDurumu, mevcut.UyelikDurumu, v => mevcut.UyelikDurumu = v);
                d |= Metin(Kirp(Al(degerler, "Durum Değişim Nedeni", "Durum Degisim Nedeni"), 200), mevcut.DurumDegisimNedeni, v => mevcut.DurumDegisimNedeni = v);
                d |= Metin(Kirp(Al(degerler, "Faaliyet Detayı", "Faaliyet Detayi"), 2000), mevcut.FaaliyetDetayi, v => mevcut.FaaliyetDetayi = v);
                d |= Metin(Kirp(Al(degerler, "NACE Faaliyet Kodu", "NACE Kodu"), 20), mevcut.NaceKodu, v => mevcut.NaceKodu = v);
                d |= Metin(Kirp(Al(degerler, "NACE Faaliyet Adı", "NACE Faaliyet Adi"), 500), mevcut.NaceAdi, v => mevcut.NaceAdi = v);
                d |= Metin(TelefonDuzenle(Al(degerler, "Cep Telefonu (GSM)", "Cep Telefonu", "GSM", "Telefon")), mevcut.Telefon, v => mevcut.Telefon = v);
                d |= Metin(TelefonDuzenle(Al(degerler, "İş Telefonu", "Is Telefonu")), mevcut.IsTelefonu, v => mevcut.IsTelefonu = v);
                d |= Metin(Al(degerler, "İl"), mevcut.Il, v => mevcut.Il = v);
                d |= Metin(ilce, mevcut.Ilce, v => mevcut.Ilce = v);
                d |= Metin(mahalle, mevcut.Mahalle, v => mevcut.Mahalle = v);
                d |= Metin(Kirp(adres, 500), mevcut.Adres, v => mevcut.Adres = v);
                d |= Zaman(TarihOku(Al(degerler, "Vergi Terk Tarihi")), mevcut.VergiTerkTarihi, v => mevcut.VergiTerkTarihi = v);
                d |= Zaman(TarihOku(Al(degerler, "Kuruluş Tarihi", "Kurulus Tarihi")), mevcut.KurulusTarihi, v => mevcut.KurulusTarihi = v);
                d |= Zaman(TarihOku(Al(degerler, "Üye Oda Karar Tarihi", "Uye Oda Karar Tarihi")), mevcut.OdaKararTarihi, v => mevcut.OdaKararTarihi = v);
                d |= Zaman(TarihOku(Al(degerler, "Durum Değişim Tarihi", "Durum Degisim Tarihi")), mevcut.DurumDegisimTarihi, v => mevcut.DurumDegisimTarihi = v);
                d |= Zaman(TarihOku(Al(degerler, "Üye Kayıt Tarihi", "Uye Kayit Tarihi", "Kayıt Tarihi")), mevcut.KayitTarihi, v => mevcut.KayitTarihi = v);
                // Meslek grubu ve görevli yalnızca dosyada gerçekten çözülebildiyse yazılır.
                // Grup nesnesi değil kimliği karşılaştırılır: gezinme özelliği yüklenmediği için
                // referans karşılaştırması her satırı "değişti" sayardı.
                if (grup is not null && grup.Id != mevcut.GrupId) { mevcut.Grup = grup; d = true; }
                if (gorevli is not null && gorevli.Id != mevcut.GorevliId) { mevcut.Gorevli = gorevli; d = true; }
                // Onay durumu (Durum) ve SonGorusmeTarihi bilinçli olarak DIŞARIDA: panelin
                // saha verisi bunlar, oda raporu bunları taşımıyor.

                if (d) degisen.Add(mevcut.Id);
                yeniYetkililer.TryAdd(mevcut.Id, []);
                foreach (var ad in yetkiliAdlari)
                    yeniYetkililer[mevcut.Id].Add(new EsnafYetkili
                    {
                        AdSoyad = ad, Gorevi = gorevAdi,
                        YetkiBaslangic = yetkiBaslangic, YetkiBitis = yetkiBitis,
                    });
                sonEsnaf = mevcut;
                continue;
            }

            // ---- Yeni üye ----
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
                UyelikDurumu = uyelikDurumu ?? "Faal",
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
                Ilce = ilce,
                Mahalle = mahalle,
                Adres = Kirp(adres, 500),
                VergiNo = Kirp(Al(degerler, "Vergi No", "Vergi Numarası"), 20),
                Durum = durum,
                KayitTarihi = TarihOku(Al(degerler, "Üye Kayıt Tarihi", "Uye Kayit Tarihi", "Kayıt Tarihi")) ?? DateTime.UtcNow,
            };

            // Kaynak satırdaki bütün yetkililer şirket kartındaki yetkili listesine yazılır.
            foreach (var ad in yetkiliAdlari)
                esnaf.Yetkililer.Add(new EsnafYetkili
                {
                    AdSoyad = ad, Gorevi = esnaf.Gorevi,
                    YetkiBaslangic = yetkiBaslangic, YetkiBitis = yetkiBitis,
                });

            db.Esnaflar.Add(esnaf);
            if (sicilNo is not null) sicilIndeksi.TryAdd(sicilNo, esnaf);
            adIndeksi.TryAdd(adAnahtari, esnaf);
            sonEsnaf = esnaf;
            eklenen++;
        }

        // Mevcut üyelerin yetkili listesi YALNIZCA genişletilir: dosyada olup listede olmayan
        // isimler eklenir, hiçbir kayıt silinmez.
        //
        // Silme bilinçli olarak yapılmıyor. Panelin kendi dışa aktarımı üye başına tek satır ve
        // tek yetkili adı üretiyor; "dosya tam listedir" varsayımıyla değiştirmek, 37 yetkilisi
        // olan bir şubeyi tek yetkiliye indirirdi. Fazlalık bir ad kalması, veri kaybından iyidir.
        foreach (var (esnafId, gelenler) in yeniYetkililer)
        {
            if (gelenler.Count == 0) continue;
            var kayit = tumEsnaflar.First(e => e.Id == esnafId);
            var bilinenler = kayit.Yetkililer.Select(y => Anahtarla(y.AdSoyad)).ToHashSet();
            // Gerçek yetkilisi olmayan üyelerde "Yetkili Adı Soyadı" sütunu unvanın kendisini
            // taşır (kaynak raporda ad boşsa unvana düşülüyor). Bunu kişi sanıp eklemeyiz:
            // aksi halde her aktarım bu üyelere unvan adında sahte bir yetkili yazardı.
            bilinenler.Add(Anahtarla(kayit.Isletme));
            // Ad alanı 120 karaktere kırpıldığı için uzun unvanlarda kırpılmış hâli de eşleşmeli.
            bilinenler.Add(Anahtarla(Kirp(kayit.Isletme, 120)));
            foreach (var yetkili in gelenler.Where(y => bilinenler.Add(Anahtarla(y.AdSoyad))))
            {
                kayit.Yetkililer.Add(yetkili);
                degisen.Add(esnafId);
            }
        }

        await db.SaveChangesAsync();
        // Dosyada görülüp hiçbir alanı değişmeyen üyeler de "atlanan" sayılır.
        atlanan += dokunulan.Count - degisen.Count(id => dokunulan.Contains(id));
        return new IceAktarmaSonucu(eklenen, atlanan, hatalar, degisen.Count);
    }

    /// <summary>
    /// Tekrar denetiminde kullanılan "ad|unvan" anahtarı.
    ///
    /// Düz <c>ToLowerInvariant</c> yetmiyordu: Türkçe "İ" harfi invariant küçültmede
    /// "i" + U+0307 (birleşen nokta) üretir, oysa aynı metin <c>BaslikYap</c>'tan geçtiğinde
    /// harf düz "i" olur. Görünmeyen bu fark yüzünden veritabanında zaten bulunan bir üye
    /// "yeni" sanılıp ikizleniyordu — ör. "BALTAŞ TURİZM ..." kaydı, aynı dosya ikinci kez
    /// yüklendiğinde ikinci bir satır olarak ekleniyordu.
    ///
    /// Anahtar bu yüzden birleşen nokta atılarak ve noktalı/noktasız i ayrımı kaldırılarak
    /// üretilir. Diğer Türkçe harfler (ş, ğ, ü, ö, ç) bilerek korunur: onlar büyük/küçük
    /// dönüşümünde kararlıdır ve farklı üyeleri birbirine karıştırmamak gerekir.
    /// </summary>
    private static string TekrarAnahtari(string? adSoyad, string? isletme) =>
        $"{Anahtarla(adSoyad)}|{Anahtarla(isletme)}";

    private static string Anahtarla(string? metin)
    {
        if (string.IsNullOrWhiteSpace(metin)) return "";
        var yazi = new StringBuilder(metin.Length);
        foreach (var harf in metin.Normalize(NormalizationForm.FormD))
        {
            if (harf == '\u0307') continue; // "İ" ayrıştığında geriye kalan birleşen nokta
            yazi.Append(harf is 'ı' or 'I' ? 'i' : char.ToLowerInvariant(harf));
        }
        // Kalan birleşen işaretler (ş, ğ, ü…) geri birleştirilir; boşluk farkları sadeleştirilir.
        var birlesik = yazi.ToString().Normalize(NormalizationForm.FormC);
        return string.Join(' ', birlesik.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
