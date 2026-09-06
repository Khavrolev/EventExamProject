# EventExamProject
Сервис для управления мероприятиями на ASP.NET Core Web API

## Требования

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- PostgreSQL (локально или в Docker) — приложение хранит данные в БД, а не в памяти

## Сборка

Из папки проекта (`EventExamProject/`):
```bash
dotnet build
```

## База данных

Строка подключения к PostgreSQL задаётся в `EventExamProject/appsettings.json`, ключ `ConnectionStrings:DefaultConnection`:

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

Схема БД (таблицы `events` и `bookings`) создаётся автоматически при первом запуске приложения через `Database.EnsureCreated()` (вызывается в `Program.cs` сразу после `builder.Build()`) — отдельно накатывать миграции не нужно. При последующих запусках метод ничего не делает, если схема уже существует. `EnsureCreated` несовместим с миграциями EF Core — если в будущем понадобятся миграции, схему нужно будет пересоздать или перейти на `Database.Migrate()`.

## Запуск

Из папки проекта (`EventExamProject/`), при поднятой и доступной PostgreSQL:
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

Юнит-тесты лежат в отдельном проекте `EventExamProject.Tests` (xUnit) и покрывают бизнес-логику `EventService` и `BookingService`: успешные CRUD-сценарии, фильтрацию, пагинацию, создание и получение бронирований, а также обработку ошибочных сценариев (несуществующий id, некорректные даты, бронь для несуществующего или удалённого мероприятия).

Тесты сервисов не ходят в реальный PostgreSQL — вместо этого используется InMemory-провайдер EF Core (`Microsoft.EntityFrameworkCore.InMemory`). Каждый тестовый класс (`EventServiceTests`, `BookingServiceTests`) в конструкторе собирает свой `ServiceCollection`, регистрирует `AppDbContext` с `UseInMemoryDatabase(dbName)` (уникальное имя базы на класс, чтобы тесты не влияли друг на друга) и сервисы как `Scoped`, после чего резолвит их через `IServiceProvider`/`IServiceScope` — так же, как это делает ASP.NET Core в реальном приложении. Тесты на конкурентность создают отдельный `scope` (и, соответственно, отдельный `AppDbContext`) на каждый параллельный запрос, а не расшаривают один и тот же контекст между потоками.

Отдельно покрыта логика мест и потокобезопасность:
- `EventTests.cs` — `TryReserveSeats`/`ReleaseSeats` на уровне модели `Event` (успешная резервация, отказ при нехватке мест, резервация последнего места, ошибка при попытке освободить больше мест, чем занято).
- `BookingTests.cs` — переходы статуса брони (`Confirm`/`Reject` заполняют `ProcessedAt`).
- `BookingServiceTests.cs` — уменьшение `AvailableSeats` при бронировании, бронирование до полного исчерпания мест, `NoAvailableSeatsException` при нехватке мест, восстановление места после отклонённой брони, а также два теста на реальную конкурентность (`Task.Run` + `Task.WhenAll`): защита от овербукинга (5 мест / 20 параллельных запросов → ровно 5 успешных) и уникальность `Id` при параллельном создании броней.

Из папки проекта (`EventExamProject/`):
```bash
dotnet test
```

## API

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
      "startAt": "2026-08-01T10:00:00",
      "endAt": "2026-08-01T11:00:00",
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
Создать новое мероприятие.

Возвращает `201 Created` с созданным объектом. Возвращает `400 Bad Request`, если `endAt` раньше `startAt` или `totalSeats` не больше 0.

**Тело запроса:**
```json
{
  "title": "Название мероприятия",
  "description": "Описание (необязательно)",
  "startAt": "2026-07-01T10:00:00",
  "endAt": "2026-07-01T12:00:00",
  "totalSeats": 10
}
```

### PUT /events/{id}
Обновить мероприятие целиком.

Возвращает `404 Not Found` если не найдено.

**Тело запроса:** аналогично `POST` (поле `totalSeats` обязательно в теле запроса, но количество мест через этот эндпоинт не меняется — обновляются только `title`, `description`, `startAt`, `endAt`).

### DELETE /events/{id}
Удалить мероприятие.

Возвращает `204 No Content` при успехе, `404 Not Found` если не найдено.

**Пример использования:**
```bash
curl -X POST http://localhost:5278/events -H "Content-Type: application/json" \
  -d '{"title":"Conference","startAt":"2026-08-01T10:00:00","endAt":"2026-08-01T12:00:00","totalSeats":10}'

curl http://localhost:5278/events/{id}
curl -X PUT http://localhost:5278/events/{id} -H "Content-Type: application/json" \
  -d '{"title":"Conference (updated)","startAt":"2026-08-01T10:00:00","endAt":"2026-08-01T13:00:00","totalSeats":10}'
curl -X DELETE http://localhost:5278/events/{id}
```

### POST /events/{id}/book
Создать бронь на мероприятие. Бронь создаётся сразу со статусом `Pending` и сразу же резервирует одно место (`availableSeats` уменьшается на 1); подтверждение или отклонение выполняется фоновым сервисом (см. «Фоновая обработка бронирований»).

Возвращает `202 Accepted` с заголовком `Location` (`/bookings/{id}`) и телом созданной брони. Возвращает `404 Not Found`, если мероприятие не найдено, и `409 Conflict`, если свободных мест не осталось.

### GET /bookings/{id}
Получить текущее состояние брони по идентификатору.

Возвращает `404 Not Found` если не найдена.

**Пример использования:**
```bash
curl -i -X POST http://localhost:5278/events/{eventId}/book
curl http://localhost:5278/bookings/{bookingId}   # сразу — Pending
curl http://localhost:5278/bookings/{bookingId}   # через несколько секунд — Confirmed
```

**Пример сценария с овербукингом:**
```bash
# Создаём мероприятие на 3 места
EVENT_ID=$(curl -s -X POST http://localhost:5278/events -H "Content-Type: application/json" \
  -d '{"title":"Conference","startAt":"2026-08-01T10:00:00","endAt":"2026-08-01T12:00:00","totalSeats":3}' \
  | jq -r .id)

# Первые три брони проходят
curl -i -X POST http://localhost:5278/events/$EVENT_ID/book   # 202 Accepted
curl -i -X POST http://localhost:5278/events/$EVENT_ID/book   # 202 Accepted
curl -i -X POST http://localhost:5278/events/$EVENT_ID/book   # 202 Accepted

# Четвёртая — мест не осталось
curl -i -X POST http://localhost:5278/events/$EVENT_ID/book
# HTTP/1.1 409 Conflict
# {"title":"No available seats","status":409,"detail":"No available seats for this event"}
```
Даже если все четыре запроса отправить одновременно (конкурентно), пройдут ровно 3 — это гарантирует `SemaphoreSlim` в `BookingService.CreateBookingAsync` (см. «Синхронизация и потокобезопасность»).

## Модель Booking

| Поле          | Тип           | Описание                                              |
|---------------|---------------|----------------------------------------------------------|
| `id`          | Guid          | Идентификатор брони                                       |
| `eventId`     | Guid          | Идентификатор мероприятия                                 |
| `status`      | BookingStatus | `Pending` (0) / `Confirmed` (1) / `Rejected` (2)           |
| `createdAt`   | DateTime      | Дата создания брони                                        |
| `processedAt` | DateTime?     | Дата обработки, заполняется фоновым сервисом               |

Хранится в PostgreSQL (таблица `bookings`), доступ — через `AppDbContext` (Entity Framework Core), аналогично мероприятиям.

## Фоновая обработка бронирований

`BookingProcessingService` (`BackgroundService`) каждые 5 секунд опрашивает БД на брони со статусом `Pending` и обрабатывает их **параллельно**, а не по очереди: для каждой брони запускается своя задача (`Task.WhenAll`), внутри которой сначала выполняется искусственная задержка `Task.Delay` (2 сек, имитация внешнего вызова), а уже после неё — запись изменений в БД.

`BookingProcessingService` — singleton, а `AppDbContext` — scoped, поэтому напрямую он его не получает: сервис принимает `IServiceScopeFactory` и создаёт scope сам. Список идентификаторов `Pending`-броней собирается в отдельном коротко живущем scope (получили ID — scope сразу закрыли), а на обработку каждой брони создаётся свой scope и свой `AppDbContext` — так параллельные задачи не расшаривают один и тот же (небезопасный для конкурентного доступа) контекст.

Для каждой брони:
- если мероприятие, к которому она относится, всё ещё существует — бронь подтверждается (`Confirm()`, статус `Confirmed`, заполняется `processedAt`);
- если мероприятие было удалено к моменту обработки — бронь отклоняется (`Reject()`, статус `Rejected`) с предупреждением в лог;
- если во время обработки происходит непредвиденная ошибка — бронь тоже отклоняется (в новом scope/контексте, чтобы не работать с потенциально неконсистентным состоянием контекста, в котором произошёл сбой), а занятое ею место возвращается в пул через `event.ReleaseSeats()`.

## Синхронизация и потокобезопасность

Сервис допускает конкурентные запросы на бронирование одного и того же мероприятия, поэтому критическая секция — «проверить доступные места и изменить их количество» — защищена синхронизацией, чтобы не допустить овербукинга:

- **Общий `static SemaphoreSlim BookingLock` в `BookingService.CreateBookingAsync`** — `BookingService` регистрируется как `Scoped` (потому что зависит от `AppDbContext`), значит на каждый запрос создаётся новый экземпляр сервиса, и обычная блокировка на приватном поле-объекте не защитила бы от гонки между разными экземплярами. Поэтому используется один статический семафор, общий для всех экземпляров процесса: он сериализует создание броней сразу по всем мероприятиям (не только по одному конкретному `eventId`) — так проще, ценой того, что бронирования на разные мероприятия лишний раз ждут друг друга. Используется именно `SemaphoreSlim`, а не `lock`, потому что внутри критической секции есть `await` (запрос к событию и `SaveChangesAsync`), а `await` внутри `lock` компилятором не допускается. `GetBookingByIdAsync` (чтение) под блокировку не попадает.
- В `BookingProcessingService` отдельный примитив синхронизации не нужен: раньше там стоял `SemaphoreSlim`, защищавший общее in-memory хранилище, но после перехода на EF Core у каждой параллельной задачи — свой изолированный `AppDbContext`, и сериализовать доступ к нему незачем.

## Валидация

- `Title`, `StartAt`, `EndAt`, `TotalSeats` — обязательные поля
- `EndAt` должен быть позже `StartAt`
- `TotalSeats` должен быть больше 0

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
| `400`  | Ошибка валидации входных данных                          |
| `404`  | Запрошенный ресурс (мероприятие или бронь) не найден      |
| `409`  | Нет свободных мест для бронирования (`NoAvailableSeatsException`) |
| `500`  | Непредвиденная ошибка сервера                             |

Для `400`, возникающего при автоматической валидации модели (например, пустой `Title` или `EndAt` раньше `StartAt`), ASP.NET Core дополнительно включает поле `errors` со списком ошибок по каждому полю:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Title": ["The Title field is required"]
  }
}
```
