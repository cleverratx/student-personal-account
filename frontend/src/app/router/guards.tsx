import { useQueryClient } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { Navigate, useLocation, useSearchParams } from 'react-router';
import { roleLine, sessionEndReason, shortName, useMe, useProfile, type UserRole } from '@/entities/session';
import { safeReturnTo } from '@/shared/lib';
import { ErrorState, ScreenSkeleton } from '@/shared/ui';
import { AppShell, SHELL_PATHS } from '@/widgets/app-shell';
import styles from './guards.module.css';

function SessionLoading() {
  return (
    <div className={styles.gate}>
      <ScreenSkeleton label="Проверяем вход" />
    </div>
  );
}

function SessionError({ onRetry, retrying }: { onRetry: () => void; retrying: boolean }) {
  return (
    <main className={styles.gate}>
      <ErrorState noun="кабинет" onRetry={onRetry} retrying={retrying} />
    </main>
  );
}

/** Каркас кабинета с именем и аватаром. Профиль догружается отдельно и экран не держит. */
function SessionShell({ role }: { role: UserRole }) {
  const { data: profile } = useProfile();
  const user = profile && { initials: profile.initials, name: shortName(profile.fullName), meta: roleLine(profile) };
  return <AppShell role={role} user={user} />;
}

/**
 * Пускает в кабинет только вошедших с нужной ролью. Гостя отправляет на вход и запоминает,
 * куда он шёл. Студент в кабинете деканата и наоборот попадают на свою стартовую страницу.
 */
export function RequireAuth({ role }: { role: UserRole }) {
  const me = useMe();
  const location = useLocation();
  const queryClient = useQueryClient();

  if (me.isPending) return <SessionLoading />;
  if (me.isError) return <SessionError onRetry={() => me.refetch()} retrying={me.isFetching} />;

  if (!me.data) {
    // Вышли сами: на чистый вход. Сессия кончилась или её не было: запоминаем, куда шли.
    if (sessionEndReason(queryClient) === 'logout') return <Navigate to="/login" replace />;
    const returnTo = `${location.pathname}${location.search}${location.hash}`;
    return <Navigate to={`/login?returnTo=${encodeURIComponent(returnTo)}`} replace />;
  }

  if (me.data.role !== role) return <Navigate to={SHELL_PATHS[me.data.role].home} replace />;

  return <SessionShell role={role} />;
}

/** Экран входа только для гостей. Вошедшего уводит туда, куда он шёл, или на его стартовую страницу. */
export function GuestOnly({ children }: { children: ReactNode }) {
  const me = useMe();
  const [params] = useSearchParams();

  if (me.isPending) return <SessionLoading />;
  // Не узнали, вошёл ли человек (например, нет сети): показываем вход, он сам скажет про сеть.
  if (me.data) return <Navigate to={safeReturnTo(params.get('returnTo')) ?? SHELL_PATHS[me.data.role].home} replace />;

  return children;
}

/** Корень сайта: вошедшего на его стартовую страницу, гостя на вход. */
export function RootRedirect() {
  const me = useMe();

  if (me.isPending) return <SessionLoading />;
  if (me.isError) return <SessionError onRetry={() => me.refetch()} retrying={me.isFetching} />;

  return <Navigate to={me.data ? SHELL_PATHS[me.data.role].home : '/login'} replace />;
}
