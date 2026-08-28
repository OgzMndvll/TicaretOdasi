"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { ClipboardList, CloudDownload, Clock3, UsersRound, CalendarDays } from "lucide-react";
import { AppShell } from "@/components/layout/app-shell";
import { FilterBar, FiltreSecim } from "@/components/ui/filter-bar";
import { StatCard } from "@/components/ui/stat-card";
import { Toast } from "@/components/ui/toast";
import { api, API_ERISIM_HATASI, IslemKaydi, IslemKaydiSecenekleri, sayiGoster, tarihGoster, Sayfali } from "@/lib/api";
import { useCanliYenileme } from "@/lib/canli";

/** İşlem türüne göre rozet tonu: silmeler kırmızı, eklemeler yeşil, geri kalanı nötr. */
function islemTonu(islem: string): string {
  if (islem.includes("silindi")) return "danger";
  if (islem.includes("eklendi")) return "success";
  if (islem.includes("değişti")) return "warning";
  return "info";
}

function gunOnce(gun: number): string {
  const t = new Date();
  t.setDate(t.getDate() - gun);
  return t.toISOString().slice(0, 10);
}

/**
 * Denetim kayıtları: kim, hangi üyede, ne zaman, hangi işlemi yaptı.
 * Yalnızca sistem yöneticisine açıktır (uç noktası da aynı politikayla kilitli).
 */
export function IslemKayitlariPage() {
  const [kayitlar, setKayitlar] = useState<IslemKaydi[]>([]);
  const [toplam, setToplam] = useState(0);
  const [ozet, setOzet] = useState<Record<string, number> | null>(null);
  const [secenekler, setSecenekler] = useState<IslemKaydiSecenekleri | null>(null);
  const [sayfa, setSayfa] = useState(1);
  const [islem, setIslem] = useState("");
  const [kullaniciId, setKullaniciId] = useState("");
  const [baslangic, setBaslangic] = useState("");
  const [bitis, setBitis] = useState("");
  const [arama, setArama] = useState("");
  const [yukleniyor, setYukleniyor] = useState(true);
  const [hata, setHata] = useState("");
  const [toast, setToast] = useState("");

  const SAYFA_BOYUTU = 50;

  /** Süzgeçleri sorgu dizesine çevirir; listeyi ve Excel çıktısını aynı kapsam besler. */
  const params = useCallback((sayfaNo?: number) => {
    const p = new URLSearchParams();
    islem.split(",").filter(Boolean).forEach(x => p.append("islem", x));
    kullaniciId.split(",").filter(Boolean).forEach(x => p.append("kullaniciId", x));
    if (baslangic) p.set("baslangic", baslangic);
    if (bitis) p.set("bitis", bitis);
    if (arama.trim()) p.set("arama", arama.trim());
    if (sayfaNo) { p.set("sayfa", String(sayfaNo)); p.set("sayfaBoyutu", String(SAYFA_BOYUTU)); }
    return p;
  }, [islem, kullaniciId, baslangic, bitis, arama]);

  const yukle = useCallback(() => {
    setYukleniyor(true);
    api.get<Sayfali<IslemKaydi>>(`/api/islemkayitlari?${params(sayfa)}`)
      .then(v => { setKayitlar(v.kayitlar); setToplam(v.toplam); setHata(""); })
      .catch(() => setHata(API_ERISIM_HATASI))
      .finally(() => setYukleniyor(false));
    api.get<Record<string, number>>("/api/islemkayitlari/istatistik").then(setOzet).catch(() => {});
  }, [params, sayfa]);

  useEffect(() => { yukle(); }, [yukle]);
  useEffect(() => {
    api.get<IslemKaydiSecenekleri>("/api/islemkayitlari/secenekler").then(setSecenekler).catch(() => {});
  }, []);
  // Başka bir kullanıcı işlem yaptığında log kendiliğinden tazelenir.
  useCanliYenileme(yukle);

  const filtreler = useMemo<FiltreSecim[]>(() => [
    {
      label: "İşlem Türü", value: islem, coklu: true,
      options: (secenekler?.islemler ?? []).map(i => ({ deger: i, etiket: i })),
      onChange: d => { setSayfa(1); setIslem(d); },
    },
    {
      label: "Kullanıcı", value: kullaniciId, coklu: true,
      options: (secenekler?.kullanicilar ?? []).map(k => ({ deger: String(k.id), etiket: `${k.adSoyad} (${sayiGoster(k.adet)})` })),
      onChange: d => { setSayfa(1); setKullaniciId(d); },
    },
  ], [islem, kullaniciId, secenekler]);

  const sayfaSayisi = Math.max(1, Math.ceil(toplam / SAYFA_BOYUTU));

  return <AppShell title="İşlem Kayıtları" sistemYonetimi>
    <FilterBar
      filters={filtreler}
      searchValue={arama}
      onSearch={d => { setSayfa(1); setArama(d); }}
      onReset={() => { setIslem(""); setKullaniciId(""); setBaslangic(""); setBitis(""); setArama(""); setSayfa(1); }}
      ekAlanlar={
        <div className="filter-select filter-range">
          <span id="log-tarih-etiketi">Tarih Aralığı</span>
          <div className="date-range" role="group" aria-labelledby="log-tarih-etiketi">
            <input type="date" aria-label="Başlangıç tarihi" value={baslangic} max={bitis || undefined}
              onChange={e => { setSayfa(1); setBaslangic(e.target.value); }} />
            <i aria-hidden="true">–</i>
            <input type="date" aria-label="Bitiş tarihi" value={bitis} min={baslangic || undefined}
              onChange={e => { setSayfa(1); setBitis(e.target.value); }} />
          </div>
        </div>
      }
      extra={<>
        <button className="secondary-button" onClick={() => { setSayfa(1); setBaslangic(gunOnce(7)); setBitis(gunOnce(0)); }}>
          <CalendarDays size={16} /> Son 7 Gün
        </button>
        <button className="secondary-button" onClick={() =>
          api.indir(`/api/islemkayitlari/disa-aktar?${params()}`)
            .then(() => setToast("İşlem kayıtları indirildi."))
            .catch(() => setToast("Dışa aktarma başarısız oldu."))}>
          <CloudDownload size={16} /> Dışa Aktar
        </button>
      </>}
    />
    <div className="stats-grid four">
      <StatCard icon={ClipboardList} label="Toplam İşlem" value={ozet ? sayiGoster(ozet.toplam) : "…"} detail="Tüm kayıtlar" />
      <StatCard icon={Clock3} label="Bugün" value={ozet ? sayiGoster(ozet.bugunku) : "…"} detail="Bugün yapılan işlemler" tone="green" />
      <StatCard icon={CalendarDays} label="Son 7 Gün" value={ozet ? sayiGoster(ozet.haftalik) : "…"} detail="Son bir haftada" tone="purple" />
      <StatCard icon={UsersRound} label="İşlem Yapan" value={ozet ? sayiGoster(ozet.kullaniciSayisi) : "…"} detail="Farklı kullanıcı" tone="orange" />
    </div>
    <section className="panel table-panel">
      {hata ? <p className="security-note" role="alert">{hata}</p>
        : <div className="table-scroll"><table>
          <thead><tr><th>Tarih</th><th>Kullanıcı</th><th>Üye</th><th>İşlem</th><th>Ayrıntı</th></tr></thead>
          <tbody>
            {!kayitlar.length && <tr><td colSpan={5} style={{ textAlign: "center", padding: "28px 0", color: "#7a8699" }}>
              {yukleniyor ? "Yükleniyor..." : "Seçili süzgeçlere uyan işlem kaydı yok."}
            </td></tr>}
            {kayitlar.map(k => <tr key={k.id}>
              <td>{tarihGoster(k.tarih)}</td>
              <td><strong>{k.kullaniciAdSoyad}</strong><small>{k.kullaniciAdi}</small></td>
              {/* Üye silinmiş olabilir: unvan kayıtta kopya durduğu için geçmiş okunur kalır. */}
              <td>{k.esnafUnvan ?? "-"}</td>
              <td><span className={`badge ${islemTonu(k.islem)}`}>{k.islem}</span></td>
              <td className="gorusme-not">{k.detay || "-"}</td>
            </tr>)}
          </tbody>
        </table></div>}
      <div className="pagination">
        <span>Toplam {sayiGoster(toplam)} kayıt</span>
        <div>
          <button onClick={() => setSayfa(s => Math.max(1, s - 1))} disabled={sayfa <= 1}>‹</button>
          <button className="active">{sayfa} / {sayfaSayisi}</button>
          <button onClick={() => setSayfa(s => Math.min(sayfaSayisi, s + 1))} disabled={sayfa >= sayfaSayisi}>›</button>
        </div>
        <span />
      </div>
    </section>
    <Toast message={toast} onClose={() => setToast("")} />
  </AppShell>;
}
