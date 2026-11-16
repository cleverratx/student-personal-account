import type { ReactNode } from 'react';
import { cx } from '@/shared/lib';
import styles from './Badge.module.css';

export type BadgeTone = 'submitted' | 'progress' | 'success' | 'danger' | 'neutral';

/** Статус всегда подписан словом: цвет помогает, но не заменяет текст. */
export function Badge({ tone = 'neutral', children }: { tone?: BadgeTone; children: ReactNode }) {
  return <span className={cx(styles.badge, styles[tone])}>{children}</span>;
}
