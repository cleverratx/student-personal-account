import createClient, { type Client } from 'openapi-fetch';
import { ApiError } from './errors';
import type { paths } from './schema';

type UnauthorizedListener = () => void;

const unauthorizedListeners = new Set<UnauthorizedListener>();

/**
 * Подписка на ответ 401: сессия кончилась или её не было. Через неё сессия на фронте сбрасывается
 * при любом 401, а не в каждом экране отдельно. Возвращает функцию отписки.
 */
export function onUnauthorized(listener: UnauthorizedListener): () => void {
  unauthorizedListeners.add(listener);
  return () => unauthorizedListeners.delete(listener);
}

// На неверный пароль вход тоже отвечает 401, но это не «сессия кончилась».
const PATHS_WITHOUT_SESSION = ['/api/auth/login'];

export function createApiClient(fetchImpl?: typeof fetch): Client<paths> {
  const client = createClient<paths>({
    // Фронт и API на одном домене. Полный адрес нужен, чтобы запрос собирался и вне браузера, в тестах.
    baseUrl: globalThis.location?.origin ?? '',
    credentials: 'same-origin',
    // В тестах подставляется свой fetch, в браузере берётся настоящий в момент запроса.
    fetch: (request) => (fetchImpl ?? globalThis.fetch)(request),
  });

  client.use({
    onResponse({ request, response }) {
      const path = new URL(request.url, 'http://localhost').pathname;
      if (response.status === 401 && !PATHS_WITHOUT_SESSION.includes(path)) {
        unauthorizedListeners.forEach((listener) => listener());
      }
      return response;
    },
    onError() {
      return ApiError.network();
    },
  });

  return client;
}

/** Клиент API для всего приложения. Пути и типы берутся из backend/openapi/openapi.json. */
export const api = createApiClient();

interface ApiResult<T> {
  data?: T;
  error?: unknown;
  response: Response;
}

/**
 * Достаёт данные из ответа или бросает ApiError. Удобно в queryFn и mutationFn TanStack Query:
 * `queryFn: () => unwrap(api.GET('/api/me'))`.
 */
export async function unwrap<T>(call: Promise<ApiResult<T>>): Promise<T> {
  let result: ApiResult<T>;
  try {
    result = await call;
  } catch (error) {
    throw error instanceof ApiError ? error : ApiError.network();
  }

  if (!result.response.ok) {
    throw ApiError.fromResponse(result.response, result.error);
  }

  return result.data as T;
}
