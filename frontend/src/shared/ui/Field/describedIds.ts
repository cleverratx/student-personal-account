import type { ReactNode } from 'react';

/** Id подсказки и ошибки, на которые поле ссылается через aria-describedby. */
export function describedIds(id: string, { hint, error }: { hint?: ReactNode; error?: string }) {
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;
  return { hintId, errorId, describedBy: [hintId, errorId].filter(Boolean).join(' ') || undefined };
}
