# Импорт нового инструмента и истории цен

Администратор создает асинхронную заявку:

```http
POST /api/market-data/imports
Idempotency-Key: optional-client-key
Content-Type: application/json

{
  "source": "MOEX",
  "isin": "RU000A10FTR1",
  "fromDate": "2026-08-06"
}
```

Поддерживаемые источники: `MOEX`, `TBANK`. ISIN проверяется вместе с контрольной цифрой. Дата не может быть в будущем.

Если инструмент с таким ISIN уже существует, API сразу возвращает `200 OK` со статусом `SkippedExisting`. Outbox-сообщение не создается, RabbitMQ и внешний источник не вызываются. Если инструмента нет, API атомарно сохраняет заявку и outbox-сообщение и возвращает `202 Accepted`.

Статус доступен через:

```http
GET /api/market-data/imports/{requestId}
```

Возможные статусы: `Queued`, `ResolvingInstrument`, `LoadingPrices`, `Succeeded`, `SkippedExisting`, `Failed`.

## Обработка

`Larchik.Jobs` публикует transactional outbox в durable RabbitMQ exchange. Используются quorum-очереди:

- `larchik.market-data.import` — основная очередь с single active consumer;
- `larchik.market-data.import.retry` — задержка перед повторной попыткой;
- `larchik.market-data.import.dead` — окончательно неуспешные и некорректные сообщения.

Воркeр повторно проверяет ISIN до первого запроса во внешний источник. Это защищает от ситуации, когда инструмент появился между HTTP-запросом и обработкой очереди. После разрешения инструмента воркер заполняет только существующие поля `instruments`; специфичный маршрут MOEX (`SECID`, board, engine, market) хранится в технической таблице заявки. Новые поля в `instruments` не добавляются.

История загружается чанками по `MarketDataImports:ChunkDays`. Повторная доставка безопасна: цены обновляются по существующему ключу, а завершенная заявка повторно не обрабатывается.

## Конфигурация

Основные секции находятся в `src/Larchik.Jobs/appsettings.json`:

- `RabbitMq` — соединение, имена очередей, retry delay, размер outbox batch;
- `MarketDataImports` — размер чанка, число попыток, категории создаваемых инструментов;
- `MarketDataImportSources` — URL MOEX и T-Bank, токен T-Bank.

Production compose передает `RABBITMQ_USER`, `RABBITMQ_PASSWORD` и `TBANK_TOKEN` через переменные окружения.
