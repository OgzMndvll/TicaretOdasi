import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Erzurum Ticaret Odası | Yönetim Sistemi",
  description: "Üye ilişkileri, görevlendirme, görüşme ve raporlama yönetim paneli.",
  robots: { index: false, follow: false },
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="tr">
      <body>{children}</body>
    </html>
  );
}
