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
}
