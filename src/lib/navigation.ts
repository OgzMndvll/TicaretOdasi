import {
  BriefcaseBusiness, ChartNoAxesCombined, CheckCheck, LayoutDashboard,
  MessageSquareText, Settings, Store, UserRound, UsersRound,
} from "lucide-react";

export const navigation = [
  { label: "Dashboard", href: "/", icon: LayoutDashboard },
  { label: "Üyeler", href: "/esnaflar", icon: Store },
  { label: "Görevlendirmeler", href: "/gorevlendirmeler", icon: BriefcaseBusiness },
  { label: "Görüşmeler", href: "/gorusmeler", icon: MessageSquareText },
  { label: "Onaylar", href: "/onaylar", icon: CheckCheck },
  { label: "Raporlar", href: "/raporlar", icon: ChartNoAxesCombined },
  { label: "Gruplar", href: "/gruplar", icon: UsersRound },
  { label: "Kullanıcılar", href: "/kullanicilar", icon: UserRound },
  { label: "Ayarlar", href: "/ayarlar", icon: Settings },
];
