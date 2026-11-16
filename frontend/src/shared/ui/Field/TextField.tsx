import { Eye, EyeOff } from 'lucide-react';
import { useId, useState, type InputHTMLAttributes } from 'react';
import { cx } from '@/shared/lib';
import styles from './Field.module.css';
import { describedIds } from './describedIds';
import { FieldShell, type FieldShellProps } from './FieldShell';

export interface TextFieldProps extends FieldShellProps, Omit<InputHTMLAttributes<HTMLInputElement>, 'id'> {
  /** Для пароля: кнопка «Показать пароль» внутри поля. */
  revealable?: boolean;
}

export function TextField({ label, hint, error, optional, revealable, type = 'text', className, ...rest }: TextFieldProps) {
  const id = useId();
  const [revealed, setRevealed] = useState(false);
  const { describedBy } = describedIds(id, { hint, error });

  const input = (
    <input
      id={id}
      className={cx(styles.input, className)}
      type={revealable && revealed ? 'text' : type}
      aria-invalid={error ? true : undefined}
      aria-describedby={describedBy}
      {...rest}
    />
  );

  return (
    <FieldShell id={id} label={label} hint={hint} error={error} optional={optional}>
      {revealable ? (
        <div className={styles.withButton}>
          {input}
          <button
            type="button"
            className={styles.inputButton}
            aria-label="Показать пароль"
            aria-pressed={revealed}
            onClick={() => setRevealed((value) => !value)}
          >
            {revealed ? <EyeOff size={20} aria-hidden="true" /> : <Eye size={20} aria-hidden="true" />}
          </button>
        </div>
      ) : (
        input
      )}
    </FieldShell>
  );
}
