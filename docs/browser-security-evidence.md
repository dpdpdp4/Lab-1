# Докази безпеки браузера (DEL-01)

Рівень: добрий («Добре»). Рівні накопичувальні; повні критерії й маршрут — на початку практичної частини методички. Заповнюйте лише розділи свого рівня й усі попередні. Не записуйте паролі, cookie/token values чи export browser storage.

## Стенд і Git-стан (Для всіх)

ОС, browser/version, API/probe origins: … Release/base\_commit/entry\_parent з receipt: … (entry\_parent — commit, від якого встановлено scaffold; збігається з base\_commit, якщо після v0.4.0 не було commit політики SDK). Vulnerable/fixed hashes, v0.5.0, main і спосіб передавання: …

Механізм сеансу (Application → Storage → Cookies → http://localhost:5080): назва cookie, Domain, Path, HttpOnly, SameSite, Secure — без Value: …

## Гіпотеза й REG-XSS-01 (Для всіх)

Карта Description: Source — поле опису інциденту → Request — POST /api/incidents → Validation — перевірка введених даних → Storage — база даних, поле Incident.Description → Response — GET /api/incidents/{id} → Sink/context — innerHTML до виправлення; textContent після виправлення.



Гіпотеза source → storage → response → sink/context: якщо користувач введе HTML-код у поле опису, він може зберегтися в БД, повернутися через API та бути інтерпретований браузером через небезпечне використання innerHTML. Це може призвести до DOM XSS.



Передумови: локальний користувач Alice; Guid створеного тестового запису; marker із resources/lab05-marker.txt; API http://localhost:5080; команди reset/start і create/read згідно з інструкцією лабораторної.

|Спостереження|До fix|Після fix|Посилання на доказ|
|-|-|-|-|
|Значення в БД/API|Маркер передано через POST, інцидент завантажено через GET 200. Збереження в БД окремо ще не перевірено|HTML-маркер повертається через API, але відображається як текст|Результат GET у browser-lab|
|DOM основного Description|Використовується innerHTML для вставлення опису|Використовується textContent замість innerHTML|git diff browser-lab.js|
|Дія marker|Marker виконався, результат executed|Marker не виконався; Console повернула null|Результат Console до та після fix|
|Апостроф/лапки, 5 < 7, Unicode, HTML-like рядок||Усі значення відобразилися коректно як звичайний текст; HTML-like рядок не інтерпретувався браузером|Скріншот XSS positive test 20261121|
|Довжина за власними правилами ЛР 2||Опис довжиною 4001 символ відхилено сервером: POST 400|Результат браузера POST: 400|
|Вікно Morgan: GET і перевірка marker; контроль Bob|||Результати GET та Console|

Finding: активний до виправлення. Передумова — авторизований користувач може створити інцидент із HTML-подібним описом. Першопричина — використання innerHTML для відображення недовірених даних. Виправлення — заміна innerHTML на textContent для Description і IncidentComment.Text. Retest — повторне виконання REG-XSS-01 з тим самим marker. Після виправлення marker відображається як звичайний текст і не виконується. Залишковий ризик — можливі інші небезпечні місця відображення недовірених даних, які потрібно перевірити окремо.



Повторювана REG-XSS-01, негативний контроль і очікування: до виправлення marker встановив data-lab05-marker="executed"; після виправлення значення атрибута залишилося null. Це підтверджує, що перевірений DOM XSS усунуто. Додатково необхідно перевірити звичайні текстові значення, лапки, Unicode, HTML-like рядки та граничну довжину опису.



## CORS і REG-CORS-01 (Для всіх)

Прогноз (Етап 3, крок 1), заповніть до зміни policy:

|Сценарій|Чи доходить request до API?|Чи може JavaScript прочитати response?|Чому|
|-|-|-|-|
|same-origin 5080 → API|Так|Так, за наявності авторизації|Same-origin запити не обмежуються CORS|
|simple GET 4173 → API|Так|Ні,до налаштування policy|Простий GET надсилається, але браузер блокує читання відповіді без ACAO|
|simple GET 4174 → API|Так|Ні|Відсутній дозвільний CORS-заголовок|
|curl/.http → API|Так|Не застосовується|CORS є обмеженням браузера, а не HTTP-клієнта|
|preflighted 4173 → API («Добре / Відмінно»)|Так, надсилається OPTIONS|Ні, до налаштування policy|Preflight не отримує необхідних CORS-заголовків|

Команди запуску probes: … Policy — лише Development, один exact origin 4173; 4174 — контроль. Для «Задовільно» без AllowCredentials().



|Клієнт / простий запит|До policy|Fixed state|Status, ACAO, Console / доказ|
|-|-|-|-|
|Same-origin 5080|Доступ дозволений без CORS-перевірки|JavaScript читає відповідь API|Same-origin, CORS не обмежує запит|
|Allowed 4173|JavaScript не міг прочитати відповідь|JavaScript читає відповідь API|200; дозволене джерело http://localhost:4173; помилок CORS немає|
|Disallowed 4174|JavaScript не міг прочитати відповідь|JavaScript не може прочитати відповідь API|ERR\_FAILED 200 (OK); ACAO відсутній для цього Origin; Console підтверджує блокування CORS|
|Direct HTTP / curl|401 для обох Origin|401 для обох Origin|Для 4173 є Access-Control-Allow-Origin і Access-Control-Allow-Credentials: true; для 4174 обидва заголовки відсутні|



|Preflight 4173|До policy не перевірено|204 No Content|Є Access-Control-Allow-Origin: http://localhost:4173, Allow-Methods: POST, Allow-Headers: content-type, Allow-Credentials: true|



|Preflight 4174|До policy не перевірено|204 No Content|Усі CORS-дозвільні заголовки відсутні|

REG-CORS-01: прямі HTTP-перевірки через curl.exe з Origin 4173 та 4174 повернули 401 Unauthorized, оскільки запити не містили авторизаційної cookie. Для 4173 сервер повернув Access-Control-Allow-Origin і Access-Control-Allow-Credentials: true. Для 4174 ці заголовки відсутні.



Preflight-перевірка: OPTIONS із Origin 4173 повернув 204 No Content із дозволеними методом POST і заголовком content-type. Для Origin 4174 відповідь також була 204 No Content, але без CORS-дозвільних заголовків.



Перевірка порядку через читання 401 без cross-origin cookie: ще не виконано.



Пояснення різниці browser/direct HTTP: браузер застосовує CORS і перевіряє, чи дозволив сервер конкретному Origin читати відповідь. Звичайні HTTP-клієнти, наприклад curl, не застосовують браузерні обмеження CORS і можуть отримати відповідь навіть тоді, коли браузер блокує її читання.



**Додатково для Добре та Відмінно**



Другий flow IncidentComment.Text: … Public seed comment Guid/текст, похідні counts за потреби, DOM до/після: … Єдиний safe helper і виклики для обох полів: …

Preflight з 4173 і 4174, OPTIONS → GET / blocked GET, exact origin/header/method: … Cookie-дослід 4173/4174/без AllowCredentials()/401 без входу: … Кінцевий AllowCredentials() — лише Development policy для 4173. Review XSS/CORS/CSRF/authorization — посилання на DEL-03: …

|Директива CSP|Фактичні ресурси застосунку|Обране джерело / причина|
|-|-|-|
|default-src|	Резервне правило для ресурсів|'self' — забороняє ресурси з інших джерел за замовчуванням|
|script-src|app.js, auth-lab.js, browser-lab.js	|'self' — дозволені лише локальні скрипти|
|style-src|styles.css|'self' — дозволені стилі з власного джерела|
|connect-src|API-запити через fetch()|'self' — запити лише до власного джерела|
|img-src|Локальні зображення, якщо використовуються|'self' — зовнішні зображення не дозволені|
|frame-ancestors|Сторінки застосунку|'none' — забороняє вбудовування сторінок у фрейми|

Місце зміни middleware: src/SecureLab.Api/Program.cs, middleware встановлення HTTP-заголовків.



Enforcement: сервер повертає HTTP 200 для browser-lab.html та фактичний Content-Security-Policy із директивами default-src, script-src, style-src, connect-src, img-src і frame-ancestors. X-Content-Type-Options та Referrer-Policy також присутні у відповіді.



Кероване порушення: створено new Image() із джерелом http://localhost:4173/csp-probe.png. Браузер заблокував завантаження відповідно до img-src 'self', що підтверджено повідомленням у Console.



|Header / control|Фактичне значення|Ризик і межа захисту / policy rationale|
|-|-|-|
|X-Content-Type-Options|nosniff|Запобігає MIME-sniffing браузером, але не замінює перевірку типів файлів|
|Referrer-Policy|no-referrer|Не передає адресу сторінки через Referer|
|frame-ancestors|'none'|Захищає від вбудовування сторінки сторонніми сайтами та зменшує ризик clickjacking|

Перевірка IncidentComment.Text: завантажено власний інцидент із ID 20000000-0000-0000-0000-000000000001. Отримано GET: 200. Публічний коментар «Заголовки листа передано аналітику.» успішно відображено через setSafeText() із використанням textContent.



У режимі інкогніто без авторизації виконано GET із дозволеного Origin 4173 до /api/incidents. API повернув 401 Unauthorized, а JavaScript зміг прочитати тіло відповіді. Це підтверджує, що CORS дозволяє читання відповіді з дозволеного Origin, але не замінює серверну авторизацію.



Перевірка розмежування коментарів для Reporter (alice): для інциденту Alice відображається публічний коментар «Заголовки листа передано аналітику.». Внутрішній коментар «Внутрішня примітка: перевірити джерело вкладення.» не відображається. У DbSeeder.cs підтверджено IsInternal = false для публічного та IsInternal = true для внутрішнього коментаря.



Cookie-дослід:

1\. Авторизований запит із Origin 4173: Cookie присутня в Request Headers, API повертає 200, JavaScript читає відповідь.

2\. Запит із Origin 4174: Cookie присутня в Request Headers, але JavaScript не може прочитати відповідь через відсутність дозволу для цього Origin.

3\. Без авторизації в режимі інкогніто з Origin 4173: API повертає 401 Unauthorized, JavaScript читає відповідь.

4\. Тимчасово без AllowCredentials(): Access-Control-Allow-Credentials відсутній, JavaScript не може прочитати відповідь із credentials: include. Перевірка виконувалася в режимі інкогніто без авторизації, тому наявність cookie для цього тесту не підтверджувалася.

5\. Після експерименту AllowCredentials() відновлено. Кінцева політика дозволяє credentials лише для http://localhost:4173 і працює тільки в Development.



## Додатково для Відмінно



DOM-тест: файл/команда, browser, vulnerable commit і FAIL, fixed commit і PASS: … Comments, повторний render без дублювання та позитивні рядки: … CSP report-only → enforcement: фактичні headers, flow і кероване порушення в обох режимах: … safe-url.js / атрибут: … Пояснення text/attribute/URL/JavaScript та ризик third-party scripts — посилання на DEL-03: …

Додаткових DEL, навчальних демо або нового журналу не подавайте. Перед поданням повторіть чекліст для свого накопичувального рівня.

