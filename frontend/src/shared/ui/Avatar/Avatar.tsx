import { User } from 'lucide-react';
import { cx } from '@/shared/lib';
import styles from './Avatar.module.css';

export interface AvatarProps {
  /** Две буквы: «АИ». Пока профиль грузится, вместо них силуэт. */
  initials?: string;
  size?: 'small' | 'default' | 'large';
  className?: string;
}

/** Кружок с инициалами. Имя рядом всегда написано текстом, поэтому для скринридера аватар скрыт. */
export function Avatar({ initials, size = 'default', className }: AvatarProps) {
  return (
    <span
      className={cx(styles.avatar, size === 'small' && styles.small, size === 'large' && styles.large, className)}
      aria-hidden="true"
    >
      {initials || <User size={size === 'large' ? 28 : 20} />}
    </span>
  );
}
