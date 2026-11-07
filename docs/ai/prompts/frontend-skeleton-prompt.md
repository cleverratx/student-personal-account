# Задача: каркас фронтенда цифрового деканата

Ветка: `chore/frontend-skeleton`.

Это задача только про каркас frontend-приложения. Не реализовывай продуктовые экраны, UI-кит, авторизацию, роли, API-клиент или генерацию типов API.

## Сначала изучи проект

Прочитай:

- `AGENTS.md`
- раздел «Структура проекта» в `README.md`
- `docs/design/README.md`
- `.github/workflows/frontend.yml`
- текущую структуру репозитория

Не изменяй `.github/`, правила проекта, существующую документацию и backend.

Если обнаружишь противоречие между этой задачей и `AGENTS.md`, остановись и сообщи о противоречии.

## Что создать

Создай frontend-приложение в каталоге `frontend/` на Vite, React и TypeScript.

### Технологии и настройки

- Vite.
- React.
- TypeScript в режиме `strict`.
- Алиас `@/` -> `src/`.
- React Router.
- TanStack Query.
- ESLint.
- Vitest.
- CSS-переменные для базовых дизайн-токенов.

Не добавляй openapi-fetch, openapi-typescript, axios, UI-библиотеки, state manager и другие зависимости без необходимости.

### Структура

Создай слои в соответствии с README:

```text
frontend/
├── public/
├── src/
│   ├── app/
│   │   ├── providers/
│   │   ├── router/
│   │   └── styles/
│   ├── pages/
│   │   ├── home/
│   │   └── not-found/
│   ├── widgets/
│   ├── entities/
│   └── shared/
│       ├── config/
│       ├── lib/
│       └── types/
├── tests/
├── .nvmrc
├── Dockerfile
├── nginx.conf
├── index.html
├── package.json
├── package-lock.json
├── tsconfig.json
├── vite.config.ts
├── vitest.config.ts
└── eslint.config.js
```

Папку `features/` пока не создавай.

Слои импортируются только сверху вниз:

```text
app -> pages -> widgets -> entities -> shared
```

Не допускай циклических импортов и импортов из нижних слоёв в верхние.

## Стартовые страницы

Реализуй только:

1. Стартовую страницу по адресу `/`.
2. Страницу `404 Not Found` для неизвестных маршрутов.

На стартовой странице достаточно минимального содержимого:

- название «Цифровой деканат»;
- текст: «Это концепт для конкурса, а не официальный сервис вуза»;
- ссылка или кнопка на несуществующий маршрут для проверки перехода на 404.

Не создавай страницы входа, «Сегодня», расписания, оценок, заявок, уведомлений, профиля и кабинета сотрудника. Маршруты продуктовых экранов, общий layout и навигация будут отдельными задачами.

## Маршрутизация

Настрой React Router:

- `/` - стартовая страница;
- `*` - страница 404.

Используй `createBrowserRouter` или другой современный подход React Router. Не создавай навигационное меню, layout, guards и проверку ролей в этой задаче.

## Состояние приложения

Подключи TanStack Query через провайдер в `src/app/providers`.

Создай минимальную обёртку приложения:

- `AppProviders`;
- `RouterProvider` или эквивалентную настройку маршрутизации;
- точку входа React-приложения.

Не реализуй получение текущего пользователя, сессии, вход, выход и защиту маршрутов.

## Стилевая основа

Создай минимальные CSS-переменные на основе `docs/design/README.md`:

```css
:root {
  --color-primary: #1F5EFF;
  --color-primary-hover: #1747CC;
  --color-accent: #FFB020;
  --color-accent-text: #5C3A00;
  --color-success: #0E8A4C;
  --color-success-text: #0B6B3A;
  --color-warning: #B45309;
  --color-danger: #C0392B;
  --color-bg: #F6F7F9;
  --color-surface: #FFFFFF;
  --color-border: #DDE1E6;
  --color-border-input: #6B7280;
  --color-text: #111827;
  --color-text-secondary: #5B6472;
  --color-text-on-primary: #FFFFFF;

  --font-family-base: 'Onest', system-ui, sans-serif;

  --space-1: 4px;
  --space-2: 8px;
  --space-3: 12px;
  --space-4: 16px;
  --space-5: 20px;
  --space-6: 24px;
  --space-8: 32px;

  --radius-sm: 8px;
  --radius-md: 12px;
  --radius-lg: 16px;
}
```

Правила использования цветов:

- `--color-accent` (`#FFB020`) нельзя использовать как цвет текста на белом фоне; допускается только как фон под тёмным текстом `#5C3A00`.
- Для обычного зелёного текста используй `--color-success-text` (`#0B6B3A`), а не `#0E8A4C`.
- Для границ полей ввода используй `--color-border-input`; `--color-border` подходит только для декоративных границ карточек и разделителей.

Подключи шрифт Onest локально через `@fontsource/onest`. Не используй Google Fonts, CDN и другие внешние источники шрифтов.

Не создавай UI-кит, компоненты Button, Input, Card, Badge, Modal и другие переиспользуемые компоненты в этой задаче.

## Переменные окружения и dev-прокси

Создай `.env.example` в `frontend/`:

```env
API_PROXY_TARGET=http://localhost:8080
```

Имя переменной должно быть именно `API_PROXY_TARGET`, без префикса `VITE_`.

Префикс `VITE_` не используется, потому что Vite может встроить такую переменную в код, который попадёт в браузер. Адрес backend нужен только конфигурации Vite и не должен попадать в клиентский bundle.

В `vite.config.ts` прочитай переменную через `loadEnv`:

```ts
import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  const apiProxyTarget = env.API_PROXY_TARGET ?? 'http://localhost:8080';

  return {
    plugins: [react()],
    resolve: {
      alias: {
        '@': path.resolve(__dirname, './src'),
      },
    },
    server: {
      proxy: {
        '/api': {
          target: apiProxyTarget,
          changeOrigin: true,
        },
      },
    },
  };
});
```

Не хардкодь адрес backend вне значения по умолчанию для локальной разработки.

## Node и CI

Создай `frontend/.nvmrc`:

```text
24
```

Это обязательно: без `.nvmrc` job `frontend` в существующем CI не запускает проверки.

Не изменяй `.github/workflows/frontend.yml`.

Убедись, что в `frontend/` есть:

- `package.json`;
- `package-lock.json`;
- `.nvmrc`.

Проверь, что команды из CI совместимы с проектом:

```bash
npm ci
npm run lint
npm test
npm run build
```

## Скрипты package.json

Добавь только необходимые скрипты:

```json
{
  "dev": "vite",
  "build": "tsc -b && vite build",
  "preview": "vite preview",
  "lint": "eslint .",
  "test": "vitest run",
  "test:watch": "vitest"
}
```

Не добавляй скрипт `api:generate`.

Не добавляй API-клиент, типы API, моки API и фикстуры ответов сервера. Клиент API и генерация типов будут отдельной задачей после появления `backend/openapi/openapi.json`.

## Docker и nginx

Создай multi-stage `Dockerfile`:

1. Этап сборки на Node.js:
   - `npm ci`;
   - `npm run build`.
2. Этап раздачи на nginx:
   - копирует результат сборки;
   - раздаёт статику;
   - для неизвестных клиентских маршрутов возвращает `index.html`;
   - проксирует `/api` на backend-сервис.

Создай `nginx.conf` с корректным SPA fallback и проксированием `/api`.

Сам nginx переменные окружения не читает. Их подставляет официальный образ nginx, но только в файлы из `/etc/nginx/templates/`. Поэтому в Dockerfile копируй конфиг как шаблон и задай значение по умолчанию:

```dockerfile
COPY nginx.conf /etc/nginx/templates/default.conf.template
ENV API_PROXY_TARGET=http://api:8080
```

Без строки `ENV` переменная останется неподставленной, и nginx не запустится. Значение по умолчанию `http://api:8080` безопасно: благодаря `resolver` и переменной в `proxy_pass` nginx стартует и без контейнера `api`.

Адрес backend в `nginx.conf` задай через переменную окружения и используй `resolver 127.0.0.11`. Это позволит контейнеру frontend запускаться даже тогда, когда сервис `api` ещё не запущен.

Пример конфигурации:

```nginx
server {
  listen 80;
  server_name _;

  root /usr/share/nginx/html;
  index index.html;

  location /api/ {
    resolver 127.0.0.11 valid=10s ipv6=off;
    set $api_upstream ${API_PROXY_TARGET};
    proxy_pass $api_upstream;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
  }

  location / {
    try_files $uri $uri/ /index.html;
  }
}
```

`compose.yaml` в этой задаче не создавай: он лежит в корне репозитория и появится вместе с базой данных. Когда он появится, адрес backend для фронта передаётся так:

```yaml
environment:
  API_PROXY_TARGET: http://api:8080
```

Не используй в `nginx.conf` литерал `http://api:8080` как обязательное значение: без запущенного контейнера `api` nginx может не стартовать из-за ошибки `host not found in upstream`.

Не включай в frontend-образ секреты и `.env`. Адрес backend не должен попадать в собранный JS. В конфиге nginx он допустим, браузер его не видит.

## Тесты

Добавь Vitest и один реальный тест:

- пользователь открывает неизвестный адрес;
- приложение показывает страницу 404.

Не пиши тесты экранов, форм, авторизации, API-клиента или ролей: соответствующие функции в этой задаче отсутствуют.

## Git

Работай в ветке:

```text
chore/frontend-skeleton
```

Используй формат коммитов:

```text
chore(frontend): ...
```

Не пушь напрямую в `main`.

## Критерии готовности

Задача выполнена, когда:

- [ ] Создан каталог `frontend/` с Vite, React и TypeScript strict.
- [ ] Настроен алиас `@/` -> `src/`.
- [ ] Подключены React Router и TanStack Query.
- [ ] Созданы слои `app`, `pages`, `widgets`, `entities`, `shared`.
- [ ] Папка `features/` не создана.
- [ ] Есть стартовая страница `/` и страница 404.
- [ ] Есть один реальный Vitest-тест перехода на 404.
- [ ] Настроен dev-proxy `/api` через `API_PROXY_TARGET`.
- [ ] Переменная прокси не имеет префикса `VITE_`.
- [ ] `vite.config.ts` читает `API_PROXY_TARGET` через `loadEnv`.
- [ ] Создан `frontend/.nvmrc` с Node 24.
- [ ] Onest подключён локально через `@fontsource/onest`.
- [ ] CSS-переменные содержат исправленные правила контраста и границ.
- [ ] Созданы Dockerfile и nginx.conf.
- [ ] `nginx.conf` использует переменную backend и `resolver 127.0.0.11`.
- [ ] `nginx.conf` копируется в образ как шаблон `/etc/nginx/templates/default.conf.template`, в Dockerfile есть `ENV API_PROXY_TARGET`.
- [ ] `compose.yaml` и `docker-compose.yml` не созданы.
- [ ] `npm ci` проходит.
- [ ] `npm run lint` проходит.
- [ ] `npm test` проходит.
- [ ] `npm run build` проходит.
- [ ] `docker build` собирает образ.
- [ ] Контейнер frontend может запуститься без запущенного контейнера `api`.
- [ ] В `.github/` нет изменений.

## Что запрещено

- Реализовывать продуктовые экраны.
- Создавать UI-кит.
- Реализовывать вход, выход, сессию или роли.
- Создавать API-клиент.
- Добавлять `api:generate`.
- Создавать или использовать OpenAPI-типы.
- Добавлять моки API или подменять серверные ответы фикстурами.
- Создавать навигацию, layout, guards или меню.
- Менять `.github/`.
- Использовать внешние CDN для шрифтов или других ресурсов.
- Добавлять фиктивные данные, которые могут быть приняты за ответы реального API.
- Использовать префикс `VITE_` для переменной адреса backend-прокси.

## Итоговый отчёт

В конце верни:

1. Список созданных и изменённых файлов.
2. Краткое описание структуры frontend.
3. Результаты команд как есть:
   - `npm ci`;
   - `npm run lint`;
   - `npm test`;
   - `npm run build`;
   - `docker build`.
4. Результат запуска контейнера frontend без запущенного сервиса `api`.
5. Что не получилось или требует решения человека.
6. Готовый текст для блока «Что сделал ИИ» в описании PR.

Заголовок PR:

```text
chore(frontend): каркас приложения на Vite и React
```
