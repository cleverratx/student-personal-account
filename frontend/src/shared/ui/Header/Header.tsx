import { ChevronLeft } from 'lucide-react';
import { useEffect, useRef, type ReactNode } from 'react';
import { Link } from 'react-router';
import styles from './Header.module.css';

export interface HeaderProps {
  title: string;
  /** Короткая строка рядом с заголовком, например дата на экране «Сегодня». */
  meta?: ReactNode;
  /** Куда ведёт «Назад» на вложенных экранах. */
  backTo?: string;
  backLabel?: string;
  /** Колокольчик и аватар в полосе сверху. На компьютере они в боковом меню, поэтому прячутся. */
  actions?: ReactNode;
}

/**
 * Полоса сверху с «Назад» и действиями, под ней крупный заголовок экрана. При переходе на экран
 * фокус ставится на заголовок, чтобы скринридер прочитал, куда пришли.
 */
export function Header({ title, meta, backTo, backLabel = 'Назад', actions }: HeaderProps) {
  const headingRef = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    document.title = `${title} — Цифровой деканат`;
    headingRef.current?.focus({ preventScroll: true });
  }, [title]);

  return (
    <>
      <div className={styles.appbar}>
        {backTo && (
          <Link className={styles.back} to={backTo} aria-label={backLabel}>
            <ChevronLeft size={24} aria-hidden="true" />
          </Link>
        )}
        {actions && <div className={styles.actions}>{actions}</div>}
      </div>
      <div className={styles.head}>
        <h1 className={styles.title} ref={headingRef} tabIndex={-1}>
          {title}
        </h1>
        {meta && <p className={styles.meta}>{meta}</p>}
      </div>
    </>
  );
}
