import { LoaderCircle } from 'lucide-react';
import type { ButtonHTMLAttributes, ReactNode } from 'react';
import { Link, type LinkProps } from 'react-router';
import { cx } from '@/shared/lib';
import styles from './Button.module.css';

export type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'danger-text';

interface ButtonLook {
  variant?: ButtonVariant;
  size?: 'default' | 'small';
  block?: boolean;
  icon?: ReactNode;
}

const variantClass: Record<ButtonVariant, string | undefined> = {
  primary: styles.primary,
  secondary: styles.secondary,
  danger: styles.danger,
  'danger-text': styles.dangerText,
};

function buttonClass({ variant = 'primary', size = 'default', block }: ButtonLook, className?: string) {
  return cx(styles.button, variantClass[variant], size === 'small' && styles.small, block && styles.block, className);
}

export interface ButtonProps extends ButtonLook, ButtonHTMLAttributes<HTMLButtonElement> {
  /** Запрос в процессе: кнопка не нажимается повторно, вместо иконки крутится индикатор. */
  loading?: boolean;
}

export function Button({
  variant,
  size,
  block,
  icon,
  loading = false,
  className,
  disabled,
  type = 'button',
  children,
  ...rest
}: ButtonProps) {
  return (
    <button
      type={type}
      className={buttonClass({ variant, size, block }, className)}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
      {...rest}
    >
      {loading ? <LoaderCircle className={styles.spinner} size={18} aria-hidden="true" /> : icon}
      {children}
    </button>
  );
}

export interface ButtonLinkProps extends ButtonLook, LinkProps {}

/** Переход, который выглядит как кнопка. Для действий нужен Button. */
export function ButtonLink({ variant = 'secondary', size, block, icon, className, children, ...rest }: ButtonLinkProps) {
  return (
    <Link className={buttonClass({ variant, size, block }, className)} {...rest}>
      {icon}
      {children}
    </Link>
  );
}
