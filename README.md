# Event Management Microservices Platform

Распределённая микросервисная платформа для управления мероприятиями и бронированиями на ASP.NET Core (.NET 10) и Apache Kafka.

## Архитектура системы

Монолитная архитектура декомпозирована на три независимых микросервиса с собственной базой данных у каждого (Database-per-Service), паттерном Transactional Outbox и асинхронным событийно-ориентированным взаимодействием через Apache Kafka.

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
         ▼ (:5455)        │         │ Kafka Topics:▼ (:5456)
┌──────────────────┐      │         │ - booking-   ┌──────────────────┐
│  users database  │      │         │   confirmed  │bookings database │
└──────────────────┘      │         │ - booking-   │(bookings +       │
                          │         │   cancelled  │ outbox_messages) │
               PostgreSQL │         │ - booking-   └──────────────────┘
               (:5454)    │         │   rejected   ▲
         ┌────────────────┴─┐       │              │
         │ events database  │───────┴──────────────┘
         │ (events +        │       ▲
         │processed_bookings│       │
         └──────────────────┘┌──────┴──────────┐
                             │  Apache Kafka   │
                             │(:9092 / :29092) │
                             └──────┬──────────┘
                                    │
                             ┌──────┴──────────┐
                             │    Zookeeper    │
                             └─────────────────┘
```

### Компоненты системы

1. **`Users` Service (`services/Users/`)**
   - Управление пользователями, регистрация и аутентификация.
   - Защита: публичная регистрация назначает исключительно роль `User`. Администратор создаётся автоматически при старте (логин `admin`, пароль `AdminPassword123!`, роль `Admin`).
   - Пароли хешируются с использованием алгоритма PBKDF2 со случайной криптографической солью (100 000 итераций) и проверкой за константное время.
   - Генерация криптографически подписанных JWT-токенов с ролями (`User`, `Admin`).
   - Собственная база данных PostgreSQL `users`.

2. **`Events` Service (`services/Events/`)**
   - Каталог мероприятий, расписание, вместимость и учет свободных мест.
   - Токен конкурентности `xmin`: оптимистическая блокировка на уровне PostgreSQL для защиты от гонок между администратором (`PUT /events`) и подписчиком Kafka.
   - Авторизация на основе ролей: создание, изменение и удаление мероприятий разрешено только роли `Admin`.
   - Фоновый сервис `BookingConfirmedConsumerService`:
     - Слушает топики `booking-confirmed` и `booking-cancelled`.
     - При получении `booking-confirmed`: гарантирует идемпотентность через `processed_bookings`. Если мест нет или мероприятие не найдено — публикует `BookingRejected`.
     - При получении `booking-cancelled`: освобождает место (`ReleaseSeats`) и удаляет запись из `processed_bookings`.
     - Обработка ошибок: при сбое `SaveChanges` выполняет `consumer.Seek()` с паузой, предотвращая потерю сообщений.
   - Фоновый сервис `KafkaTopicInitializer`: инициализирует топики `booking-confirmed`, `booking-cancelled`, `booking-rejected` (по 3 партиции) при старте.
   - Собственная база данных PostgreSQL `events`.

3. **`Bookings` Service (`services/Bookings/`)**
   - Регистрация и управление бронированиями мероприятий.
   - Проверка прав: `GET /bookings/{id}` возвращает бронь только её владельцу либо администратору (для чужих пользователей возвращает `403 Forbidden`).
   - Строковый статус бронирования: перечисление `BookingStatus` сериализуется строкой (`Pending`, `Confirmed`, `Rejected`, `Cancelled`).
   - Transactional Outbox:
     - При подтверждении бронирования запись `BookingConfirmed` сохраняется в таблицу `outbox_messages` в рамках одной транзакции с обновлением статуса брони.
     - При отмене бронирования (`DELETE /bookings/{id}`) запись `BookingCancelled` сохраняется в `outbox_messages` в рамках транзакции отмены.
     - Фоновый сервис `OutboxPublisherService` гарантирует доставку неотправленных сообщений даже при перезагрузке брокера.
   - Фоновый сервис `BookingRejectedConsumerService`: слушает топик `booking-rejected` и переводит статус бронирования в `Rejected`, если в `Events` мероприятие не найдено или мест не хватило.
   - Собственная база данных PostgreSQL `bookings`.

4. **`Contracts` Library (`shared/Contracts/`)**
   - Общая сборка моделей сообщений (`BookingConfirmed`, `BookingCancelled`, `BookingRejected`) и констант топиков (`KafkaTopics`).
   - Не содержит бизнес-логики и зависимостей от инфраструктуры.

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

## Потоки данных событий

### 1. Подтверждение бронирования (`BookingConfirmed`)

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

[Bookings Service: BookingProcessingService]
   │
   │ 4. Опрос Pending-бронирований
   │ 5. Транзакция: Status = Confirmed + INSERT в outbox_messages
   ▼
[PostgreSQL: bookings database]
   │
   │ 6. Немедленная отправка или через OutboxPublisherService
   ▼
[Kafka: topic 'booking-confirmed' (Key = EventId)]
   │
   │ 7. Получение сообщения (GroupId = 'events-consumer-group')
   ▼
[Events Service: BookingConfirmedConsumerService]
   │
   │ 8. Проверка идемпотентности по таблице processed_bookings
   │ 9. Если мероприятие найдено и есть места:
   │    - AvailableSeats = AvailableSeats - 1 (с проверкой xmin)
   │    - INSERT в processed_bookings
   │    Иначе:
   │    - Публикация BookingRejected в топик 'booking-rejected'
   ▼
[PostgreSQL: events database]
   │
   │ 10. Фиксация offset (Commit) в Kafka
   ▼
[Kafka Broker]
```

### 2. Отмена бронирования (`BookingCancelled`)

```
[Client]
   │
   │ 1. DELETE /bookings/{id} + JWT
   ▼
[Bookings Service: BookingController]
   │
   │ 2. Транзакция: Status = Cancelled + INSERT в outbox_messages
   ▼
[PostgreSQL: bookings database]
   │
   │ 3. 204 No Content
   ▼
[Client]

──────────────────────── АСИНХРОННЫЙ ПРОЦЕСС ────────────────────────

[Bookings Service: OutboxPublisherService]
   │
   │ 4. Публикация BookingCancelled (Key = EventId)
   ▼
[Kafka: topic 'booking-cancelled']
   │
   │ 5. Получение сообщения
   ▼
[Events Service: BookingConfirmedConsumerService]
   │
   │ 6. AvailableSeats = AvailableSeats + 1
   │ 7. DELETE из processed_bookings
   ▼
[PostgreSQL: events database]
```

### 3. Компенсация при отказе (`BookingRejected`)

```
[Events Service]
   │
   │ Мероприятие не найдено или нет свободных мест
   ▼
[Kafka: topic 'booking-rejected']
   │
   │ Получение сообщения
   ▼
[Bookings Service: BookingRejectedConsumerService]
   │
   │ Status = Rejected, ProcessedAt = UtcNow
   ▼
[PostgreSQL: bookings database]
```

---

## Запуск системы через Docker Compose

Для запуска всех сервисов, баз данных и инфраструктуры Kafka:

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

---

## Запуск тестов

Юнит-тесты покрывают сущности `Booking`, `Event`, логику резервирования и освобождения мест, а также идемпотентность подписчика:

```bash
dotnet test
```

---

## Сквозной сценарий тестирования (End-to-End)

### Шаг 1. Получение токена администратора

Администратор создаётся автоматически при старте сервиса `Users`:

```bash
ADMIN_TOKEN=$(curl -s -X POST http://localhost:5001/auth/login \
  -H "Content-Type: application/json" \
  -d '{"login":"admin","password":"AdminPassword123!"}' | jq -r .token)

echo "Admin Token: $ADMIN_TOKEN"
```

### Шаг 2. Создание мероприятия администратором

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

### Шаг 3. Регистрация обычного пользователя

При публичной регистрации всегда присваивается роль `User`:

```bash
curl -s -X POST http://localhost:5001/auth/register \
  -H "Content-Type: application/json" \
  -d '{"login":"user_alice","password":"UserPassword123!"}'

USER_TOKEN=$(curl -s -X POST http://localhost:5001/auth/login \
  -H "Content-Type: application/json" \
  -d '{"login":"user_alice","password":"UserPassword123!"}' | jq -r .token)

echo "User Token: $USER_TOKEN"
```

### Шаг 4. Создание бронирования пользователем

```bash
BOOKING_RESPONSE=$(curl -s -X POST http://localhost:5003/bookings \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $USER_TOKEN" \
  -d "{\"eventId\":\"$EVENT_ID\"}")

echo "$BOOKING_RESPONSE" | jq .
BOOKING_ID=$(echo "$BOOKING_RESPONSE" | jq -r .id)
```

Ответ сервиса: `202 Accepted`, статус `"Pending"`.

### Шаг 5. Ожидание асинхронной обработки и Kafka

```bash
sleep 4
```

### Шаг 6. Проверка подтверждения бронирования

```bash
curl -s -X GET "http://localhost:5003/bookings/$BOOKING_ID" \
  -H "Authorization: Bearer $USER_TOKEN" | jq .
```
Статус бронирования: `"Confirmed"`.

### Шаг 7. Проверка уменьшения свободных мест

```bash
curl -s -X GET "http://localhost:5002/events/$EVENT_ID" | jq .
```
Поле `availableSeats`: `9` (уменьшилось на 1).

### Шаг 8. Отмена бронирования и возврат места

```bash
curl -s -X DELETE "http://localhost:5003/bookings/$BOOKING_ID" \
  -H "Authorization: Bearer $USER_TOKEN"

sleep 3

curl -s -X GET "http://localhost:5002/events/$EVENT_ID" | jq .
```
Поле `availableSeats`: снова `10` (место успешно возвращено через событие `BookingCancelled`).
