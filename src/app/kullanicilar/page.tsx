import { redirect } from "next/navigation";

// Kullanıcılar ekranı "Aktif Çalışanlar" olarak yeniden adlandırıldı; eski bağlantılar yönlendirilir.
export default function Page() { redirect("/calisanlar"); }
