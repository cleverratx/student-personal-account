import { ChevronDown } from 'lucide-react';
import { useId, type SelectHTMLAttributes } from 'react';
import { cx } from '@/shared/lib';
import styles from './Field.module.css';
import { describedIds } from './describedIds';
import { FieldShell, type FieldShellProps } from './FieldShell';

export interface SelectProps extends FieldShellProps, Omit<SelectHTMLAttributes<HTMLSelectElement>, 'id'> {}

export function Select({ label, hint, error, optional, className, children, ...rest }: SelectProps) {
  const id = useId();
  const { describedBy } = describedIds(id, { hint, error });

  return (
    <FieldShell id={id} label={label} hint={hint} error={error} optional={optional}>
      <div className={styles.withButton}>
        <select
          id={id}
          className={cx(styles.input, styles.select, className)}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy}
          {...rest}
        >
          {children}
        </select>
        <ChevronDown className={styles.chevron} size={20} aria-hidden="true" />
      </div>
    </FieldShell>
  );
}
