import { render, screen, within } from '@testing-library/react';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { routes } from '@/app/router/routes';

function renderAt(path: string) {
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  render(<RouterProvider router={router} />);
  return router;
}

function navs() {
  // Боковое меню и нижняя панель: в jsdom медиазапросы не работают, поэтому в DOM есть обе.
  return screen.getAllByRole('navigation', { name: 'Разделы кабинета' });
}

describe('каркас кабинета', () => {
  it('с корня ведёт на «Сегодня»', async () => {
    const router = renderAt('/');

    expect(await screen.findByRole('heading', { level: 1, name: 'Сегодня' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/today');
  });

  it('у студента в нижней панели пять разделов, уведомления только в боковом меню и под колокольчиком', async () => {
    renderAt('/schedule');
    await screen.findByRole('heading', { level: 1, name: 'Расписание' });

    const [side, bottom] = navs();
    const labels = (nav: HTMLElement) => within(nav).getAllByRole('link').map((link) => link.textContent);
    expect(labels(bottom!)).toEqual(['Сегодня', 'Расписание', 'Учёба', 'Заявки', 'Профиль']);
    expect(labels(side!)).toEqual(['Сегодня', 'Расписание', 'Учёба', 'Заявки', 'Уведомления', 'Профиль']);
    expect(within(bottom!).getByRole('link', { name: 'Расписание' })).toHaveAttribute('aria-current', 'page');
    // Пункт бокового меню и колокольчик в шапке.
    const notificationLinks = screen.getAllByRole('link', { name: 'Уведомления' });
    expect(notificationLinks).toHaveLength(2);
    notificationLinks.forEach((link) => expect(link).toHaveAttribute('href', '/notifications'));
  });

  it('у сотрудника свои разделы, /staff открывает очередь заявок', async () => {
    const router = renderAt('/staff');

    expect(await screen.findByRole('heading', { level: 1, name: 'Заявки' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/staff/requests');
    const bottom = navs()[1]!;
    expect(within(bottom).getAllByRole('link').map((link) => link.textContent)).toEqual([
      'Заявки',
      'Расписание',
      'Объявления',
      'Профиль',
    ]);
  });

  it.each([
    ['/today', 'Сегодня'],
    ['/grades', 'Учёба'],
    ['/requests', 'Заявки'],
    ['/notifications', 'Уведомления'],
    ['/profile', 'Профиль'],
    ['/staff/schedule', 'Расписание'],
    ['/staff/announcements', 'Объявления'],
    ['/staff/notifications', 'Уведомления'],
    ['/staff/profile', 'Профиль'],
  ])('%s открывает заглушку «%s»', async (path, title) => {
    renderAt(path);

    expect(await screen.findByRole('heading', { level: 1, name: title })).toBeInTheDocument();
    expect(screen.getByText('Раздел пока в разработке')).toBeInTheDocument();
    expect(document.title).toBe(`${title} — Цифровой деканат`);
  });
});
