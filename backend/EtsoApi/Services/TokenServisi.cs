using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EtsoApi.Models;
using Microsoft.IdentityModel.Tokens;

namespace EtsoApi.Services;

public class TokenServisi(IConfiguration config)
{
    public const string RolClaim = "rol";
    public const string AdSoyadClaim = "adSoyad";

    public static byte[] AnahtarBaytlari(IConfiguration config)
    {
        var gizli = config["Jwt:Anahtar"]
            ?? throw new InvalidOperationException("Jwt:Anahtar yapılandırması eksik.");
        var baytlar = Encoding.UTF8.GetBytes(gizli);
        if (baytlar.Length < 32)
            throw new InvalidOperationException("Jwt:Anahtar en az 32 bayt olmalıdır.");
        return baytlar;
    }

    public (string Token, DateTime Bitis) Uret(Kullanici kullanici)
    {
        var bitis = DateTime.UtcNow.AddHours(8);
        var kimlik = new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sub, kullanici.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, kullanici.KullaniciAdi),
            new Claim(AdSoyadClaim, kullanici.AdSoyad),
            new Claim(RolClaim, kullanici.Rol),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        ]);
        var tanim = new SecurityTokenDescriptor
        {
            Subject = kimlik,
            Issuer = config["Jwt:Yayinci"],
            Audience = config["Jwt:Hedef"],
            Expires = bitis,
            NotBefore = DateTime.UtcNow,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(AnahtarBaytlari(config)), SecurityAlgorithms.HmacSha256),
        };
        var uretici = new JwtSecurityTokenHandler();
        return (uretici.WriteToken(uretici.CreateToken(tanim)), bitis);
    }
}
