import { plural, type PluralForms } from '@/shared/lib';
import type { ApiError } from '@/shared/api';

const MINUTES: PluralForms = { one: 'минуту', few: 'минуты', many: 'минут' };
const SECONDS: PluralForms = { one: 'секунду', few: 'секунды', many: 'секунд' };

/** «через 5 минут», «через 40 секунд». */
export function waitText(seconds: number): string {
  return seconds > 60 ? `через ${plural(Math.ceil(seconds / 60), MINUTES)}` : `через ${plural(seconds, SECONDS)}`;
}

export type LoginFailure =
  | { kind: 'fields'; fields: { login?: string; password?: string } }
  | { kind: 'message'; message: string }
  | { kind: 'locked'; message: string; seconds: number };

/** Ответ сервера на вход в то, что увидит человек. */
export function describeLoginError(error: ApiError): LoginFailure {
  if (error.kind === 'network') {
    return { kind: 'message', message: 'Нет связи с сервером. Проверьте интернет и попробуйте ещё раз.' };
  }

  if (error.status === 400 && Object.keys(error.fieldErrors).length > 0) {
    return { kind: 'fields', fields: { login: error.fieldErrors.login, password: error.fieldErrors.password } };
  }

  if (error.status === 401) {
    return { kind: 'message', message: 'Неверный логин или пароль.' };
  }

  if (error.status === 429) {
    const seconds = error.retryAfterSeconds ?? 300;
    return {
      kind: 'locked',
      seconds,
      message: `Слишком много попыток входа. Вход временно закрыт, попробуйте ${waitText(seconds)}.`,
    };
  }

  return { kind: 'message', message: 'Не получилось войти: сервер ответил ошибкой. Попробуйте ещё раз чуть позже.' };
}
