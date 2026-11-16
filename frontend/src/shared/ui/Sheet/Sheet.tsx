import { X } from 'lucide-react';
import { useEffect, useId, useRef, type MouseEvent, type ReactNode } from 'react';
import { IconButton } from '../IconButton/IconButton';
import styles from './Sheet.module.css';

export interface SheetProps {
  open: boolean;
  onClose: () => void;
  title: string;
  subtitle?: string;
  children: ReactNode;
}

/**
 * На телефоне лист снизу, шире 560 px окно по центру. Закрывается крестиком, Esc и кликом по фону.
 * Сделан на <dialog>: фокус внутри окна и его возврат после закрытия браузер берёт на себя.
 */
export function Sheet({ open, onClose, title, subtitle, children }: SheetProps) {
  const ref = useRef<HTMLDialogElement>(null);
  const titleId = useId();

  useEffect(() => {
    const dialog = ref.current;
    if (!dialog) return;
    // В jsdom у <dialog> нет showModal, поэтому вызов через «?.».
    if (open && !dialog.open) dialog.showModal?.();
    if (!open && dialog.open) dialog.close?.();
  }, [open]);

  useEffect(() => {
    if (!open) return;
    document.documentElement.style.overflow = 'hidden';
    return () => {
      document.documentElement.style.overflow = '';
    };
  }, [open]);

  const onBackdropClick = (event: MouseEvent<HTMLDialogElement>) => {
    if (event.target === ref.current) onClose();
  };

  return (
    <dialog
      ref={ref}
      className={styles.sheet}
      aria-labelledby={titleId}
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      onClick={onBackdropClick}
    >
      {open && (
        <>
          <div className={styles.grip} aria-hidden="true">
            <span />
          </div>
          <div className={styles.head}>
            <div className={styles.titles}>
              <h2 className={styles.title} id={titleId}>
                {title}
              </h2>
              {subtitle && <p className={styles.subtitle}>{subtitle}</p>}
            </div>
            <IconButton plain label="Закрыть" icon={<X size={20} aria-hidden="true" />} onClick={onClose} />
          </div>
          <div className={styles.body}>{children}</div>
        </>
      )}
    </dialog>
  );
}
