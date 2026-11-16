import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { routes } from '@/app/router/routes';
import { json, mockFetch } from './helpers/fetchMock';

function renderLogin(path = '/login') {
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
  return router;
}

async function fillAndSubmit(login = 'a.ivanova', password = 'Correct-horse-1') {
  const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime.bind(vi) });
  if (login) await user.type(await screen.findByLabelText('Логин'), login);
  if (password) await user.type(screen.getByLabelText('Пароль'), password);
  await user.click(screen.getByRole('button', { name: /Войти/ }));
  return user;
}

// Ненастоящие часы, которые идут сами: userEvent ждёт между нажатиями, а отсчёт блокировки можно промотать.
beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('экран входа', () => {
  it('подсказывает менеджеру паролей поля и показывает пометку «концепт»', async () => {
    renderLogin();

    expect(await screen.findByRole('heading', { level: 1, name: 'Вход в кабинет' })).toBeInTheDocument();
    expect(screen.getByLabelText('Логин')).toHaveAttribute('autocomplete', 'username');
    expect(screen.getByLabelText('Пароль')).toHaveAttribute('autocomplete', 'current-password');
    expect(screen.getByText(/концепт для конкурса, а не официальный сервис вуза/)).toBeInTheDocument();
  });

  it('пустую форму не отправляет и подсвечивает поля', async () => {
    const { fetchMock } = mockFetch({});
    renderLogin();

    await fillAndSubmit('', '');

    expect(screen.getByLabelText('Логин')).toHaveAccessibleDescription('Введите логин');
    expect(screen.getByLabelText('Пароль')).toHaveAccessibleDescription('Введите пароль');
    expect(screen.getByLabelText('Логин')).toHaveFocus();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('студента после входа ведёт на «Сегодня»', async () => {
    const { calls } = mockFetch({ 'POST /api/auth/login': () => json(200, { role: 'Student' }) });
    const router = renderLogin();

    await fillAndSubmit(' a.ivanova ');

    await vi.waitFor(() => expect(router.state.location.pathname).toBe('/today'));
    expect(await calls[0]!.json()).toEqual({ login: 'a.ivanova', password: 'Correct-horse-1' });
  });

  it('сотрудника ведёт в очередь заявок', async () => {
    mockFetch({ 'POST /api/auth/login': () => json(200, { role: 'Staff' }) });
    const router = renderLogin();

    await fillAndSubmit('o.krylova');

    await vi.waitFor(() => expect(router.state.location.pathname).toBe('/staff/requests'));
  });

  it('возвращает на страницу из returnTo, но только внутри сайта', async () => {
    mockFetch({ 'POST /api/auth/login': () => json(200, { role: 'Student' }) });
    const router = renderLogin('/login?returnTo=%2Frequests%3Ftab%3Ddone');

    await fillAndSubmit();

    await vi.waitFor(() => expect(router.state.location.pathname).toBe('/requests'));
    expect(router.state.location.search).toBe('?tab=done');
  });

  it('не уводит на чужой сайт через returnTo', async () => {
    mockFetch({ 'POST /api/auth/login': () => json(200, { role: 'Student' }) });
    const router = renderLogin('/login?returnTo=%2F%2Fevil.example%2Fphish');

    await fillAndSubmit();

    await vi.waitFor(() => expect(router.state.location.pathname).toBe('/today'));
  });

  it('на неверный пароль говорит об этом и не пускает дальше', async () => {
    mockFetch({
      'POST /api/auth/login': () =>
        json(401, { type: 'https://dekanat.local/errors/invalid-credentials', title: 'Не удалось войти', status: 401, detail: 'Неверный логин или пароль.' }),
    });
    const router = renderLogin();

    await fillAndSubmit();

    expect(await screen.findByRole('alert')).toHaveTextContent('Неверный логин или пароль.');
    expect(router.state.location.pathname).toBe('/login');
  });

  it('при блокировке говорит, сколько ждать, и не даёт нажать «Войти» до конца', async () => {
    mockFetch({
      'POST /api/auth/login': () =>
        json(429, { title: 'Слишком много попыток входа', status: 429, retryAfterSeconds: 300 }, { 'Retry-After': '300' }),
    });
    renderLogin();

    await fillAndSubmit();

    expect(await screen.findByRole('alert')).toHaveTextContent('попробуйте через 5 минут');
    const button = screen.getByRole('button', { name: /Войти через/ });
    expect(button).toBeDisabled();
    expect(button).toHaveTextContent('Войти через 5:00');

    act(() => vi.advanceTimersByTime(60_000));
    expect(button).toHaveTextContent('Войти через 4:00');

    act(() => vi.advanceTimersByTime(240_000));
    expect(screen.getByRole('button', { name: 'Войти' })).toBeEnabled();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('без сети просит проверить интернет', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.reject(new TypeError('Failed to fetch'))));
    renderLogin();

    await fillAndSubmit();

    expect(await screen.findByRole('alert')).toHaveTextContent('Нет связи с сервером. Проверьте интернет и попробуйте ещё раз.');
  });

  it('показывает ошибки полей от сервера', async () => {
    mockFetch({
      'POST /api/auth/login': () => json(400, { title: 'Проверьте поля', status: 400, errors: { password: ['Пароль не длиннее 128 символов'] } }),
    });
    renderLogin();

    await fillAndSubmit();

    expect(await screen.findByText('Пароль не длиннее 128 символов')).toBeInTheDocument();
    expect(screen.getByLabelText('Пароль')).toHaveAttribute('aria-invalid', 'true');
  });

  it('пока идёт запрос, кнопка занята и второй раз не отправляет', async () => {
    let release: (response: Response) => void = () => {};
    const { fetchMock } = mockFetch({ 'POST /api/auth/login': () => new Promise<Response>((resolve) => (release = resolve)) });
    renderLogin();

    const user = await fillAndSubmit();
    const button = screen.getByRole('button', { name: 'Войти' });
    expect(button).toHaveAttribute('aria-busy', 'true');
    await user.click(button);
    expect(fetchMock).toHaveBeenCalledOnce();

    release(json(200, { role: 'Student' }));
  });
});
