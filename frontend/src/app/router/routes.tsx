import type { RouteObject } from 'react-router';
import { HomePage } from '@/pages/home';
import { NotFoundPage } from '@/pages/not-found';

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
  { path: '/', element: <HomePage /> },
  ...devRoutes,
  { path: '*', element: <NotFoundPage /> },
];
