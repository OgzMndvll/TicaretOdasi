using EtsoApi.Data;
using EtsoApi.Models;
using Microsoft.AspNetCore.Http;

namespace EtsoApi.Services;

/// <summary>
/// Denetim kaydı yazar. Controller'lar bunu çağırır; "kim" bilgisi istekteki jetondan
/// okunduğu için ayrıca parametre olarak geçirilmez.
///
/// Kayıt <b>eklenir ama kaydedilmez</b>: çağıran zaten kendi <c>SaveChangesAsync</c>'ini
/// çağırıyor ve log satırı asıl değişiklikle aynı işlemde yazılsın isteniyor. Asıl işlem
/// başarısız olursa log satırı da yazılmaz.
/// </summary>
public class IslemGunlugu(EtsoDbContext db, IHttpContextAccessor erisim)
{
    public void Yaz(string islem, Esnaf? esnaf = null, string? detay = null)
    {
        var kullanici = erisim.HttpContext?.User;
        db.IslemKayitlari.Add(new IslemKaydi
        {
            KullaniciId = kullanici?.KullaniciId(),
            // Jeton yoksa (konsol komutları) işlemi "sistem" adına yazarız; log'da boşluk kalmasın.
            KullaniciAdi = kullanici?.KullaniciAdiniAl() ?? "sistem",
            KullaniciAdSoyad = kullanici?.AdSoyadiniAl() ?? "Sistem",
            EsnafId = esnaf?.Id,
            EsnafUnvan = esnaf?.Isletme,
            Islem = islem,
            Detay = detay,
            Tarih = DateTime.UtcNow,
        });
    }
}
