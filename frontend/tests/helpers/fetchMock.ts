type Handler = (request: Request) => Response | Promise<Response>;

/** Подменяет fetch: ответ выбирается по «МЕТОД путь». Неожиданный запрос роняет тест. */
export function mockFetch(routes: Record<string, Handler>) {
  const calls: Request[] = [];
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = input instanceof Request ? input : new Request(input, init);
    calls.push(request);
    const key = `${request.method} ${new URL(request.url).pathname}`;
    const handler = routes[key];
    if (!handler) throw new Error(`Неожиданный запрос ${key}`);
    return handler(request);
  });
  vi.stubGlobal('fetch', fetchMock);
  return { calls, fetchMock };
}

export function json(status: number, body: unknown, headers: Record<string, string> = {}) {
  const type = status >= 400 ? 'application/problem+json' : 'application/json';
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': type, ...headers } });
}
