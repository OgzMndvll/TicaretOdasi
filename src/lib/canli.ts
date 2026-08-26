"use client";

import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import { useCallback, useEffect, useRef, useState } from "react";
import { API_URL } from "./api";
import { tokenAl } from "./auth";

/** Sunucudan gelen değişiklik haberi. Veri taşımaz; ekran veriyi API'den yeniden okur. */
export interface CanliOlay {
  /** "gorusme" | "esnaf" | "kullanici" | "grup" */
  tur: string;
  id?: number | null;
  zaman?: string;
}

export type CanliDurum = "kapali" | "baglaniyor" | "bagli";

/** Sunucudaki olay adı (bkz. backend/EtsoApi/Services/CanliBildirim.cs). */
const OLAY = "veriDegisti";

// Tüm ekranlar tek bir bağlantıyı paylaşır. Her ekran kendi WebSocket'ini açsaydı sayfa
// geçişlerinde sürekli bağlantı kurulup kapanır, sunucuda da gereksiz oturum birikirdi.
let baglanti: HubConnection | null = null;
let aboneSayisi = 0;
let kapatmaZamanlayicisi: ReturnType<typeof setTimeout> | null = null;
const olayDinleyicileri = new Set<(olay: CanliOlay) => void>();
const durumDinleyicileri = new Set<(durum: CanliDurum) => void>();
let sonDurum: CanliDurum = "kapali";

function durumYaz(durum: CanliDurum) {
  sonDurum = durum;
  durumDinleyicileri.forEach(bildir => bildir(durum));
}

function baglantiyiKur(): HubConnection {
  if (baglanti) return baglanti;
  const yeni = new HubConnectionBuilder()
    .withUrl(`${API_URL}/hubs/panel`, {
      // Jetonu SignalR hem negotiate isteğinin Authorization başlığına hem de WebSocket
      // adresinin sorgu dizesine koyar; sunucu ikisini de kabul eder.
      accessTokenFactory: () => tokenAl() ?? "",
      // Oturum çerezle değil jetonla taşınıyor; çerez göndermeye gerek yok.
      withCredentials: false,
    })
    // Kopmada artan aralıklarla yeniden dener; ağ dalgalanmasında kullanıcı hiçbir şey yapmaz.
    .withAutomaticReconnect([0, 2000, 5000, 10000, 20000, 30000])
    .configureLogging(LogLevel.Warning)
    .build();

  yeni.onreconnecting(() => durumYaz("baglaniyor"));
  yeni.onreconnected(() => durumYaz("bagli"));
  yeni.onclose(() => durumYaz("kapali"));
  yeni.on(OLAY, (olay: CanliOlay) => olayDinleyicileri.forEach(bildir => bildir(olay)));

  baglanti = yeni;
  return yeni;
}

/**
 * İlk bağlantıyı kurar, başarısız olursa artan aralıklarla yeniden dener.
 *
 * `withAutomaticReconnect` yalnızca bir kez kurulmuş bağlantı koptuğunda devreye girer; panel,
 * API henüz ayağa kalkmadan açılırsa kanal aksi halde o oturum boyunca kapalı kalırdı.
 */
function baglantiyiBaslat(c: HubConnection, deneme = 0) {
  if (aboneSayisi === 0 || c !== baglanti || !tokenAl()) return;
  durumYaz("baglaniyor");
  c.start()
    .then(() => durumYaz("bagli"))
    .catch(() => {
      durumYaz("kapali");
      const bekleme = Math.min(30_000, 2_000 * 2 ** deneme);
      setTimeout(() => {
        if (c === baglanti && c.state === HubConnectionState.Disconnected) baglantiyiBaslat(c, deneme + 1);
      }, bekleme);
    });
}

/**
 * Canlı veri kanalına abone olur ve bağlantı durumunu döndürür.
 *
 * Gelen haberler `gecikmeMs` boyunca biriktirilir: toplu bir içe aktarma yüzlerce olay
 * yayımlayabilir, her biri için ayrı yeniden yükleme yapmak listeyi titretirdi.
 *
 * @param onDegisim Değişiklik haberi geldiğinde çağrılır. Referansı değişse de yeniden abone olunmaz.
 */
export function useCanliVeri(onDegisim: (olay: CanliOlay) => void, gecikmeMs = 400): CanliDurum {
  const [durum, setDurum] = useState<CanliDurum>(sonDurum);
  const geriCagri = useRef(onDegisim);

  useEffect(() => { geriCagri.current = onDegisim; }, [onDegisim]);

  useEffect(() => {
    // Oturum yoksa kanal açılmaz; giriş ekranında boşuna bağlantı denenmesin.
    if (!tokenAl()) return;

    let zamanlayici: ReturnType<typeof setTimeout> | null = null;
    const dinleyici = (olay: CanliOlay) => {
      if (zamanlayici) clearTimeout(zamanlayici);
      zamanlayici = setTimeout(() => geriCagri.current(olay), gecikmeMs);
    };

    olayDinleyicileri.add(dinleyici);
    durumDinleyicileri.add(setDurum);
    aboneSayisi++;
    if (kapatmaZamanlayicisi) { clearTimeout(kapatmaZamanlayicisi); kapatmaZamanlayicisi = null; }

    const c = baglantiyiKur();
    if (c.state === HubConnectionState.Disconnected) baglantiyiBaslat(c);
    else setDurum(c.state === HubConnectionState.Connected ? "bagli" : "baglaniyor");

    return () => {
      if (zamanlayici) clearTimeout(zamanlayici);
      olayDinleyicileri.delete(dinleyici);
      durumDinleyicileri.delete(setDurum);
      aboneSayisi--;
      // Sayfa geçişlerinde abone sayısı bir an sıfıra düşebildiği (ve React'in katı modda
      // efektleri iki kez çalıştırdığı) için kapatma kısa bir süre ertelenir.
      if (aboneSayisi === 0) {
        kapatmaZamanlayicisi = setTimeout(() => {
          kapatmaZamanlayicisi = null;
          if (aboneSayisi > 0 || !baglanti) return;
          const kapanan = baglanti;
          baglanti = null;
          kapanan.stop().catch(() => {});
          durumYaz("kapali");
        }, 3000);
      }
    };
    // gecikmeMs sabit kullanılıyor; değişse bile yeniden abone olmaya gerek yok.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return durum;
}

/** Durum rozetinde gösterilecek kısa metin. */
export function canliEtiket(durum: CanliDurum): string {
  switch (durum) {
    case "bagli": return "Canlı";
    case "baglaniyor": return "Bağlanıyor";
    default: return "Çevrimdışı";
  }
}

/** Bir ekranın kendi yükleme fonksiyonunu canlı kanala bağlaması için kısayol. */
export function useCanliYenileme(yukle: () => void): CanliDurum {
  return useCanliVeri(useCallback(() => yukle(), [yukle]));
}
