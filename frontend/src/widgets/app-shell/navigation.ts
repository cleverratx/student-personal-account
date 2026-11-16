import { Bell, CalendarDays, FileText, GraduationCap, House, Megaphone, User, type LucideIcon } from 'lucide-react';

export type ShellRole = 'Student' | 'Staff';

export interface ShellNavItem {
  to: string;
  label: string;
  icon: LucideIcon;
  /** На телефоне раздел открывается колокольчиком в шапке, а не из нижней панели. */
  desktopOnly?: boolean;
}

/** Разделы и их порядок из docs/design/screens.md. Иконки из docs/design/README.md. */
export const NAVIGATION: Record<ShellRole, ShellNavItem[]> = {
  Student: [
    { to: '/today', label: 'Сегодня', icon: House },
    { to: '/schedule', label: 'Расписание', icon: CalendarDays },
    { to: '/grades', label: 'Учёба', icon: GraduationCap },
    { to: '/requests', label: 'Заявки', icon: FileText },
    { to: '/notifications', label: 'Уведомления', icon: Bell, desktopOnly: true },
    { to: '/profile', label: 'Профиль', icon: User },
  ],
  Staff: [
    { to: '/staff/requests', label: 'Заявки', icon: FileText },
    { to: '/staff/schedule', label: 'Расписание', icon: CalendarDays },
    { to: '/staff/announcements', label: 'Объявления', icon: Megaphone },
    { to: '/staff/notifications', label: 'Уведомления', icon: Bell, desktopOnly: true },
    { to: '/staff/profile', label: 'Профиль', icon: User },
  ],
};

export const SHELL_PATHS: Record<ShellRole, { home: string; notifications: string; profile: string; cabinet: string }> = {
  Student: { home: '/today', notifications: '/notifications', profile: '/profile', cabinet: 'Кабинет студента' },
  Staff: { home: '/staff/requests', notifications: '/staff/notifications', profile: '/staff/profile', cabinet: 'Кабинет деканата' },
};
