# Erzurum Ticaret Odası Yönetim Paneli

Next.js frontend ve ASP.NET Core API ile geliştirilen Erzurum Ticaret Odası yönetim paneli.

## Gereksinimler

- Node.js `>=22`
- .NET SDK `>=10`
- MySQL uyumlu veritabanı

## Frontend

```bash
npm install
npm run dev
npm run build
```

Uygulama varsayılan olarak `http://localhost:3000` adresinde çalışır.

## Backend

Geliştirme ayarları için örnek dosyayı kopyalayın:

```bash
cp backend/EtsoApi/appsettings.Development.example.json backend/EtsoApi/appsettings.Development.json
```

`appsettings.Development.json` içindeki veritabanı bağlantısını, `Jwt:Anahtar` değerini ve ilk kurulum şifrelerini kendi güvenli değerlerinizle doldurun.

```bash
dotnet restore backend/EtsoApi/EtsoApi.csproj
dotnet run --project backend/EtsoApi/EtsoApi.csproj
```

API varsayılan olarak `http://localhost:5180` adresinde çalışır.

## Güvenlik Notları

- `.env*`, `appsettings.Development.json`, derleme çıktıları ve loglar git dışında tutulur.
- JWT anahtarı, veritabanı parolası ve başlangıç kullanıcı parolaları kaynak koda yazılmaz.
- Canlı ortamda `ConnectionStrings__EtsoDb`, `Jwt__Anahtar`, `Bootstrap__AdminPassword` ve gerekiyorsa `Bootstrap__GorevliPassword` ortam değişkenleri kullanılmalıdır.
