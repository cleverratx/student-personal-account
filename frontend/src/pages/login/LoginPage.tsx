import { useEffect } from 'react';
import { LoginForm } from '@/features/auth';
import { Brand, ConceptNote } from '@/shared/ui';
import styles from './LoginPage.module.css';

/** После входа сессия обновляется, и с этого экрана уводит охрана маршрутов, учитывая returnTo. */
export function LoginPage() {
  useEffect(() => {
    document.title = 'Вход — Цифровой деканат';
  }, []);

  return (
    <main className={styles.page}>
      <div className={styles.login}>
        <div>
          <Brand />
          <h1 className={styles.title}>Вход в кабинет</h1>
          <p className={styles.lead}>Расписание, оценки, справки и объявления деканата в одном месте.</p>
        </div>
        <LoginForm />
        <ConceptNote />
      </div>
    </main>
  );
}
