# EventExamProject
Сервис для управления мероприятиями на ASP.NET Core Web API

## Требования

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Сборка

Из папки проекта (`EventExamProject/`):
```bash
dotnet build
```

## Запуск

Из папки проекта (`EventExamProject/`):
```bash
dotnet run
```

Сервис запустится на `http://localhost:5278` (порт можно изменить в `Properties/launchSettings.json`).

## Swagger UI

После запуска документация и интерактивное тестирование API доступны по адресу:

```
http://localhost:5278/swagger/index.html
```

## API

### GET /events
Получить список всех мероприятий.

### GET /events/{id}
Получить мероприятие по идентификатору.

Возвращает `404 Not Found` если не найдено.

### POST /events
Создать новое мероприятие.

Возвращает `201 Created` с созданным объектом.

**Тело запроса:**
```json
{
  "title": "Название мероприятия",
  "description": "Описание (необязательно)",
  "startAt": "2026-07-01T10:00:00",
  "endAt": "2026-07-01T12:00:00"
}
```

### PUT /events/{id}
Обновить мероприятие целиком.

Возвращает `404 Not Found` если не найдено.

**Тело запроса:** аналогично `POST`.

### DELETE /events/{id}
Удалить мероприятие.

Возвращает `204 No Content` при успехе, `404 Not Found` если не найдено.

## Валидация

- `Title`, `StartAt`, `EndAt` — обязательные поля
- `EndAt` должен быть позже `StartAt`
