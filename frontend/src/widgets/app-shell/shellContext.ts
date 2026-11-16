import { createContext, useContext } from 'react';
import type { ShellRole } from './navigation';

/** Кто вошёл: этого хватает шапке и боковому меню. Профиль целиком грузит экран «Профиль». */
export interface ShellUser {
  initials: string;
  name: string;
  /** Строка под именем в меню: группа у студента, должность у сотрудника. */
  meta?: string;
}

export interface ShellState {
  role: ShellRole;
  user?: ShellUser;
}

export const ShellContext = createContext<ShellState>({ role: 'Student' });

export function useShell(): ShellState {
  return useContext(ShellContext);
}
