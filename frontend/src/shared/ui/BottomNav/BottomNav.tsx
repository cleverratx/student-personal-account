import type { LucideIcon } from 'lucide-react';
import { NavLink } from 'react-router';
import { cx } from '@/shared/lib';
import styles from './BottomNav.module.css';

export interface NavItem {
  to: string;
  label: string;
  icon: LucideIcon;
  /** Красная точка, например «есть готовая справка». Текст нужен для скринридера. */
  badge?: string;
}

/** Нижняя панель разделов на телефоне. На компьютере вместо неё боковое меню. */
export function BottomNav({ items, className }: { items: NavItem[]; className?: string }) {
  return (
    <nav className={cx(styles.tabbar, className)} aria-label="Разделы кабинета">
      {items.map(({ to, label, icon: Icon, badge }) => (
        <NavLink key={to} to={to} className={styles.item}>
          <span className={styles.icon}>
            <Icon size={22} aria-hidden="true" />
            {badge && <span className={styles.badge} />}
          </span>
          {label}
          {badge && <span className="visually-hidden">, {badge}</span>}
        </NavLink>
      ))}
    </nav>
  );
}
