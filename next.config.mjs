/** @type {import('next').NextConfig} */

const genelApiAdresi = process.env.NEXT_PUBLIC_API_URL ?? "https://api.courseintellect.com.tr";
// Yerelde tarayıcı API'ye doğrudan bağlanmaz. macOS 26 + .NET 10'un loopback bağlantı
// sorunundan etkilenmemesi için istekler aynı kaynakta /api/proxy üzerinden Next'e gelir.
const apiHedefi = (process.env.API_INTERNAL_URL
  ?? (genelApiAdresi.startsWith("/") ? "http://127.0.0.1:5180" : genelApiAdresi))
  .replace(/\/+$/, "");

// API başka bir kaynakta olduğunda CSP'nin connect-src listesine açıkça eklenmesi gerekir.
const apiKaynagi = (() => {
  try {
    return new URL(apiHedefi).origin;
  } catch {
    return "http://localhost:5180";
  }
})();

// Panel bir alt yolda yayınlanabilir (ör. ajansorkestra.com.tr/EtsoSecim). basePath derleme
// anında sabitlenir, bu yüzden ortam değişkeninden okunur ve sondaki "/" temizlenir.
// Boş bırakılırsa (yerel geliştirme) uygulama kök dizinde çalışır.
const altYol = (process.env.NEXT_PUBLIC_BASE_PATH ?? "").replace(/\/+$/, "");

// SignalR canlı kanalı aynı API kaynağına ws/wss şemasıyla bağlanır. CSP3'te "https://x"
// ifadesi "wss://x" ile de eşleşir; yine de tarayıcı farklılıklarına yer bırakmamak için
// soket kaynağı connect-src listesine açıkça yazılır.
const apiSoketKaynagi = apiKaynagi.replace(/^http/, "ws");

const gelistirme = process.env.NODE_ENV !== "production";

// Panel, oturum jetonunu tarayıcı deposunda tuttuğu için sayfanın kendisi de korunmalı:
// çerçevelenmeye (clickjacking), tip tahminine ve dış kaynak yüklemesine kapalıdır.
//
// Not: script-src'te 'unsafe-inline' bulunuyor; Next kendi önyükleme betiklerini satır içi
// gömdüğü için nonce üreten bir middleware olmadan kaldırılamaz. Bu haliyle CSP, XSS'e karşı
// tek başına bariyer değil ek savunmadır; asıl koruma React'in öntanımlı kaçışlamasıdır.
// 'unsafe-eval' yalnızca geliştirmede (React Fast Refresh) açıktır, canlıda kapalıdır.
const csp = [
  "default-src 'self'",
  `script-src 'self' 'unsafe-inline'${gelistirme ? " 'unsafe-eval'" : ""}`,
  "style-src 'self' 'unsafe-inline'",
  "img-src 'self' data: blob:",
  "font-src 'self' data:",
  `connect-src 'self' ${apiKaynagi} ${apiSoketKaynagi}${gelistirme ? " ws: wss:" : ""}`,
  "object-src 'none'",
  "base-uri 'self'",
  "form-action 'self'",
  "frame-ancestors 'none'",
  "frame-src 'none'",
  "manifest-src 'self'",
  ...(gelistirme ? [] : ["upgrade-insecure-requests"]),
].join("; ");

const guvenlikBasliklari = [
  { key: "Content-Security-Policy", value: csp },
  // frame-ancestors'ı desteklemeyen eski tarayıcılar için yedek.
  { key: "X-Frame-Options", value: "DENY" },
  { key: "X-Content-Type-Options", value: "nosniff" },
  { key: "Referrer-Policy", value: "same-origin" },
  { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=(), payment=(), usb=()" },
  { key: "Cross-Origin-Opener-Policy", value: "same-origin" },
  { key: "X-Permitted-Cross-Domain-Policies", value: "none" },
];

const nextConfig = {
  ...(altYol ? { basePath: altYol } : {}),
  poweredByHeader: false,
  reactStrictMode: true,
  // Geliştirme modundaki yuvarlak "N" rozetini gizler (yalnızca dev'de görünürdü).
  devIndicators: false,
  outputFileTracingRoot: process.cwd(),
  images: { formats: ["image/avif", "image/webp"] },
  async rewrites() {
    return [{ source: "/api/proxy/:path*", destination: `${apiHedefi}/:path*` }];
  },
  async headers() {
    // Kaynak deseni basePath'e göre otomatik ön eklenir; burada kök desen yeterlidir.
    return [{ source: "/:path*", headers: guvenlikBasliklari }];
  },
};

export default nextConfig;
