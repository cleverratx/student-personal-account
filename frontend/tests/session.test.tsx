import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { fakeBackend, PASSWORD } from './helpers/fakeBackend';
import { json } from './helpers/fetchMock';
import { renderApp } from './helpers/renderApp';

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('охрана маршрутов', () => {
  it('гостя отправляет на вход и после входа возвращает туда, куда он шёл', async () => {
    fakeBackend();
    const { router } = renderApp('/grades');

    expect(await screen.findByRole('heading', { level: 1, name: 'Вход в кабинет' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/login');
    expect(router.state.location.search).toBe('?returnTo=%2Fgrades');

    await userEvent.type(screen.getByLabelText('Логин'), 'a.ivanova');
    await userEvent.type(screen.getByLabelText('Пароль'), PASSWORD);
    await userEvent.click(screen.getByRole('button', { name: 'Войти' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Учёба' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/grades');
  });

  it('вошедшего с экрана входа уводит в кабинет', async () => {
    fakeBackend({ session: 'Student' });
    const { router } = renderApp('/login');

    expect(await screen.findByRole('heading', { level: 1, name: 'Сегодня' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/today');
  });

  it('корень ведёт гостя на вход, а сотрудника в очередь заявок', async () => {
    fakeBackend({ session: 'Staff' });
    const { router } = renderApp('/');

    expect(await screen.findByRole('heading', { level: 1, name: 'Заявки' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/staff/requests');
  });

  it('студента в кабинет деканата не пускает', async () => {
    const { calls } = fakeBackend({ session: 'Student' });
    const { router } = renderApp('/staff/requests');

    expect(await screen.findByRole('heading', { level: 1, name: 'Сегодня' })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/today');
    expect(calls.some((request) => new URL(request.url).pathname.startsWith('/api/staff'))).toBe(false);
  });

  it('сотрудника в кабинет студента не пускает', async () => {
    fakeBackend({ session: 'Staff' });
    const { router } = renderApp('/profile');

    await vi.waitFor(() => expect(router.state.location.pathname).toBe('/staff/requests'));
  });

  it('на любой ответ 401 сбрасывает сессию и отправляет на вход', async () => {
    fakeBackend({ session: 'Student', overrides: { 'GET /api/me/profile': () => json(401, { title: 'Нужно войти', status: 401 }) } });
    const { router } = renderApp('/schedule');

    expect(await screen.findByRole('heading', { level: 1, name: 'Вход в кабинет' })).toBeInTheDocument();
    expect(router.state.location.search).toBe('?returnTo=%2Fschedule');
  });

  it('если не удалось узнать, вошёл ли человек, предлагает повторить', async () => {
    let fail = true;
    fakeBackend({
      session: 'Student',
      overrides: {
        'GET /api/me': () =>
          fail ? json(503, { title: 'Сервис недоступен', status: 503 }) : json(200, { id: '3f1c7a52-8e0b-4d6a-9c41-2b7e5d90a1f3', login: 'a.ivanova', role: 'Student' }),
      },
    });
    renderApp('/today');

    expect(await screen.findByRole('alert')).toHaveTextContent('Не удалось загрузить кабинет');
    fail = false;
    await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));

    expect(await screen.findByRole('heading', { level: 1, name: 'Сегодня' })).toBeInTheDocument();
  });
});

describe('профиль', () => {
  it('показывает данные студента, пометку «концепт» и имя в меню', async () => {
    fakeBackend({ session: 'Student' });
    renderApp('/profile');

    expect(await screen.findByRole('heading', { level: 2, name: 'Иванова Анастасия Сергеевна' })).toBeInTheDocument();
    expect(screen.getByText('09.03.04 Программная инженерия')).toBeInTheDocument();
    expect(screen.getByText('1 курс, ПИ-21')).toBeInTheDocument();
    expect(screen.getByText('2026-0123')).toBeInTheDocument();
    expect(within(screen.getByRole('main')).getByText(/концепт для конкурса, а не официальный сервис вуза/)).toBeInTheDocument();
    expect(screen.getByText('Анастасия Иванова')).toBeInTheDocument();
  });

  it('у сотрудника показывает должность', async () => {
    fakeBackend({ session: 'Staff' });
    renderApp('/staff/profile');

    expect(await screen.findByRole('heading', { level: 2, name: 'Крылова Ольга Викторовна' })).toBeInTheDocument();
    expect(screen.getAllByText('Специалист по учебной работе').length).toBeGreaterThan(0);
    expect(screen.getByText('каб. 214')).toBeInTheDocument();
  });

  it('если профиль не загрузился, даёт повторить', async () => {
    let fail = true;
    fakeBackend({
      session: 'Student',
      overrides: {
        'GET /api/me/profile': () =>
          fail
            ? json(500, { title: 'Внутренняя ошибка', status: 500 })
            : json(200, {
                role: 'Student',
                login: 'a.ivanova',
                fullName: 'Иванова Анастасия Сергеевна',
                initials: 'АИ',
                student: null,
                staff: null,
              }),
      },
    });
    renderApp('/profile');

    expect(await screen.findByRole('alert')).toHaveTextContent('Не удалось загрузить профиль');
    fail = false;
    await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));

    expect(await screen.findByRole('heading', { level: 2, name: 'Иванова Анастасия Сергеевна' })).toBeInTheDocument();
  });

  it('после выхода ведёт на вход, а «Назад» профиль уже не показывает', async () => {
    const backend = fakeBackend({ session: 'Student' });
    const { router } = renderApp('/today');
    await screen.findByRole('heading', { level: 1, name: 'Сегодня' });
    await router.navigate('/profile');
    await screen.findByRole('heading', { level: 2, name: 'Иванова Анастасия Сергеевна' });

    await userEvent.click(screen.getByRole('button', { name: 'Выйти' }));

    await vi.waitFor(() => expect(router.state.location.pathname + router.state.location.search).toBe('/login'));
    expect(await screen.findByRole('heading', { level: 1, name: 'Вход в кабинет' })).toBeInTheDocument();
    expect(backend.state.session).toBeNull();

    await router.navigate(-1);

    // Запись профиля в истории заменена входом, а «Сегодня» без сессии снова отправляет на вход.
    await vi.waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    expect(await screen.findByRole('heading', { level: 1, name: 'Вход в кабинет' })).toBeInTheDocument();
    expect(screen.queryByText('Иванова Анастасия Сергеевна')).not.toBeInTheDocument();
    expect(screen.queryByText('Анастасия Иванова')).not.toBeInTheDocument();
  });

  it('если выйти не получилось, остаётся в кабинете и говорит почему', async () => {
    fakeBackend({
      session: 'Student',
      overrides: { 'POST /api/auth/logout': () => Promise.reject(new TypeError('Failed to fetch')) },
    });
    const { router } = renderApp('/profile');
    await screen.findByRole('heading', { level: 2, name: 'Иванова Анастасия Сергеевна' });

    await userEvent.click(screen.getByRole('button', { name: 'Выйти' }));

    expect(await screen.findByText('Не удалось выйти: нет связи с сервером')).toBeInTheDocument();
    expect(router.state.location.pathname).toBe('/profile');
  });
});
