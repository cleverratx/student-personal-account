import { screen, within } from '@testing-library/react';
import { fakeBackend } from './helpers/fakeBackend';
import { renderApp } from './helpers/renderApp';

function navs() {
  // Боковое меню и нижняя панель: в jsdom медиазапросы не работают, поэтому в DOM есть обе.
  return screen.getAllByRole('navigation', { name: 'Разделы кабинета' });
}

const labels = (nav: HTMLElement) => within(nav).getAllByRole('link').map((link) => link.textContent);

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('каркас кабинета', () => {
  it('у студента в нижней панели пять разделов, уведомления только в боковом меню и под колокольчиком', async () => {
    fakeBackend({ session: 'Student' });
    renderApp('/schedule');
    await screen.findByRole('heading', { level: 1, name: 'Расписание' });

    const [side, bottom] = navs();
    expect(labels(bottom!)).toEqual(['Сегодня', 'Расписание', 'Учёба', 'Заявки', 'Профиль']);
    expect(labels(side!)).toEqual(['Сегодня', 'Расписание', 'Учёба', 'Заявки', 'Уведомления', 'Профиль']);
    expect(within(bottom!).getByRole('link', { name: 'Расписание' })).toHaveAttribute('aria-current', 'page');
    // Пункт бокового меню и колокольчик в шапке.
    const notificationLinks = screen.getAllByRole('link', { name: 'Уведомления' });
    expect(notificationLinks).toHaveLength(2);
    notificationLinks.forEach((link) => expect(link).toHaveAttribute('href', '/notifications'));
  });

  it('в меню имя и группа из профиля', async () => {
    fakeBackend({ session: 'Student' });
    renderApp('/today');

    expect(await screen.findByText('Анастасия Иванова')).toBeInTheDocument();
    expect(screen.getByText('ПИ-21')).toBeInTheDocument();
  });

  it('у сотрудника свои разделы, /staff открывает очередь заявок', async () => {
    fakeBackend({ session: 'Staff' });
    const { router } = renderApp('/staff');

    expect(await screen.findByRole('heading', { level: 1, name: 'Заявки' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/staff/requests');
    expect(labels(navs()[1]!)).toEqual(['Заявки', 'Расписание', 'Объявления', 'Профиль']);
  });

  it.each([
    ['/today', 'Сегодня', 'Student'],
    ['/grades', 'Учёба', 'Student'],
    ['/requests', 'Заявки', 'Student'],
    ['/notifications', 'Уведомления', 'Student'],
    ['/staff/schedule', 'Расписание', 'Staff'],
    ['/staff/announcements', 'Объявления', 'Staff'],
    ['/staff/notifications', 'Уведомления', 'Staff'],
  ] as const)('%s открывает заглушку «%s»', async (path, title, role) => {
    fakeBackend({ session: role });
    renderApp(path);

    expect(await screen.findByRole('heading', { level: 1, name: title })).toBeInTheDocument();
    expect(screen.getByText('Раздел пока в разработке')).toBeInTheDocument();
    expect(document.title).toBe(`${title} — Цифровой деканат`);
  });
});
