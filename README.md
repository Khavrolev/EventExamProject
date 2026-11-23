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
└────────┬─────────┘ └────┬──────▲─┬────┘ └────────┬─────────┘
         │                │      │ │               │
         │ PostgreSQL     │      │ └──┐ Redis      │ PostgreSQL
         ▼ (:5455)        │      │    ▼ (:6379)    ▼ (:5456)
┌──────────────────┐      │      │ ┌──────────────┐┌──────────────────┐
│  users database  │      │      │ │ Redis Cache  ││bookings database │
└──────────────────┘      │      │ └──────────────┘│(bookings +       │
               PostgreSQL │      │ Kafka Topics:   │ outbox_messages) │
               (:5454)    │      │ - booking-      └──────────────────┘
         ┌────────────────┴─┐    │   confirmed     ▲
         │ events database  │────┼─────────────────┘
         │ (events +        │    │ - booking-
         │processed_bookings│    │   cancelled
         └──────────────────┘┌───┴──────────────┐
                             │  Apache Kafka    │
                             │ (:9092 / :29092) │
                             └───────┬──────────┘
                                     │
                             ┌───────┴──────────┐
                             │    Zookeeper     │
                             └──────────────────┘
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
   - Распределённый кеш Redis (`StackExchange.Redis`):
     - Кеширование отдельного мероприятия по ключу `event:{id}` (стратегия **обновление при записи**).
     - Кеширование виджета «Топ-10 самых популярных событий» по ключу `events:top10` (по TTL).
     - Отказоустойчивость: деградация в PostgreSQL без падения сервиса при недоступности Redis.
   - Токен конкурентности `xmin`: оптимистическая блокировка на уровне PostgreSQL для защиты от гонок между администратором (`PUT /events`) и подписчиком Kafka.
   - Авторизация на основе ролей: создание, изменение и удаление мероприятий разрешено только роли `Admin`.
   - Фоновый сервис `BookingConfirmedConsumerService`:
     - Слушает топики `booking-confirmed` и `booking-cancelled`.
     - При получении `booking-confirmed`: гарантирует идемпотентность через `processed_bookings`. Если мест нет или мероприятие не найдено — публикует `BookingRejected`. При успешном резервировании сохраняет БД и обновляет кеш `event:{id}`.
     - При получении `booking-cancelled`: освобождает место (`ReleaseSeats`), удаляет запись из `processed_bookings`, сохраняет БД и обновляет кеш `event:{id}`.
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
| Redis Cache | `6379` | `6379` | Распределённый кеш сервиса Events |
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

## Стратегия кеширования данных (Redis Cache)

В сервисе `Events` внедрено распределённое кеширование на базе **Redis** (библиотека `StackExchange.Redis`), существенно снижающее нагрузку на реляционную базу данных PostgreSQL при частом чтении.

### 1. Архитектурная изоляция и абстракция кеша
- **Абстракция в слое Application**: интерфейс [`ICacheService`](file:///Users/khavrolev/projects/practicum/services/Events/Events.Application/Abstractions/ICacheService.cs) расположен в слое `Events.Application.Abstractions`. Слой приложения и доменная модель ничего не знают о библиотеке `StackExchange.Redis` или деталях сетевого протокола.
- **Реализация в слое Infrastructure**: класс [`RedisCacheService`](file:///Users/khavrolev/projects/practicum/services/Events/Events.Infrastructure/Caching/RedisCacheService.cs) находится в `Events.Infrastructure.Caching`. Он инкапсулирует работу с `IConnectionMultiplexer`, `IDatabase`, JSON-сериализацию (`System.Text.Json`) и установку времени жизни (TTL).
- **Регистрация в DI**: подключение `IConnectionMultiplexer` регистрируется как потокобезопасный **Singleton** с опцией `AbortOnConnectFail = false`.

### 2. Сценарии кеширования: что и почему кешируется

| Сценарий / Ресурс | Ключ кеша | TTL | Стратегия обновления | Назначение и обоснование |
|---|---|---|---|---|
| **Отдельное мероприятие** (`GET /events/{id}`) | `event:{id}` | 10 мин | **Обновление при записи** (Write-Through) | Карточка мероприятия — самый частый запрос. Кеширование снимает нагрузку `SELECT` по ключу с PostgreSQL. |
| **Виджет «Топ-10 популярных событий»** (`GET /events/top`) | `events:top10` | 5 мин | **Исключительно по TTL** | Публичный неавторизованный виджет на главной странице. Расчёт формулы по всей таблице на каждый HTTP-запрос дорог для БД. Небольшое отставание топа некритично. |

#### Обоснование выбора значений TTL:
1. **Мероприятие (`event:{id}`) — TTL 10 минут**:
   - Является страхующим предохранителем (safety net). Даже если произойдёт непредвиденная рассинхронизация или частичный сбой при доставке сообщений, устаревшие данные автоматически вытеснятся из памяти Redis через 10 минут.
   - Защищает память Redis от бесконтрольного разрастания при появлении тысяч неактивных или архивных событий.
2. **Топ-10 (`events:top10`) — TTL 5 минут**:
   - Топ-10 — это рейтинговый агрегат, процент проданных мест $\frac{\text{TotalSeats} - \text{AvailableSeats}}{\text{TotalSeats}}$ меняется постепенно.
   - Инвалидировать или пересчитывать глобальный топ при каждом единичном бронировании или отмене мест в системе нецелесообразно и создало бы чрезмерную нагрузку на CPU/БД. Обновление строго по 5-минутному TTL обеспечивает оптимальный баланс между актуальностью витрины и производительностью.

### 3. Обоснование выбранного подхода: «Обновление при записи» (Write-Through)

Для отдельного мероприятия (`event:{id}`) выбран подход **обновления при записи** вместо простой инвалидации (удаления).

#### Преимущества перед инвалидацией при записи:
- **Отсутствие эффекта холодного кеша (Cache Miss)**: при инвалидации удалённый ключ требует повторного обращения к PostgreSQL при следующем читающем запросе. При обновлении при записи свежие данные уже находятся в оперативной памяти Redis в момент обращения клиента.
- **Защита от «наплыва запросов» (Cache Stampede / Dog-piling)**: если популярное мероприятие активно просматривают сотни пользователей одновременно, удаление ключа из кеша спровоцировало бы всплеск параллельных запросов в БД. Запись обновлённого значения устраняет этот риск.

#### Поведение при изменении данных и строгий порядок операций:
1. **Мутирующие операции REST API (`POST /events`, `PUT /events/{id}`, `DELETE /events/{id}`)**:
   - **Создание** (`POST /events`): сначала `await eventRepository.AddAsync(newEvent)` сохраняет событие в PostgreSQL, после чего объект сразу помещается в кеш `event:{id}` с TTL.
   - **Обновление** (`PUT /events/{id}`): сначала `await eventRepository.UpdateAsync(existingEvent)` фиксирует изменения в PostgreSQL. Строго после успешного сохранения обновлённый `EventInfoDto` перезаписывается в кеш `event:{id}` с TTL.
   - **Удаление** (`DELETE /events/{id}`): сначала `await eventRepository.DeleteAsync(eventToDelete)` удаляет запись из PostgreSQL. Строго после этого ключ `event:{id}` удаляется из кеша (`RemoveAsync`).
2. **Асинхронные события бронирования через Kafka (`BookingConfirmedConsumerService`)**:
   - При получении сообщения **`booking-confirmed`**: сервис уменьшает `AvailableSeats`, выполняет `context.SaveChanges()` в БД, и **только после фиксации в PostgreSQL** обновляет кеш `event:{id}` актуальным числом оставшихся мест.
   - При получении сообщения **`booking-cancelled`**: сервис освобождает места (`ReleaseSeats`), сохраняет изменения в БД через `context.SaveChanges()`, и **только после этого** обновляет кеш `event:{id}`.

#### Почему порядок операций критически важен:
> [!IMPORTANT]
> **Сначала PostgreSQL — потом Redis Cache.**
> База данных является единственным источником истины (Source of Truth). Если операция записи в БД завершится ошибкой (например, сбой соединения или конфликт конкурентности `xmin`), кеш не будет затронут и в нём не появятся фантомные или невалидные данные. Если же оборвётся запись в кеш после успешного сохранения в БД, данные в базе останутся корректными, а кеш деградирует или обновится по TTL / при следующем событии.

### 4. Устойчивость к сбоям и плавная деградация (Fault Tolerance)
- **Изоляция ошибок**: все методы работы с Redis в `RedisCacheService` заключены в блоки `try-catch`.
- **Логирование без падения**: если контейнер Redis недоступен, упал, или сетевой запрос завершился по таймауту, исключение перехватывается и логируется (`ILogger.LogError`). Ошибка **не пробрасывается** клиенту.
- **Плавная деградация (Graceful Degradation)**: при ошибке чтения метод `GetAsync` возвращает `null`. Для сервиса это выглядит как штатный промах кеша (`Cache Miss`), запрос прозрачно перенаправляется напрямую в PostgreSQL, и пользователь получает актуальный ответ `200 OK`.
- **Автономный запуск сервиса**: благодаря параметру `AbortOnConnectFail = false` сервис `Events` успешно запускается и готов обслуживать HTTP-запросы даже в сценарии, когда Redis полностью отключён.

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

Юнит-тесты покрывают сущности `Booking`, `Event`, логику резервирования и освобождения мест, идемпотентность подписчика, а также все сценарии кеширования в Redis (Cache Hit, Cache Miss, Write-Through обновление, инвалидацию и порядок операций):

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

### Шаг 9. Проверка виджета «Топ-10 популярных событий» и кеша Redis

Запрос списка топ-10 мероприятий (без авторизации):

```bash
curl -s -X GET "http://localhost:5002/events/top" | jq .
```

Проверка наличия ключей и TTL в Redis контейнере:

```bash
# Список ключей в кеше
docker exec eventapi-redis redis-cli keys "*"

# Проверка значения и TTL кеша мероприятия (10 мин = ~600с)
docker exec eventapi-redis redis-cli get "event:$EVENT_ID"
docker exec eventapi-redis redis-cli ttl "event:$EVENT_ID"

# Проверка TTL виджета топ-10 (5 мин = ~300с)
docker exec eventapi-redis redis-cli ttl "events:top10"
```

### Шаг 10. Проверка устойчивости (Graceful Degradation при недоступном Redis)

Сервис прозрачно переключается на базу данных при сбое Redis без ошибок для клиента:

```bash
# Временная остановка Redis
docker stop eventapi-redis

# Запрос выполняется успешно из PostgreSQL (200 OK)
curl -s -w "\nHTTP_STATUS: %{http_code}\n" "http://localhost:5002/events/$EVENT_ID" | jq .

# Запуск Redis обратно
docker start eventapi-redis
```
