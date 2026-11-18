import { fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { fakeBackend } from './helpers/fakeBackend';
import { json } from './helpers/fetchMock';
import { renderApp } from './helpers/renderApp';

// Среда, 18 ноября 2026, 10:15 по Москве.
const NOW = new Date('2026-11-18T07:15:00Z');

const WEEK = {
  from: '2026-11-16',
  to: '2026-11-22',
  timeZone: 'Europe/Moscow',
  group: 'ПИ-21',
  items: [
    {
      id: '0b6f2c1e-1a2b-4c3d-8e4f-000000000001',
      discipline: 'Линейная алгебра',
      type: 'Lecture',
      startsAt: '2026-11-18T06:00:00Z',
      endsAt: '2026-11-18T07:30:00Z',
      room: '101',
      teacher: 'Ермакова Л. П.',
      isCancelled: false,
      change: null,
    },
    {
      id: '0b6f2c1e-1a2b-4c3d-8e4f-000000000002',
      discipline: 'Физика',
      type: 'Practice',
      startsAt: '2026-11-18T09:20:00Z',
      endsAt: '2026-11-18T10:50:00Z',
      room: '501',
      teacher: 'Шубин Р. Е.',
      isCancelled: false,
      change: {
        type: 'RoomChange',
        oldStartsAt: null,
        newStartsAt: null,
        oldRoom: '120',
        newRoom: '501',
        reason: 'В аудитории ремонтируют проектор',
        changedAt: '2026-11-18T02:00:00Z',
      },
    },
    {
      id: '0b6f2c1e-1a2b-4c3d-8e4f-000000000003',
      discipline: 'Основы программирования',
      type: 'Lab',
      startsAt: '2026-11-18T11:30:00Z',
      endsAt: '2026-11-18T13:00:00Z',
      room: '410',
      teacher: 'Карпенко Н. В.',
      isCancelled: true,
      change: {
        type: 'Cancellation',
        oldStartsAt: null,
        newStartsAt: null,
        oldRoom: null,
        newRoom: null,
        reason: 'Преподаватель на больничном',
        changedAt: '2026-11-17T15:00:00Z',
      },
    },
    {
      id: '0b6f2c1e-1a2b-4c3d-8e4f-000000000004',
      discipline: 'Линейная алгебра',
      type: 'Practice',
      startsAt: '2026-11-18T14:50:00Z',
      endsAt: '2026-11-18T16:20:00Z',
      room: '305',
      teacher: 'Ермакова Л. П.',
      isCancelled: false,
      change: {
        type: 'Reschedule',
        oldStartsAt: '2026-11-18T07:40:00Z',
        newStartsAt: '2026-11-18T14:50:00Z',
        oldRoom: '214',
        newRoom: '305',
        reason: 'Преподаватель на заседании учёного совета',
        changedAt: '2026-11-17T12:00:00Z',
      },
    },
    {
      id: '0b6f2c1e-1a2b-4c3d-8e4f-000000000005',
      discipline: 'Английский язык',
      type: 'Practice',
      startsAt: '2026-11-20T06:00:00Z',
      endsAt: '2026-11-20T07:30:00Z',
      room: '412',
      teacher: 'Осипова М. Л.',
      isCancelled: false,
      change: null,
    },
  ],
};

function scheduleBackend(overrides: Record<string, (request: Request) => Response | Promise<Response>> = {}) {
  return fakeBackend({
    session: 'Student',
    overrides: {
      'GET /api/schedule': (request) => {
        const query = new URL(request.url).searchParams;
        const from = query.get('from')!;
        // Соседние недели экран подгружает заранее, у них пар нет.
        return json(200, from === WEEK.from ? WEEK : { ...WEEK, from, to: query.get('to'), items: [] });
      },
      ...overrides,
    },
  });
}

beforeEach(() => {
  // Подменяем только часы: таймеры настоящие, чтобы запросы и userEvent работали как обычно.
  vi.useFakeTimers({ toFake: ['Date'], now: NOW });
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

/** Подпись над списком пар: «Сегодня», «Завтра». В меню тоже есть «Сегодня», поэтому ищем только здесь. */
function dayLabel(text: string) {
  return screen.findByText(text, { selector: 'p[aria-live] span' });
}

async function openSchedule(path = '/schedule') {
  const app = renderApp(path);
  await screen.findByRole('list', { name: /^Пары/ });
  return app;
}

describe('экран «Расписание»', () => {
  it('показывает пары сегодняшнего дня по часам вуза: время, тип, аудиторию и преподавателя', async () => {
    scheduleBackend();
    await openSchedule();

    expect(await dayLabel('Сегодня')).toBeInTheDocument();
    const rows = within(screen.getByRole('list', { name: 'Пары, среда, 18 ноября' })).getAllByRole('listitem');
    expect(rows).toHaveLength(4);
    expect(rows[0]).toHaveTextContent('09:0010:30');
    expect(rows[0]).toHaveTextContent('Лекция, ауд. 101, Ермакова Л. П.');
    // Группа рядом с заголовком экрана, имя в меню тоже подписано группой.
    expect(within(screen.getByRole('main')).getByText('ПИ-21')).toBeInTheDocument();
  });

  it('подсвечивает текущую пару и пишет, через сколько следующая', async () => {
    scheduleBackend();
    await openSchedule();

    const rows = within(screen.getByRole('list', { name: /^Пары/ })).getAllByRole('listitem');
    expect(rows[0]).toHaveAttribute('aria-current', 'time');
    expect(rows[0]).toHaveTextContent('Идёт сейчас');
    // Отменённая пара не может быть следующей.
    expect(rows[1]).toHaveTextContent('Начнётся через 2 ч 5 мин');
    expect(rows[2]).not.toHaveTextContent('Начнётся');
  });

  it('перенос, другая аудитория и отмена видны без открытия деталей', async () => {
    scheduleBackend();
    await openSchedule();

    const rows = within(screen.getByRole('list', { name: /^Пары/ })).getAllByRole('listitem');
    expect(rows[1]).toHaveTextContent('Другая аудитория, раньше ауд. 120');
    expect(rows[1]).toHaveTextContent('В аудитории ремонтируют проектор');
    expect(rows[2]).toHaveTextContent('Пара отменена');
    expect(rows[2]).toHaveTextContent('Преподаватель на больничном');
    expect(rows[3]).toHaveTextContent('17:5019:20');
    expect(rows[3]).toHaveTextContent('Перенесли с 10:40, ауд. 214');
    // Прежнее время и аудитория зачёркнуты.
    expect(rows[3]!.querySelector('s')).toHaveTextContent('10:40, ауд. 214');
  });

  it('спрашивает у сервера неделю целиком и только даты, без id группы или студента', async () => {
    const { calls } = scheduleBackend();
    await openSchedule();

    const request = calls.find((call) => new URL(call.url).pathname === '/api/schedule')!;
    const query = new URL(request.url).searchParams;
    expect([...query.keys()].sort()).toEqual(['from', 'to']);
    expect(query.get('from')).toBe('2026-11-16');
    expect(query.get('to')).toBe('2026-11-22');
  });

  it('листает дни кнопками и возвращается к сегодня одним касанием', async () => {
    scheduleBackend();
    const { router } = await openSchedule();

    await userEvent.click(screen.getByRole('button', { name: 'Следующий день' }));
    expect(await dayLabel('Завтра')).toBeInTheDocument();
    expect(screen.getByText('Пар нет')).toBeInTheDocument();
    expect(router.state.location.search).toBe('?date=2026-11-19');

    await userEvent.click(screen.getByRole('button', { name: 'Следующий день' }));
    expect(screen.getByText('Английский язык')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'К сегодня' }));
    expect(await dayLabel('Сегодня')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'К сегодня' })).not.toBeInTheDocument();
    expect(router.state.location.search).toBe('');
  });

  it('свайп влево открывает следующий день, вправо предыдущий, вертикальный жест ничего не листает', async () => {
    scheduleBackend();
    await openSchedule();
    const area = screen.getByRole('list', { name: /^Пары/ }).parentElement!;

    fireEvent.touchStart(area, { touches: [{ clientX: 300, clientY: 200 }] });
    fireEvent.touchEnd(area, { changedTouches: [{ clientX: 120, clientY: 210 }] });
    expect(await dayLabel('Завтра')).toBeInTheDocument();

    fireEvent.touchStart(area, { touches: [{ clientX: 200, clientY: 100 }] });
    fireEvent.touchEnd(area, { changedTouches: [{ clientX: 240, clientY: 400 }] });
    expect(await dayLabel('Завтра')).toBeInTheDocument();

    fireEvent.touchStart(area, { touches: [{ clientX: 100, clientY: 200 }] });
    fireEvent.touchEnd(area, { changedTouches: [{ clientX: 300, clientY: 200 }] });
    expect(await dayLabel('Сегодня')).toBeInTheDocument();
  });

  it('неделя: дни с понедельника по субботу, сегодняшний отмечен, пустой день «Пар нет»', async () => {
    scheduleBackend();
    await openSchedule();

    await userEvent.click(screen.getByRole('radio', { name: 'Неделя' }));

    const days = screen.getAllByRole('heading', { level: 2 });
    expect(days.map((day) => day.textContent)).toEqual([
      'Понедельник, 16 ноября',
      'Вторник, 17 ноября',
      'Среда, 18 ноябрясегодня',
      'Четверг, 19 ноября',
      'Пятница, 20 ноября',
      'Суббота, 21 ноября',
    ]);
    expect(screen.getByText('Эта неделя')).toBeInTheDocument();
    expect(screen.getByText('16–21 ноября')).toBeInTheDocument();
    expect(screen.getAllByText('Пар нет')).toHaveLength(4);

    await userEvent.click(screen.getByRole('button', { name: 'Следующая неделя' }));
    expect(await screen.findByText('На этой неделе пар нет')).toBeInTheDocument();
    expect(screen.getByText('Следующая неделя')).toBeInTheDocument();
  });

  it('если расписание не загрузилось, даёт повторить', async () => {
    let fail = true;
    scheduleBackend({
      'GET /api/schedule': (request) => {
        const from = new URL(request.url).searchParams.get('from');
        if (fail) return json(503, { title: 'Сервис временно недоступен', status: 503 });
        return json(200, from === WEEK.from ? WEEK : { ...WEEK, items: [] });
      },
    });
    renderApp('/schedule');

    expect(await screen.findByRole('alert')).toHaveTextContent('Не удалось загрузить расписание');
    fail = false;
    await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));

    expect(await screen.findByRole('list', { name: /^Пары/ })).toBeInTheDocument();
  });
});
