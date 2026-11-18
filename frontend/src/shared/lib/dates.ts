/**
 * Календарная дата без времени, «2026-11-18». День считаем во времени вуза, поэтому храним строкой,
 * а не Date: у Date всегда есть часовой пояс браузера, и пара в 00:30 по Москве уехала бы на вчера.
 */
export type DateKey = string;

const keyFormatters = new Map<string, Intl.DateTimeFormat>();

function keyFormatter(timeZone: string) {
  let formatter = keyFormatters.get(timeZone);
  if (!formatter) {
    formatter = new Intl.DateTimeFormat('en-CA', { timeZone, year: 'numeric', month: '2-digit', day: '2-digit' });
    keyFormatters.set(timeZone, formatter);
  }
  return formatter;
}

/** В какой день по часам вуза приходится момент. */
export function dateInZone(instant: Date | string, timeZone: string): DateKey {
  return keyFormatter(timeZone).format(typeof instant === 'string' ? new Date(instant) : instant);
}

function toUtcDate(key: DateKey): Date {
  const [year, month, day] = key.split('-').map(Number);
  return new Date(Date.UTC(year!, month! - 1, day!));
}

function fromUtcDate(date: Date): DateKey {
  return date.toISOString().slice(0, 10);
}

export function addDays(key: DateKey, days: number): DateKey {
  const date = toUtcDate(key);
  date.setUTCDate(date.getUTCDate() + days);
  return fromUtcDate(date);
}

/** День недели с понедельника: 0 понедельник, 6 воскресенье. */
export function weekdayIndex(key: DateKey): number {
  return (toUtcDate(key).getUTCDay() + 6) % 7;
}

export function startOfWeek(key: DateKey): DateKey {
  return addDays(key, -weekdayIndex(key));
}

export function daysBetween(from: DateKey, to: DateKey): number {
  return Math.round((toUtcDate(to).getTime() - toUtcDate(from).getTime()) / 86_400_000);
}

export function isDateKey(value: string | null): value is DateKey {
  return value !== null && /^\d{4}-\d{2}-\d{2}$/.test(value) && !Number.isNaN(toUtcDate(value).getTime()) && fromUtcDate(toUtcDate(value)) === value;
}

const timeFormatters = new Map<string, Intl.DateTimeFormat>();

/** «09:00» по часам вуза. */
export function formatTime(instant: string, timeZone: string): string {
  let formatter = timeFormatters.get(timeZone);
  if (!formatter) {
    formatter = new Intl.DateTimeFormat('ru-RU', { timeZone, hour: '2-digit', minute: '2-digit' });
    timeFormatters.set(timeZone, formatter);
  }
  return formatter.format(new Date(instant));
}

const dayMonth = new Intl.DateTimeFormat('ru-RU', { timeZone: 'UTC', day: 'numeric', month: 'long' });
const weekdayLong = new Intl.DateTimeFormat('ru-RU', { timeZone: 'UTC', weekday: 'long' });
const weekdayShort = new Intl.DateTimeFormat('ru-RU', { timeZone: 'UTC', weekday: 'short' });

/** «18 ноября». */
export function formatDayMonth(key: DateKey): string {
  return dayMonth.format(toUtcDate(key));
}

/** «среда». */
export function formatWeekday(key: DateKey): string {
  return weekdayLong.format(toUtcDate(key));
}

/** «ср». */
export function formatWeekdayShort(key: DateKey): string {
  return weekdayShort.format(toUtcDate(key));
}

/** «16–21 ноября» или «30 ноября – 5 декабря». */
export function formatDayRange(from: DateKey, to: DateKey): string {
  const [, fromMonth, fromDay] = from.split('-');
  const [, toMonth] = to.split('-');
  if (fromMonth === toMonth) return `${Number(fromDay)}\u2013${formatDayMonth(to)}`;
  return `${formatDayMonth(from)} \u2013 ${formatDayMonth(to)}`;
}
