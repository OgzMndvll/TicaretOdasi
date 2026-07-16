const TOKEN_ANAHTARI = "etso_token";

export interface Kimlik { id: number; adSoyad: string; kullaniciAdi: string; rol: string; bitis: number }

export function tokenAl(): string | null {
  if (typeof window === "undefined") return null;
  return window.localStorage.getItem(TOKEN_ANAHTARI) ?? window.sessionStorage.getItem(TOKEN_ANAHTARI);
}

/**
 * "Beni Hatırla" işaretliyse token tarayıcı kapansa da kalır (localStorage);
 * işaretli değilse yalnızca o sekme açık kaldığı sürece yaşar (sessionStorage).
 */
export function tokenKaydet(token: string, hatirla = true) {
  const hedef = hatirla ? window.localStorage : window.sessionStorage;
  const digeri = hatirla ? window.sessionStorage : window.localStorage;
  digeri.removeItem(TOKEN_ANAHTARI);
  hedef.setItem(TOKEN_ANAHTARI, token);
}

export function cikisYap() {
  window.localStorage.removeItem(TOKEN_ANAHTARI);
  window.sessionStorage.removeItem(TOKEN_ANAHTARI);
  window.location.href = "/giris";
}

/** JWT gövdesini çözer (imza doğrulaması sunucuda yapılır; burada yalnızca görüntüleme amaçlı okunur). */
export function kimlik(): Kimlik | null {
  const token = tokenAl();
  if (!token) return null;
  try {
    const ham = atob(token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/"));
    // atob latin1 döndürür; Türkçe karakterler için baytlar UTF-8 olarak yeniden çözülür.
    const govde = JSON.parse(new TextDecoder("utf-8").decode(Uint8Array.from(ham, c => c.charCodeAt(0))));
    if (typeof govde.exp === "number" && govde.exp * 1000 < Date.now()) return null;
    return {
      id: Number(govde.sub),
      adSoyad: govde.adSoyad ?? "",
      kullaniciAdi: govde.unique_name ?? "",
      rol: govde.rol ?? "",
      bitis: govde.exp,
    };
  } catch {
    return null;
  }
}

export function girisliMi(): boolean {
  return kimlik() !== null;
}

export function yoneticiMi(): boolean {
  return kimlik()?.rol === "Yönetici";
}
