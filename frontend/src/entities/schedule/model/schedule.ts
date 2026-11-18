import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { api, unwrap, type ApiError, type components } from '@/shared/api';
import { addDays, type DateKey } from '@/shared/lib';

export type Lesson = components['schemas']['LessonDto'];
export type LessonChange = components['schemas']['LessonChangeDto'];
export type LessonType = components['schemas']['LessonType'];
export type Schedule = components['schemas']['ScheduleResponse'];

export const scheduleKeys = {
  week: (weekStart: DateKey) => ['schedule', 'week', weekStart] as const,
};

function fetchWeek(weekStart: DateKey) {
  // Группу сервер берёт из сессии, в запросе только даты.
  return unwrap(api.GET('/api/schedule', { params: { query: { from: weekStart, to: addDays(weekStart, 6) } } }));
}

/**
 * Пары недели, с понедельника по воскресенье. День тоже берём из недели: листание дней внутри неё
 * не делает новых запросов. Соседние недели подгружаются заранее, чтобы свайп через выходные был мгновенным.
 */
export function useWeekSchedule(weekStart: DateKey) {
  const queryClient = useQueryClient();
  const query = useQuery<Schedule, ApiError>({ queryKey: scheduleKeys.week(weekStart), queryFn: () => fetchWeek(weekStart) });
  const loaded = query.isSuccess;

  useEffect(() => {
    if (!loaded) return;
    for (const neighbour of [addDays(weekStart, -7), addDays(weekStart, 7)]) {
      void queryClient.prefetchQuery({ queryKey: scheduleKeys.week(neighbour), queryFn: () => fetchWeek(neighbour) });
    }
  }, [loaded, queryClient, weekStart]);

  return query;
}
