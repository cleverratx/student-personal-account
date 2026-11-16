import { NavLink, Outlet } from 'react-router';
import { Avatar, BottomNav, Brand, ConceptNote } from '@/shared/ui';
import styles from './AppShell.module.css';
import { NAVIGATION, SHELL_PATHS } from './navigation';
import { ShellContext, type ShellState } from './shellContext';

/**
 * Каркас кабинета: боковое меню на компьютере, нижняя панель на телефоне, экран посередине.
 * Шапку с колокольчиком рисует сам экран через ScreenHeader, потому что заголовок у каждого свой.
 */
export function AppShell({ role, user }: ShellState) {
  const items = NAVIGATION[role];
  const paths = SHELL_PATHS[role];

  return (
    <ShellContext.Provider value={{ role, user }}>
      <div className={styles.app}>
        <a className={styles.skipLink} href="#main">
          К содержимому
        </a>

        <aside className={styles.sidebar}>
          <NavLink className={styles.brandLink} to={paths.home} aria-label={`Цифровой деканат, ${paths.cabinet.toLowerCase()}`}>
            <Brand sub={paths.cabinet} />
          </NavLink>
          <nav className={styles.sideNav} aria-label="Разделы кабинета">
            {items.map(({ to, label, icon: Icon }) => (
              <NavLink key={to} to={to}>
                <Icon size={20} aria-hidden="true" />
                {label}
              </NavLink>
            ))}
          </nav>
          <div className={styles.sideFooter}>
            <ConceptNote className={styles.sideNote} />
            {user && (
              <NavLink className={styles.sideUser} to={paths.profile}>
                <Avatar initials={user.initials} size="small" />
                <span className={styles.sideUserText}>
                  <span className={styles.sideUserName}>{user.name}</span>
                  {user.meta && <span className={styles.sideUserMeta}>{user.meta}</span>}
                </span>
              </NavLink>
            )}
          </div>
        </aside>

        <div className={styles.workspace}>
          <main className={styles.screen} id="main">
            <Outlet />
          </main>
        </div>

        <BottomNav className={styles.tabbar} items={items.filter((item) => !item.desktopOnly)} />
      </div>
    </ShellContext.Provider>
  );
}
