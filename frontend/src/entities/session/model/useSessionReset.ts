import { useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { onUnauthorized } from '@/shared/api';
import { resetSession } from './session';

/**
 * Любой ответ 401 значит, что сессии больше нет: истекла, вышли в другой вкладке или сервер
 * её сбросил. Сбрасываем её и на фронте, а охрана маршрутов отправит на вход.
 */
export function useSessionReset() {
  const queryClient = useQueryClient();
  useEffect(() => onUnauthorized(() => resetSession(queryClient, 'expired')), [queryClient]);
}
