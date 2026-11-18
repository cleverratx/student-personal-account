import { json, mockFetch } from './fetchMock';

type Role = 'Student' | 'Staff';
type Handler = (request: Request) => Response | Promise<Response>;

// Вымышленные люди, как в демо-данных бэкенда.
export const PASSWORD = 'Correct-horse-1';

const USERS: Record<Role, { id: string; login: string; profile: object }> = {
  Student: {
    id: '3f1c7a52-8e0b-4d6a-9c41-2b7e5d90a1f3',
    login: 'a.ivanova',
    profile: {
      role: 'Student',
      login: 'a.ivanova',
      fullName: 'Иванова Анастасия Сергеевна',
      initials: 'АИ',
      student: {
        faculty: 'Факультет информатики',
        programCode: '09.03.04',
        programName: 'Программная инженерия',
        course: 1,
        group: 'ПИ-21',
        gradeBookNumber: '2026-0123',
      },
      staff: null,
    },
  },
  Staff: {
    id: '9a64d0e2-5b37-4f18-8c2d-71e3b6f04c85',
    login: 'o.krylova',
    profile: {
      role: 'Staff',
      login: 'o.krylova',
      fullName: 'Крылова Ольга Викторовна',
      initials: 'ОК',
      student: null,
      staff: { faculty: 'Деканат факультета информатики', position: 'Специалист по учебной работе', office: 'каб. 214' },
    },
  },
};

const unauthorized = () => json(401, { title: 'Нужно войти', status: 401 });

/**
 * Бэкенд в памяти: вход, выход, /api/me и профиль с настоящей логикой сессии.
 * overrides подменяют отдельные ответы, например чтобы вернуть 500.
 */
export function fakeBackend({ session = null as Role | null, overrides = {} as Record<string, Handler> } = {}) {
  const state = { session };

  const routes: Record<string, Handler> = {
    'POST /api/auth/login': async (request) => {
      const body = (await request.json()) as { login: string; password: string };
      const role = (Object.keys(USERS) as Role[]).find((key) => USERS[key].login === body.login);
      if (!role || body.password !== PASSWORD) {
        return json(401, { title: 'Не удалось войти', status: 401, detail: 'Неверный логин или пароль.' });
      }
      state.session = role;
      return json(200, { role });
    },
    'POST /api/auth/logout': () => {
      if (!state.session) return unauthorized();
      state.session = null;
      return new Response(null, { status: 204 });
    },
    'GET /api/me': () => {
      if (!state.session) return unauthorized();
      const user = USERS[state.session];
      return json(200, { id: user.id, login: user.login, role: state.session });
    },
    'GET /api/me/profile': () => (state.session ? json(200, USERS[state.session].profile) : unauthorized()),
    // Пустая неделя: экраны, которым расписание не важно, не должны падать на этом запросе.
    'GET /api/schedule': (request) => {
      if (!state.session) return unauthorized();
      const query = new URL(request.url).searchParams;
      return json(200, { from: query.get('from'), to: query.get('to'), timeZone: 'Europe/Moscow', group: 'ПИ-21', items: [] });
    },
    ...overrides,
  };

  return { state, ...mockFetch(routes) };
}
