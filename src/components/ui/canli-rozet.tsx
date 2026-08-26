"use client";

import { WifiOff } from "lucide-react";
import { CanliDurum, canliEtiket } from "@/lib/canli";

const ACIKLAMA: Record<CanliDurum, string> = {
  bagli: "Canlı bağlantı açık: başka bir kullanıcının girdiği kayıtlar bu ekrana kendiliğinden düşer.",
  baglaniyor: "Canlı bağlantı kuruluyor…",
  kapali: "Canlı bağlantı yok. Veriler yalnızca siz bir işlem yaptığınızda ya da sayfayı yenilediğinizde güncellenir.",
};

/** Canlı veri kanalının durumu. Listenin kendiliğinden tazelenip tazelenmediği buradan görülür. */
export function CanliRozet({ durum }: { durum: CanliDurum }) {
  return <span className={`canli-rozet ${durum}`} title={ACIKLAMA[durum]}>
    {durum === "kapali" ? <WifiOff size={12} /> : <i aria-hidden="true" />}
    {canliEtiket(durum)}
  </span>;
}
