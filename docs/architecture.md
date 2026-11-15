# Карта архітектури SecureLab

## 1. Компоненти системи

| Компонент | Розташування | Відповідальність |
|---|---|---|
| Browser client | `src/SecureLab.Api/Client/` | Надсилає HTTP-запити, отримує JSON і безпечно відображає дані через DOM API |
| Presentation | `src/SecureLab.Api/Presentation/` | Обробляє HTTP-запити, перевіряє зовнішні параметри та формує відповіді |
| Application | `src/SecureLab.Api/Application/` | Виконує запити до інцидентів і формує статистику |
| Data | `src/SecureLab.Api/Data/` | Містить сутності, DbContext, міграції та початкові навчальні дані |
| PostgreSQL | `infra/compose.yaml` | Зберігає навчальні дані в локальній базі |
| Tests | `tests/SecureLab.Api.Tests/` | Перевіряє HTTP endpoints та безпечну поведінку клієнта |

## 2. Наскрізний маршрут запиту

Для отримання статистики за рівнями критичності використовується такий маршрут:

```text
Browser client
  → GET /api/incidents/severity-summary
  → Presentation/Endpoints/IncidentEndpoints.cs
  → перевірка параметра status
  → Application/Incidents/IncidentQueries.cs
  → Entity Framework Core
  → PostgreSQL
  → групування за Severity та підрахунок Count
  → IncidentSeveritySummaryResponse
  → JSON response
  → Client/app.js
  → document.createElement() та textContent
  → відображення статистики у браузері
```

## 3. Реалізований endpoint

**Маршрут:** `GET /api/incidents/severity-summary`

Endpoint повертає кількість інцидентів для кожного рівня критичності.

Підтримується необов'язковий query-параметр `status`.

Дозволені значення:

- `New`
- `Triaged`
- `InProgress`
- `Resolved`
- `Closed`

Якщо передано недопустиме значення, API повертає HTTP 400 із повідомленням про помилку валідації.

Приклади запитів:

```http
GET /api/incidents/severity-summary
GET /api/incidents/severity-summary?status=Triaged
GET /api/incidents/severity-summary?status=Resolved
GET /api/incidents/severity-summary?status=Unknown
```

## 4. Формат відповіді

Для відповіді створено окремий DTO:

`src/SecureLab.Api/Presentation/Contracts/IncidentResponses.cs`

`IncidentSeveritySummaryResponse` містить два поля:

- `Severity` — рівень критичності;
- `Count` — кількість інцидентів.

Приклад відповіді:

```json
[
  {
    "severity": "High",
    "count": 1
  },
  {
    "severity": "Low",
    "count": 1
  },
  {
    "severity": "Medium",
    "count": 1
  }
]
```

## 5. Обробка даних у Application

Основна логіка розташована у файлі:

`src/SecureLab.Api/Application/Incidents/IncidentQueries.cs`

Для отримання статистики використовується Entity Framework Core.

Запит виконує такі операції:

1. Отримує інциденти через `AsNoTracking()`.
2. За наявності параметра `status` застосовує фільтрацію.
3. Групує записи за рівнем критичності через `GroupBy()`.
4. Підраховує кількість записів через `Count()`.
5. Сортує результат за рівнем критичності.
6. Асинхронно отримує результат через `ToListAsync()`.

У відповідь включаються тільки ті рівні критичності, для яких існують записи. Групи з нульовою кількістю не створюються.

Для відстеження виконання запитів використовується структуроване логування.

## 6. Клієнтський інтерфейс

Клієнтська частина розташована в `src/SecureLab.Api/Client/`.

Основні файли:

- `index.html` — структура сторінки та блок статистики;
- `styles.css` — оформлення інтерфейсу;
- `app.js` — HTTP-запити, обробка відповідей та відображення даних.

Статистика синхронізована з фільтром статусу інцидентів.

У клієнті передбачено такі стани:

- завантаження статистики;
- успішне відображення результатів;
- відсутність результатів;
- помилка завантаження.

Для безпечного відображення інформації використовується `textContent` та створення DOM-елементів через `document.createElement()`. Небезпечний `innerHTML` не використовується.

## 7. Межі довіри

| Межа | Потенційний ризик | Механізм захисту |
|---|---|---|
| Користувач → Browser client | Користувач може вводити довільні значення | Обмеження доступних значень у формі |
| Browser client → API | HTTP-запит можна змінити поза інтерфейсом | Серверна перевірка параметра `status` |
| API → PostgreSQL | Некоректні або небезпечні параметри запиту | Параметризовані запити EF Core |
| PostgreSQL → API | У базі можуть міститися недовірені текстові дані | Використання DTO та обмеження полів відповіді |
| API → Browser DOM | Отриманий текст може містити небезпечний HTML | `textContent` замість `innerHTML` |

## 8. Конфігураційні входи

- `global.json` — визначає версію .NET SDK.
- `src/SecureLab.Api/appsettings*.json` — містить налаштування застосунку та локальне підключення до БД.
- `infra/compose.yaml` — визначає конфігурацію PostgreSQL, порт і навчальні облікові дані.
- `ConnectionStrings__SecureLab` — змінна середовища для перевизначення рядка підключення без збереження реальних облікових даних у Git.

## 9. Запуск і відновлення бази даних

Запуск PostgreSQL:

```powershell
docker compose --env-file infra/.env.example -f infra/compose.yaml up -d --wait
```

Запуск API:

```powershell
dotnet run --project src/SecureLab.Api
```

Під час першого запуску в середовищі Development застосовуються готові EF Core migrations та додаються навчальні дані.

Для відновлення початкового стану бази даних використовується:

```powershell
dotnet run --project src/SecureLab.Api -- --reset-database
```

Команда очищає відомі навчальні таблиці локальної бази даних і повторно створює початкові дані. Вона доступна тільки в середовищі Development.

Зупинка PostgreSQL:

```powershell
docker compose --env-file infra/.env.example -f infra/compose.yaml down
```

## 10. Автоматизоване тестування

Інтеграційні тести розташовані в `tests/SecureLab.Api.Tests/IncidentEndpointTests.cs`.

Перевіряються такі сценарії нового endpoint:

1. Запит без фільтра — повертаються всі наявні групи критичності.
2. Запит зі статусом `Triaged` — повертається відповідна група.
3. Запит зі статусом `Resolved` — повертається порожній масив.
4. Запит зі статусом `Unknown` — повертається HTTP 400.

Також перевіряються отримання списку інцидентів, обробка невідомого ID, захист внутрішніх полів та відсутність небезпечного `innerHTML`.

Результат тестування після реалізації: 8 тестів успішно пройшли, помилок немає.