import { OctagonAlert } from 'lucide-react';
import type { ReactNode } from 'react';
import { describedIds } from './describedIds';
import styles from './Field.module.css';

export interface FieldShellProps {
  label: string;
  hint?: ReactNode;
  error?: string;
  optional?: boolean;
}

/** Подпись сверху, подсказка и ошибка снизу. Само поле передаётся в children. */
export function FieldShell({ id, label, hint, error, optional, children }: FieldShellProps & { id: string; children: ReactNode }) {
  const { hintId, errorId } = describedIds(id, { hint, error });
  return (
    <div className={styles.field}>
      <label className={styles.label} htmlFor={id}>
        {label}
        {optional && <span className={styles.optional}> необязательно</span>}
      </label>
      {children}
      {hint && (
        <p className={styles.hint} id={hintId}>
          {hint}
        </p>
      )}
      {error && (
        <p className={styles.error} id={errorId}>
          <OctagonAlert size={16} aria-hidden="true" />
          <span>{error}</span>
        </p>
      )}
    </div>
  );
}
