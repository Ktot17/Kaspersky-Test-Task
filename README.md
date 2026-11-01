# ЧикинП.Е._Тестовое задание_DevC#

REST API для управления уязвимостями и фонового сканирования репозиториев.

## Запуск

Нужен .NET 10 SDK.

```bash
dotnet run --project WebComponent
```

Сервис поднимется на `http://localhost:5064`. В логах будет строка `Vulnerability Tracker API starting. MaxParallelScans=N`.

Тесты:

```bash
dotnet test
```

## Конфиг

`WebComponent/appsettings.json`, секция `Scanning`:

```json
{
  "Scanning": {
    "MaxParallelScans": 3,
    "QueueCapacity": 1000
  }
}
```

- `MaxParallelScans` — сколько сканирований идёт одновременно, остальные в очереди со статусом `Queued`. Читается на старте, менять на лету нельзя.
- `QueueCapacity` — размер очереди. Если переполнится — `POST /scans` отдаст `503`, задача станет `Failed`.

## Структура проекта

```
BLComponent         — домен: менеджеры, ScanStateMachine, ScanWorker, StubScanner,
                      модели, интерфейсы (Abstractions), исключения
DBComponent         — in-memory реализации: репозитории, очередь, реестр CTS
WebComponent        — контроллеры, DTO, ExceptionHandlers, Program.cs, appsettings.json
BLComponent.Tests   — тесты BL
DBComponent.Tests   — тесты DB
```

Зависимости: `Web → BL ← DB`. BL не знает про Web и DB.

## Эндпоинты

Все ошибки — `ProblemDetails` (RFC 7807). Enum'ы сериализуются строками (`"High"`, `"Queued"`).

**`POST /api/v1/vulnerabilities`** — добавить уязвимость.

```bash
curl -X POST http://localhost:5064/api/v1/vulnerabilities \
  -H "Content-Type: application/json" \
  -d '{"name": "SQLi", "severity": "High"}'
```

`201` + `Location` + `{ "id": "..." }`. `400` при невалидном имени/severity. Дата создания ставится сервером.

**`GET /api/v1/vulnerabilities/{id}`** — `200` с полным объектом или `404`.

**`POST /api/v1/scans`** — запустить сканирование.

```bash
curl -X POST http://localhost:5064/api/v1/scans \
  -H "Content-Type: application/json" \
  -d '{"repositoryUrl": "https://gitlab.com/example/repo.git"}'
```

`202` + `Location` + `{ "id": "..." }`. `400` если URL не абсолютный http/https. `503` если очередь переполнена.

**`GET /api/v1/scans/{id}`** — статус.

```json
{
  "id": "...",
  "status": "Completed",
  "lastUpdatedAt": "2026-01-01T12:00:04+00:00",
  "result": { "findings": [{ "name": "...", "severity": "High" }] },
  "errorMessage": null
}
```

`status` — `Queued` / `Running` / `Completed` / `Failed` / `Cancelled`. `result` только при `Completed`, `errorMessage` только при `Failed`. `404` если нет.

**`POST /api/v1/scans/{id}/cancel`** — отменить.

```bash
curl -X POST http://localhost:5064/api/v1/scans/{id}/cancel
```

```json
{
  "id": "...",
  "success": true,
  "cancelledAt": "2026-01-01T12:00:02+00:00",
  "reason": null
}
```

Если задача уже в терминальном статусе — `200` с `success: false` и `reason`. `404` если нет. Работает для `Queued` и `Running`.

## Ограничения

- Нет персистентности — in-memory, по условию.
- `StubScanner` не сканирует по-настоящему, возвращает пустой массив через `Task.Delay`. Реальный сканер — отдельная реализация `IScanner`, регистрируется в `Program.cs` вместо `StubScanner`.
- `MaxParallelScans` читается на старте, менять без рестарта нельзя.
