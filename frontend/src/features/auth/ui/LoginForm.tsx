import { OctagonAlert } from 'lucide-react';
import { useRef, useState, type FormEvent } from 'react';
import { Button, TextField } from '@/shared/ui';
import { describeLoginError } from '../model/loginError';
import { useCountdown } from '../model/useCountdown';
import { useLogin, type LoginResponse } from '../model/useLogin';
import styles from './LoginForm.module.css';

interface FieldErrors {
  login?: string;
  password?: string;
}

function formatTimer(seconds: number) {
  const minutes = Math.floor(seconds / 60);
  return `${minutes}:${String(seconds % 60).padStart(2, '0')}`;
}

export function LoginForm({ onSuccess }: { onSuccess?: (result: LoginResponse) => void }) {
  const login = useLogin();
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<{ message: string; locked: boolean } | null>(null);
  const lock = useCountdown();
  const locked = lock.running;
  // Сообщение о блокировке живёт, пока идёт отсчёт.
  const visibleError = formError && (!formError.locked || locked) ? formError.message : null;
  const loginRef = useRef<HTMLInputElement>(null);
  const passwordRef = useRef<HTMLInputElement>(null);

  const focusFirstInvalid = (errors: FieldErrors) => {
    if (errors.login) loginRef.current?.focus();
    else if (errors.password) passwordRef.current?.focus();
  };

  const onSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (login.isPending || locked) return;

    const data = new FormData(event.currentTarget);
    const values = { login: String(data.get('username') ?? '').trim(), password: String(data.get('password') ?? '') };
    const errors: FieldErrors = {
      login: values.login ? undefined : 'Введите логин',
      password: values.password ? undefined : 'Введите пароль',
    };
    setFieldErrors(errors);
    setFormError(null);
    if (errors.login || errors.password) {
      focusFirstInvalid(errors);
      return;
    }

    login.mutate(values, {
      onSuccess: (result) => onSuccess?.(result),
      onError: (error) => {
        const failure = describeLoginError(error);
        if (failure.kind === 'fields') {
          setFieldErrors(failure.fields);
          focusFirstInvalid(failure.fields);
          return;
        }
        setFormError({ message: failure.message, locked: failure.kind === 'locked' });
        if (failure.kind === 'locked') lock.start(failure.seconds);
        else lock.stop();
      },
    });
  };

  return (
    <form className={styles.form} onSubmit={onSubmit} noValidate aria-label="Вход">
      <TextField
        ref={loginRef}
        label="Логин"
        name="username"
        autoComplete="username"
        autoCapitalize="none"
        autoCorrect="off"
        spellCheck={false}
        error={fieldErrors.login}
        onChange={() => fieldErrors.login && setFieldErrors((current) => ({ ...current, login: undefined }))}
      />
      <TextField
        ref={passwordRef}
        label="Пароль"
        name="password"
        type="password"
        revealable
        autoComplete="current-password"
        error={fieldErrors.password}
        onChange={() => fieldErrors.password && setFieldErrors((current) => ({ ...current, password: undefined }))}
      />
      {visibleError && (
        <p className={styles.formError} role="alert">
          <OctagonAlert size={16} aria-hidden="true" />
          <span>{visibleError}</span>
        </p>
      )}
      <Button type="submit" block loading={login.isPending} disabled={locked}>
        {locked ? (
          <>
            Войти через <span className={styles.timer}>{formatTimer(lock.secondsLeft)}</span>
          </>
        ) : (
          'Войти'
        )}
      </Button>
    </form>
  );
}
