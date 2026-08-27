"use client";

import { Pencil, Phone, Plus, SquarePen, Trash2, UsersRound } from "lucide-react";
import { Modal } from "./modal";
import { durumTonu, tarihGoster, EsnafYetkilisi } from "@/lib/api";

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
export function DetailModal({ open, baslik, satirlar, yetkililer, birincilYetkili, gorusmeler, onClose, onDuzenle, onGorusmeEkle, onGorusmeDuzenle, onGorusmeSil }: {
  open: boolean; baslik: string; satirlar: DetaySatiri[];
  /** Üyenin oda kaydındaki tüm yetkilileri. Tek yetkili varsa bölüm gösterilmez (özette zaten var). */
  yetkililer?: EsnafYetkilisi[];
  /** Özetteki "Yetkili Kişi" alanında görünen ad; listede birincil olarak işaretlenir. */
  birincilYetkili?: string;
  gorusmeler?: DetayGorusme[]; onClose: () => void;
  /** Üyelik durumu düzenleme ekranını açar. Yalnızca üye kartında bulunur. */
  onDuzenle?: () => void;
  onGorusmeEkle?: () => void;
  onGorusmeDuzenle?: (g: DetayGorusme) => void;
  onGorusmeSil?: (g: DetayGorusme) => void;
}) {
  return <Modal open={open} title={baslik} onClose={onClose} genis={!!gorusmeler} ustBaslik="Üye kartı">
    <div className="action-form">
      {onDuzenle && <div className="kart-islemleri">
        <button type="button" className="secondary-button" onClick={onDuzenle}>
          <SquarePen size={16} /> Düzenle
        </button>
      </div>}
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
      {/* Oda kaydında birden çok yetkili bulunan şirketler (banka şubeleri, kooperatifler…) için.
          Tek yetkili varsa bölüm çizilmez: o ad zaten yukarıdaki özette duruyor. */}
      {yetkililer && yetkililer.length > 1 && <section className="yetkili-bolumu">
        <header>
          <h3><UsersRound size={15} /> Yetkililer ({yetkililer.length})</h3>
          <small>Oda kaydındaki tüm imza yetkilileri</small>
        </header>
        <ul className="yetkili-listesi">
          {yetkililer.map(y => {
            const birincil = !!birincilYetkili && y.adSoyad === birincilYetkili;
            return <li key={y.id} className={birincil ? "birincil" : ""}>
              <b>{y.adSoyad}</b>
              {/* Görev ve yetki tarihleri kaynak raporda çoğunlukla boş; yalnızca doluysa yazılır. */}
              {y.gorevi && <small>{y.gorevi}</small>}
              {(y.yetkiBaslangic || y.yetkiBitis) && <small>
                {tarihGoster(y.yetkiBaslangic).slice(0, 10)} – {y.yetkiBitis ? tarihGoster(y.yetkiBitis).slice(0, 10) : "süresiz"}
              </small>}
              {birincil && <em>Birincil</em>}
            </li>;
          })}
        </ul>
      </section>}
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
              <th>Görüşme</th><th>Görüşen Çalışan</th><th>Görüşecek Kişi</th><th>Tarih</th><th>Sonuç</th><th>Takip Durumu</th><th>Not</th>
              {(onGorusmeDuzenle || onGorusmeSil) && <th>İşlemler</th>}
            </tr></thead>
            <tbody>{gorusmeler.map(g => <tr key={g.id}>
              <td><strong>{g.sira}. Görüşme</strong></td>
              <td>{g.gorevli ?? "-"}</td>
              <td>{g.ikinciGorevli ?? "-"}</td>
              <td>{tarihGoster(g.tarih)}</td>
              <td><span className={`badge ${durumTonu(g.sonuc)}`}>{g.sonuc}</span></td>
              {/* Sunucuda bool tutulur: true = "Takip Edilecek", false = "Gelmeyecek". */}
              <td><span className={`badge ${g.takipGerekli ? "warning" : "neutral"}`}>
                {g.takipGerekli ? "Takip Edilecek" : "Gelmeyecek"}</span></td>
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
