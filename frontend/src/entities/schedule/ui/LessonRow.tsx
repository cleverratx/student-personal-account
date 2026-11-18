import { CalendarSync, CircleX, DoorOpen, Radio } from 'lucide-react';
import type { ReactNode } from 'react';
import { cx, dateInZone, formatDayMonth, formatTime, formatWeekdayShort } from '@/shared/lib';
import { LESSON_TYPE_LABEL } from '../lib/labels';
import { startsIn, type LessonState } from '../lib/lessonState';
import type { Lesson, LessonChange } from '../model/schedule';
import styles from './LessonRow.module.css';

export interface LessonRowProps {
  lesson: Lesson;
  timeZone: string;
  state: LessonState;
  now: Date;
}

const NBSP = '\u00a0';

/** «Шубин Р. Е.» не разрывается между строками. */
function noBreak(text: string): string {
  return text.replaceAll(' ', NBSP);
}

/** Когда пара была до переноса: только время, если день тот же, иначе ещё и день. */
function oldPlace(change: LessonChange, lesson: Lesson, timeZone: string): string {
  const parts: string[] = [];
  if (change.oldStartsAt) {
    const oldDay = dateInZone(change.oldStartsAt, timeZone);
    const time = formatTime(change.oldStartsAt, timeZone);
    parts.push(
      oldDay === dateInZone(lesson.startsAt, timeZone)
        ? time
        : `${formatWeekdayShort(oldDay)}, ${formatDayMonth(oldDay)}, ${time}`,
    );
  }
  if (change.oldRoom) parts.push(`ауд.${NBSP}${change.oldRoom}`);
  return parts.join(', ');
}

function ChangeLine({ lesson, timeZone }: { lesson: Lesson; timeZone: string }) {
  const change = lesson.change;
  if (!change) return null;

  let icon: ReactNode;
  let what: ReactNode;
  if (change.type === 'Cancellation') {
    icon = <CircleX size={16} aria-hidden="true" />;
    what = 'Пара отменена';
  } else if (change.type === 'Reschedule') {
    icon = <CalendarSync size={16} aria-hidden="true" />;
    what = (
      <>
        Перенесли с <s>{oldPlace(change, lesson, timeZone)}</s>
      </>
    );
  } else {
    icon = <DoorOpen size={16} aria-hidden="true" />;
    what = (
      <>
        Другая аудитория, раньше <s>{oldPlace(change, lesson, timeZone)}</s>
      </>
    );
  }

  return (
    <div className={cx(styles.change, change.type === 'Cancellation' && styles.changeCancelled)}>
      {icon}
      <p className={styles.changeText}>
        <span className={styles.changeWhat}>{what}</span>
        {change.reason && <span className={styles.reason}>{change.reason}</span>}
      </p>
    </div>
  );
}

/** Строка пары: время, предмет, тип, аудитория, преподаватель и что поменялось. */
export function LessonRow({ lesson, timeZone, state, now }: LessonRowProps) {
  const start = formatTime(lesson.startsAt, timeZone);
  const end = formatTime(lesson.endsAt, timeZone);
  const minutesToStart = Math.ceil((new Date(lesson.startsAt).getTime() - now.getTime()) / 60_000);
  // «Через N минут» только для сегодняшней пары: про завтрашнюю это бесполезно.
  const showStartsIn = state === 'next' && dateInZone(lesson.startsAt, timeZone) === dateInZone(now, timeZone);

  return (
    <li
      className={cx(
        styles.row,
        state === 'now' && styles.now,
        state === 'past' && styles.past,
        lesson.isCancelled && styles.cancelled,
      )}
      aria-current={state === 'now' ? 'time' : undefined}
    >
      <p className={styles.time}>
        <span>{start}</span>
        <span>{end}</span>
      </p>
      <div className={styles.main}>
        <p className={styles.title}>{lesson.discipline}</p>
        <p className={styles.meta}>
          {LESSON_TYPE_LABEL[lesson.type]}, ауд.{NBSP}{lesson.room}, {noBreak(lesson.teacher)}
        </p>
        {state === 'now' && (
          <p className={styles.status}>
            <Radio size={16} aria-hidden="true" />
            Идёт сейчас
          </p>
        )}
        {showStartsIn && <p className={styles.status}>Начнётся {startsIn(minutesToStart)}</p>}
      </div>
      <ChangeLine lesson={lesson} timeZone={timeZone} />
    </li>
  );
}
