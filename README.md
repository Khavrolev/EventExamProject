# Event Management Microservices Platform

Распределённая микросервисная платформа для управления мероприятиями и бронированиями на ASP.NET Core (.NET 10) и Apache Kafka.

## Архитектура системы

Монолитная архитектура декомпозирована на три независимых микросервиса с собственной базой данных у каждого (Database-per-Service) и асинхронным событийно-ориентированным взаимодействием через Apache Kafka.

```
                     ┌──────────────────┐
                     │   API Clients    │
                     └────────┬─────────┘
                              │ HTTP / REST
         ┌────────────────────┼────────────────────┐
         │                    │                    │
         ▼                    ▼                    ▼
┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐
│  Users Service   │ │  Events Service  │ │ Bookings Service │
│   (:5001)        │ │   (:5002)        │ │   (:5003)        │
└────────┬─────────┘ └────┬─────────▲───┘ └────────┬─────────┘
         │                │         │              │
         │ PostgreSQL     │         │              │ PostgreSQL
         ▼ (:5455)        │         │ Kafka        ▼ (:5456)
┌──────────────────┐      │         │ Topic        ┌──────────────────┐
│  users database  │      │         │ 'booking-    │bookings database │
└──────────────────┘      │         │  confirmed'  └──────────────────┘
                          │         │              ▲
               PostgreSQL │         └──────────────┤
               (:5454)    ▼                        │
         ┌──────────────────┐             ┌────────┴─────────┐
         │ events database  │             │   Apache Kafka   │
         │ (events +        │             │ (:9092 / :29092) │
         │processed_bookings│             └────────▲─────────┘
         └──────────────────┘                      │
                                          ┌────────┴─────────┐
                                          │    Zookeeper     │
                                          └──────────────────┘
```

### Компоненты системы

1. **`Users` Service (`services/Users/`)**
   - Управление пользователями, регистрация и аутентификация.
   - Генерация криптографически подписанных JWT-токенов с ролями (`User`, `Admin`).
   - Собственная база данных PostgreSQL `users`.

2. **`Events` Service (`services/Events/`)**
   - Каталог мероприятий, расписание, вместимость и учет свободных мест.
   - Авторизация на основе ролей: создание, изменение и удаление мероприятий разрешено только роли `Admin`.
   - Фоновый сервис `BookingConfirmedConsumerService` — Kafka Consumer для топика `booking-confirmed`.
   - Гарантия идемпотентности обработки сообщений через таблицу `processed_bookings`.
   - Сервис инициализации топика `KafkaTopicInitializer` при старте через `IAdminClient`.
   - Собственная база данных PostgreSQL `events`.

3. **`Bookings` Service (`services/Bookings/`)**
   - Регистрация заявок на бронирование мероприятий.
   - Фоновый сервис `BookingProcessingService` — асинхронная обработка заявок со статусом `Pending`, перевод в статус `Confirmed`.
   - Сервис `KafkaBookingEventProducer` — singleton-продюсер сообщений `BookingConfirmed` в Kafka с партиционированием по `EventId`.
   - Собственная база данных PostgreSQL `bookings`.

4. **`Contracts` Library (`shared/Contracts/`)**
   - Общая сборка моделей сообщений и констант топиков Kafka (`KafkaTopics.BookingConfirmed`, `BookingConfirmedEvent`).
   - Не содержит бизнес-логики и зависимостей от инфраструктуры.

5. **`Apache Kafka & Zookeeper`**
   - Брокер сообщений для асинхронного межсервисного взаимодействия.
   - Топик `booking-confirmed` с партициями для масштабирования.

---

## Таблица портов

| Сервис / Ресурс | Хост-порт | Внутренний порт контейнера | Назначение |
|---|---|---|---|
| Users Service | `5001` | `5001` | REST API аутентификации и пользователей |
| Events Service | `5002` | `5002` | REST API управления мероприятиями |
| Bookings Service | `5003` | `5003` | REST API управления бронированиями |
| Users Database | `5455` | `5432` | PostgreSQL БД сервиса Users (`users`) |
| Events Database | `5454` | `5432` | PostgreSQL БД сервиса Events (`events`) |
| Bookings Database | `5456` | `5432` | PostgreSQL БД сервиса Bookings (`bookings`) |
| Apache Kafka | `9092` | `29092` | Брокер сообщений (`INTERNAL://kafka:29092`, `EXTERNAL://localhost:9092`) |
| Zookeeper | — | `2181` | Координатор Kafka |

---

## Поток данных события BookingConfirmed

```
[Client]
   │
   │ 1. POST /bookings { eventId } + JWT
   ▼
[Bookings Service: BookingController]
   │
   │ 2. Save booking (Status = Pending)
   ▼
[PostgreSQL: bookings database]
   │
   │ 3. 202 Accepted (Location: /bookings/{id})
   ▼
[Client]

──────────────────────── АСИНХРОННЫЙ ПРОЦЕСС ────────────────────────

[Bookings Service: BookingProcessingService (BackgroundService)]
   │
   │ 4. Периодический опрос Pending-бронирований
   │ 5. Имитация обработки (Task.Delay 2s)
   │ 6. Обновление статуса: Status = Confirmed, ProcessedAt = UtcNow
   ▼
[PostgreSQL: bookings database]
   │
   │ 7. Публикация события BookingConfirmed (Key = EventId)
   ▼
[Kafka: topic 'booking-confirmed']
   │
   │ 8. Получение сообщения (GroupId = 'events-consumer-group')
   ▼
[Events Service: BookingConfirmedConsumerService (BackgroundService)]
   │
   │ 9. Проверка идемпотентности: SELECT EXISTS в processed_bookings
   │ 10. Если не обработано:
   │     - UPDATE events SET AvailableSeats = AvailableSeats - 1
   │     - INSERT INTO processed_bookings (BookingId, ProcessedAt)
   ▼
[PostgreSQL: events database]
   │
   │ 11. Фиксация offset (Commit) в Kafka
   ▼
[Kafka Broker]
```

### Гарантии надежности и идемпотентности:
- **Сохранение в БД перед отправкой:** `Bookings` сначала гарантированно фиксирует статус `Confirmed` в БД `bookings`, и только после успешного коммита отправляет событие в Kafka.
- **Партиционирование:** Ключом Kafka-сообщения является `EventId`, что гарантирует строгую очередность обработки событий одного мероприятия в рамках одной партиции.
- **Идемпотентность на стороне потребителя:** Сервис `Events` сохраняет `BookingId` в таблицу `processed_bookings`. При повторной доставке сообщения (at-least-once доставка) дублирующее событие пропускается, исключая повторное списание мест.
- **Устойчивость к недоступности Kafka:** Сервис `Events` при старте выполняет создание топика через `IAdminClient` с обработкой исключений, не блокируя запуск сервиса, если брокер временно недоступен.

---

## Запуск системы через Docker Compose

Для запуска всех сервисов, баз данных и инфраструктуры Kafka требуется одна команда:

```bash
docker compose up -d
```

Для пересборки контейнеров после изменения исходного кода:

```bash
docker compose up -d --build
```

Остановка всех сервисов:

```bash
docker compose down
```

Остановка с удалением томов данных:

```bash
docker compose down -v
```

### Автоматическая миграция БД
При старте каждого микросервиса автоматически вызывается `ApplyMigrations()`, который накатывает все недостающие миграции Entity Framework Core на соответствующую базу данных. Предварительное ручное создание таблиц не требуется.

---

## Документация Swagger UI

Каждый сервис предоставляет интерактивную документацию Swagger c поддержкой Bearer JWT:

- Users API: `http://localhost:5001/swagger`
- Events API: `http://localhost:5002/swagger`
- Bookings API: `http://localhost:5003/swagger`

Для авторизации в Swagger UI:
1. Выполните `POST /auth/login` в Users Service и скопируйте `token`.
2. Нажмите кнопку **Authorize** в правом верхнем углу Swagger UI любого сервиса.
3. Вставьте токен в поле Value (слово `Bearer` указывать не требуется, оно подставляется автоматически) и нажмите **Authorize**.

---

## Сквозной сценарий тестирования (End-to-End)

Ниже приведён полный сценарий проверки работоспособности через `curl` и утилиту `jq`.

### Шаг 1. Регистрация и получение токена администратора

```bash
curl -s -X POST http://localhost:5001/auth/register \
  -H "Content-Type: application/json" \
  -d '{"login":"admin","password":"AdminPassword123!","role":"Admin"}'

ADMIN_TOKEN=$(curl -s -X POST http://localhost:5001/auth/login \
  -H "Content-Type: application/json" \
  -d '{"login":"admin","password":"AdminPassword123!"}' | jq -r .token)

echo "Admin Token: $ADMIN_TOKEN"
```

### Шаг 2. Создание мероприятия администратором

Создание мероприятия требует роли `Admin` (без токена сервис возвращает `401 Unauthorized`, а с токеном роли `User` — `403 Forbidden`).

```bash
EVENT_RESPONSE=$(curl -s -X POST http://localhost:5002/events \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  -d '{
    "title": "HighLoad .NET Architecture",
    "description": "Event-driven architecture with Kafka",
    "startAt": "2026-12-15T10:00:00Z",
    "endAt": "2026-12-15T18:00:00Z",
    "totalSeats": 10
  }')

echo "$EVENT_RESPONSE" | jq .
EVENT_ID=$(echo "$EVENT_RESPONSE" | jq -r .id)
echo "Created Event ID: $EVENT_ID"
```

Начальное состояние: `totalSeats = 10`, `availableSeats = 10`.

### Шаг 3. Регистрация обычного пользователя и получение токена

```bash
curl -s -X POST http://localhost:5001/auth/register \
  -H "Content-Type: application/json" \
  -d '{"login":"user_alice","password":"UserPassword123!","role":"User"}'

USER_TOKEN=$(curl -s -X POST http://localhost:5001/auth/login \
  -H "Content-Type: application/json" \
  -d '{"login":"user_alice","password":"UserPassword123!"}' | jq -r .token)

echo "User Token: $USER_TOKEN"
```

### Шаг 4. Создание бронирования

Запрос выполняется пользователем в Bookings Service:

```bash
BOOKING_RESPONSE=$(curl -s -X POST http://localhost:5003/bookings \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -d "{\"eventId\":\"$EVENT_ID\"}")

echo "$BOOKING_RESPONSE" | jq .
BOOKING_ID=$(echo "$BOOKING_RESPONSE" | jq -r .id)
echo "Created Booking ID: $BOOKING_ID"
```

Ответ сервиса: `202 Accepted`, статус `0` (`Pending`).

### Шаг 5. Ожидание асинхронной обработки и публикации события в Kafka

Фоновый процесс `BookingProcessingService` с интервалом опроса проверяет заявки, переводит статус бронирования в `1` (`Confirmed`) и публикует `BookingConfirmed` в Kafka топик `booking-confirmed`.

```bash
sleep 4
```

### Шаг 6. Проверка подтверждения бронирования в Bookings Service

```bash
curl -s -X GET "http://localhost:5003/bookings/$BOOKING_ID" \
  -H "Authorization: Bearer $USER_TOKEN" | jq .
```

Статус бронирования изменился на `1` (`Confirmed`), поле `processedAt` заполнено датой обработки.

### Шаг 7. Проверка уменьшения свободных мест в Events Service

Фоновый сервис `BookingConfirmedConsumerService` принял событие из Kafka и уменьшил количество доступных мест:

```bash
curl -s -X GET "http://localhost:5002/events/$EVENT_ID" | jq .
```

Результат:
```json
{
  "id": "...",
  "title": "HighLoad .NET Architecture",
  "totalSeats": 10,
  "availableSeats": 9
}
```
Значение `availableSeats` уменьшилось с 10 до 9, подтверждая успешное сквозное асинхронное взаимодействие между микросервисами через Kafka.

---

## Локальная разработка и сборка решения

Сборка всего решения со всеми микросервисами и общей библиотекой контрактов:

```bash
dotnet build EventExamProject.sln
```

### Запуск сервисов локально вне Docker

При локальном запуске каждый сервис подключается к соответствующей базе данных и локальному брокеру Kafka:

```bash
dotnet run --project services/Users/Users.Presentation/Users.Presentation.csproj
dotnet run --project services/Events/Events.Presentation/Events.Presentation.csproj
dotnet run --project services/Bookings/Bookings.Presentation/Bookings.Presentation.csproj
```
