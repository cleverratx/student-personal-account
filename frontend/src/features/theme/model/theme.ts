import { useState } from 'react';

export type Theme = 'system' | 'light' | 'dark';

// Тот же ключ читает скрипт в index.html до первой отрисовки.
const STORAGE_KEY = 'dekanat-theme';

function readTheme(): Theme {
  try {
    const value = localStorage.getItem(STORAGE_KEY);
    return value === 'light' || value === 'dark' ? value : 'system';
  } catch {
    return 'system';
  }
}

/** Тема хранится только в этом браузере. Без доступа к хранилищу просто не запоминается. */
export function useTheme() {
  const [theme, setTheme] = useState<Theme>(readTheme);

  const change = (next: Theme) => {
    setTheme(next);
    if (next === 'system') delete document.documentElement.dataset.theme;
    else document.documentElement.dataset.theme = next;
    try {
      if (next === 'system') localStorage.removeItem(STORAGE_KEY);
      else localStorage.setItem(STORAGE_KEY, next);
    } catch {
      // Приватный режим или запрет хранилища: тема действует до перезагрузки.
    }
  };

  return [theme, change] as const;
}
