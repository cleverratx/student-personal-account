import { ChevronLeft, ChevronRight } from 'lucide-react';
import { useRef, type TouchEvent } from 'react';
import { useSearchParams } from 'react-router';
import { LessonList, useWeekSchedule, type Lesson } from '@/entities/schedule';
import { UNIVERSITY_TIME_ZONE } from '@/shared/config';
import {
  addDays,
  cx,
  dateInZone,
  daysBetween,
  formatDayMonth,
  formatDayRange,
  formatWeekday,
  isDateKey,
  startOfWeek,
  useNow,
  type DateKey,
} from '@/shared/lib';
import { Button, EmptyState, ErrorState, IconButton, ScreenSkeleton, SegmentedControl } from '@/shared/ui';
import { ScreenHeader } from '@/widgets/app-shell';
import styles from './SchedulePage.module.css';

type View = 'day' | 'week';

const SWIPE_DISTANCE = 56;

function capitalize(text: string): string {
  return text.charAt(0).toUpperCase() + text.slice(1);
}

function dayName(date: DateKey, today: DateKey): string {
  const shift = daysBetween(today, date);
  if (shift === 0) return 'Сегодня';
  if (shift === 1) return 'Завтра';
  if (shift === -1) return 'Вчера';
  return capitalize(formatWeekday(date));
}

function weekName(weekStart: DateKey, today: DateKey): string {
  const shift = daysBetween(startOfWeek(today), weekStart) / 7;
  if (shift === 0) return 'Эта неделя';
  if (shift === 1) return 'Следующая неделя';
  if (shift === -1) return 'Прошлая неделя';
  return 'Неделя';
}

function lessonsOn(lessons: Lesson[], date: DateKey, timeZone: string): Lesson[] {
  return lessons.filter((lesson) => dateInZone(lesson.startsAt, timeZone) === date);
}

/**
 * Расписание группы. Вид и дата живут в адресе (?view=week&date=2026-11-18): «Назад» возвращает
 * к тому же дню, а ссылку на неделю можно переслать. Всё время показываем по часам вуза.
 */
export function SchedulePage() {
  const now = useNow();
  const [params, setParams] = useSearchParams();
  const view: View = params.get('view') === 'week' ? 'week' : 'day';
  const requested = params.get('date');

  // До ответа сервера берём пояс из настроек, после него тот, что прислал сервер.
  const weekProbe = startOfWeek(isDateKey(requested) ? requested : dateInZone(now, UNIVERSITY_TIME_ZONE));
  const schedule = useWeekSchedule(weekProbe);
  const timeZone = schedule.data?.timeZone ?? UNIVERSITY_TIME_ZONE;
  const today = dateInZone(now, timeZone);
  const date = isDateKey(requested) ? requested : today;
  const weekStart = startOfWeek(date);

  const go = (next: { view?: View; date?: DateKey }) => {
    const search = new URLSearchParams(params);
    const nextView = next.view ?? view;
    const nextDate = next.date ?? date;
    if (nextView === 'week') search.set('view', 'week');
    else search.delete('view');
    if (nextDate === today) search.delete('date');
    else search.set('date', nextDate);
    // Листание не копит историю: «Назад» уводит с экрана, а не на прошлый день.
    setParams(search, { replace: true });
  };

  const step = view === 'day' ? 1 : 7;
  const isCurrent = view === 'day' ? date === today : weekStart === startOfWeek(today);

  const touchStart = useRef<{ x: number; y: number } | null>(null);
  const onTouchStart = (event: TouchEvent) => {
    const touch = event.touches[0];
    touchStart.current = touch ? { x: touch.clientX, y: touch.clientY } : null;
  };
  const onTouchEnd = (event: TouchEvent) => {
    const start = touchStart.current;
    const touch = event.changedTouches[0];
    touchStart.current = null;
    if (!start || !touch) return;
    const dx = touch.clientX - start.x;
    const dy = touch.clientY - start.y;
    // Только явный горизонтальный жест, чтобы прокрутка страницы не листала дни.
    if (Math.abs(dx) < SWIPE_DISTANCE || Math.abs(dx) < Math.abs(dy) * 1.5) return;
    go({ date: addDays(date, dx < 0 ? step : -step) });
  };

  const weekDays = Array.from({ length: 7 }, (_, index) => addDays(weekStart, index));
  const lessons = schedule.data?.items ?? [];
  // Воскресенье показываем, только если в него что-то поставили.
  const visibleDays = weekDays.filter((day, index) => index < 6 || lessonsOn(lessons, day, timeZone).length > 0);

  let content;
  if (schedule.isPending) {
    content = <ScreenSkeleton rows={3} label="Загружаем расписание" />;
  } else if (schedule.isError) {
    content = <ErrorState noun="расписание" message={schedule.error.kind === 'http' ? schedule.error.message : undefined} onRetry={() => schedule.refetch()} retrying={schedule.isFetching} />;
  } else if (view === 'day') {
    content = (
      <LessonList
        lessons={lessonsOn(lessons, date, timeZone)}
        timeZone={timeZone}
        now={now}
        label={`Пары, ${formatWeekday(date)}, ${formatDayMonth(date)}`}
      />
    );
  } else if (lessons.length === 0) {
    content = <EmptyState title="На этой неделе пар нет" text="Каникулы или праздники. Листайте дальше, чтобы увидеть следующую неделю." />;
  } else {
    content = (
      <div className={styles.week}>
        {visibleDays.map((day) => (
          <section key={day} className={cx(styles.day, day < today && styles.pastDay)} aria-labelledby={`day-${day}`}>
            <h2 className={styles.dayHead} id={`day-${day}`}>
              {capitalize(formatWeekday(day))}, {formatDayMonth(day)}
              {day === today && <span className={styles.todayTag}>сегодня</span>}
            </h2>
            <LessonList
              lessons={lessonsOn(lessons, day, timeZone)}
              timeZone={timeZone}
              now={now}
              label={`Пары, ${formatWeekday(day)}, ${formatDayMonth(day)}`}
            />
          </section>
        ))}
      </div>
    );
  }

  return (
    <>
      <ScreenHeader title="Расписание" meta={schedule.data?.group} />
      <div className={styles.body}>
        <div className={styles.toolbar}>
          <SegmentedControl<View>
            label="Как показать расписание"
            value={view}
            onChange={(next) => go({ view: next })}
            options={[
              { value: 'day', label: 'День' },
              { value: 'week', label: 'Неделя' },
            ]}
          />
          {!isCurrent && (
            <Button size="small" variant="secondary" onClick={() => go({ date: today })}>
              {view === 'day' ? 'К сегодня' : 'К этой неделе'}
            </Button>
          )}
        </div>

        <div className={styles.bar}>
          <IconButton
            label={view === 'day' ? 'Предыдущий день' : 'Предыдущая неделя'}
            icon={<ChevronLeft size={22} aria-hidden="true" />}
            onClick={() => go({ date: addDays(date, -step) })}
          />
          <p className={styles.label} aria-live="polite">
            <span className={styles.labelName}>{view === 'day' ? dayName(date, today) : weekName(weekStart, today)}</span>
            <span className={styles.labelDates}>
              {view === 'day'
                ? `${formatWeekday(date)}, ${formatDayMonth(date)}`
                : formatDayRange(weekStart, addDays(weekStart, 5))}
            </span>
          </p>
          <IconButton
            label={view === 'day' ? 'Следующий день' : 'Следующая неделя'}
            icon={<ChevronRight size={22} aria-hidden="true" />}
            onClick={() => go({ date: addDays(date, step) })}
          />
        </div>

        <div className={styles.swipe} onTouchStart={onTouchStart} onTouchEnd={onTouchEnd}>
          {content}
        </div>
      </div>
    </>
  );
}
