# Черновик API цифрового деканата

> Это черновик контракта для моков фронта и согласования backend.
> Когда появится `backend/openapi/openapi.json`, он станет основным источником контракта, а этот документ останется вспомогательным.

## Общие правила

- Базовый путь: `/api`.
- Формат запросов и ответов: `application/json`.
- Время в ответах: UTC в формате ISO 8601.
- Идентификаторы в путях: UUID.
- Аутентификация: cookie-сессия, которую устанавливает `POST /api/auth/login`.
- Доступ сотрудника: роль `Staff`; студент получает `403` при обращении к `/api/staff/*`.
- Ошибки: ProblemDetails с понятным текстом на русском.
- Факультет студента и сотрудника определяется сервером по данным сессии. Клиент не передаёт `facultyId` для доступа к чужим данным.
- Студент не получает персональные данные сотрудников: вместо ФИО сотрудника в объявлениях возвращается название подразделения.

### Пример ошибки

```http
HTTP/1.1 404 Not Found
Content-Type: application/problem+json

{
  "type": "https://dekanat.local/errors/request-not-found",
  "title": "Заявка не найдена",
  "status": 404,
  "detail": "Заявка не найдена или недоступна."
}
```

## Аутентификация

### `POST /api/auth/login`

Вход по логину и паролю. При успехе сервер устанавливает cookie-сессию.

**Запрос**

```http
POST /api/auth/login
Content-Type: application/json

{
  "login": "student@example.ru",
  "password": "Password123!"
}
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Set-Cookie: dekanat_session=...; HttpOnly; Secure; SameSite=Lax
Content-Type: application/json

{
  "role": "Student"
}
```

Поле `redirect` не возвращается: фронт сам определяет стартовый маршрут по роли пользователя.

**Ответ 401**

```http
HTTP/1.1 401 Unauthorized
Content-Type: application/problem+json

{
  "type": "https://dekanat.local/errors/invalid-credentials",
  "title": "Не удалось войти",
  "status": 401,
  "detail": "Неверный логин или пароль."
}
```

**Ответ 429**

Используется в двух случаях:

- пользователь превысил допустимое число неудачных попыток входа;
- с одного IP поступает слишком много попыток входа.

Ответ не должен раскрывать, существует ли указанный логин.

```http
HTTP/1.1 429 Too Many Requests
Retry-After: 300
Content-Type: application/problem+json

{
  "type": "https://dekanat.local/errors/too-many-login-attempts",
  "title": "Слишком много попыток входа",
  "status": 429,
  "detail": "Слишком много попыток входа. Повторите позже."
}
```

### `POST /api/auth/logout`

Выход из кабинета. Сервер удаляет cookie-сессию.

**Запрос**

```http
POST /api/auth/logout
```

**Ответ 204**

```http
HTTP/1.1 204 No Content
```

## Профиль

### `GET /api/me`

Возвращает профиль текущего пользователя. Для студента данные берутся из `StudentProfile`, для сотрудника из `StaffProfile`.

**Запрос**

```http
GET /api/me
```

**Ответ 200, студент**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "id": "3f6c2b7e-8d1a-4f6b-9c2e-1b7f8a2d4c10",
  "role": "Student",
  "fullName": "Иванова Настя",
  "faculty": "Факультет информатики",
  "program": "Программная инженерия",
  "course": 1,
  "group": "ПИ-21",
  "gradeBookNumber": "2026-0123"
}
```

**Ответ 200, сотрудник**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "id": "8a4b1c2d-9e3f-4a5b-8c6d-7e1f2a3b4c5d",
  "role": "Staff",
  "fullName": "Петрова Ольга",
  "faculty": "Факультет информатики"
}
```

## Главный экран

### `GET /api/today`

Возвращает данные главного экрана студента одним запросом.

**Запрос**

```http
GET /api/today
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "nextLesson": {
    "id": "1c2d3e4f-5a6b-4c7d-8e9f-0a1b2c3d4e5f",
    "discipline": "Математический анализ",
    "teacher": "Иванов И. И.",
    "group": "ПИ-21",
    "startsAt": "2026-11-07T09:20:00Z",
    "endsAt": "2026-11-07T10:50:00Z",
    "room": "305",
    "type": "Practice",
    "isCancelled": false,
    "change": {
      "type": "Reschedule",
      "oldStartsAt": "2026-11-07T07:40:00Z",
      "newStartsAt": "2026-11-07T09:20:00Z",
      "oldRoom": "214",
      "newRoom": "305",
      "reason": "Занятость аудитории",
      "changedAt": "2026-11-06T15:20:00Z"
    }
  },
  "nextDeadline": {
    "assessmentId": "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e",
    "discipline": "Физика",
    "title": "Подать заявление на пересдачу",
    "deadline": "2026-11-18"
  },
  "importantAnnouncements": [
    {
      "id": "a7b8c9d0-e1f2-4a3b-8c4d-5e6f7a8b9c0d",
      "title": "Перенос консультации по физике",
      "content": "Консультация перенесена на сегодня, 16:30, аудитория 305.",
      "publishedAt": "2026-11-07T07:00:00Z",
      "expiresAt": "2026-11-07T20:59:00Z",
      "isImportant": true,
      "department": "Деканат факультета информатики"
    }
  ],
  "admission": {
    "status": "AtRisk",
    "items": [
      {
        "assessmentId": "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e",
        "discipline": "Физика",
        "status": "Debt",
        "notAdmittedReason": "Не сдана лабораторная №3",
        "retakeDeadline": "2026-11-18",
        "canCreateRetakeRequest": true
      }
    ]
  },
  "activeRequests": [
    {
      "id": "c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f",
      "type": {
        "id": "d4e5f6a7-b8c9-4d0e-9f1a-2b3c4d5e6f7a",
        "name": "Справка в военкомат"
      },
      "status": "InProgress",
      "createdAt": "2026-11-05T07:12:00Z",
      "estimatedReadyDate": "2026-11-12"
    }
  ],
  "importantNotifications": [
    {
      "id": "f6a7b8c9-d0e1-4f2a-8b3c-4d5e6f7a8b9c",
      "type": "RequestStatus",
      "title": "Справка готова",
      "content": "Забрать в деканате до 17:00.",
      "isImportant": true,
      "isRead": false,
      "createdAt": "2026-11-07T08:00:00Z",
      "links": {
        "requestId": "c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f"
      }
    }
  ]
}
```

На главном экране показываются только важные объявления. Обычные объявления доступны в разделе объявлений.

## Расписание

### `GET /api/schedule?from=&to=`

Возвращает занятия группы текущего студента за период.

Параметр `groupId` не используется: студент получает расписание только своей группы.

**Запрос**

```http
GET /api/schedule?from=2026-11-07&to=2026-11-07
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "from": "2026-11-07",
  "to": "2026-11-07",
  "items": [
    {
      "id": "1c2d3e4f-5a6b-4c7d-8e9f-0a1b2c3d4e5f",
      "discipline": "Математический анализ",
      "teacher": "Иванов И. И.",
      "group": "ПИ-21",
      "startsAt": "2026-11-07T09:20:00Z",
      "endsAt": "2026-11-07T10:50:00Z",
      "room": "305",
      "type": "Practice",
      "isCancelled": false,
      "change": {
        "type": "Reschedule",
        "oldStartsAt": "2026-11-07T07:40:00Z",
        "newStartsAt": "2026-11-07T09:20:00Z",
        "oldRoom": "214",
        "newRoom": "305",
        "reason": "Занятость аудитории",
        "changedAt": "2026-11-06T15:20:00Z"
      }
    }
  ]
}
```

### `GET /api/schedule/{id}`

Возвращает детали занятия текущего студента и историю изменений.

**Запрос**

```http
GET /api/schedule/1c2d3e4f-5a6b-4c7d-8e9f-0a1b2c3d4e5f
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "id": "1c2d3e4f-5a6b-4c7d-8e9f-0a1b2c3d4e5f",
  "discipline": "Математический анализ",
  "teacher": "Иванов И. И.",
  "group": "ПИ-21",
  "date": "2026-11-07",
  "startsAt": "2026-11-07T09:20:00Z",
  "endsAt": "2026-11-07T10:50:00Z",
  "room": "305",
  "type": "Practice",
  "isCancelled": false,
  "history": [
    {
      "type": "Reschedule",
      "oldStartsAt": "2026-11-07T07:40:00Z",
      "newStartsAt": "2026-11-07T09:20:00Z",
      "oldRoom": "214",
      "newRoom": "305",
      "reason": "Занятость аудитории",
      "changedAt": "2026-11-06T15:20:00Z"
    }
  ]
}
```

## Оценки

### `GET /api/grades?semester=`

Возвращает оценки и зачёты студента. Без параметра возвращается текущий семестр.

**Запрос**

```http
GET /api/grades?semester=1
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "semester": 1,
  "averageGrade": 4.3,
  "session": {
    "startsAt": "2027-01-14",
    "endsAt": "2027-01-25"
  },
  "admission": {
    "status": "AtRisk",
    "items": [
      {
        "assessmentId": "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e",
        "discipline": "Физика",
        "status": "Debt",
        "notAdmittedReason": "Не сдана лабораторная №3",
        "retakeDeadline": "2026-11-18",
        "canCreateRetakeRequest": true
      }
    ]
  },
  "items": [
    {
      "id": "a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d",
      "discipline": "Математический анализ",
      "semester": 1,
      "controlForm": "Exam",
      "grade": null,
      "score": null,
      "date": "2027-01-20",
      "status": "Planned"
    },
    {
      "id": "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e",
      "discipline": "Физика",
      "semester": 1,
      "controlForm": "Exam",
      "grade": null,
      "score": null,
      "date": null,
      "status": "Debt"
    }
  ]
}
```

### `GET /api/grades/{id}`

Возвращает оценки по одной дисциплине и прошлые заявления на пересдачу.

Поле `attempts` содержит не результаты попыток сдачи, а историю заявлений студента на пересдачу.

**Запрос**

```http
GET /api/grades/b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "id": "b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e",
  "discipline": "Физика",
  "semester": 1,
  "controlForm": "Exam",
  "currentStatus": "Debt",
  "notAdmittedReason": "Не сдана лабораторная №3",
  "retakeDeadline": "2026-11-18",
  "canCreateRetakeRequest": true,
  "attempts": [
    {
      "requestId": "d5e6f7a8-b9c0-4d1e-8f2a-3b4c5d6e7f8a",
      "date": "2026-10-30",
      "status": "Rejected"
    }
  ]
}
```

## Заявки

### `GET /api/requests`

Возвращает заявки текущего студента. Поддерживает фильтр по статусу.

**Запрос**

```http
GET /api/requests?status=InProgress
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "items": [
    {
      "id": "c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f",
      "type": {
        "id": "d4e5f6a7-b8c9-4d0e-9f1a-2b3c4d5e6f7a",
        "name": "Справка в военкомат"
      },
      "status": "InProgress",
      "createdAt": "2026-11-05T07:12:00Z",
      "estimatedReadyDate": "2026-11-12",
      "issuePlace": null,
      "issueDeadline": null,
      "fields": {
        "comment": "Нужна для военкомата"
      }
    }
  ]
}
```

### `GET /api/requests/types`

Возвращает типы заявок для карточек быстрого оформления.

**Запрос**

```http
GET /api/requests/types
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "items": [
    {
      "id": "d4e5f6a7-b8c9-4d0e-9f1a-2b3c4d5e6f7a",
      "name": "Справка в военкомат",
      "description": "Справка для военкомата",
      "processingDays": 5,
      "fieldSchema": {
        "comment": {
          "type": "string",
          "required": false,
          "title": "Комментарий"
        }
      }
    },
    {
      "id": "e5f6a7b8-c9d0-4e1f-8a2b-3c4d5e6f7a8b",
      "name": "Заявление на пересдачу",
      "description": "Заявление на пересдачу дисциплины",
      "processingDays": 2,
      "fieldSchema": {
        "assessmentId": {
          "type": "uuid",
          "required": true,
          "title": "Дисциплина"
        },
        "comment": {
          "type": "string",
          "required": false,
          "title": "Комментарий"
        }
      }
    }
  ]
}
```

### `POST /api/requests`

Создаёт заявку текущего студента.

**Запрос**

```http
POST /api/requests
Content-Type: application/json

{
  "requestTypeId": "d4e5f6a7-b8c9-4d0e-9f1a-2b3c4d5e6f7a",
  "fields": {
    "comment": "Нужна для военкомата"
  }
}
```

**Ответ 201**

```http
HTTP/1.1 201 Created
Content-Type: application/json

{
  "id": "c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f",
  "type": {
    "id": "d4e5f6a7-b8c9-4d0e-9f1a-2b3c4d5e6f7a",
    "name": "Справка в военкомат"
  },
  "status": "Submitted",
  "createdAt": "2026-11-07T10:00:00Z",
  "estimatedReadyDate": "2026-11-14",
  "issuePlace": null,
  "issueDeadline": null,
  "fields": {
    "comment": "Нужна для военкомата"
  }
}
```

### `GET /api/requests/{id}`

Возвращает заявку текущего студента. Если заявка принадлежит другому студенту, сервер отвечает `404`.

**Запрос**

```http
GET /api/requests/c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "id": "c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f",
  "type": {
    "id": "d4e5f6a7-b8c9-4d0e-9f1a-2b3c4d5e6f7a",
    "name": "Справка в военкомат"
  },
  "status": "InProgress",
  "createdAt": "2026-11-05T07:12:00Z",
  "estimatedReadyDate": "2026-11-12",
  "issuePlace": null,
  "issueDeadline": null,
  "fields": {
    "comment": "Нужна для военкомата"
  },
  "history": [
    {
      "status": "Submitted",
      "comment": null,
      "changedAt": "2026-11-05T07:12:00Z"
    },
    {
      "status": "InProgress",
      "comment": "Передана в деканат",
      "changedAt": "2026-11-05T11:30:00Z"
    }
  ]
}
```

### `POST /api/requests/{id}/cancel`

Отменяет заявку текущего студента, пока это допустимо по её статусу.

**Запрос**

```http
POST /api/requests/c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f/cancel
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "id": "c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f",
  "status": "Cancelled",
  "createdAt": "2026-11-05T07:12:00Z",
  "estimatedReadyDate": "2026-11-12",
  "issuePlace": null,
  "issueDeadline": null,
  "fields": {
    "comment": "Нужна для военкомата"
  }
}
```

## Уведомления

### `GET /api/notifications`

Возвращает уведомления текущего пользователя. Параметр `important` фильтрует важные уведомления.

**Запрос**

```http
GET /api/notifications?important=true
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "items": [
    {
      "id": "f6a7b8c9-d0e1-4f2a-8b3c-4d5e6f7a8b9c",
      "type": "RequestStatus",
      "title": "Справка готова",
      "content": "Забрать в деканате до 17:00.",
      "isImportant": true,
      "isRead": false,
      "createdAt": "2026-11-07T08:00:00Z",
      "readAt": null,
      "links": {
        "requestId": "c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f"
      }
    }
  ]
}
```

### `GET /api/notifications/unread-count`

Возвращает число непрочитанных уведомлений текущего пользователя.

**Запрос**

```http
GET /api/notifications/unread-count
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "count": 3
}
```

### `POST /api/notifications/{id}/read`

Помечает одно уведомление прочитанным.

**Запрос**

```http
POST /api/notifications/f6a7b8c9-d0e1-4f2a-8b3c-4d5e6f7a8b9c/read
```

**Ответ 204**

```http
HTTP/1.1 204 No Content
```

### `POST /api/notifications/read-all`

Помечает все уведомления текущего пользователя прочитанными.

**Запрос**

```http
POST /api/notifications/read-all
```

**Ответ 204**

```http
HTTP/1.1 204 No Content
```

## Объявления

### `GET /api/announcements/{id}`

Возвращает объявление, доступное текущему студенту.

Студенту не передаются ФИО и идентификатор сотрудника. Вместо автора возвращается только название подразделения.

**Запрос**

```http
GET /api/announcements/a7b8c9d0-e1f2-4a3b-8c4d-5e6f7a8b9c0d
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "id": "a7b8c9d0-e1f2-4a3b-8c4d-5e6f7a8b9c0d",
  "title": "Перенос консультации по физике",
  "content": "Консультация перенесена на сегодня, 16:30, аудитория 305.",
  "publishedAt": "2026-11-07T07:00:00Z",
  "expiresAt": "2026-11-07T20:59:00Z",
  "isImportant": true,
  "department": "Деканат факультета информатики",
  "audience": {
    "type": "Group",
    "groupId": "6f1e2d3c-4b5a-4968-8776-655443332211",
    "groupName": "ПИ-21"
  }
}
```

## Кабинет сотрудника

### `GET /api/staff/requests`

Возвращает очередь заявок сотрудников.

Факультет определяется по `StaffProfile` текущего сотрудника из сессии. Параметр `facultyId` не принимается: сотрудник не может запросить заявки другого факультета.

Поддерживается фильтр по статусу.

**Запрос**

```http
GET /api/staff/requests?status=Submitted
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "items": [
    {
      "id": "c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f",
      "student": {
        "id": "3f6c2b7e-8d1a-4f6b-9c2e-1b7f8a2d4c10",
        "fullName": "Иванова Настя",
        "group": "ПИ-21"
      },
      "type": {
        "id": "d4e5f6a7-b8c9-4d0e-9f1a-2b3c4d5e6f7a",
        "name": "Справка в военкомат"
      },
      "status": "Submitted",
      "createdAt": "2026-11-05T07:12:00Z",
      "estimatedReadyDate": "2026-11-12",
      "fields": {
        "comment": "Нужна для военкомата"
      }
    }
  ]
}
```

### `GET /api/staff/requests/{id}`

Возвращает карточку заявки для сотрудника.

**Запрос**

```http
GET /api/staff/requests/c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "id": "c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f",
  "student": {
    "id": "3f6c2b7e-8d1a-4f6b-9c2e-1b7f8a2d4c10",
    "fullName": "Иванова Настя",
    "group": "ПИ-21"
  },
  "type": {
    "id": "d4e5f6a7-b8c9-4d0e-9f1a-2b3c4d5e6f7a",
    "name": "Справка в военкомат"
  },
  "status": "Submitted",
  "createdAt": "2026-11-05T07:12:00Z",
  "estimatedReadyDate": "2026-11-12",
  "fields": {
    "comment": "Нужна для военкомата"
  },
  "history": [
    {
      "status": "Submitted",
      "comment": null,
      "changedAt": "2026-11-05T07:12:00Z"
    }
  ],
  "availableTransitions": [
    {
      "status": "InProgress",
      "title": "Взять в работу"
    },
    {
      "status": "Rejected",
      "title": "Отклонить"
    }
  ]
}
```

### `POST /api/staff/requests/{id}/status`

Меняет статус заявки. Допустимый следующий статус возвращает поле `availableTransitions`.

При переводе в `Ready` сотрудник обязан указать место и срок выдачи.

**Запрос, перевод в работу**

```http
POST /api/staff/requests/c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f/status
Content-Type: application/json

{
  "status": "InProgress",
  "comment": "Передана в деканат"
}
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "id": "c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f",
  "status": "InProgress",
  "createdAt": "2026-11-05T07:12:00Z",
  "estimatedReadyDate": "2026-11-12",
  "issuePlace": null,
  "issueDeadline": null,
  "fields": {
    "comment": "Нужна для военкомата"
  },
  "history": [
    {
      "status": "Submitted",
      "comment": null,
      "changedAt": "2026-11-05T07:12:00Z"
    },
    {
      "status": "InProgress",
      "comment": "Передана в деканат",
      "changedAt": "2026-11-07T10:30:00Z"
    }
  ]
}
```

**Запрос, заявка готова**

```http
POST /api/staff/requests/c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f/status
Content-Type: application/json

{
  "status": "Ready",
  "comment": "Документ готов к выдаче",
  "issuePlace": "Деканат, кабинет 204",
  "issueDeadline": "2026-11-14T17:00:00Z"
}
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "id": "c3d4e5f6-a7b8-4c9d-8e0f-1a2b3c4d5e6f",
  "status": "Ready",
  "createdAt": "2026-11-05T07:12:00Z",
  "estimatedReadyDate": "2026-11-12",
  "issuePlace": "Деканат, кабинет 204",
  "issueDeadline": "2026-11-14T17:00:00Z",
  "fields": {
    "comment": "Нужна для военкомата"
  },
  "history": [
    {
      "status": "Submitted",
      "comment": null,
      "changedAt": "2026-11-05T07:12:00Z"
    },
    {
      "status": "InProgress",
      "comment": "Передана в деканат",
      "changedAt": "2026-11-05T11:30:00Z"
    },
    {
      "status": "Ready",
      "comment": "Документ готов к выдаче",
      "changedAt": "2026-11-07T10:30:00Z"
    }
  ]
}
```

Если `status` равен `Ready`, а `issuePlace` или `issueDeadline` не переданы, сервер возвращает `400`.

## Расписание сотрудника

### `GET /api/staff/schedule?groupId=&from=&to=`

Возвращает расписание выбранной группы для сотрудника.

**Запрос**

```http
GET /api/staff/schedule?groupId=6f1e2d3c-4b5a-4968-8776-655443332211&from=2026-11-07&to=2026-11-07
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "from": "2026-11-07",
  "to": "2026-11-07",
  "items": [
    {
      "id": "1c2d3e4f-5a6b-4c7d-8e9f-0a1b2c3d4e5f",
      "discipline": "Математический анализ",
      "teacher": "Иванов И. И.",
      "group": "ПИ-21",
      "startsAt": "2026-11-07T07:40:00Z",
      "endsAt": "2026-11-07T09:10:00Z",
      "room": "214",
      "type": "Practice",
      "isCancelled": false,
      "canTransfer": true,
      "canCancel": true
    }
  ]
}
```

### `POST /api/staff/lessons/{id}/transfer`

Переносит пару. Если новая аудитория занята, сервер возвращает `409`.

**Запрос**

```http
POST /api/staff/lessons/1c2d3e4f-5a6b-4c7d-8e9f-0a1b2c3d4e5f/transfer
Content-Type: application/json

{
  "startsAt": "2026-11-07T09:20:00Z",
  "endsAt": "2026-11-07T10:50:00Z",
  "room": "305",
  "reason": "Занятость аудитории"
}
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "id": "1c2d3e4f-5a6b-4c7d-8e9f-0a1b2c3d4e5f",
  "startsAt": "2026-11-07T09:20:00Z",
  "endsAt": "2026-11-07T10:50:00Z",
  "room": "305",
  "type": "Practice",
  "isCancelled": false
}
```

**Ответ 409**

```http
HTTP/1.1 409 Conflict
Content-Type: application/problem+json

{
  "type": "https://dekanat.local/errors/room-not-available",
  "title": "Аудитория занята",
  "status": 409,
  "detail": "Аудитория 305 занята в выбранное время."
}
```

### `POST /api/staff/lessons/{id}/cancel`

Отменяет пару.

**Запрос**

```http
POST /api/staff/lessons/1c2d3e4f-5a6b-4c7d-8e9f-0a1b2c3d4e5f/cancel
Content-Type: application/json

{
  "reason": "Отмена по уважительной причине"
}
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "id": "1c2d3e4f-5a6b-4c7d-8e9f-0a1b2c3d4e5f",
  "startsAt": "2026-11-07T07:40:00Z",
  "endsAt": "2026-11-07T09:10:00Z",
  "room": "214",
  "type": "Practice",
  "isCancelled": true
}
```

## Объявления сотрудника

### `GET /api/staff/announcements`

Возвращает объявления, доступные сотруднику.

**Запрос**

```http
GET /api/staff/announcements
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "items": [
    {
      "id": "a7b8c9d0-e1f2-4a3b-8c4d-5e6f7a8b9c0d",
      "title": "Перенос консультации по физике",
      "content": "Консультация перенесена на сегодня, 16:30, аудитория 305.",
      "publishedAt": "2026-11-07T07:00:00Z",
      "expiresAt": "2026-11-07T20:59:00Z",
      "isImportant": true,
      "author": {
        "id": "8a4b1c2d-9e3f-4a5b-8c6d-7e1f2a3b4c5d",
        "fullName": "Петрова Ольга"
      },
      "audience": {
        "type": "Group",
        "groupId": "6f1e2d3c-4b5a-4968-8776-655443332211",
        "groupName": "ПИ-21"
      }
    }
  ]
}
```

### `GET /api/staff/announcements/{id}`

Возвращает детали объявления для сотрудника.

**Запрос**

```http
GET /api/staff/announcements/a7b8c9d0-e1f2-4a3b-8c4d-5e6f7a8b9c0d
```

**Ответ 200**

```http
HTTP/1.1 200 OK
Content-Type: application/json

{
  "id": "a7b8c9d0-e1f2-4a3b-8c4d-5e6f7a8b9c0d",
  "title": "Перенос консультации по физике",
  "content": "Консультация перенесена на сегодня, 16:30, аудитория 305.",
  "publishedAt": "2026-11-07T07:00:00Z",
  "expiresAt": "2026-11-07T20:59:00Z",
  "isImportant": true,
  "author": {
    "id": "8a4b1c2d-9e3f-4a5b-8c6d-7e1f2a3b4c5d",
    "fullName": "Петрова Ольга"
  },
  "audience": {
    "type": "Group",
    "groupId": "6f1e2d3c-4b5a-4968-8776-655443332211",
    "groupName": "ПИ-21"
  }
}
```

### `POST /api/staff/announcements`

Создаёт объявление и рассылает уведомления выбранной аудитории.

**Запрос**

```http
POST /api/staff/announcements
Content-Type: application/json

{
  "title": "Перенос консультации по физике",
  "content": "Консультация перенесена на сегодня, 16:30, аудитория 305.",
  "isImportant": true,
  "expiresAt": "2026-11-07T20:59:00Z",
  "audience": {
    "type": "Group",
    "groupId": "6f1e2d3c-4b5a-4968-8776-655443332211"
  }
}
```

**Ответ 201**

```http
HTTP/1.1 201 Created
Content-Type: application/json

{
  "id": "a7b8c9d0-e1f2-4a3b-8c4d-5e6f7a8b9c0d",
  "title": "Перенос консультации по физике",
  "content": "Консультация перенесена на сегодня, 16:30, аудитория 305.",
  "publishedAt": "2026-11-07T10:00:00Z",
  "expiresAt": "2026-11-07T20:59:00Z",
  "isImportant": true,
  "audience": {
    "type": "Group",
    "groupId": "6f1e2d3c-4b5a-4968-8776-655443332211",
    "groupName": "ПИ-21"
  }
}
```

## Статусы заявок

| Статус | Название в интерфейсе |
|---|---|
| `Submitted` | Подана |
| `InProgress` | В работе |
| `Ready` | Готова |
| `Issued` | Выдана |
| `Rejected` | Отклонена |
| `Cancelled` | Отменена |

## Коды ответов

| Код | Когда возвращается |
|---|---|
| 200 | Запрос выполнен |
| 201 | Созданы заявка или объявление |
| 204 | Выход, отметка уведомления прочитанным или отметка всех уведомлений прочитанными |
| 400 | Некорректные данные запроса, включая отсутствие `issuePlace` или `issueDeadline` при статусе `Ready` |
| 401 | Пользователь не вошёл |
| 403 | Студент обращается к сотрудническому эндпоинту или недостаточно полномочий |
| 404 | Ресурс не найден или недоступен текущему пользователю |
| 409 | Аудитория занята в выбранное время |
| 429 | Слишком много попыток входа: блокировка аккаунта или ограничение по IP |

## Моки для фронта

Для старта фронта достаточно моков:

- `GET /api/me`
- `GET /api/today`
- `GET /api/schedule`
- `GET /api/schedule/{id}`
- `GET /api/grades`
- `GET /api/grades/{id}`
- `GET /api/requests`, `POST /api/requests`, `GET /api/requests/{id}`, `POST /api/requests/{id}/cancel`
- `GET /api/notifications`, `GET /api/notifications/unread-count`, `POST /api/notifications/read-all`
- `GET /api/announcements/{id}`
- `GET /api/staff/requests`, `GET /api/staff/requests/{id}`, `POST /api/staff/requests/{id}/status`
- `GET /api/staff/schedule`, `POST /api/staff/lessons/{id}/transfer`, `POST /api/staff/lessons/{id}/cancel`
- `GET /api/staff/announcements`, `GET /api/staff/announcements/{id}`, `POST /api/staff/announcements`

Моки должны использовать те же JSON-структуры, что и примеры выше. Тогда при подключении настоящего backend фронт меняет только базовый URL и убирает моки, не переписывая экраны.
