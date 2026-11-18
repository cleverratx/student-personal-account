import { plural } from '@/shared/lib';
import type { Lesson } from '../model/schedule';

export type LessonState = 'past' | 'now' | 'next' | 'later';

const MINUTES = { one: 'минуту', few: 'минуты', many: 'минут' };

/** «через 25 минут», «через 2 ч 5 мин». */
export function startsIn(minutes: number): string {
  if (minutes < 60) return `через ${plural(Math.max(1, minutes), MINUTES)}`;
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return rest ? `через ${hours}\u00a0ч ${rest}\u00a0мин` : `через ${hours}\u00a0ч`;
}

/**
 * Состояние пар относительно текущего момента. Следующей считается первая неотменённая пара,
 * которая ещё не началась. Отменённая пара никогда не «идёт» и не «следующая».
 */
export function lessonStates(lessons: Lesson[], now: Date): Map<string, LessonState> {
  const time = now.getTime();
  const states = new Map<string, LessonState>();
  let nextFound = false;

  for (const lesson of lessons) {
    const start = new Date(lesson.startsAt).getTime();
    const end = new Date(lesson.endsAt).getTime();
    if (end <= time) states.set(lesson.id, 'past');
    else if (start <= time && !lesson.isCancelled) states.set(lesson.id, 'now');
    else if (!nextFound && !lesson.isCancelled && start > time) {
      states.set(lesson.id, 'next');
      nextFound = true;
    } else states.set(lesson.id, 'later');
  }

  return states;
}
