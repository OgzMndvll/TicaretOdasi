"use client";

import { Pencil, Phone, Plus, Trash2 } from "lucide-react";
import { Modal } from "./modal";
import { durumTonu, tarihGoster } from "@/lib/api";

export interface DetaySatiri {
  etiket: string; deger: string | null | undefined; rozet?: boolean;
  /** "telefon": değer aranabilir bağlantı olarak gösterilir. */
  tur?: "telefon";
}
export interface DetayGorusme {
  id: number; sira: number; tarih: string; sonuc: string; not?: string | null;
  takipGerekli?: boolean; gorevliId?: number | null; gorevli?: string | null;
  ikinciGorevliId?: number | null; ikinciGorevli?: string | null;
}

/**
 * Üye kartı. Görüşmeler ayrı bir sayfa değil, üyenin kendi ekranında yönetilir:
 * geçmiş burada listelenir, yeni görüşme buradan eklenir, kayıtlar buradan düzenlenir/silinir.
 */
export function DetailModal({ open, baslik, satirlar, gorusmeler, onClose, onGorusmeEkle, onGorusmeDuzenle, onGorusmeSil }: {
  open: boolean; baslik: string; satirlar: DetaySatiri[];
  gorusmeler?: DetayGorusme[]; onClose: () => void;
  onGorusmeEkle?: () => void;
  onGorusmeDuzenle?: (g: DetayGorusme) => void;
  onGorusmeSil?: (g: DetayGorusme) => void;
}) {
  return <Modal open={open} title={baslik} onClose={onClose} genis={!!gorusmeler} ustBaslik="Üye kartı">
    <div className="action-form">
      {/* Üç alanlık üye özeti tek satırda ve vurgulu; daha kalabalık kartlar iki sütunda kalır. */}
      <div className={satirlar.length <= 3 ? "form-grid ozet-grid" : "form-grid"}>
        {satirlar.map(s => <div key={s.etiket} className="form-field">
          <span>{s.etiket}</span>
          {s.rozet
            ? <div><span className={`badge ${durumTonu(s.deger ?? "")}`}>{s.deger ?? "-"}</span></div>
            : s.tur === "telefon" && s.deger
              ? <a className="ozet-telefon" href={`tel:${s.deger.replace(/[^\d+]/g, "")}`}>
                  <Phone size={14} />{s.deger}
                </a>
              : <strong className="detay-deger">{s.deger || "-"}</strong>}
        </div>)}
      </div>
      {gorusmeler && <section className="gorusme-bolumu">
        <header>
          <h3>Görüşmeler ({gorusmeler.length})</h3>
          {onGorusmeEkle && <button type="button" className="dark-button" onClick={onGorusmeEkle}>
            <Plus size={16} />Yeni Görüşme
          </button>}
        </header>
        {gorusmeler.length === 0
          ? <p className="gorusme-bos">Bu üyeyle henüz görüşme yapılmamış. &quot;Yeni Görüşme&quot; ile ilk kaydı ekleyin.</p>
          : <div className="table-scroll"><table>
            <thead><tr>
              <th>Görüşme</th><th>Görüşen Çalışan</th><th>Görüşecek Kişi</th><th>Tarih</th><th>Sonuç</th><th>Takip</th><th>Not</th>
              {(onGorusmeDuzenle || onGorusmeSil) && <th>İşlemler</th>}
            </tr></thead>
            <tbody>{gorusmeler.map(g => <tr key={g.id}>
              <td><strong>{g.sira}. Görüşme</strong></td>
              <td>{g.gorevli ?? "-"}</td>
              <td>{g.ikinciGorevli ?? "-"}</td>
              <td>{tarihGoster(g.tarih)}</td>
              <td><span className={`badge ${durumTonu(g.sonuc)}`}>{g.sonuc}</span></td>
              <td>{g.takipGerekli ? <span className="badge warning">Gerekli</span> : "-"}</td>
              <td className="gorusme-not">{g.not || "-"}</td>
              {(onGorusmeDuzenle || onGorusmeSil) && <td><div className="row-actions">
                {onGorusmeDuzenle && <button type="button" aria-label="Görüşmeyi düzenle" title="Düzenle"
                  onClick={() => onGorusmeDuzenle(g)}><Pencil size={15} /></button>}
                {onGorusmeSil && <button type="button" aria-label="Görüşmeyi sil" title="Sil"
                  onClick={() => onGorusmeSil(g)}><Trash2 size={15} /></button>}
              </div></td>}
            </tr>)}</tbody>
          </table></div>}
      </section>}
      <footer><button type="button" className="secondary-button" onClick={onClose}>Kapat</button></footer>
    </div>
  </Modal>;
}
