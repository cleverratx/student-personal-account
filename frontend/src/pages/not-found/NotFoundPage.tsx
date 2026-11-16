import { Link } from 'react-router';
import styles from './NotFoundPage.module.css';

export function NotFoundPage() {
  return (
    <main className={styles.page}>
      <h1 className={styles.title}>Страница не найдена</h1>
      <p className={styles.text}>Возможно, ссылка устарела или в адресе опечатка.</p>
      <Link to="/">На главную</Link>
    </main>
  );
}
