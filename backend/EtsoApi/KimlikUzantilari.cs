using System.Security.Claims;

namespace EtsoApi;

public static class KimlikUzantilari
{
    /// <summary>Oturumdaki kullanıcının veritabanı kimliği (JWT sub claim'i).</summary>
    public static int? KullaniciId(this ClaimsPrincipal user)
    {
        var ham = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return int.TryParse(ham, out var id) ? id : null;
    }

    public static bool Yonetici(this ClaimsPrincipal user) => user.IsInRole("Yönetici");

    /// <summary>Oturumdaki kullanıcı adı (denetim kaydı için).</summary>
    public static string? KullaniciAdiniAl(this ClaimsPrincipal user) =>
        user.FindFirstValue("unique_name") ?? user.FindFirstValue(ClaimTypes.Name);

    public static string? AdSoyadiniAl(this ClaimsPrincipal user) =>
        user.FindFirstValue(Services.TokenServisi.AdSoyadClaim);

    /// <summary>
    /// Kullanıcı/şifre yönetimi ve sistem ayarları yetkisi. Panele giren herkes "Yönetici"dir
    /// ve aynı ekranları görür; bu bayrak yalnızca hesap açma, şifre belirleme, ayarlar ve
    /// işlem kayıtları için aranır.
    /// </summary>
    public static bool SistemYoneticisi(this ClaimsPrincipal user) =>
        user.FindFirstValue(Services.TokenServisi.SistemYoneticisiClaim) == "true";
}
