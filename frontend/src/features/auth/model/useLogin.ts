import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api, unwrap, type ApiError, type components } from '@/shared/api';

export type LoginRequest = components['schemas']['LoginRequest'];
export type LoginResponse = components['schemas']['LoginResponse'];

/**
 * Вход. После успеха кэш запросов очищается целиком: данные прошлого пользователя
 * не должны показаться новому даже на мгновение.
 */
export function useLogin() {
  const queryClient = useQueryClient();

  return useMutation<LoginResponse, ApiError, LoginRequest>({
    mutationFn: (body) => unwrap(api.POST('/api/auth/login', { body })),
    onSuccess: () => queryClient.clear(),
  });
}
