# EventExamProject
Сервис для управления мероприятиями на ASP.NET Core Web API

## Требования

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL (локально или в Docker) — приложение хранит данные в БД, а не в памяти
- Docker — нужен для интеграционных тестов (`EventExamProject.IntegrationTests`), которые сами поднимают контейнер с PostgreSQL через Testcontainers

## Архитектура

Решение разбито на четыре отдельные сборки (`.csproj`) по принципам чистой архитектуры. Зависимости между ними направлены только «внутрь» и проверяются компилятором через `<ProjectReference>`:

```
EventExamProject.Presentation
        │
        ├──▶ EventExamProject.Application
        │            │
        │            └──▶ EventExamProject.Domain
        │
        └──▶ EventExamProject.Infrastructure
                     │
                     ├──▶ EventExamProject.Application
                     └──▶ EventExamProject.Domain
```

- **`EventExamProject.Domain`** — доменные сущности (`Event`, `Booking`, `User`), перечисления `BookingStatus` и `UserRole`, value object `EventDetails`, доменные исключения (`NotFoundException`, `NoAvailableSeatsException`, `DomainValidationException`, `EventAlreadyStartedException`, `BookingLimitExceededException`, `ForbiddenException`). Не зависит ни от одного фреймворка (ASP.NET Core, EF Core) и ни от одного другого проекта решения.
- **`EventExamProject.Application`** — бизнес-логика (use cases): `EventService`, `BookingService`, `UserService`, фоновый `BookingProcessingService`; интерфейсы портов `IEventRepository`/`IBookingRepository`/`IUserRepository` (абстракции доступа к данным — что нужно бизнес-логике от хранилища, а не как оно устроено) и `IPasswordHasher`/`IJwtTokenGenerator` (абстракции безопасности); DTO (`EventDto`, `EventInfoDto`, `BookingInfoDto`, `PaginatedResultDto` и т.д.) — сервисы возвращают уже готовые DTO, а не доменные сущности; extension-метод `AddApplicationServices()` для регистрации в DI. Зависит только от `Domain` — ни одной ссылки на `Infrastructure`.
- **`EventExamProject.Infrastructure`** — реализация портов, объявленных в `Application`: `AppDbContext`, конфигурации EF Core (`EventConfiguration`, `BookingConfiguration`), репозитории (`EventRepository`, `BookingRepository`, `UserRepository`), реализации безопасности (`Sha256PasswordHasher`, `JwtTokenGenerator`, `JwtOptions`), миграции; extension-методы `AddInfrastructureServices()` и `ApplyMigrations()`.
- **`EventExamProject.Presentation`** — HTTP-слой: контроллеры (`EventsController`, `BookingController`, `AuthController`, тонкие — без бизнес-логики и без обращений к `Domain`, так как сервисы уже отдают DTO), глобальный обработчик исключений (`ExceptionHandlingMiddleware`, маппит доменные исключения в HTTP-статусы), composition root (`Program.cs` + `Extensions/ServiceExtensions.cs`), который просто вызывает `AddApplicationServices()` и `AddInfrastructureServices()`.

Тестовые проекты (`EventExamProject.Tests`, `EventExamProject.IntegrationTests`) ссылаются напрямую на `Application` и `Infrastructure`, а не на весь `EventExamProject.Presentation` — Presentation им не нужен.

## Сборка

`.sln` лежит в корне репозитория (`practicum/`) и включает все четыре слоя и оба тестовых проекта:
```bash
dotnet build
```

## База данных

Строка подключения к PostgreSQL задаётся в `EventExamProject.Presentation/appsettings.json`, ключ `ConnectionStrings:DefaultConnection`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5454;Database=eventapi;Username=postgres;Password=postgres"
  }
}
```

Поправьте `Host`/`Port`/`Database`/`Username`/`Password` под свою БД (например, через переменные окружения `ConnectionStrings__DefaultConnection` или `appsettings.Development.json`, если не хотите менять файл в репозитории).

Быстрый способ поднять БД локально через Docker (порт подобран под значение по умолчанию в `appsettings.json`):
```bash
docker run -d --name eventapi-postgres \
  -e POSTGRES_DB=eventapi -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres \
  -p 5454:5432 postgres:16-alpine
```

Схема БД (таблицы `events`, `bookings`, `users`, внешние ключи `bookings.EventId → events.Id` и `bookings.UserId → users.Id`, уникальный индекс `users.Login`) управляется миграциями EF Core, а не создаётся автоматически «по слепку» модели. При каждом запуске приложение само применяет все ещё не применённые миграции: сразу после `builder.Build()` в `Program.cs` вызывается `app.Services.ApplyMigrations()` (extension-метод из `EventExamProject.Infrastructure`, под капотом — `AppDbContext.Database.Migrate()`). Если БД пустая — миграции создадут схему с нуля; если часть миграций уже применена — накатятся только новые. Ручных `CREATE TABLE`/`ALTER TABLE` выполнять не нужно.

## Миграции

`AppDbContext` и миграции лежат в `EventExamProject.Infrastructure/Persistence/` и `EventExamProject.Infrastructure/Migrations/` соответственно — отдельно от запускаемого (startup) проекта `EventExamProject.Presentation`. Поэтому все команды `dotnet-ef` нужно запускать с явным указанием обоих проектов: `--project` (где лежит `AppDbContext`) и `--startup-project` (откуда брать конфигурацию и DI).

Установить инструмент (один раз):
```bash
dotnet tool install --global dotnet-ef
```

Создать новую миграцию после изменения сущностей/конфигураций EF Core (из корня репозитория, `practicum/`):
```bash
dotnet ef migrations add <ИмяМиграции> \
  --project EventExamProject.Infrastructure/EventExamProject.Infrastructure.csproj \
  --startup-project EventExamProject.Presentation/EventExamProject.Presentation.csproj
```

Применить миграции к БД вручную, не запуская приложение (обычно не требуется — `Program.cs` делает это сам при старте):
```bash
dotnet ef database update \
  --project EventExamProject.Infrastructure/EventExamProject.Infrastructure.csproj \
  --startup-project EventExamProject.Presentation/EventExamProject.Presentation.csproj
```

Откатить последнюю ещё не применённую миграцию (если она не подошла):
```bash
dotnet ef migrations remove \
  --project EventExamProject.Infrastructure/EventExamProject.Infrastructure.csproj \
  --startup-project EventExamProject.Presentation/EventExamProject.Presentation.csproj
```

## Запуск

Из папки проекта (`EventExamProject.Presentation/`), при поднятой и доступной PostgreSQL:
```bash
dotnet run
```

Сервис запустится на `http://localhost:5278` (порт можно изменить в `Properties/launchSettings.json`).

## Swagger UI

После запуска документация и интерактивное тестирование API доступны по адресу:

```
http://localhost:5278/swagger/index.html
```

## Тесты

Тесты разделены на два проекта: юнит-тесты (`EventExamProject.Tests`) на InMemory-провайдере EF Core и интеграционные (`EventExamProject.IntegrationTests`) на реальном PostgreSQL через Testcontainers.

### Юнит-тесты

Лежат в `EventExamProject.Tests` (xUnit) и покрывают бизнес-логику `EventService` и `BookingService`: успешные CRUD-сценарии, фильтрацию, пагинацию, создание и получение бронирований, а также обработку ошибочных сценариев (несуществующий id, некорректные даты, бронь для несуществующего или удалённого мероприятия).

Тесты сервисов не ходят в реальный PostgreSQL — вместо этого используется InMemory-провайдер EF Core (`Microsoft.EntityFrameworkCore.InMemory`). Каждый тестовый класс (`EventServiceTests`, `BookingServiceTests`) в конструкторе собирает свой `ServiceCollection`, регистрирует `AppDbContext` с `UseInMemoryDatabase(dbName)` (уникальное имя базы на класс, чтобы тесты не влияли друг на друга), репозитории (`IEventRepository`/`IBookingRepository`) и сервисы как `Scoped`, после чего резолвит их через `IServiceProvider`/`IServiceScope` — так же, как это делает ASP.NET Core в реальном приложении. Тесты на конкурентность создают отдельный `scope` (и, соответственно, отдельный `AppDbContext`) на каждый параллельный запрос, а не расшаривают один и тот же контекст между потоками.

Отдельно покрыта логика мест и потокобезопасность:
- `EventTests.cs` — `TryReserveSeats`/`ReleaseSeats` на уровне модели `Event` (успешная резервация, отказ при нехватке мест, резервация последнего места, ошибка при попытке освободить больше мест, чем занято).
- `BookingTests.cs` — переходы статуса брони (`Confirm`/`Reject`/`Cancel` заполняют `ProcessedAt`, повторная отмена запрещена).
- `BookingServiceTests.cs` (новые правила) — бронь прошедшего события, лимит активных броней (в том числе при параллельных запросах), независимость лимитов разных пользователей, отмена своей/чужой брони, отмена администратором, возврат места при отмене.
- `UserTests.cs`, `UserServiceTests.cs`, `SecurityTests.cs` — создание пользователя, регистрация и вход (хеш вместо пароля, одинаковая ошибка для неверного логина и пароля), хеширование и содержимое JWT-токена.
- `BookingServiceTests.cs` — уменьшение `AvailableSeats` при бронировании, бронирование до полного исчерпания мест, `NoAvailableSeatsException` при нехватке мест, восстановление места после отклонённой брони, а также два теста на реальную конкурентность (`Task.Run` + `Task.WhenAll`): защита от овербукинга (5 мест / 20 параллельных запросов → ровно 5 успешных) и уникальность `Id` при параллельном создании броней.

Из корня репозитория (`practicum/`) — явно указываем проект, иначе `dotnet test` подхватит весь `.sln`, включая интеграционные тесты, которым нужен Docker:
```bash
dotnet test EventExamProject.Tests/EventExamProject.Tests.csproj
```

### Интеграционные тесты

Лежат в `EventExamProject.IntegrationTests` и проверяют слой доступа к данным (`EventRepository`, `BookingRepository`) на настоящем PostgreSQL, а не на InMemory-провайдере — то есть реально пишут и читают строки через миграции EF Core применённые к живой БД.

**Для запуска нужен запущенный Docker** — тесты сами поднимают контейнер `postgres:16-alpine` через [Testcontainers](https://dotnet.testcontainers.org/) (`Testcontainers.PostgreSql`), без ручной подготовки БД.

Особенности реализации:
- Один контейнер Postgres поднимается один раз на весь тестовый прогон (`DatabaseFixture` + `[CollectionDefinition]`/`[Collection]`), а не по контейнеру на каждый тестовый класс.
- Перед **каждым** тестом схема приводится к чистому состоянию: `Database.EnsureDeletedAsync()` + `Database.MigrateAsync()` (в `RepositoryTestBase.InitializeAsync`) — тесты изолированы и не зависят от порядка запуска.
- `EventRepositoryTests` покрывает все методы (`GetAllAsync`, `GetByIdAsync`, `AddAsync`, `UpdateAsync`, `DeleteAsync`), включая все вариации фильтров (`Title`, `From`, `To`, их комбинации, граничные значения дат) и пагинацию (обычная страница, страница за пределами данных, `PageSize = 0`).
- `BookingRepositoryTests` покрывает CRUD, `GetPendingBookingIdsAsync` (используется фоновым сервисом — только брони со статусом `Pending`) и `CountActiveByUserAsync` (считаются только `Pending`/`Confirmed` брони нужного пользователя).
- `UserRepositoryTests` проверяет сохранение пользователя, поиск по логину и **уникальность логина**; `MigrationsTests` — наличие таблицы `users`, внешнего ключа `bookings.UserId → users.Id` и уникального индекса на логин.
- Изменения проверяются через **отдельный** `AppDbContext`, указывающий на тот же контейнер, а не через тот же трекнутый экземпляр — так тест подтверждает, что данные реально попали в БД, а не просто остались в change tracker'е.

Запуск из корня репозитория (`practicum/`), Docker должен быть запущен:
```bash
dotnet test EventExamProject.IntegrationTests/EventExamProject.IntegrationTests.csproj
```

## Аутентификация и авторизация

Доступ к API защищён JWT-токенами (схема `Bearer`). Токен выдаёт `POST /auth/login`, проверяет его middleware `UseAuthentication()` в Presentation.

### Ролевая модель

В системе две роли (`UserRole`): `User` (по умолчанию) и `Admin`. Роль задаётся при регистрации и попадает в токен как claim `role`.

| Действие                                              | Аноним | User             | Admin |
|--------------------------------------------------------|--------|------------------|-------|
| `POST /auth/register`, `POST /auth/login`              | ✅     | ✅               | ✅    |
| `GET /events`, `GET /events/{id}`                      | ✅     | ✅               | ✅    |
| `POST /events`, `PUT /events/{id}`, `DELETE /events/{id}` | `401` | `403`          | ✅    |
| `POST /events/{id}/book`, `GET /bookings/{id}`         | `401`  | ✅               | ✅    |
| `DELETE /bookings/{id}` (отмена брони)                 | `401`  | только свою (иначе `403`) | любую |

Права на события проверяются атрибутом `[Authorize(Roles = "Admin")]`. Право отменять бронь проверяется в `BookingService.CancelBookingAsync`: идентификатор и роль текущего пользователя контроллер берёт из claims токена и передаёт в сервис.

### Бизнес-правила бронирования

- Нельзя забронировать событие, которое уже началось (`StartAt <= now`) — `400 Bad Request`.
- У одного пользователя не может быть больше **10 активных броней** (статусы `Pending` и `Confirmed`; `Rejected` и `Cancelled` не считаются) — `409 Conflict`. Лимит задан константой `BookingService.MaxActiveBookingsPerUser`, его значение указано в тексте ошибки. Лимиты разных пользователей независимы.
- Отмена брони переводит её в `Cancelled` и возвращает место в событие. Повторная отмена — `400 Bad Request`.

### Как получить токен через Swagger

1. Откройте `http://localhost:5278/swagger/index.html`.
2. Выполните `POST /auth/register` с телом `{"login": "alice", "password": "secret123"}` — ответ `204`. Для администратора добавьте `"role": "Admin"` (передача роли открыта для удобства тестирования; в реальной системе администраторов так создавать нельзя).
3. Выполните `POST /auth/login` с теми же логином и паролем и скопируйте значение поля `token` из ответа.
4. Нажмите кнопку **Authorize** вверху страницы, вставьте токен (слово `Bearer` добавлять не нужно) и нажмите **Authorize**. Теперь Swagger добавляет заголовок `Authorization: Bearer <token>` ко всем запросам.
5. Проверьте права: `POST /events` без токена вернёт `401`, с токеном обычного пользователя — `403`, с токеном администратора — `201`.

То же через curl:
```bash
curl -X POST http://localhost:5278/auth/register -H "Content-Type: application/json" \
  -d '{"login":"boss","password":"secret123","role":"Admin"}'
TOKEN=$(curl -s -X POST http://localhost:5278/auth/login -H "Content-Type: application/json" \
  -d '{"login":"boss","password":"secret123"}' | jq -r .token)
curl -H "Authorization: Bearer $TOKEN" http://localhost:5278/bookings/{id}
```

### Настройка JWT

Параметры токена лежат в секции `Jwt` файла `EventExamProject.Presentation/appsettings.json`:

```json
{
  "Jwt": {
    "Secret": "dev-only-secret-change-me-in-production-0123456789",
    "Issuer": "EventExamProject",
    "Audience": "EventExamProject.Clients",
    "ExpirationMinutes": 60
  }
}
```

- `Secret` — ключ подписи HMAC-SHA256, не короче 32 символов.
- `Issuer` / `Audience` — издатель и аудитория, которые проверяются при валидации.
- `ExpirationMinutes` — время жизни токена.

⚠️ Значение `Secret` из репозитория годится только для локальной разработки. **В продакшне используйте длинный случайный секрет и не храните его в git**: переопределите его переменной окружения (`Jwt__Secret`), user-secrets или секретным хранилищем вашей платформы. Любой, кто знает секрет, может выпустить токен с ролью `Admin`.

### Хранение паролей

Пароль хранится в таблице `users` только в виде хеша SHA-256 (hex), открытый пароль не сохраняется. Проверка идёт через `IPasswordHasher.Verify` с сравнением за постоянное время. При неверном логине и неверном пароле возвращается одно и то же сообщение `Invalid login or password`, чтобы нельзя было перебором выяснить, какие логины существуют. Учтите, что SHA-256 без соли — упрощение для учебного проекта; в реальной системе для паролей нужны bcrypt/Argon2/PBKDF2.

## API

### POST /auth/register
Зарегистрировать пользователя. Доступно без токена.

**Тело запроса:**
```json
{ "login": "alice", "password": "secret123", "role": "User" }
```
`login` — 3–100 символов, `password` — 6–100 символов, `role` — `User` (по умолчанию) или `Admin`. Возвращает `204 No Content`; `400 Bad Request` — ошибка валидации или логин уже занят.

### POST /auth/login
Получить JWT-токен. Доступно без токена.

**Тело запроса:** `{ "login": "alice", "password": "secret123" }`. Возвращает `200 OK` с телом `{ "token": "..." }`; `400 Bad Request` — неверные учётные данные.

### GET /events
Получить список мероприятий с фильтрацией и пагинацией.

**Query-параметры (все опциональные):**

| Параметр   | Тип      | По умолчанию | Описание                                                              |
|------------|----------|--------------|------------------------------------------------------------------------|
| `title`    | string   | —            | Поиск по названию: частичное совпадение, регистр не учитывается         |
| `from`     | DateTime | —            | Только мероприятия, которые начинаются не раньше указанной даты        |
| `to`       | DateTime | —            | Только мероприятия, которые заканчиваются не позже указанной даты      |
| `page`     | int      | `1`          | Номер страницы                                                          |
| `pageSize` | int      | `10`         | Количество элементов на странице                                       |

Все переданные фильтры применяются одновременно (логическое И).

**Пример запроса:**
```
GET /events?title=meeting&from=2026-08-01&to=2026-08-31&page=1&pageSize=10
```

**Пример ответа (`200 OK`):**
```json
{
  "data": [
    {
      "id": "9b85dfc3-7fd9-4c3f-9702-60087c287f26",
      "title": "Team Meeting",
      "description": "Еженедельная встреча",
      "startAt": "2027-08-01T10:00:00",
      "endAt": "2027-08-01T11:00:00",
      "totalSeats": 10,
      "availableSeats": 7
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 10
}
```
`totalCount` — общее количество мероприятий, подходящих под фильтр (до пагинации).

### Модель Event

| Поле             | Тип      | Описание                                                              |
|------------------|----------|--------------------------------------------------------------------------|
| `totalSeats`     | int      | Общее количество мест на мероприятии. Обязательно при создании, должно быть больше 0 (иначе `400 Bad Request`) |
| `availableSeats` | int      | Текущее количество свободных мест. При создании равно `totalSeats`, уменьшается при каждой успешной брони и восстанавливается, если бронь отклонена |

### GET /events/{id}
Получить мероприятие по идентификатору.

Возвращает `404 Not Found` если не найдено.

### POST /events
Создать новое мероприятие. **Только `Admin`** (`401` без токена, `403` для роли `User`).

Возвращает `201 Created` с созданным объектом. Возвращает `400 Bad Request`, если `endAt` раньше `startAt` или `totalSeats` не больше 0.

**Тело запроса:**
```json
{
  "title": "Название мероприятия",
  "description": "Описание (необязательно)",
  "startAt": "2027-07-01T10:00:00",
  "endAt": "2027-07-01T12:00:00",
  "totalSeats": 10
}
```

### PUT /events/{id}
Обновить мероприятие целиком. **Только `Admin`** (`401` без токена, `403` для роли `User`).

Возвращает `404 Not Found` если не найдено.

**Тело запроса:** аналогично `POST` — обновляются все поля, включая `totalSeats`. При изменении `totalSeats` уже забронированные места (`totalSeats - availableSeats` на момент обновления) сохраняются, а `availableSeats` пересчитывается: `availableSeats = totalSeats - забронированные`. Если новое значение `totalSeats` меньше числа уже забронированных мест — `400 Bad Request` (нельзя уменьшить вместимость ниже того, что уже занято).

### DELETE /events/{id}
Удалить мероприятие. **Только `Admin`** (`401` без токена, `403` для роли `User`).

Возвращает `204 No Content` при успехе, `404 Not Found` если не найдено.

**Пример использования:**
```bash
curl -X POST http://localhost:5278/events -H "Content-Type: application/json" -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{"title":"Conference","startAt":"2027-08-01T10:00:00","endAt":"2027-08-01T12:00:00","totalSeats":10}'

curl http://localhost:5278/events/{id}
curl -X PUT http://localhost:5278/events/{id} -H "Content-Type: application/json" -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{"title":"Conference (updated)","startAt":"2027-08-01T10:00:00","endAt":"2027-08-01T13:00:00","totalSeats":10}'
curl -X DELETE http://localhost:5278/events/{id} -H "Authorization: Bearer $ADMIN_TOKEN"
```

### POST /events/{id}/book
Создать бронь на мероприятие. Бронь создаётся сразу со статусом `Pending` и сразу же резервирует одно место (`availableSeats` уменьшается на 1); подтверждение или отклонение выполняется фоновым сервисом (см. «Фоновая обработка бронирований»).

Требует аутентификации (`401` без токена); бронь привязывается к пользователю из токена.

Возвращает `202 Accepted` с заголовком `Location` (`/bookings/{id}`) и телом созданной брони. Возвращает `400 Bad Request`, если мероприятие уже началось, `404 Not Found`, если мероприятие не найдено, и `409 Conflict`, если свободных мест не осталось или превышен лимит активных броней (10).

### GET /bookings/{id}
Получить текущее состояние брони по идентификатору. Требует аутентификации (`401` без токена).

Возвращает `404 Not Found` если не найдена.

### DELETE /bookings/{id}
Отменить бронь (статус `Cancelled`, место возвращается в мероприятие). Требует аутентификации. Пользователь может отменить только свою бронь, администратор — любую.

Возвращает `204 No Content` при успехе, `400 Bad Request` при повторной отмене, `401` без токена, `403 Forbidden` при попытке отменить чужую бронь, `404 Not Found` если бронь не найдена.

**Пример использования:**
```bash
curl -i -X POST http://localhost:5278/events/{eventId}/book -H "Authorization: Bearer $TOKEN"
curl http://localhost:5278/bookings/{bookingId} -H "Authorization: Bearer $TOKEN"   # сразу — Pending
curl http://localhost:5278/bookings/{bookingId} -H "Authorization: Bearer $TOKEN"   # через несколько секунд — Confirmed
curl -i -X DELETE http://localhost:5278/bookings/{bookingId} -H "Authorization: Bearer $TOKEN"   # отмена
```

**Пример сценария с овербукингом:**
```bash
# Создаём мероприятие на 3 места
EVENT_ID=$(curl -s -X POST http://localhost:5278/events -H "Content-Type: application/json" -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{"title":"Conference","startAt":"2027-08-01T10:00:00","endAt":"2027-08-01T12:00:00","totalSeats":3}' \
  | jq -r .id)

# Первые три брони проходят
curl -i -X POST http://localhost:5278/events/$EVENT_ID/book -H "Authorization: Bearer $TOKEN"   # 202 Accepted
curl -i -X POST http://localhost:5278/events/$EVENT_ID/book -H "Authorization: Bearer $TOKEN"   # 202 Accepted
curl -i -X POST http://localhost:5278/events/$EVENT_ID/book -H "Authorization: Bearer $TOKEN"   # 202 Accepted

# Четвёртая — мест не осталось
curl -i -X POST http://localhost:5278/events/$EVENT_ID/book -H "Authorization: Bearer $TOKEN"
# HTTP/1.1 409 Conflict
# {"title":"No available seats","status":409,"detail":"No available seats for this event"}
```
Даже если все четыре запроса отправить одновременно (конкурентно), пройдут ровно 3 — это гарантирует `SemaphoreSlim` в `BookingService.CreateBookingAsync` (см. «Синхронизация и потокобезопасность»).

## Модель Booking

| Поле          | Тип           | Описание                                              |
|---------------|---------------|----------------------------------------------------------|
| `id`          | Guid          | Идентификатор брони                                       |
| `eventId`     | Guid          | Идентификатор мероприятия                                 |
| `status`      | BookingStatus | `Pending` (0) / `Confirmed` (1) / `Rejected` (2) / `Cancelled` (3) |
| `createdAt`   | DateTime      | Дата создания брони                                        |
| `processedAt` | DateTime?     | Дата обработки, заполняется фоновым сервисом               |

Хранится в PostgreSQL (таблица `bookings`). Сервисы `EventService`/`BookingService` (`EventExamProject.Application`) не обращаются к `AppDbContext` напрямую — они зависят только от портов `IEventRepository`/`IBookingRepository` (объявлены в `EventExamProject.Application`), а конкретную реализацию на EF Core (`EventExamProject.Infrastructure/Repositories`) подставляет DI, где оба репозитория зарегистрированы как `Scoped`.

В БД у брони есть также `UserId` (внешний ключ на `users`, в ответе API не возвращается) — владелец брони, по которому проверяется право отмены.

## Фоновая обработка бронирований

`BookingProcessingService` (`BackgroundService`) каждые 5 секунд опрашивает БД на брони со статусом `Pending` и обрабатывает их **параллельно**, а не по очереди: для каждой брони запускается своя задача (`Task.WhenAll`), внутри которой сначала выполняется искусственная задержка `Task.Delay` (2 сек, имитация внешнего вызова), а уже после неё — запись изменений в БД.

`BookingProcessingService` — singleton, а `AppDbContext` — scoped, поэтому напрямую он его не получает: сервис принимает `IServiceScopeFactory` и создаёт scope сам. Список идентификаторов `Pending`-броней собирается в отдельном коротко живущем scope (получили ID — scope сразу закрыли), а на обработку каждой брони создаётся свой scope и свой `AppDbContext` — так параллельные задачи не расшаривают один и тот же (небезопасный для конкурентного доступа) контекст.

Для каждой брони:
- если мероприятие, к которому она относится, всё ещё существует — бронь подтверждается (`Confirm()`, статус `Confirmed`, заполняется `processedAt`);
- если мероприятие было удалено к моменту обработки — бронь отклоняется (`Reject()`, статус `Rejected`) с предупреждением в лог;
- если во время обработки происходит непредвиденная ошибка — бронь тоже отклоняется (в новом scope/контексте, чтобы не работать с потенциально неконсистентным состоянием контекста, в котором произошёл сбой), а занятое ею место возвращается в пул через `event.ReleaseSeats()`.

## Синхронизация и потокобезопасность

Сервис допускает конкурентные запросы на бронирование одного и того же мероприятия, поэтому критическая секция — «проверить доступные места и изменить их количество» — защищена синхронизацией, чтобы не допустить овербукинга:

- **`ConcurrentDictionary<Guid, SemaphoreSlim>` в `BookingService.CreateBookingAsync`** — `BookingService` регистрируется как `Scoped`, значит на каждый запрос создаётся новый экземпляр сервиса, и обычная блокировка на приватном поле-объекте не защитила бы от гонки между разными экземплярами. Поэтому семафоры статические, но не общие на всё приложение: `GetLockFor(eventId)` берёт (или лениво создаёт через `GetOrAdd`) отдельный `SemaphoreSlim` на конкретный `eventId` — бронирования разных мероприятий не блокируют друг друга, сериализуется только конкурентный доступ к одному и тому же мероприятию. Используется именно `SemaphoreSlim`, а не `lock`, потому что внутри критической секции есть `await` (запрос к событию и `SaveChangesAsync`), а `await` внутри `lock` компилятором не допускается. `GetBookingByIdAsync` (чтение) под блокировку не попадает.
- **Лимит броней на пользователя.** Проверка «посчитать активные брони + создать новую» тоже должна быть атомарной, иначе параллельные запросы одного пользователя могут обойти лимит. Для этого есть второй словарь семафоров — по `userId`. Порядок захвата всегда «пользователь → мероприятие», поэтому взаимной блокировки нет. Отмена брони берёт только семафор мероприятия, пока возвращает место.
- В `BookingProcessingService` отдельный примитив синхронизации не нужен: раньше там стоял `SemaphoreSlim`, защищавший общее in-memory хранилище, но после перехода на EF Core у каждой параллельной задачи — свой изолированный `AppDbContext`, и сериализовать доступ к нему незачем.

## Валидация

- `Title`, `StartAt`, `EndAt`, `TotalSeats` — обязательные поля
- `EndAt` должен быть позже `StartAt`
- `TotalSeats` должен быть больше 0
- `page` — целое число ≥ 1, `pageSize` — от 1 до 100 (ограничено `[Range]` на `PaginationParamsDto`, чтобы `GET /events?page=0` или `?pageSize=-1` падали с `400` до вызова сервиса и репозитория, а не с `500` от Postgres на отрицательный `OFFSET`/`LIMIT`)

### Часовой пояс `StartAt`/`EndAt`

Колонки `events.StartAt`/`events.EndAt` — `timestamp with time zone` (Postgres хранит их как UTC-момент времени независимо от часового пояса сессии/сервера). Npgsql на запись принимает только `DateTime` с `Kind=Utc`, поэтому на C#-стороне действует единая конвенция: **любая дата на входе трактуется как UTC**. `Event.Create`/`Event.Update` (а также фильтры `from`/`to` в `EventRepository.GetAllAsync`) нормализуют входное значение через `Event.NormalizeToUtcConvention` перед использованием (`Kind=Utc` — как есть; `Kind=Local` — конвертируется в UTC; `Kind=Unspecified`, то есть дата без явного смещения в JSON, — трактуется как уже UTC). Клиентам следует присылать даты в UTC (например, с суффиксом `Z`) либо помнить, что дата без смещения будет интерпретирована как UTC.

## Формат ответа при ошибках

Все ошибки обрабатываются глобальным middleware (`ExceptionHandlingMiddleware`) и возвращаются в едином JSON-формате на основе [Problem Details (RFC 7807)](https://www.rfc-editor.org/rfc/rfc7807):

```json
{
  "title": "Not Found",
  "status": 404,
  "detail": "Event with id 00000000-0000-0000-0000-000000000000 was not found"
}
```

Используемые статус-коды:

| Статус | Когда возвращается                                    |
|--------|----------------------------------------------------------|
| `400`  | Ошибка валидации входных данных (`System.ComponentModel.DataAnnotations.ValidationException`) или нарушение бизнес-инварианта домена (`DomainValidationException`, например нельзя уменьшить `TotalSeats` ниже уже забронированных мест, занятый логин, неверные учётные данные, повторная отмена брони), бронь мероприятия, которое уже началось (`EventAlreadyStartedException`) |
| `401`  | Нет токена или он недействителен (тело ответа пустое)      |
| `403`  | Недостаточно прав: роль не `Admin` для управления событиями или отмена чужой брони (`ForbiddenException`) |
| `404`  | Запрошенный ресурс (мероприятие или бронь) не найден      |
| `409`  | Нет свободных мест (`NoAvailableSeatsException`) или превышен лимит активных броней (`BookingLimitExceededException`) |
| `500`  | Непредвиденная ошибка сервера                             |

Для `400`, возникающего при автоматической валидации модели (например, пустой `Title`, `EndAt` раньше `StartAt` или `page`/`pageSize` вне допустимого диапазона), ASP.NET Core дополнительно включает поле `errors` со списком ошибок по каждому полю:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Title": ["The Title field is required"]
  }
}
```

Для `500` в `detail` уходит общий текст («An unexpected error occurred. Please try again later.»), а не `ex.Message` — подробности непредвиденной ошибки (включая детали БД) попадают только в лог сервера (`_logger.LogError`), наружу клиенту не передаются.
