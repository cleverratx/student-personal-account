import { useEffect, useState } from 'react';

/** Текущее время, которое обновляется раз в интервал: для «идёт сейчас» и «через 20 минут». */
export function useNow(intervalMs = 30_000): Date {
  const [now, setNow] = useState(() => new Date());

  useEffect(() => {
    const timer = setInterval(() => setNow(new Date()), intervalMs);
    return () => clearInterval(timer);
  }, [intervalMs]);

  return now;
}
