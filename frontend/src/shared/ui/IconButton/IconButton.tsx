import type { ButtonHTMLAttributes, ReactNode } from 'react';
import { Link, type LinkProps } from 'react-router';
import { cx } from '@/shared/lib';
import styles from './IconButton.module.css';

interface IconLook {
  /** Подпись для скринридера. У кнопки без текста она обязательна. */
  label: string;
  icon: ReactNode;
  plain?: boolean;
  /** Число в красном кружке, например непрочитанные уведомления. Ноль не показываем. */
  counter?: number;
}

function counterBadge(counter?: number) {
  if (!counter) return null;
  return (
    <span className={styles.counter} aria-hidden="true">
      {counter > 99 ? '99+' : counter}
    </span>
  );
}

export interface IconButtonProps extends IconLook, Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'children'> {}

export function IconButton({ label, icon, plain, counter, className, type = 'button', ...rest }: IconButtonProps) {
  return (
    <button type={type} className={cx(styles.iconButton, plain && styles.plain, className)} aria-label={label} {...rest}>
      {icon}
      {counterBadge(counter)}
    </button>
  );
}

export interface IconLinkProps extends IconLook, Omit<LinkProps, 'children'> {}

export function IconLink({ label, icon, plain, counter, className, ...rest }: IconLinkProps) {
  return (
    <Link className={cx(styles.iconButton, plain && styles.plain, className)} aria-label={label} {...rest}>
      {icon}
      {counterBadge(counter)}
    </Link>
  );
}
