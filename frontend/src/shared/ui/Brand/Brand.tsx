import styles from './Brand.module.css';

/**
 * Знак и название сервиса. Это наша монограмма, а не логотип вуза: по условиям конкурса
 * логотип вуза не используем.
 */
export function Brand({ sub }: { sub?: string }) {
  return (
    <span className={styles.brand}>
      <span className={styles.mark} aria-hidden="true">
        ЦД
      </span>
      <span className={styles.text}>
        <span className={styles.name}>Цифровой деканат</span>
        {sub && <span className={styles.sub}>{sub}</span>}
      </span>
    </span>
  );
}
