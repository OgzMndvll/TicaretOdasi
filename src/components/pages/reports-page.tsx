"use client";

import { useEffect, useState } from "react";
import { CheckCircle2, CircleHelp, CloudDownload, MessageSquareText, Store, XCircle } from "lucide-react";
import { AppShell } from "@/components/layout/app-shell";
import { BarList, DonutChart, LineChart } from "@/components/ui/charts";
import { StatCard } from "@/components/ui/stat-card";
import { Toast } from "@/components/ui/toast";
import { api, DashboardOzet, GrupRaporSatiri, sayiGoster, yuzde } from "@/lib/api";

export function ReportsPage() {
  const [ozet, setOzet] = useState<DashboardOzet | null>(null);
  const [rapor, setRapor] = useState<GrupRaporSatiri[]>([]);
  const [hata, setHata] = useState("");
  const [toast, setToast] = useState("");

  useEffect(() => {
    api.get<DashboardOzet>("/api/dashboard").then(setOzet)
      .catch(() => setHata("Veriler alınamadı. API'nin çalıştığından emin olun (http://localhost:5180)."));
    api.get<GrupRaporSatiri[]>("/api/raporlar/gruplar").then(setRapor).catch(() => {});
  }, []);

  const toplamGorusme = ozet?.aralikGorusme ?? 0;

  return <AppShell title="Raporlar">
    {hata && <section className="panel"><p role="alert">{hata}</p></section>}
    <div className="stats-grid five">
      <StatCard icon={Store} label="Toplam Üye" value={ozet ? sayiGoster(ozet.toplamEsnaf) : "…"} detail="Tüm üyeler" />
      <StatCard icon={MessageSquareText} label="Görüşülen Üye" value={ozet ? sayiGoster(ozet.gorusulen) : "…"} detail={ozet ? yuzde(ozet.gorusulen, ozet.toplamEsnaf) : ""} />
      <StatCard icon={CheckCircle2} label="Onay Oranı" value={ozet ? yuzde(ozet.onayVeren, ozet.toplamEsnaf) : "…"} detail={ozet ? `${sayiGoster(ozet.onayVeren)} onay` : ""} tone="green" />
      <StatCard icon={XCircle} label="Ret Oranı" value={ozet ? yuzde(ozet.onayVermeyen, ozet.toplamEsnaf) : "…"} detail={ozet ? `${sayiGoster(ozet.onayVermeyen)} ret` : ""} tone="red" />
      <StatCard icon={CircleHelp} label="Kararsız Oranı" value={ozet ? yuzde(ozet.kararsiz, ozet.toplamEsnaf) : "…"} detail={ozet ? `${sayiGoster(ozet.kararsiz)} kararsız` : ""} tone="orange" />
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
      <section className="panel"><h2>Günlük Görüşme Trendi <small>(son 30 gün: {sayiGoster(toplamGorusme)} görüşme)</small></h2><LineChart data={ozet?.gunlukGorusmeler ?? []} /></section>
      <section className="panel"><h2>Çalışanlara Göre Performans</h2><BarList items={(ozet?.gorevliPerformans ?? []).map(p => ({ name: p.adSoyad, value: p.adet }))} /></section>
    </div>
    <section className="panel table-panel">
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
        <h2>Grup / Meslek Grubuna Göre Rapor</h2>
        <button className="dark-button" onClick={() =>
          api.indir("/api/raporlar/gruplar/disa-aktar")
            .then(() => setToast("Grup raporu indirildi."))
            .catch(() => setToast("Rapor indirilemedi. API'nin çalıştığından emin olun."))}>
          <CloudDownload size={16} /> Excel&apos;e Aktar
        </button>
      </div>
      <div className="table-scroll"><table>
      <thead><tr><th>No</th><th>Grup / Meslek Grubu</th><th>Toplam Üye</th><th>Görüşme</th><th>Onaylayan</th><th>Reddedilen</th><th>Kararsız</th><th>Görüşülmeyen</th><th>Onay Oranı</th></tr></thead>
      <tbody>{rapor.map(r => <tr key={r.id}>
        <td>{r.no ?? "-"}</td><td><strong>{r.ad}</strong></td><td>{sayiGoster(r.toplamEsnaf)}</td><td>{sayiGoster(r.gorusme)}</td>
        <td>{sayiGoster(r.onaylayan)}</td><td>{sayiGoster(r.reddedilen)}</td><td>{sayiGoster(r.kararsiz)}</td><td>{sayiGoster(r.gorusulmeyen)}</td>
        <td><span className="ratio"><i style={{ width: `${Math.min(100, r.onayOrani)}%` }} />%{r.onayOrani.toLocaleString("tr-TR")}</span></td>
      </tr>)}</tbody>
    </table></div></section>
    <Toast message={toast} onClose={() => setToast("")} />
  </AppShell>;
}
