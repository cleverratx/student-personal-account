import { useQuery, type QueryClient } from '@tanstack/react-query';
import { api, ApiError, unwrap, type components } from '@/shared/api';

export type Me = components['schemas']['MeResponse'];
export type Profile = components['schemas']['ProfileResponse'];
export type UserRole = components['schemas']['UserRole'];

export const sessionKeys = {
  me: ['session', 'me'] as const,
  profile: ['session', 'profile'] as const,
  /** Почему сессии больше нет: человек вышел сам или она кончилась. */
  ended: ['session', 'ended'] as const,
};

export type SessionEnd = 'logout' | 'expired';

/** Кто вошёл. null значит гость: на 401 не ошибка, а ответ «никто». */
async function fetchMe(): Promise<Me | null> {
  try {
    return await unwrap(api.GET('/api/me'));
  } catch (error) {
    if (error instanceof ApiError && error.isUnauthorized) return null;
    throw error;
  }
}

/**
 * Сессия меняется только входом, выходом и ответом 401, поэтому сама по себе не устаревает.
 * Остальное время её не перезапрашиваем.
 */
export function useMe() {
  return useQuery({ queryKey: sessionKeys.me, queryFn: fetchMe, staleTime: Infinity });
}

export function useProfile() {
  return useQuery({ queryKey: sessionKeys.profile, queryFn: () => unwrap(api.GET('/api/me/profile')) });
}

function isMe(queryKey: readonly unknown[]) {
  return queryKey[0] === sessionKeys.me[0] && queryKey[1] === sessionKeys.me[1];
}

/** Сессии больше нет: выкидываем все данные прошлого пользователя и запоминаем, что теперь гость. */
export function resetSession(queryClient: QueryClient, reason: SessionEnd) {
  queryClient.removeQueries({ predicate: (query) => !isMe(query.queryKey) });
  queryClient.setQueryData<SessionEnd>(sessionKeys.ended, reason);
  queryClient.setQueryData<Me | null>(sessionKeys.me, null);
}

export function sessionEndReason(queryClient: QueryClient): SessionEnd | undefined {
  return queryClient.getQueryData<SessionEnd>(sessionKeys.ended);
}

/** После входа: старые данные выкидываем и заново спрашиваем, кто вошёл. */
export function refreshSession(queryClient: QueryClient) {
  queryClient.removeQueries({ predicate: (query) => !isMe(query.queryKey) });
  return queryClient.invalidateQueries({ queryKey: sessionKeys.me });
}
