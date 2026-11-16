import { Inbox, OctagonAlert, RefreshCw } from 'lucide-react';
import type { ReactNode } from 'react';
import { cx } from '@/shared/lib';
import { Button } from '../Button/Button';
import styles from './States.module.css';

/** Скелетон экрана: заголовок с крупным блоком и список строк, как у большинства экранов кабинета. */
export function ScreenSkeleton({ rows = 2, label = 'Загружаем' }: { rows?: number; label?: string }) {
  return (
    <div className={styles.skeleton} role="status" aria-busy="true" aria-label={label}>
      <div className={styles.group}>
        <span className={cx(styles.bone, styles.boneTitle)} />
        <span className={cx(styles.bone, styles.boneBlock)} />
      </div>
      <div className={styles.group}>
        <span className={cx(styles.bone, styles.boneTitle)} />
        {Array.from({ length: rows }, (_, index) => (
          <span key={index} className={cx(styles.bone, styles.boneRow)} />
        ))}
      </div>
    </div>
  );
}

/** Одна «кость» нужного размера, если скелетон экрана по форме не подходит. */
export function Skeleton({ width, height = 16 }: { width?: number | string; height?: number }) {
  return <span className={styles.bone} style={{ width, height }} aria-hidden="true" />;
}

export interface EmptyStateProps {
  title: string;
  text?: ReactNode;
  icon?: ReactNode;
  action?: ReactNode;
}

/** Пусто: что здесь появится и что сделать дальше. */
export function EmptyState({ title, text, icon, action }: EmptyStateProps) {
  return (
    <div className={cx(styles.state, styles.empty)}>
      {icon ?? <Inbox size={32} aria-hidden="true" />}
      <p className={styles.title}>{title}</p>
      {text && <p className={styles.text}>{text}</p>}
      {action && <div className={styles.action}>{action}</div>}
    </div>
  );
}

export interface ErrorStateProps {
  /** Что не загрузилось, в винительном падеже: «расписание», «заявки». */
  noun: string;
  /** Текст ошибки от сервера, если он понятнее общего. */
  message?: string;
  onRetry: () => void;
  retrying?: boolean;
}

/** Не загрузилось: что случилось и кнопка «Повторить». */
export function ErrorState({ noun, message, onRetry, retrying }: ErrorStateProps) {
  return (
    <div className={cx(styles.state, styles.error)} role="alert">
      <OctagonAlert size={32} aria-hidden="true" />
      <p className={styles.title}>Не удалось загрузить {noun}</p>
      <p className={styles.text}>{message ?? 'Проверьте подключение к интернету и попробуйте ещё раз.'}</p>
      <Button
        className={styles.action}
        variant="secondary"
        icon={<RefreshCw size={18} aria-hidden="true" />}
        loading={retrying}
        onClick={onRetry}
      >
        Повторить
      </Button>
    </div>
  );
}
