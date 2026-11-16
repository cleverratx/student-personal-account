import { Bell } from 'lucide-react';
import { Link } from 'react-router';
import { Avatar, Header, IconLink, type HeaderProps } from '@/shared/ui';
import styles from './AppShell.module.css';
import { SHELL_PATHS } from './navigation';
import { useShell } from './shellContext';

/** Заголовок экрана кабинета: на телефоне справа колокольчик и аватар, на компьютере они в меню. */
export function ScreenHeader(props: Omit<HeaderProps, 'actions'>) {
  const { role, user } = useShell();
  const paths = SHELL_PATHS[role];

  return (
    <Header
      {...props}
      actions={
        <>
          <IconLink to={paths.notifications} label="Уведомления" icon={<Bell size={22} aria-hidden="true" />} />
          <Link className={styles.avatarLink} to={paths.profile} aria-label="Профиль">
            <Avatar initials={user?.initials} />
          </Link>
        </>
      }
    />
  );
}
