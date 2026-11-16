import { useCallback, useEffect, useState } from 'react';

interface Countdown {
  until: number;
  now: number;
}

/** Обратный отсчёт в секундах. start(300) запускает, на нуле отсчёт сам останавливается. */
export function useCountdown() {
  const [state, setState] = useState<Countdown | null>(null);
  const until = state?.until;

  const start = useCallback((seconds: number) => {
    const now = Date.now();
    setState({ until: now + seconds * 1000, now });
  }, []);

  const stop = useCallback(() => setState(null), []);

  useEffect(() => {
    if (until === undefined) return;
    const timer = setInterval(() => {
      const now = Date.now();
      setState(now >= until ? null : { until, now });
    }, 1000);
    return () => clearInterval(timer);
  }, [until]);

  const secondsLeft = state ? Math.max(0, Math.ceil((state.until - state.now) / 1000)) : 0;
  return { secondsLeft, running: secondsLeft > 0, start, stop };
}
