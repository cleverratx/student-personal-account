/** Тело ошибки в формате ProblemDetails, как его отдаёт бэкенд. */
export interface ProblemBody {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
  requestId?: string;
  retryAfterSeconds?: number;
}

export type ApiErrorKind = 'http' | 'network';

const NETWORK_MESSAGE = 'Нет связи с сервером. Проверьте интернет и попробуйте ещё раз.';
const UNKNOWN_MESSAGE = 'Сервер ответил ошибкой. Попробуйте ещё раз чуть позже.';

/**
 * Понятная ошибка вместо сырого ответа. Текст для человека лежит в message,
 * ошибки полей формы в fieldErrors, по одной на поле.
 */
export class ApiError extends Error {
  readonly kind: ApiErrorKind;
  readonly status: number;
  readonly title: string;
  readonly detail: string | undefined;
  readonly fieldErrors: Record<string, string>;
  readonly requestId: string | undefined;
  readonly retryAfterSeconds: number | undefined;

  private constructor(kind: ApiErrorKind, status: number, problem: ProblemBody) {
    const title = problem.title ?? (kind === 'network' ? 'Нет связи' : 'Ошибка');
    super(problem.detail ?? problem.title ?? (kind === 'network' ? NETWORK_MESSAGE : UNKNOWN_MESSAGE));
    this.name = 'ApiError';
    this.kind = kind;
    this.status = status;
    this.title = title;
    this.detail = problem.detail;
    this.requestId = problem.requestId;
    this.retryAfterSeconds = problem.retryAfterSeconds;
    this.fieldErrors = Object.fromEntries(
      Object.entries(problem.errors ?? {})
        .filter(([, messages]) => messages.length > 0)
        .map(([field, messages]) => [field, messages[0]!]),
    );
  }

  static fromResponse(response: Response, body: unknown): ApiError {
    const problem = isProblem(body) ? body : {};
    const retryHeader = Number(response.headers.get('Retry-After'));
    return new ApiError('http', response.status, {
      ...problem,
      status: problem.status ?? response.status,
      retryAfterSeconds: problem.retryAfterSeconds ?? (Number.isFinite(retryHeader) && retryHeader > 0 ? retryHeader : undefined),
    });
  }

  static network(): ApiError {
    return new ApiError('network', 0, { detail: NETWORK_MESSAGE });
  }

  get isUnauthorized(): boolean {
    return this.status === 401;
  }
}

function isProblem(body: unknown): body is ProblemBody {
  return typeof body === 'object' && body !== null && ('title' in body || 'status' in body || 'errors' in body);
}
