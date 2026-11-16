import { ApiError, createApiClient, onUnauthorized, unwrap } from '@/shared/api';

function jsonResponse(status: number, body: unknown, headers: Record<string, string> = {}) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json', ...headers },
  });
}

describe('клиент API', () => {
  it('возвращает данные удачного ответа с типами из OpenAPI', async () => {
    const client = createApiClient(async () => jsonResponse(200, { id: '1', login: 'a.ivanova', role: 'Student' }));

    const me = await unwrap(client.GET('/api/me'));

    expect(me.role).toBe('Student');
    expect(me.login).toBe('a.ivanova');
  });

  it('превращает ProblemDetails с полями в понятную ошибку формы', async () => {
    const client = createApiClient(async () =>
      jsonResponse(400, {
        title: 'Проверьте введённые данные',
        status: 400,
        errors: { login: ['Введите логин'], password: ['Введите пароль', 'Пароль не длиннее 128 символов'] },
        requestId: 'abc:1',
      }),
    );

    const error = await unwrap(client.POST('/api/auth/login', { body: { login: '', password: '' } })).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    const apiError = error as ApiError;
    expect(apiError.status).toBe(400);
    expect(apiError.title).toBe('Проверьте введённые данные');
    expect(apiError.fieldErrors).toEqual({ login: 'Введите логин', password: 'Введите пароль' });
    expect(apiError.requestId).toBe('abc:1');
  });

  it('берёт время ожидания из ответа 429', async () => {
    const client = createApiClient(async () =>
      jsonResponse(
        429,
        { title: 'Слишком много попыток входа', detail: 'Слишком много попыток входа. Повторите позже.', status: 429 },
        { 'Retry-After': '287' },
      ),
    );

    const error = (await unwrap(client.POST('/api/auth/login', { body: { login: 'x', password: 'y' } })).catch((e: unknown) => e)) as ApiError;

    expect(error.retryAfterSeconds).toBe(287);
    expect(error.message).toBe('Слишком много попыток входа. Повторите позже.');
  });

  it('объясняет ответ без ProblemDetails общим текстом', async () => {
    const client = createApiClient(async () => new Response('<html>Bad Gateway</html>', { status: 502 }));

    const error = (await unwrap(client.GET('/api/health')).catch((e: unknown) => e)) as ApiError;

    expect(error.kind).toBe('http');
    expect(error.status).toBe(502);
    expect(error.message).toBe('Сервер ответил ошибкой. Попробуйте ещё раз чуть позже.');
  });

  it('отличает обрыв сети от ответа сервера', async () => {
    const client = createApiClient(async () => {
      throw new TypeError('Failed to fetch');
    });

    const error = (await unwrap(client.GET('/api/me')).catch((e: unknown) => e)) as ApiError;

    expect(error.kind).toBe('network');
    expect(error.message).toBe('Нет связи с сервером. Проверьте интернет и попробуйте ещё раз.');
  });

  it('сообщает о 401, кроме неверного пароля при входе', async () => {
    const listener = vi.fn();
    const unsubscribe = onUnauthorized(listener);
    const client = createApiClient(async () => jsonResponse(401, { title: 'Нужно войти в кабинет', status: 401 }));

    await unwrap(client.POST('/api/auth/login', { body: { login: 'x', password: 'y' } })).catch(() => undefined);
    expect(listener).not.toHaveBeenCalled();

    await unwrap(client.GET('/api/me/profile')).catch(() => undefined);
    expect(listener).toHaveBeenCalledTimes(1);

    unsubscribe();
  });
});
