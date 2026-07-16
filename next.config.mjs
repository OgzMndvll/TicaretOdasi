/** @type {import('next').NextConfig} */
const nextConfig = {
  poweredByHeader: false,
  reactStrictMode: true,
  // Geliştirme modundaki yuvarlak "N" rozetini gizler (yalnızca dev'de görünürdü).
  devIndicators: false,
  outputFileTracingRoot: process.cwd(),
  images: { formats: ["image/avif", "image/webp"] },
};

export default nextConfig;
