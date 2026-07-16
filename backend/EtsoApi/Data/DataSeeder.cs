using EtsoApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EtsoApi.Data;

/// <summary>
/// Boş veritabanını, frontend prototipindeki örnek kayıtlarla tutarlı,
/// gerçekçi hacimde başlangıç verisiyle doldurur. Veritabanında kayıt varsa dokunmaz.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(EtsoDbContext db)
    {
        if (await db.Gruplar.AnyAsync()) return;

        var rnd = new Random(2026);

        var gruplar = new List<Grup>
        {
            new() { Ad = "Otomotiv", Aciklama = "Otomotiv ve yan sanayi" },
            new() { Ad = "Giyim", Aciklama = "Giyim ve tekstil ürünleri" },
            new() { Ad = "Market", Aciklama = "Market ve bakkallar" },
            new() { Ad = "İnşaat", Aciklama = "İnşaat ve yapı malzemeleri" },
            new() { Ad = "Elektronik", Aciklama = "Elektronik ve beyaz eşya" },
            new() { Ad = "Mobilya", Aciklama = "Mobilya ve dekorasyon" },
            new() { Ad = "Yiyecek & İçecek", Aciklama = "Yiyecek, içecek ve restoran", Durum = "Pasif" },
            new() { Ad = "Sağlık", Aciklama = "Sağlık ve medikal" },
            new() { Ad = "Kırtasiye", Aciklama = "Kırtasiye ve ofis malzemeleri" },
            new() { Ad = "Tekstil", Aciklama = "Tekstil imalatı" },
        };
        db.Gruplar.AddRange(gruplar);

        var kullanicilar = new List<Kullanici>
        {
            new() { AdSoyad = "Ahmet Yılmaz", KullaniciAdi = "ahmet.yilmaz", Rol = "Yönetici", Gorev = "Sistem Yöneticisi", Birim = "Bilgi İşlem", Eposta = "ahmet.yilmaz@erzto.org.tr", Telefon = "0532 123 45 67" },
            new() { AdSoyad = "Fatma Demir", KullaniciAdi = "fatma.demir", Rol = "Görevli", Gorev = "Dış İlişkiler Uzmanı", Birim = "Dış İlişkiler", Eposta = "fatma.demir@erzto.org.tr", Telefon = "0533 234 56 78" },
            new() { AdSoyad = "Mehmet Kaya", KullaniciAdi = "mehmet.kaya", Rol = "Görevli", Gorev = "Üye Temsilcisi", Birim = "Yakutiye Bölgesi", Eposta = "mehmet.kaya@erzto.org.tr", Telefon = "0531 345 67 89" },
            new() { AdSoyad = "Ayşe Yıldız", KullaniciAdi = "ayse.yildiz", Rol = "Görevli", Gorev = "Üye Temsilcisi", Birim = "Palandöken Bölgesi", Eposta = "ayse.yildiz@erzto.org.tr", Telefon = "0530 456 78 90" },
            new() { AdSoyad = "Murat Şahin", KullaniciAdi = "murat.sahin", Rol = "Yönetici", Gorev = "Genel Sekreter", Birim = "Yönetim", Eposta = "murat.sahin@erzto.org.tr", Telefon = "0532 567 89 01" },
            new() { AdSoyad = "Hasan Aktaş", KullaniciAdi = "hasan.aktas", Rol = "Görevli", Gorev = "Denetim Uzmanı", Birim = "Denetim", Eposta = "hasan.aktas@erzto.org.tr", Telefon = "0535 678 90 12", Durum = "Pasif" },
            new() { AdSoyad = "Zeynep Kaya", KullaniciAdi = "zeynep.kaya", Rol = "Görevli", Gorev = "İstatistik Uzmanı", Birim = "Raporlama", Eposta = "zeynep.kaya@erzto.org.tr", Telefon = "0534 789 01 23" },
        };
        db.Kullanicilar.AddRange(kullanicilar);
        await db.SaveChangesAsync();

        string[] adlar = ["Mustafa", "Zeynep", "İbrahim", "Ali", "Hasan", "Mehmet", "Elif", "Hüseyin", "Emine", "Osman", "Hatice", "Yusuf", "Fadime", "Ömer", "Sultan", "Ramazan", "Kadir", "Selim", "Nurten", "Bekir"];
        string[] soyadlar = ["BAKIR", "KAYA", "POLAT", "YILMAZ", "AKTAŞ", "GÜL", "DEMİR", "ARSLAN", "ÇELİK", "ŞAHİN", "KOÇ", "AYDIN", "ÖZTÜRK", "KILIÇ", "ASLAN", "TAŞ", "KURT", "YİĞİT", "EKİNCİ", "BULUT"];
        string[] isletmeTur = ["Ticaret", "Market", "Butik", "Elektronik", "İnşaat", "Mobilya", "Kırtasiye", "Tekstil", "Gıda", "Otomotiv"];
        string[] ilceler = ["Yakutiye", "Palandöken", "Aziziye"];
        string[][] mahalleler =
        [
            ["Lalapaşa", "Muratpaşa", "Camiikebir", "Rabia Ana", "Gez"],
            ["Yunusemre", "Abdurrahmangazi", "Yenişehir", "Sancak", "Adnan Menderes"],
            ["Ilıca", "Selçuklu", "Kayapa", "Gezköy", "Dadaşkent"],
        ];
        string[] durumlar = ["Onay Verdi", "Onay Vermedi", "Kararsız", "Görüşülmedi"];
        // Dashboard'daki oranlara yakın bir dağılım: %46 onay, %16 red, %2 kararsız, %36 görüşülmemiş
        int[] durumAgirlik = [46, 16, 2, 36];

        var aktifGorevliler = kullanicilar.Where(k => k.Durum == "Aktif").ToList();
        var esnaflar = new List<Esnaf>();
        var simdi = DateTime.UtcNow;

        for (var i = 0; i < 160; i++)
        {
            var ad = adlar[rnd.Next(adlar.Length)];
            var soyad = soyadlar[rnd.Next(soyadlar.Length)];
            var grup = gruplar[rnd.Next(gruplar.Count)];
            var ilceIdx = rnd.Next(ilceler.Length);
            var durum = PickWeighted(rnd, durumlar, durumAgirlik);
            var gorusuldu = durum != "Görüşülmedi";

            esnaflar.Add(new Esnaf
            {
                AdSoyad = $"{ad} {soyad}",
                Isletme = $"{Baslik(soyad)} {isletmeTur[rnd.Next(isletmeTur.Length)]}",
                VergiNo = $"{rnd.Next(100, 999)}{rnd.Next(1000, 9999)}{rnd.Next(100, 999)}",
                GrupId = grup.Id,
                Ilce = ilceler[ilceIdx],
                Mahalle = mahalleler[ilceIdx][rnd.Next(mahalleler[ilceIdx].Length)],
                Telefon = $"05{rnd.Next(30, 45)} {rnd.Next(100, 999)} {rnd.Next(10, 99)} {rnd.Next(10, 99)}",
                GorevliId = aktifGorevliler[rnd.Next(aktifGorevliler.Count)].Id,
                Durum = durum,
                SonGorusmeTarihi = gorusuldu ? simdi.AddDays(-rnd.Next(0, 180)).AddMinutes(-rnd.Next(0, 600)) : null,
                KayitTarihi = simdi.AddDays(-rnd.Next(30, 720)),
            });
        }
        db.Esnaflar.AddRange(esnaflar);
        await db.SaveChangesAsync();

        string[] gorusmeNotlari =
        [
            "Üyelik yenileme konusunda bilgilendirildi.",
            "Yeni destek programları anlatıldı, olumlu karşıladı.",
            "İş yeri ziyareti yapıldı, talepleri kaydedildi.",
            "Telefonla ulaşıldı, yüz yüze görüşme planlandı.",
            "Aidat yapılandırması hakkında görüşüldü.",
            "Eğitim programına davet edildi.",
        ];

        var gorusmeler = new List<Gorusme>();
        foreach (var esnaf in esnaflar.Where(m => m.SonGorusmeTarihi != null))
        {
            var adet = rnd.Next(1, 4);
            for (var j = 0; j < adet; j++)
            {
                var sonGorusme = j == 0;
                gorusmeler.Add(new Gorusme
                {
                    EsnafId = esnaf.Id,
                    GorevliId = esnaf.GorevliId!.Value,
                    Tarih = sonGorusme ? esnaf.SonGorusmeTarihi!.Value : esnaf.SonGorusmeTarihi!.Value.AddDays(-rnd.Next(10, 150)),
                    Sonuc = sonGorusme ? (esnaf.Durum == "Görüşülmedi" ? "Kararsız" : esnaf.Durum) : durumlar[rnd.Next(3)],
                    Not = gorusmeNotlari[rnd.Next(gorusmeNotlari.Length)],
                    TakipGerekli = rnd.Next(100) < 20,
                });
            }
        }
        db.Gorusmeler.AddRange(gorusmeler);

        string[] gorevlendirmeDurumlari = ["Aktif", "Tamamlandı", "İptal Edildi"];
        int[] gorevlendirmeAgirlik = [65, 30, 5];
        var gorevlendirmeler = new List<Gorevlendirme>();
        for (var i = 0; i < 40; i++)
        {
            var esnaf = esnaflar[rnd.Next(esnaflar.Count)];
            gorevlendirmeler.Add(new Gorevlendirme
            {
                GorevliId = aktifGorevliler[rnd.Next(aktifGorevliler.Count)].Id,
                EsnafId = esnaf.Id,
                GrupId = esnaf.GrupId,
                Tarih = simdi.AddDays(-rnd.Next(0, 120)),
                Not = "Saha ziyareti ve bilgilendirme görevlendirmesi.",
                Durum = PickWeighted(rnd, gorevlendirmeDurumlari, gorevlendirmeAgirlik),
            });
        }
        db.Gorevlendirmeler.AddRange(gorevlendirmeler);

        string[] onayDurumlari = ["Bekliyor", "Onaylandı", "Reddedildi", "İptal Edildi"];
        int[] onayAgirlik = [19, 67, 11, 3];
        string[] islemTurleri = ["Üyelik Onayı", "Bilgi Güncelleme", "Grup Değişikliği", "Kayıt Silme Talebi"];
        var onaylar = new List<Onay>();
        for (var i = 0; i < 90; i++)
        {
            var esnaf = esnaflar[rnd.Next(esnaflar.Count)];
            onaylar.Add(new Onay
            {
                EsnafId = esnaf.Id,
                GorevliId = esnaf.GorevliId,
                IslemTuru = islemTurleri[rnd.Next(islemTurleri.Length)],
                Tarih = simdi.AddDays(-rnd.Next(0, 90)),
                Durum = PickWeighted(rnd, onayDurumlari, onayAgirlik),
            });
        }
        db.Onaylar.AddRange(onaylar);

        await db.SaveChangesAsync();
    }

    private static string PickWeighted(Random rnd, string[] values, int[] weights)
    {
        var toplam = weights.Sum();
        var hedef = rnd.Next(toplam);
        var birikim = 0;
        for (var i = 0; i < values.Length; i++)
        {
            birikim += weights[i];
            if (hedef < birikim) return values[i];
        }
        return values[^1];
    }

    private static string Baslik(string s) =>
        char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();
}
