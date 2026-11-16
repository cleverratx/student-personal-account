import { CircleCheck } from 'lucide-react';
import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react';
import { cx } from '@/shared/lib';
import styles from './Toast.module.css';
import { ToastContext, type ShowToast } from './toastContext';

interface ToastState {
  text: string;
  undo?: () => void;
}

/** Держит один тост на всё приложение: новый заменяет старый. */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toast, setToast] = useState<ToastState | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout>>(undefined);

  const show = useCallback<ShowToast>((text, options = {}) => {
    clearTimeout(timer.current);
    setToast({ text, undo: options.undo });
    timer.current = setTimeout(() => setToast(null), options.undo ? 6000 : 4000);
  }, []);

  useEffect(() => () => clearTimeout(timer.current), []);

  const undo = () => {
    clearTimeout(timer.current);
    toast?.undo?.();
    setToast(null);
  };

  return (
    <ToastContext.Provider value={show}>
      {children}
      <div className={cx(styles.toast, toast && styles.visible)} role="status" aria-live="polite">
        {toast && (
          <>
            <CircleCheck className={styles.icon} size={18} aria-hidden="true" />
            <span className={styles.text}>{toast.text}</span>
            {toast.undo && (
              <button type="button" className={styles.action} onClick={undo}>
                Отменить
              </button>
            )}
          </>
        )}
      </div>
    </ToastContext.Provider>
  );
}
