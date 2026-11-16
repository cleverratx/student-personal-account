import { useEffect } from 'react';
import { Brand, ButtonLink } from '@/shared/ui';
import styles from './NotFoundPage.module.css';

export function NotFoundPage() {
  useEffect(() => {
    document.title = 'Страница не найдена — Цифровой деканат';
  }, []);

  return (
    <main className={styles.page}>
      <Brand />
      <div className={styles.body}>
        <p className={styles.code}>404</p>
        <h1 className={styles.title}>Страница не найдена</h1>
        <p className={styles.text}>Возможно, ссылка устарела или в адресе опечатка.</p>
      </div>
      <ButtonLink to="/" variant="primary">
        На главную
      </ButtonLink>
    </main>
  );
}
