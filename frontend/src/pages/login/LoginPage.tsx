import { useEffect } from 'react';
import { useNavigate, useSearchParams } from 'react-router';
import { LoginForm, type LoginResponse } from '@/features/auth';
import { safeReturnTo } from '@/shared/lib';
import { Brand, ConceptNote } from '@/shared/ui';
import { SHELL_PATHS } from '@/widgets/app-shell';
import styles from './LoginPage.module.css';

export function LoginPage() {
  const navigate = useNavigate();
  const [params] = useSearchParams();

  useEffect(() => {
    document.title = 'Вход — Цифровой деканат';
  }, []);

  const onSuccess = ({ role }: LoginResponse) => {
    // Вернуть туда, откуда отправили на вход. Чужой кабинет откроется, только если роль подходит:
    // это проверяет охрана маршрутов, а здесь достаточно, чтобы адрес был внутри сайта.
    navigate(safeReturnTo(params.get('returnTo')) ?? SHELL_PATHS[role].home, { replace: true });
  };

  return (
    <main className={styles.page}>
      <div className={styles.login}>
        <div>
          <Brand />
          <h1 className={styles.title}>Вход в кабинет</h1>
          <p className={styles.lead}>Расписание, оценки, справки и объявления деканата в одном месте.</p>
        </div>
        <LoginForm onSuccess={onSuccess} />
        <ConceptNote />
      </div>
    </main>
  );
}
