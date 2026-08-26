"use client";

import { useEffect, useRef, useState } from "react";
import { Check, ChevronDown } from "lucide-react";

/**
 * Onay kutulu açılır süzgeç: aynı süzgeçte birden çok değer seçilebilir
 * (ör. "Onay Verdi" + "Kararsız"). Seçimler virgülle birleştirilerek dışarı verilir.
 */
export function CokluSecim({ label, options, value, onChange }: {
  label: string;
  options: { deger: string; etiket: string }[];
  /** Virgülle ayrılmış seçili değerler. Boş dize = süzme yok. */
  value: string;
  onChange: (deger: string) => void;
}) {
  const [acik, setAcik] = useState(false);
  const [arama, setArama] = useState("");
  const kapRef = useRef<HTMLDivElement>(null);
  const secili = value ? value.split(",").filter(Boolean) : [];

  useEffect(() => {
    if (!acik) return;
    const kapat = (e: PointerEvent) => {
      if (!kapRef.current?.contains(e.target as Node)) setAcik(false);
    };
    const klavye = (e: KeyboardEvent) => { if (e.key === "Escape") setAcik(false); };
    document.addEventListener("pointerdown", kapat);
    document.addEventListener("keydown", klavye);
    return () => {
      document.removeEventListener("pointerdown", kapat);
      document.removeEventListener("keydown", klavye);
    };
  }, [acik]);

  function degistir(deger: string) {
    const yeni = secili.includes(deger) ? secili.filter(s => s !== deger) : [...secili, deger];
    onChange(yeni.join(","));
  }

  const gorunen = options.filter(o => !arama.trim() || o.etiket.toLocaleLowerCase("tr").includes(arama.toLocaleLowerCase("tr")));
  const ozet = secili.length === 0
    ? "Tümü"
    : secili.length === 1
      ? (options.find(o => o.deger === secili[0])?.etiket ?? "1 seçili")
      : `${secili.length} seçili`;

  return <div className="coklu-secim" ref={kapRef}>
    <button type="button" className={`coklu-kutu ${acik ? "acik" : ""} ${secili.length ? "dolu" : ""}`}
      aria-haspopup="listbox" aria-expanded={acik} onClick={() => { setArama(""); setAcik(a => !a); }}>
      <span title={ozet}>{ozet}</span>
      {secili.length > 1 && <b>{secili.length}</b>}
      <ChevronDown size={15} />
    </button>
    {acik && <div className="coklu-panel" role="listbox" aria-multiselectable="true" aria-label={label}>
      {options.length > 8 && <input className="coklu-arama" autoFocus placeholder="Ara..." value={arama}
        maxLength={60} onChange={e => setArama(e.target.value)} />}
      <div className="coklu-liste">
        {gorunen.length === 0 && <p className="coklu-bos">
          {options.length === 0 ? "Liste boş." : "Aramayla eşleşen seçenek yok."}
        </p>}
        {gorunen.map(o => {
          const isaretli = secili.includes(o.deger);
          return <button type="button" key={o.deger} role="option" aria-selected={isaretli}
            className={isaretli ? "secili" : ""} onClick={() => degistir(o.deger)}>
            <i>{isaretli && <Check size={12} />}</i>
            <span>{o.etiket}</span>
          </button>;
        })}
      </div>
      {secili.length > 0 && <footer>
        <span>{secili.length} seçili</span>
        <button type="button" onClick={() => onChange("")}>Temizle</button>
      </footer>}
    </div>}
  </div>;
}
