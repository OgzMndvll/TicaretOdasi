using System.Threading.RateLimiting;
using System.Text.Json;
using System.Text.Json.Serialization;
using EtsoApi;
using EtsoApi.Controllers;
using EtsoApi.Data;
using EtsoApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Gizli bilgiler kaynak kodda tutulmaz.
// Geliştirme: appsettings.Development.json (git'e girmez) — Canlı: ConnectionStrings__EtsoDb ve Jwt__Anahtar ortam değişkenleri.
var connectionString = builder.Configuration.GetConnectionString("EtsoDb");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "Veritabanı bağlantı dizesi tanımlı değil. Canlıda 'ConnectionStrings__EtsoDb' ortam değişkenini, " +
        "geliştirmede appsettings.Development.json dosyasını doldurun.");

builder.Services.AddDbContext<EtsoDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<TokenServisi>();

// Ters vekil (nginx vb.) arkasında gerçek istemci IP'si — rate limit'in doğru çalışması için şart.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

builder.Services.AddCors(options => options.AddPolicy("frontend", policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:3000"])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders("Content-Disposition")));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Yayinci"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Hedef"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(TokenServisi.AnahtarBaytlari(builder.Configuration)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = TokenServisi.RolClaim,
        };

        // Token 8 saat geçerli olduğundan, imzası doğru olsa bile her istekte kullanıcının
        // GÜNCEL durumu doğrulanır. Aksi halde pasife alınan/silinen/rolü düşürülen bir kullanıcı
        // elindeki token ile eski yetkileriyle çalışmaya devam ederdi.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var db = ctx.HttpContext.RequestServices.GetRequiredService<EtsoDbContext>();
                var id = ctx.Principal?.KullaniciId();
                if (id is null) { ctx.Fail("Geçersiz token."); return; }

                var kullanici = await db.Kullanicilar.AsNoTracking()
                    .Where(k => k.Id == id)
                    .Select(k => new { k.Rol, k.Durum, k.SifreGuncelleme })
                    .FirstOrDefaultAsync();

                if (kullanici is null) { ctx.Fail("Kullanıcı bulunamadı."); return; }
                if (kullanici.Durum != "Aktif") { ctx.Fail("Hesap pasif."); return; }

                // Rol token'da donmuş olabilir; yetki her zaman veritabanındaki güncel role göre verilir.
                if (ctx.Principal!.FindFirst(TokenServisi.RolClaim)?.Value != kullanici.Rol)
                { ctx.Fail("Yetki değişti, yeniden giriş yapın."); return; }

                // Şifre değiştirildiyse/sıfırlandıysa, o andan önce üretilmiş tüm token'lar geçersizdir.
                // Üretim anı token'ın 'nbf' claim'inden okunur (SecurityToken tipi handler'a göre değişebildiği
                // için tipe bağlı okuma kırılgandır; nbf'yi TokenServisi her token'a yazar).
                var nbf = ctx.Principal!.FindFirst("nbf")?.Value;
                var uretim = long.TryParse(nbf, out var saniye)
                    ? DateTimeOffset.FromUnixTimeSeconds(saniye).UtcDateTime
                    : DateTime.MinValue;

                // 'nbf' saniyeye aşağı yuvarlandığı için 5 sn tolerans bırakılır (şifre değiştiren kullanıcının
                // kendi taze token'ı elenmesin). Daha geniş tolerans, iptal edilen token'a yaşam süresi tanır.
                // DateTime.MinValue'da taşmayı önlemek için alt sınır korunur.
                var esik = kullanici.SifreGuncelleme > DateTime.MinValue.AddMinutes(1)
                    ? kullanici.SifreGuncelleme.AddSeconds(-5)
                    : DateTime.MinValue;
                if (uretim < esik)
                { ctx.Fail("Şifre değişti, yeniden giriş yapın."); return; }
            },
        };
    });

// Varsayılan kural: panele yalnızca Yönetici rolü erişir; kimliği doğrulanmamış ya da
// yönetici olmayan hiçbir istek hiçbir endpoint'e ulaşamaz. Çalışan kayıtları görüşmelerde
// seçilmek içindir, giriş yapmazlar. İstisnalar (giriş vb.) [AllowAnonymous] ile tek tek açılır.
builder.Services.AddAuthorization(options =>
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireRole(EtsoApi.Models.Kullanici.YoneticiRolu)
        .Build());

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "{\"mesaj\":\"Çok fazla istek gönderildi. Lütfen bir dakika sonra tekrar deneyin.\"}", ct);
    };

    static string Istemci(HttpContext c) => c.Connection.RemoteIpAddress?.ToString() ?? "bilinmeyen";

    // Giriş ve şifre uçları: IP başına dakikada 10 deneme (brute-force / credential stuffing koruması).
    options.AddPolicy("giris", context => RateLimitPartition.GetFixedWindowLimiter(
        Istemci(context),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));

    // Diğer tüm uçlar: IP başına dakikada 300 istek (kaba kuvvet ve veri kazıma yavaşlatma).
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            Istemci(context),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EtsoDbContext>();
    await db.Database.MigrateAsync();

    // Toplu yükleme komutu: "dotnet run -- ice-aktar <dosya.xlsx>".
    // Oda üye raporu binlerce satır olabildiği için HTTP yüklemesi yerine doğrudan çalıştırılabilir.
    // API'nin içe aktarma ucuyla aynı servisi kullanır; iş kuralları tek yerde durur.
    if (args.Length >= 2 && args[0] == "ice-aktar")
    {
        var yol = args[1];
        if (!File.Exists(yol))
        {
            Console.Error.WriteLine($"Dosya bulunamadı: {yol}");
            return 1;
        }
        await using var akis = File.OpenRead(yol);
        var satirlar = ExcelServisi.Oku(akis);
        Console.WriteLine($"{satirlar.Count} satır okundu, aktarılıyor...");
        var sonuc = await UyeIceAktarmaServisi.AktarAsync(db, satirlar);
        Console.WriteLine($"Eklenen: {sonuc.Eklenen}  Atlanan: {sonuc.Atlanan}  Hata: {sonuc.Hatalar.Count}");
        foreach (var hata in sonuc.Hatalar.Take(20)) Console.WriteLine("  " + hata);
        if (sonuc.Hatalar.Count > 20) Console.WriteLine($"  ... {sonuc.Hatalar.Count - 20} hata daha");
        return 0;
    }

    // Ana üye listesini güvenli biçimde değiştirir: önce mevcut üyeler ve ilişkili kayıtlar
    // JSON yedeğine alınır, sonra kaynak dosyadaki grup kapsamı ve üyeler tek transaction'da kurulur.
    // Kullanım: dotnet run -- uye-listesini-degistir <dosya.xlsx> <yedek.json>
    if (args.Length >= 3 && args[0] == "uye-listesini-degistir")
    {
        var yol = Path.GetFullPath(args[1]);
        var yedekYolu = Path.GetFullPath(args[2]);
        if (!File.Exists(yol))
        {
            Console.Error.WriteLine($"Dosya bulunamadı: {yol}");
            return 1;
        }

        await using var akis = File.OpenRead(yol);
        var satirlar = ExcelServisi.Oku(akis);
        if (satirlar.Count == 0)
        {
            Console.Error.WriteLine("Kaynak dosyada aktarılabilir üye satırı bulunamadı; mevcut veri korunuyor.");
            return 1;
        }

        var grupAnahtari = ExcelServisi.Normalize("Grup");
        var grupNumaralari = satirlar
            .Select(s => s.Degerler.GetValueOrDefault(grupAnahtari))
            .Select(x => System.Text.RegularExpressions.Regex.Match(x ?? "", @"^\s*(\d{1,3})"))
            .Where(x => x.Success)
            .Select(x => int.Parse(x.Groups[1].Value))
            .Distinct()
            .OrderBy(x => x)
            .ToArray();
        if (grupNumaralari.Length == 0)
        {
            Console.Error.WriteLine("Kaynak dosyada geçerli grup numarası bulunamadı; mevcut veri korunuyor.");
            return 1;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(yedekYolu)!);
        var yedek = new
        {
            OlusturmaZamani = DateTime.UtcNow,
            KaynakDosya = yol,
            Gruplar = await db.Gruplar.AsNoTracking().ToListAsync(),
            Esnaflar = await db.Esnaflar.AsNoTracking().ToListAsync(),
            EsnafYetkilileri = await db.EsnafYetkilileri.AsNoTracking().ToListAsync(),
            Gorusmeler = await db.Gorusmeler.AsNoTracking().ToListAsync(),
            Gorevlendirmeler = await db.Gorevlendirmeler.AsNoTracking().ToListAsync(),
            Onaylar = await db.Onaylar.AsNoTracking().ToListAsync(),
        };
        await File.WriteAllTextAsync(yedekYolu, JsonSerializer.Serialize(yedek, new JsonSerializerOptions
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
        }));
        Console.WriteLine($"Mevcut veri yedeklendi: {yedekYolu}");

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            // Açık silme sırası hem bağımlılıkları görünür kılar hem veritabanı sağlayıcısından bağımsızdır.
            await db.Onaylar.ExecuteDeleteAsync();
            await db.Gorevlendirmeler.ExecuteDeleteAsync();
            await db.Gorusmeler.ExecuteDeleteAsync();
            await db.EsnafYetkilileri.ExecuteDeleteAsync();
            await db.Esnaflar.ExecuteDeleteAsync();

            // Dosyada olmayan gruplar kaldırılır; 1–30 gibi mevcut açıklayıcı grup adları korunur.
            await db.Gruplar
                .Where(g => g.No == null || !grupNumaralari.Contains(g.No.Value))
                .ExecuteDeleteAsync();
            await db.Gruplar
                .Where(g => g.No != null && grupNumaralari.Contains(g.No.Value))
                .ExecuteUpdateAsync(g => g
                    .SetProperty(x => x.Durum, "Aktif")
                    .SetProperty(x => x.GuncellemeTarihi, DateTime.UtcNow));

            var sonuc = await UyeIceAktarmaServisi.AktarAsync(db, satirlar);
            var veritabaniToplam = await db.Esnaflar.CountAsync();
            var grupDisiUye = await db.Esnaflar.CountAsync(e => e.GrupId == null);
            var durumSayilari = await db.Esnaflar
                .GroupBy(e => e.UyelikDurumu)
                .Select(g => new { Durum = g.Key, Adet = g.Count() })
                .OrderBy(x => x.Durum)
                .ToListAsync();
            var grupSayilari = await db.Gruplar
                .Where(g => g.No != null && grupNumaralari.Contains(g.No.Value))
                .OrderBy(g => g.No)
                .Select(g => new { No = g.No!.Value, Adet = g.Esnaflar.Count })
                .ToListAsync();

            if (sonuc.Hatalar.Count > 0
                || sonuc.Eklenen + sonuc.Atlanan != satirlar.Count
                || veritabaniToplam != sonuc.Eklenen
                || grupDisiUye != 0
                || grupSayilari.Count != grupNumaralari.Length)
            {
                throw new InvalidOperationException(
                    $"Aktarım doğrulaması başarısız: kaynak={satirlar.Count}, eklenen={sonuc.Eklenen}, " +
                    $"atlanan={sonuc.Atlanan}, hata={sonuc.Hatalar.Count}, veritabanı={veritabaniToplam}, " +
                    $"grupsuz={grupDisiUye}, grup={grupSayilari.Count}/{grupNumaralari.Length}.");
            }

            await transaction.CommitAsync();
            Console.WriteLine($"Kaynak satır: {satirlar.Count}  Eklenen: {sonuc.Eklenen}  Birleştirilen/atlanan: {sonuc.Atlanan}  Uyarı: {sonuc.Hatalar.Count}");
            Console.WriteLine($"Durum dağılımı: {string.Join(", ", durumSayilari.Select(x => $"{x.Durum}={x.Adet}"))}");
            Console.WriteLine($"Grup dağılımı: {string.Join(", ", grupSayilari.Select(x => $"{x.No}={x.Adet}"))}");
            foreach (var hata in sonuc.Hatalar.Take(20)) Console.WriteLine("  " + hata);
            return 0;
        }
        catch
        {
            await transaction.RollbackAsync();
            Console.Error.WriteLine("Aktarım tamamlanamadı; transaction geri alındı ve eski veri korundu.");
            throw;
        }
    }

    // Sistemde etkin bir Yönetici yoksa (temiz kurulum ya da yönetici hesabının silinmesi) bir tane
    // oluşturulur; aksi halde panele hiç girilemez hale gelinir. Hesap adı config/env ile belirlenir.
    var yoneticiKullaniciAdi = (builder.Configuration["Bootstrap:AdminKullaniciAdi"] ?? "yonetici").ToLowerInvariant();
    if (!await db.Kullanicilar.AnyAsync(k => k.Rol == "Yönetici" && k.Durum == "Aktif")
        && !await db.Kullanicilar.AnyAsync(k => k.KullaniciAdi == yoneticiKullaniciAdi))
    {
        db.Kullanicilar.Add(new EtsoApi.Models.Kullanici
        {
            AdSoyad = builder.Configuration["Bootstrap:AdminAdSoyad"] ?? "Sistem Yöneticisi",
            KullaniciAdi = yoneticiKullaniciAdi,
            Rol = "Yönetici",
            Gorev = "Sistem Yöneticisi",
            Birim = "Bilgi İşlem",
        });
        await db.SaveChangesAsync();
    }

    // Şifresi olmayan kullanıcılara başlangıç şifresi atanır.
    // Sabit parola kaynak koda yazılmaz; temiz kurulumda giriş yapılacak hesap config/env ile belirlenir.
    var baslangicSifreleri = new Dictionary<string, string>
        {
            [yoneticiKullaniciAdi] = builder.Configuration["Bootstrap:AdminPassword"] ?? string.Empty,
        }
        .Where(x => !string.IsNullOrWhiteSpace(x.Value))
        .ToDictionary(x => x.Key, x => x.Value);
    var sifresizler = await db.Kullanicilar.Where(k => k.SifreHash == null).ToListAsync();
    foreach (var kullanici in sifresizler)
    {
        var sifre = baslangicSifreleri.GetValueOrDefault(kullanici.KullaniciAdi)
            ?? Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        kullanici.SifreHash = AuthController.Hashle(kullanici, sifre);
    }
    if (sifresizler.Count > 0) await db.SaveChangesAsync();
}

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}
else
{
    // Canlıda yalnızca HTTPS; tarayıcıya 180 gün boyunca HTTP'ye düşme demesi söylenir.
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Güvenlik başlıkları. API yalnızca JSON/dosya döndürür; hiçbir şekilde çerçevelenmemeli
// ve içerik tipi tarayıcı tarafından tahmin edilmemelidir.
app.Use(async (context, next) =>
{
    var basliklar = context.Response.Headers;
    basliklar["X-Content-Type-Options"] = "nosniff";
    basliklar["X-Frame-Options"] = "DENY";
    basliklar["Referrer-Policy"] = "no-referrer";
    basliklar["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    basliklar["Cache-Control"] = "no-store";
    await next();
});

app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Basit istek günlüğü: Ayarlar > Sistem Logları ekranından indirilebilir.
var logDizini = Path.Combine(app.Environment.ContentRootPath, "logs");
Directory.CreateDirectory(logDizini);
var logDosyasi = Path.Combine(logDizini, "istekler.log");
var logKilidi = new object();
const long enFazlaLogBoyutu = 10 * 1024 * 1024; // 10 MB'ı aşınca dosya devredilir (disk dolmasın).

// Satır sonu/kontrol karakterleri temizlenir: aksi halde URL'ye %0A koyup sahte log satırı yazılabilirdi.
static string LogGuvenli(string? metin, int enFazla = 300)
{
    if (string.IsNullOrEmpty(metin)) return "";
    var temiz = new string(metin.Where(k => !char.IsControl(k)).ToArray());
    return temiz.Length <= enFazla ? temiz : temiz[..enFazla];
}

app.Use(async (context, next) =>
{
    var baslangic = DateTime.Now;
    await next();
    var kimlik = context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name ?? "?" : "anonim";
    var satir = $"{baslangic:yyyy-MM-dd HH:mm:ss} [{LogGuvenli(kimlik, 60)}] {LogGuvenli(context.Request.Method, 10)} " +
                $"{LogGuvenli(context.Request.Path + context.Request.QueryString)} -> {context.Response.StatusCode} " +
                $"({(DateTime.Now - baslangic).TotalMilliseconds:F0} ms)\n";
    lock (logKilidi)
    {
        var bilgi = new FileInfo(logDosyasi);
        if (bilgi.Exists && bilgi.Length > enFazlaLogBoyutu)
        {
            var arsiv = Path.Combine(logDizini, $"istekler-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.Move(logDosyasi, arsiv, overwrite: true);
        }
        File.AppendAllText(logDosyasi, satir);
    }
});

app.MapControllers();

app.Run();
return 0;
