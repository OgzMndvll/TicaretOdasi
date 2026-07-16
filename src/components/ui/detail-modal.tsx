"use client";

import { Modal } from "./modal";
import { durumTonu, tarihGoster } from "@/lib/api";

export interface DetaySatiri { etiket: string; deger: string | null | undefined; rozet?: boolean }
export interface DetayGorusme { id: number; tarih: string; sonuc: string; not?: string | null; gorevli?: string | null }

export function DetailModal({ open, baslik, satirlar, gorusmeler, onClose }: {
  open: boolean; baslik: string; satirlar: DetaySatiri[];
  gorusmeler?: DetayGorusme[]; onClose: () => void;
}) {
  return <Modal open={open} title={baslik} onClose={onClose}>
    <div className="action-form">
      <div className="form-grid">
        {satirlar.map(s => <div key={s.etiket} className="form-field">
          <span>{s.etiket}</span>
          {s.rozet
            ? <div><span className={`badge ${durumTonu(s.deger ?? "")}`}>{s.deger ?? "-"}</span></div>
            : <strong style={{ fontSize: 14 }}>{s.deger || "-"}</strong>}
        </div>)}
      </div>
      {gorusmeler && <>
        <h3 style={{ margin: "18px 0 8px" }}>Görüşme Geçmişi ({gorusmeler.length})</h3>
        {gorusmeler.length === 0 ? <p style={{ color: "#7a8699" }}>Henüz görüşme kaydı yok.</p>
          : <div className="table-scroll"><table>
            <thead><tr><th>Tarih</th><th>Görevli</th><th>Sonuç</th><th>Not</th></tr></thead>
            <tbody>{gorusmeler.map(g => <tr key={g.id}>
              <td>{tarihGoster(g.tarih)}</td><td>{g.gorevli ?? "-"}</td>
              <td><span className={`badge ${durumTonu(g.sonuc)}`}>{g.sonuc}</span></td>
              <td>{g.not ?? "-"}</td>
            </tr>)}</tbody>
          </table></div>}
      </>}
      <footer><button type="button" className="secondary-button" onClick={onClose}>Kapat</button></footer>
    </div>
  </Modal>;
}
