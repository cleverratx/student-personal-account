import { Link } from 'react-router';
import styles from './HomePage.module.css';

export function HomePage() {
  return (
    <main className={styles.page}>
      <h1 className={styles.title}>Цифровой деканат</h1>
      <p className={styles.note}>Это концепт для конкурса, а не официальный сервис вуза.</p>
      <Link to="/nesushchestvuyushchiy-adres">Проверить страницу 404</Link>
    </main>
  );
}
