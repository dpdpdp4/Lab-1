# Власна карта source-to-sink

Поле: Incident.Description. Стан: гілка `lab/5-browser-xss`. Базовий стан: `v0.4.0`. Вхід: HTTP POST `/api/incidents`, а також PUT `/api/incidents/{id}`. Контрольний запис: власний інцидент Alice.

## Потік 1. Incident.Description

| Крок | Файл та функція/DTO | Конкретне поле/значення | Доказ і довіра |
|---|---|---|---|
| Source | `Client/browser-lab.js`, обробник submit форми `#edit` | `event.currentTarget.elements.description.value` | Значення вводить користувач у textarea. Недовірені дані. |
| Request | `Scaffolding/Lab02Endpoints.cs`, `CreateIncidentRequest` | `request.Description`, POST `/api/incidents` | Значення передається в JSON тілі запиту. DTO містить `string? Description`. |
| Validation/mapping | `Scaffolding/Lab02Endpoints.cs`, обробник POST | Перевірка `IsNullOrWhiteSpace`, довжини до 4000 символів, `Trim()` | Перевіряється наявність і довжина, але це не робить текст довіреним HTML. Для High/Critical також перевіряється мінімальна довжина 40 символів. |
| Storage | `Data/Entities/Incident.cs`, `Data/SecureLabDbContext.cs`, `Scaffolding/Lab02Endpoints.cs` | `Incident.Description`; `description`, максимум 4000 символів | Значення записується в БД через `db.Incidents.Add(incident)` та `SaveChangesAsync(ct)`. |
| Update | `Presentation/Endpoints/IncidentEndpoints.cs`, `UpdateAsync` | `request.Description` → `incident.Description` | Reporter може редагувати лише власний інцидент. Перевіряється довжина до 4000 символів, застосовується `Trim()`. |
| Response | `Application/Incidents/IncidentQueries.cs`, `GetDetailsAsync` | `incident.Description` → `IncidentDetailsResponse.Description` | Формується DTO відповіді GET `/api/incidents/{id}`. Для не-Analyst обмежується доступ власними інцидентами. |
| Client transform | `Client/browser-lab.js`, `load()` | `current.description` → `setSafeText(...)` | Значення отримується через `response.json()`. Helper використовує `element.textContent`. |
| Client transform | `Client/app.js`, `createTextElement()` та `renderIncidentDetails()` | `incident.description` → `createTextElement("p", incident.description)` | Функція створює DOM-елемент і присвоює текст через `textContent`. |
| Sink/context | `browser-lab.html`, `#description`; основний інтерфейс у `app.js` | Відображення опису як текстового вмісту елемента | Використовується текстовий DOM-контекст, а не `innerHTML`. HTML-подібний ввід не повинен виконуватися. |

## Потік 2. IncidentComment.Text

| Крок | Файл та функція/DTO | Конкретне поле/значення | Доказ і довіра |
|---|---|---|---|
| Source | `Data/DbSeeder.cs`, `SeedAsync` | `IncidentComment.Text` | Тестові коментарі додаються через seed. Це синтетичні дані, а не введення через HTTP endpoint створення коментаря. |
| Request | Відсутній | Endpoint створення коментаря не реалізований | Не слід вказувати вигаданий POST-маршрут. Для перевірки використовується seed. |
| Validation/mapping | `Data/DbSeeder.cs`, initializer `IncidentComment` | `Text`, `IsInternal`, `Incident`, `Author` | Значення задаються безпосередньо в initializer. Окремої HTTP-валідації створення коментаря немає. |
| Storage | `Data/DbSeeder.cs`, `IncidentComments.AddRange(...)` | Три коментарі з ID `...0001`, `...0002`, `...0003` | Перші два мають `IsInternal = false`, третій — `IsInternal = true`. Збереження виконується через `SaveChangesAsync()`. |
| Response | `Application/Incidents/IncidentQueries.cs`, `GetDetailsAsync` | `comment.Text` → `IncidentCommentResponse.Text` | Коментарі сортуються за `CreatedAtUtc`. Для Reporter внутрішні коментарі відсіюються умовою `isAnalyst || !comment.IsInternal`. |
| Additional response | `Presentation/Endpoints/IncidentCommentEndpoints.cs`, `GetCommentAsync` | `comment.Text` → `IncidentCommentResponse.Text` | Окремий GET `/api/incident-comments/{id}`. Reporter отримує лише публічний коментар до власного інциденту; інакше повертається 404. |
| Client transform | `Client/browser-lab.js`, `load()` | `comment.text` → `setSafeText(paragraph, comment.text)` | Helper використовує `textContent`. |
| Client transform | `Client/app.js`, `renderIncidentDetails()` | `comment.text` → `document.createTextNode(comment.text)` | Текст коментаря додається як текстовий вузол, а не HTML. |
| Sink/context | `#comments` у `browser-lab.html`; список коментарів у `app.js` | Відображення тексту коментаря | Використовується безпечний текстовий DOM-контекст. Внутрішні коментарі додатково обмежені серверною авторизацією. |

## Інвентаризація source-to-sink

| Файл/функція | Категорія даних | Контекст | Обґрунтоване рішення |
|---|---|---|---|
| `browser-lab.js`, submit | `Incident.Description` | Введення користувача | Передавати як JSON-значення, не формувати HTML-рядок. |
| `Lab02Endpoints.cs`, POST | `Incident.Description` | HTTP request / validation | Перевіряти обов'язковість і довжину; не вважати валідацію захистом від XSS. |
| `IncidentEndpoints.cs`, `UpdateAsync` | `Incident.Description` | Оновлення власного ресурсу | Перевіряти роль Reporter і власника інциденту. |
| `IncidentQueries.cs`, `GetDetailsAsync` | `Incident.Description`, `IncidentComment.Text` | API response | Повертати DTO; обмежувати доступ до чужих даних і внутрішніх коментарів. |
| `IncidentCommentEndpoints.cs`, `GetCommentAsync` | `IncidentComment.Text` | Окреме читання дочірнього ресурсу | Перевіряти роль, власника інциденту та `IsInternal`. |
| `browser-lab.js`, `setSafeText()` | Опис і коментарі | DOM text context | Використовувати `textContent`, не `innerHTML`. |
| `app.js`, `createTextElement()` | Опис, назва, метадані | DOM text context | Присвоювати текст через `textContent`. |
| `app.js`, `renderIncidentDetails()` | `IncidentComment.Text` | DOM text context | Створювати текстовий вузол через `createTextNode()`. |

## Фактичні перевірки

- Перевірено відображення HTML-подібного тексту в описі: текст не інтерпретується як HTML.
- Перевірено XSS marker: після виправлення маркер відображається як звичайний текст, а `data-lab05-marker` не встановлюється.
- Перевірено позитивний текстовий ввід з апострофами, лапками, символами `<`, `>`, українськими літерами та Unicode.
- Перевірено обмеження довжини опису: надто довгий опис повертає HTTP 400.
- Перевірено Reporter Alice: публічний коментар відображається, внутрішній коментар не відображається.
- Докази ручних перевірок наведено в `docs/browser-security-evidence.md`.

## Межі перевірки

Наявність безпечного текстового виведення в перевірених клієнтських функціях не означає автоматичної безпеки всіх можливих DOM-контекстів. Для URL, HTML-атрибутів та інших контекстів потрібні окремі правила обробки. Створення коментарів через HTTP у цьому API не реалізоване, тому потік коментарів починається з тестового seed.
