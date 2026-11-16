import { LogOut } from 'lucide-react';
import { Button, useToast } from '@/shared/ui';
import { useLogout } from '../model/useLogout';

export function LogoutButton() {
  const logout = useLogout();
  const toast = useToast();

  return (
    <Button
      variant="danger-text"
      icon={<LogOut size={18} aria-hidden="true" />}
      loading={logout.isPending}
      onClick={() =>
        logout.mutate(undefined, {
          onError: (error) =>
            toast(error.kind === 'network' ? 'Не удалось выйти: нет связи с сервером' : 'Не удалось выйти, попробуйте ещё раз'),
        })
      }
    >
      Выйти
    </Button>
  );
}
