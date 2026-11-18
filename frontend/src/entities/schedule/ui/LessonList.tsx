import { lessonStates } from '../lib/lessonState';
import type { Lesson } from '../model/schedule';
import styles from './LessonList.module.css';
import { LessonRow } from './LessonRow';

export interface LessonListProps {
  lessons: Lesson[];
  timeZone: string;
  now: Date;
  /** Подпись списка для скринридера, например «Пары, среда, 18 ноября». */
  label: string;
}

/** Пары одного дня или «Пар нет». */
export function LessonList({ lessons, timeZone, now, label }: LessonListProps) {
  if (lessons.length === 0) return <p className={styles.dayOff}>Пар нет</p>;

  const states = lessonStates(lessons, now);
  return (
    <ol className={styles.list} aria-label={label}>
      {lessons.map((lesson) => (
        <LessonRow key={lesson.id} lesson={lesson} timeZone={timeZone} state={states.get(lesson.id) ?? 'later'} now={now} />
      ))}
    </ol>
  );
}
