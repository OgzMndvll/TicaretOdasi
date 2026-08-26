"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { CheckCircle2, CircleHelp, MessageSquareText, Store, UserRound, UsersRound, XCircle } from "lucide-react";
import { AppShell } from "@/components/layout/app-shell";
import { ActionModal } from "@/components/ui/action-modal";
import { BarList, DonutChart, LineChart } from "@/components/ui/charts";
import { FilterBar, FiltreSecim } from "@/components/ui/filter-bar";
import { StatCard } from "@/components/ui/stat-card";
import { Toast } from "@/components/ui/toast";
import { api, API_ERISIM_HATASI, DashboardOzet, GrupKaydi, durumTonu, sayiGoster, tarihGoster, yuzde } from "@/lib/api";
import { CanliRozet } from "@/components/ui/canli-rozet";
import { useCanliYenileme } from "@/lib/canli";

/** Bugünden n gün önceki tarihi date girdisinin beklediği "yyyy-aa-gg" biçiminde verir. */
function gunOnce(n: number): string {
  const t = new Date();
  t.setDate(t.getDate() - n);
  return `${t.getFullYear()}-${String(t.getMonth() + 1).padStart(2, "0")}-${String(t.getDate()).padStart(2, "0")}`;
}

const HAZIR_ARALIKLAR = [7, 30, 90];

/** Üyenin onay durumuna göre satır rengi (görüşmeler tablosuyla aynı kural). */
function satirTonu(durum?: string | null): string {
  switch (durum) {
    case "Onay Verdi": return "satir-onayli";
    case "Onay Vermedi": return "satir-red";
    case "Kararsız": return "satir-kararsiz";
    default: return "";
  }
}

export function DashboardPage() {
  const [action, setAction] = useState("");
  const [toast, setToast] = useState("");
  const [ozet, setOzet] = useState<DashboardOzet | null>(null);
  const [hata, setHata] = useState("");
  const [gruplar, setGruplar] = useState<GrupKaydi[]>([]);

  const [grupId, setGrupId] = useState("");
  const [baslangic, setBaslangic] = useState(gunOnce(30));
  const [bitis, setBitis] = useState(gunOnce(0));

  useEffect(() => {
    api.get<GrupKaydi[]>("/api/gruplar").then(setGruplar).catch(() => {});
  }, []);

  const yukle = useCallback(() => {
    const params = new URLSearchParams();
    if (grupId) params.set("grupId", grupId);
    if (baslangic) params.set("baslangic", baslangic);
    if (bitis) params.set("bitis", bitis);
    api.get<DashboardOzet>(`/api/dashboard?${params}`)
      .then(v => { setOzet(v); setHata(""); })
      .catch(() => setHata(API_ERISIM_HATASI));
  }, [grupId, baslangic, bitis]);

  useEffect(() => { yukle(); }, [yukle]);

  // Başka bir kullanıcı görüşme girdiğinde özet ve grafikler kendiliğinden tazelenir.
  const canliDurum = useCanliYenileme(yukle);

  const filtreler = useMemo<FiltreSecim[]>(() => [
    {
      label: "Meslek Grubu",
      value: grupId,
      options: gruplar.map(g => ({ deger: String(g.id), etiket: g.no ? `${g.no}. ${g.ad}` : g.ad })),
      onChange: setGrupId,
    },
    {
      label: "Hazır Aralık",
      value: "",
      options: HAZIR_ARALIKLAR.map(g => ({ deger: String(g), etiket: `Son ${g} gün` })),
      onChange: deger => { if (deger) { setBaslangic(gunOnce(Number(deger))); setBitis(gunOnce(0)); } },
    },
  ], [gruplar, grupId]);

  const secilenGrup = gruplar.find(g => String(g.id) === grupId);
  const kapsam = secilenGrup ? `${secilenGrup.no ? `${secilenGrup.no}. ` : ""}${secilenGrup.ad}` : "Tüm üyeler";

  // Kart dar olduğu için aralık "Son 30 gün" gibi kısa yazılır; tam tarihler başlıkta durur.
  const gunSayisi = ozet ? ozet.gunlukGorusmeler.length : 0;
  const aralikOzeti = ozet ? `Son ${gunSayisi} gün` : "";
  const aralikTam = ozet ? `${ozet.baslangic} — ${ozet.bitis}` : "";

  return <AppShell title="Dashboard">
    <FilterBar
      filters={filtreler}
      onReset={() => { setGrupId(""); setBaslangic(gunOnce(30)); setBitis(gunOnce(0)); }}
      ekAlanlar={
        // Başlangıç ve bitiş tek bir "aralık" alanı gibi görünür; iki ayrı etiketli kutu
        // hem satırı taşırıyor hem de aralık olduklarını göstermiyordu.
        <div className="filter-select filter-range">
          <span id="tarih-araligi-etiketi">Tarih Aralığı</span>
          <div className="date-range" role="group" aria-labelledby="tarih-araligi-etiketi">
            <input type="date" aria-label="Başlangıç tarihi" value={baslangic} max={bitis}
              onChange={e => setBaslangic(e.target.value)} />
            <i aria-hidden="true">–</i>
            <input type="date" aria-label="Bitiş tarihi" value={bitis} min={baslangic}
              onChange={e => setBitis(e.target.value)} />
          </div>
        </div>
      }
    />
    {hata && <section className="panel"><p role="alert">{hata}</p></section>}
    <div className="stats-grid five">
      <StatCard icon={UsersRound} label="Toplam Üye" value={ozet ? sayiGoster(ozet.toplamEsnaf) : "…"} detail={kapsam} />
      <StatCard icon={MessageSquareText} label="Aralıktaki Görüşme" value={ozet ? sayiGoster(ozet.aralikGorusme) : "…"}
        detail={aralikOzeti} detailTitle={aralikTam} tone="purple" />
      <StatCard icon={CheckCircle2} label="Onay Veren" value={ozet ? sayiGoster(ozet.onayVeren) : "…"}
        detail={ozet ? `Üyelerin ${yuzde(ozet.onayVeren, ozet.toplamEsnaf)}'i` : ""} tone="green" />
      <StatCard icon={XCircle} label="Onay Vermeyen" value={ozet ? sayiGoster(ozet.onayVermeyen) : "…"}
        detail={ozet ? `Üyelerin ${yuzde(ozet.onayVermeyen, ozet.toplamEsnaf)}'i` : ""} tone="red" />
      <StatCard icon={CircleHelp} label="Kararsız" value={ozet ? sayiGoster(ozet.kararsiz) : "…"}
        detail={ozet ? `Üyelerin ${yuzde(ozet.kararsiz, ozet.toplamEsnaf)}'ü` : ""} tone="orange" />
    </div>
    <div className="dashboard-charts">
      <section className="panel"><h2>Onay Durumuna Göre Dağılım</h2>
        <DonutChart segments={ozet ? [
          { label: "Onay Veren", value: ozet.onayVeren, color: "green" },
          { label: "Onay Vermeyen", value: ozet.onayVermeyen, color: "red" },
          { label: "Kararsız", value: ozet.kararsiz, color: "orange" },
          { label: "Görüşülmemiş", value: ozet.gorusulmemis, color: "gray" },
        ] : []} />
      </section>
      <section className="panel"><h2>Günlük Görüşme Sayısı</h2><LineChart data={ozet?.gunlukGorusmeler ?? []} /></section>
      <section className="panel"><h2>Çalışanlara Göre Görüşme Dağılımı</h2>
        <BarList items={(ozet?.gorevliPerformans ?? []).map(p => ({ name: p.adSoyad, value: p.adet }))} />
      </section>
    </div>
    <div className="content-with-aside dashboard-bottom">
      <section className="panel table-panel"><h2 className="baslik-satiri">Son Görüşmeler <CanliRozet durum={canliDurum} /></h2><div className="table-scroll"><table>
        <thead><tr><th>Görüşme</th><th>Üye Adı</th><th>İş Yeri / Unvan</th><th>Meslek Grubu</th><th>Çalışan</th><th>Tarih</th><th>Sonuç</th></tr></thead>
        <tbody>{!(ozet?.sonGorusmeler ?? []).length
          ? <tr><td colSpan={7} style={{ textAlign: "center", padding: "28px 0", color: "#7a8699" }}>Seçili aralıkta görüşme yok.</td></tr>
          : (ozet?.sonGorusmeler ?? []).map(g => <tr key={g.id} className={satirTonu(g.esnafDurum)}>
            <td><strong>{g.sira}. Görüşme</strong></td>
            <td><strong>{g.esnaf}</strong></td><td>{g.isletme}</td>
            <td>{g.grup ? (g.grupNo ? `${g.grupNo}. ${g.grup}` : g.grup) : "-"}</td>
            <td>{g.gorevli}</td><td>{tarihGoster(g.tarih)}</td>
            <td><span className={`badge ${durumTonu(g.sonuc)}`}>{g.sonuc}</span></td>
          </tr>)}</tbody>
      </table></div></section>
      <aside className="panel quick-panel"><h2>Hızlı İşlemler</h2>
        {[["Yeni Üye Ekle", Store], ["Yeni Görüşme", MessageSquareText], ["Yeni Çalışan Ekle", UserRound], ["Yeni Grup Ekle", UsersRound]].map(([label, Icon]) =>
          <button key={label as string} onClick={() => setAction(label as string)}>
            <span>{(() => { const I = Icon as typeof Store; return <I size={20} />; })()}</span>
            <div><b>{label as string}</b><small>Yeni işlem oluştur</small></div>
          </button>)}
      </aside>
    </div>
    <ActionModal action={action} open={!!action} onClose={() => setAction("")} onSuccess={setToast} onSaved={yukle} />
    <Toast message={toast} onClose={() => setToast("")} />
  </AppShell>;
}
