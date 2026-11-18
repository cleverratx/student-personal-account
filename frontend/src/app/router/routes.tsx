import type { RouteObject } from 'react-router';
import { Navigate } from 'react-router';
import { LoginPage } from '@/pages/login';
import { NotFoundPage } from '@/pages/not-found';
import { ProfilePage } from '@/pages/profile';
import { SchedulePage } from '@/pages/schedule';
import { StubPage } from '@/pages/stub';
import { GuestOnly, RequireAuth, RootRedirect } from './guards';

// Витрина UI-кита есть только в dev-сервере. В сборке import.meta.env.DEV равно false,
// и сборщик выкидывает и маршрут, и сам модуль страницы.
const devRoutes: RouteObject[] = import.meta.env.DEV
  ? [
      {
        path: '/dev/ui',
        lazy: async () => {
          const { DevUiPage } = await import('@/pages/dev-ui');
          return { Component: DevUiPage };
        },
      },
    ]
  : [];

export const routes: RouteObject[] = [
  { path: '/', element: <RootRedirect /> },
  {
    path: '/login',
    element: (
      <GuestOnly>
        <LoginPage />
      </GuestOnly>
    ),
  },
  {
    element: <RequireAuth role="Student" />,
    children: [
      { path: '/today', element: <StubPage title="Сегодня" text="Следующая пара, ближайший срок, важные объявления и активные заявки." /> },
      { path: '/schedule', element: <SchedulePage /> },
      { path: '/grades', element: <StubPage title="Учёба" text="Допуск к сессии, долги и оценки по предметам." /> },
      { path: '/requests', element: <StubPage title="Заявки" text="Справки и заявления со статусами и примерной датой готовности." /> },
      { path: '/notifications', element: <StubPage title="Уведомления" text="Объявления, переносы пар и изменения статусов заявок." /> },
      { path: '/profile', element: <ProfilePage /> },
    ],
  },
  {
    path: '/staff',
    element: <RequireAuth role="Staff" />,
    children: [
      { index: true, element: <Navigate to="/staff/requests" replace /> },
      { path: 'requests', element: <StubPage title="Заявки" text="Очередь заявок студентов с фильтром по статусу." /> },
      { path: 'schedule', element: <StubPage title="Расписание" text="Расписание групп, переносы и отмены пар." /> },
      { path: 'announcements', element: <StubPage title="Объявления" text="Объявления для всех, факультета или группы." /> },
      { path: 'notifications', element: <StubPage title="Уведомления" text="Новые заявки и ответы студентов." /> },
      { path: 'profile', element: <ProfilePage /> },
    ],
  },
  ...devRoutes,
  { path: '*', element: <NotFoundPage /> },
];
