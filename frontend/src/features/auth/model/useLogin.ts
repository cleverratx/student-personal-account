import { useMutation, useQueryClient } from '@tanstack/react-query';
import { refreshSession } from '@/entities/session';
import { api, unwrap, type ApiError, type components } from '@/shared/api';

export type LoginRequest = components['schemas']['LoginRequest'];
export type LoginResponse = components['schemas']['LoginResponse'];

/**
 * Вход. После успеха данные прошлого пользователя выкидываются, а сессия перезапрашивается.
 * Кнопка остаётся занятой, пока не станет известно, кто вошёл, и охрана маршрутов не уведёт с экрана входа.
 */
export function useLogin() {
  const queryClient = useQueryClient();

  return useMutation<LoginResponse, ApiError, LoginRequest>({
    mutationFn: (body) => unwrap(api.POST('/api/auth/login', { body })),
    onSuccess: () => refreshSession(queryClient),
  });
}
