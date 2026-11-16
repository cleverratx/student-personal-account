import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router';
import { resetSession } from '@/entities/session';
import { api, ApiError, unwrap } from '@/shared/api';

/**
 * Выход. Переход на вход делается с заменой записи в истории, а данные кабинета удаляются из памяти,
 * поэтому кнопка «Назад» после выхода профиль не покажет.
 */
export function useLogout() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  return useMutation<void, ApiError>({
    mutationFn: async () => {
      try {
        await unwrap(api.POST('/api/auth/logout'));
      } catch (error) {
        // Сессия уже кончилась: выйти всё равно получилось.
        if (error instanceof ApiError && error.isUnauthorized) return;
        throw error;
      }
    },
    onSuccess: () => {
      // Причина «logout» говорит охране маршрутов не добавлять returnTo: вышли сами, возвращать некуда.
      resetSession(queryClient, 'logout');
      navigate('/login', { replace: true });
    },
  });
}
