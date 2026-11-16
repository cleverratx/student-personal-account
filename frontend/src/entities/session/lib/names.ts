import type { Profile } from '../model/session';

/** «Иванова Анастасия Сергеевна» → «Анастасия Иванова», как в меню и шапке. */
export function shortName(fullName: string): string {
  const [last, first] = fullName.trim().split(/\s+/);
  return first && last ? `${first} ${last}` : fullName.trim();
}

/** Строка под именем: группа студента или должность сотрудника. */
export function roleLine(profile: Profile): string | undefined {
  return profile.student?.group ?? profile.staff?.position;
}
