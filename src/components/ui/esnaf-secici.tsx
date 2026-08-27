"use client";

import { useEffect, useRef, useState } from "react";
import { ChevronDown, Search, X } from "lucide-react";
import { api, grupEtiketi, EsnafKaydi, GrupKaydi, Sayfali } from "@/lib/api";

/**
 * Binlerce esnaf arasından hızlı seçim: yazarak arama + grup filtresi, sonuçlar A-Z.
 * Form gönderiminde seçilen esnafın kimliği `name` alanıyla iletilir.
 */
export function EsnafSecici({ name, required, defaultId, defaultEtiket, sadeceGorevlendirilmis = false, sadeceFaal = false, onSecim }: {
  name: string; required?: boolean; defaultId?: string; defaultEtiket?: string;
  /** true: yalnızca oturumdaki görevlinin kabul ettiği görevlendirmelerdeki esnaflar listelenir. */
  sadeceGorevlendirilmis?: boolean;
  /** true: yalnızca üyeliği "Faal" olanlar listelenir (görüşme yalnızca onlarla yapılabilir). */
  sadeceFaal?: boolean;
  /** Seçim değiştiğinde üst forma bildirilir (ör. görüşme sırasını hesaplamak için). */
  onSecim?: (esnafId: string) => void;
}) {
  const [acik, setAcik] = useState(false);
  const [arama, setArama] = useState("");
  const [grupId, setGrupId] = useState("");
  const [gruplar, setGruplar] = useState<GrupKaydi[]>([]);
  const [sonuclar, setSonuclar] = useState<EsnafKaydi[]>([]);
  const [toplam, setToplam] = useState(0);
  const [yukleniyor, setYukleniyor] = useState(false);
  const [secili, setSecili] = useState<{ id: string; etiket: string } | null>(
    defaultId ? { id: defaultId, etiket: defaultEtiket ?? `Kayıt #${defaultId}` } : null);
  const kapRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!acik) return;
    api.get<GrupKaydi[]>("/api/gruplar").then(setGruplar).catch(() => {});
  }, [acik]);

  // Arama/grup değiştikçe kısa bir gecikmeyle API'den A-Z sıralı sonuç çek
  useEffect(() => {
    if (!acik) return;
    setYukleniyor(true);
    const zamanlayici = setTimeout(() => {
      const params = new URLSearchParams({ sirala: "ad", sayfaBoyutu: "50" });
      if (sadeceGorevlendirilmis) params.set("gorevlendirilmis", "true");
      // Askıdaki/pasif üyelerle görüşme kaydı açılamıyor; listede de görünmesinler.
      if (sadeceFaal) params.set("uyelikDurumu", "Faal");
      if (arama.trim()) params.set("arama", arama.trim());
      if (grupId) params.set("grupId", grupId);
      api.get<Sayfali<EsnafKaydi>>(`/api/esnaflar?${params}`)
        .then(v => { setSonuclar(v.kayitlar); setToplam(v.toplam); })
        .catch(() => setSonuclar([]))
        .finally(() => setYukleniyor(false));
    }, 250);
    return () => clearTimeout(zamanlayici);
  }, [acik, arama, grupId, sadeceGorevlendirilmis, sadeceFaal]);

  // Dışarı tıklayınca kapan
  useEffect(() => {
    if (!acik) return;
    const kapat = (e: PointerEvent) => {
      if (!kapRef.current?.contains(e.target as Node)) setAcik(false);
    };
    document.addEventListener("pointerdown", kapat);
    return () => document.removeEventListener("pointerdown", kapat);
  }, [acik]);

  return <div className="esnaf-secici" ref={kapRef}>
    <input type="hidden" name={name} value={secili?.id ?? ""} />
    <button type="button" className={`esnaf-secici-kutu ${acik ? "acik" : ""}`} onClick={() => setAcik(a => !a)}>
      <span className={secili ? "" : "bos"}>{secili?.etiket ?? (sadeceFaal ? "Faal üye seçin — yazarak arayabilirsiniz" : "Üye seçin — yazarak arayabilirsiniz")}</span>
      {secili
        ? <i role="button" aria-label="Seçimi temizle" onClick={e => { e.stopPropagation(); setSecili(null); onSecim?.(""); }}><X size={15} /></i>
        : <ChevronDown size={15} />}
    </button>
    {/* required doğrulaması: görünmez ama form doğrulamasına katılan alan */}
    {required && <input tabIndex={-1} aria-hidden className="esnaf-secici-dogrulama" required value={secili?.id ?? ""} onChange={() => {}} />}
    {acik && <div className="esnaf-secici-panel">
      <div className="esnaf-secici-filtreler">
        <div className="esnaf-secici-arama">
          <Search size={15} />
          <input autoFocus placeholder={`${sadeceFaal ? "Faal üye" : "Üye"} adı, unvan veya telefon...`} value={arama} maxLength={100}
            onChange={e => setArama(e.target.value)} />
        </div>
        <select aria-label="Gruba göre filtrele" value={grupId} onChange={e => setGrupId(e.target.value)}>
          <option value="">Tüm Gruplar</option>
          {gruplar.map(g => <option key={g.id} value={g.id}>{grupEtiketi(g.no, g.ad)}</option>)}
        </select>
      </div>
      <ul>
        {yukleniyor && <li className="bilgi">Aranıyor...</li>}
        {!yukleniyor && sonuclar.length === 0 && <li className="bilgi">
          {sadeceFaal ? "Faal üye bulunamadı. Askıdaki üyelerle görüşme yapılamaz." : "Sonuç bulunamadı."}
        </li>}
        {!yukleniyor && sonuclar.map(e => <li key={e.id}>
          <button type="button" onClick={() => { setSecili({ id: String(e.id), etiket: `${e.adSoyad} — ${e.isletme}` }); setAcik(false); onSecim?.(String(e.id)); }}>
            <b>{e.adSoyad}</b>
            <small>{e.isletme}{e.grup ? ` · ${e.grup}` : ""}{e.ilce ? ` · ${e.ilce}` : ""}</small>
          </button>
        </li>)}
      </ul>
      {!yukleniyor && toplam > sonuclar.length &&
        <p className="esnaf-secici-dip">İlk {sonuclar.length} sonuç gösteriliyor (toplam {toplam.toLocaleString("tr-TR")}). Aramayı daraltın.</p>}
    </div>}
  </div>;
}
