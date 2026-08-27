"use client";

import { useRef, useState } from "react";
import { CloudUpload, Download, FileSpreadsheet } from "lucide-react";
import { Modal } from "./modal";
import { api, ApiError, IceAktarmaSonucu } from "@/lib/api";

export function ImportModal({ open, baslik, yuklemeYolu, sablonYolu, onClose, onDone }: {
  open: boolean; baslik: string; yuklemeYolu: string; sablonYolu: string;
  onClose: () => void; onDone: (mesaj: string) => void;
}) {
  const dosyaRef = useRef<HTMLInputElement>(null);
  const [secili, setSecili] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [hata, setHata] = useState("");
  const [sonuc, setSonuc] = useState<IceAktarmaSonucu | null>(null);

  function kapat() {
    setSecili(null); setHata(""); setSonuc(null);
    if (dosyaRef.current) dosyaRef.current.value = "";
    onClose();
  }

  async function yukle() {
    if (!secili) { setHata("Önce bir .xlsx dosyası seçin."); return; }
    setBusy(true); setHata(""); setSonuc(null);
    try {
      const s = await api.yukle<IceAktarmaSonucu>(yuklemeYolu, secili);
      setSonuc(s);
      if (s.eklenen > 0 || s.guncellenen > 0)
        onDone(`${s.eklenen} yeni kayıt eklendi, ${s.guncellenen} kayıt güncellendi.`);
    } catch (e) {
      setHata(e instanceof ApiError ? e.message : "Sunucuya ulaşılamadı.");
    } finally {
      setBusy(false);
    }
  }

  return <Modal open={open} title={baslik} onClose={kapat}>
    <div className="action-form">
      <p style={{ lineHeight: 1.6, color: "#5b6779" }}>
        Sistem şablonunun yanı sıra oda üye listelerindeki <b>UNVAN</b>, <b>ADRES</b>, <b>İŞ TELEFONU</b>,
        <b> CEP TELEFONU (GSM)</b> ve <b>YETKİLİ ADI SOYADI</b> sütunları da otomatik tanınır.
        Sistemde zaten kayıtlı olan üyeler <b>ikizlenmez</b>: değişen alanları güncellenir,
        değişmeyenler olduğu gibi bırakılır. Görüşme sonuçları ve atanan görevli içe aktarmadan
        etkilenmez; dosyada boş bırakılan bir sütun mevcut bilgiyi silmez.
      </p>
      <button type="button" className="secondary-button" style={{ alignSelf: "flex-start" }}
        onClick={() => api.indir(sablonYolu).catch(() => setHata("Şablon indirilemedi."))}>
        <Download size={16} /> Boş Şablonu İndir
      </button>
      <label className="form-field">
        <span>Excel Dosyası (.xlsx)</span>
        <input ref={dosyaRef} type="file" accept=".xlsx"
          onChange={e => { setSecili(e.target.files?.[0] ?? null); setSonuc(null); setHata(""); }} />
      </label>
      {secili && <p style={{ display: "flex", alignItems: "center", gap: 8 }}><FileSpreadsheet size={17} />{secili.name} ({Math.ceil(secili.size / 1024)} KB)</p>}
      {hata && <p role="alert" style={{ color: "#c0392b" }}>{hata}</p>}
      {sonuc && <div className="panel" style={{ padding: 14 }}>
        <b>Sonuç:</b> {sonuc.eklenen} yeni kayıt eklendi, {sonuc.guncellenen} kayıt güncellendi,
        {" "}{sonuc.atlanan} kayıt değişmediği için olduğu gibi bırakıldı.
        {sonuc.hatalar.length > 0 && <>
          <br /><b>Uyarılar ({sonuc.hatalar.length}):</b>
          <ul style={{ margin: "6px 0 0 18px", maxHeight: 160, overflowY: "auto" }}>
            {sonuc.hatalar.map((h, i) => <li key={i}>{h}</li>)}
          </ul>
        </>}
      </div>}
      <footer>
        <button type="button" className="secondary-button" onClick={kapat}>{sonuc ? "Kapat" : "Vazgeç"}</button>
        <button type="button" className="primary-button" disabled={busy || !secili} onClick={yukle}>
          <CloudUpload size={17} />{busy ? "Aktarılıyor..." : "İçe Aktar"}
        </button>
      </footer>
    </div>
  </Modal>;
}
