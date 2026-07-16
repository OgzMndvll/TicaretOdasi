"use client";

import { useCallback, useEffect, useState } from "react";
import { CheckCircle2, CircleHelp, MessageSquareText, Store, UserRound, UsersRound, XCircle } from "lucide-react";
import { AppShell } from "@/components/layout/app-shell";
import { ActionModal } from "@/components/ui/action-modal";
import { BarList, DonutChart, LineChart } from "@/components/ui/charts";
import { StatCard } from "@/components/ui/stat-card";
import { Toast } from "@/components/ui/toast";
import { api, API_ERISIM_HATASI, DashboardOzet, durumTonu, sayiGoster, tarihGoster, yuzde } from "@/lib/api";

export function DashboardPage() {
  const [action, setAction] = useState("");
  const [toast, setToast] = useState("");
  const [ozet, setOzet] = useState<DashboardOzet | null>(null);
  const [hata, setHata] = useState("");

  const yukle = useCallback(() => {
    api.get<DashboardOzet>("/api/dashboard")
      .then(v => { setOzet(v); setHata(""); })
      .catch(() => setHata(API_ERISIM_HATASI));
  }, []);

  useEffect(() => { yukle(); }, [yukle]);

  return <AppShell title="Dashboard">
    {hata && <section className="panel"><p role="alert">{hata}</p></section>}
    <div className="stats-grid five">
      <StatCard icon={UsersRound} label="Toplam Üye" value={ozet ? sayiGoster(ozet.toplamEsnaf) : "…"} detail="Tüm kayıtlı üyeler" />
      <StatCard icon={UserRound} label="Görüşülen Üye" value={ozet ? sayiGoster(ozet.gorusulen) : "…"} detail={ozet ? `Toplam üyenin ${yuzde(ozet.gorusulen, ozet.toplamEsnaf)}'i` : ""} />
      <StatCard icon={CheckCircle2} label="Onay Veren" value={ozet ? sayiGoster(ozet.onayVeren) : "…"} detail={ozet ? `Toplam üyenin ${yuzde(ozet.onayVeren, ozet.toplamEsnaf)}'i` : ""} tone="green" />
      <StatCard icon={XCircle} label="Onay Vermeyen" value={ozet ? sayiGoster(ozet.onayVermeyen) : "…"} detail={ozet ? `Toplam üyenin ${yuzde(ozet.onayVermeyen, ozet.toplamEsnaf)}'i` : ""} tone="red" />
      <StatCard icon={CircleHelp} label="Kararsız" value={ozet ? sayiGoster(ozet.kararsiz) : "…"} detail={ozet ? `Toplam üyenin ${yuzde(ozet.kararsiz, ozet.toplamEsnaf)}'ü` : ""} tone="orange" />
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
      <section className="panel"><h2>Aylık Görüşme İstatistikleri</h2><LineChart data={ozet?.aylikGorusmeler ?? []} /></section>
      <section className="panel"><h2>Görevlilere Göre Görüşme Dağılımı</h2><BarList items={(ozet?.gorevliPerformans ?? []).map(p => ({ name: p.adSoyad, value: p.adet }))} /></section>
    </div>
    <div className="content-with-aside dashboard-bottom">
      <section className="panel table-panel"><h2>Son Görüşmeler</h2><div className="table-scroll"><table>
        <thead><tr><th>Üye Adı</th><th>İş Yeri / Unvan</th><th>Grup</th><th>Görevli</th><th>Görüşme Tarihi</th><th>Görüşme Durumu</th><th>Onay Durumu</th></tr></thead>
        <tbody>{(ozet?.sonGorusmeler ?? []).map(g => <tr key={g.id}>
          <td><strong>{g.esnaf}</strong></td><td>{g.isletme}</td><td>{g.grup ?? "-"}</td><td>{g.gorevli}</td>
          <td>{tarihGoster(g.tarih)}</td><td><span className="badge success">Görüşüldü</span></td>
          <td><span className={`badge ${durumTonu(g.sonuc)}`}>{g.sonuc}</span></td>
        </tr>)}</tbody>
      </table></div></section>
      <aside className="panel quick-panel"><h2>Hızlı İşlemler</h2>
        {[["Yeni Üye Ekle", Store], ["Yeni Görevlendirme", UserRound], ["Yeni Görüşme", MessageSquareText], ["Yeni Grup Ekle", UsersRound]].map(([label, Icon]) =>
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
