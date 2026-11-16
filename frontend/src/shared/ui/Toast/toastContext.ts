import { createContext, useContext } from 'react';

export interface ToastOptions {
  /** Отменить только что сделанное. С отменой тост висит дольше. */
  undo?: () => void;
}

export type ShowToast = (text: string, options?: ToastOptions) => void;

export const ToastContext = createContext<ShowToast | null>(null);

/** Короткое сообщение после действия: «Заявка отправлена», при желании с «Отменить». */
export function useToast(): ShowToast {
  const show = useContext(ToastContext);
  if (!show) throw new Error('useToast работает только внутри ToastProvider');
  return show;
}
