# Larchik Deploy (Traefik + Docker + GitHub Actions)

## Что поднимается
- `larchik-api` (ASP.NET Core API)
- `larchik-web` (Caddy + статический frontend)
- `larchik-jobs` (отдельный background jobs host)
- `larchik-db` (PostgreSQL, внешний порт `5434`)
- `${APP_NAME}-loki` и `${APP_NAME}-grafana` (observability-стек проекта)

## Где лежат конфиги
- Workflow: [deploy.yml](/Users/alex/Repos/Larchik/.github/workflows/deploy.yml)
- Compose: [docker-compose.prod.yml](/Users/alex/Repos/Larchik/docker/docker-compose.prod.yml)
- API image build: [api.Dockerfile](/Users/alex/Repos/Larchik/docker/api.Dockerfile)
- Jobs image build: [jobs.Dockerfile](/Users/alex/Repos/Larchik/docker/jobs.Dockerfile)
- Web image build: [web.Dockerfile](/Users/alex/Repos/Larchik/docker/web.Dockerfile)
- Loki config: [config.yml](/Users/alex/Repos/Larchik/docker/observability/loki/config.yml)
- Grafana provisioning: [provider.yml](/Users/alex/Repos/Larchik/docker/observability/grafana/provisioning/dashboards/provider.yml)

## Что нужно на сервере заранее
- установлен Docker с `docker compose`
- поднят Traefik с внешней сетью `proxy`
- workflow сам создаст сеть `observability`, если ее еще нет

## GitHub Secrets

Обязательные для deploy workflow:
- `SSH_HOST`
- `SSH_PORT` если не `22`
- `SSH_USER`
- `SSH_PASSWORD`
- `GHCR_USER`
- `GHCR_TOKEN`
- `LARCHIK_WEB_HOST`
- `LARCHIK_GRAFANA_HOST`
- `LARCHIK_GRAFANA_ADMIN_PASSWORD`
- `LARCHIK_DB_USER`
- `LARCHIK_DB_PASSWORD`
- `LARCHIK_DB_NAME`
- `LARCHIK_ADMIN_EMAIL`
- `LARCHIK_ADMIN_PASSWORD`
- `LARCHIK_TBANK_TOKEN`

## Какие runtime переменные попадут в `.env` на сервере

Workflow генерирует `.env` c этими значениями:
- `APP_NAME=larchik`
- `GHCR_REPO=<owner/repo в нижнем регистре>`
- `WEB_HOST=<из секрета LARCHIK_WEB_HOST>`
- `GRAFANA_HOST=<из секрета LARCHIK_GRAFANA_HOST>`
- `GRAFANA_CONTAINER_PORT=3302`
- `DB_HOST_PORT=5434`
- `LOKI_URL=http://larchik-loki:3100`
- `DB_USER`
- `DB_PASSWORD`
- `DB_NAME`
- `ADMIN_EMAIL`
- `ADMIN_PASSWORD`
- `TBANK_TOKEN`
- `GRAFANA_ADMIN_USER=admin`
- `GRAFANA_ADMIN_PASSWORD`

Из них в контейнеры приложения прокидываются:
- `ConnectionStrings__DefaultConnection`
- `Cors__Origins=https://${WEB_HOST}`
- `Frontend__BaseUrl=https://${WEB_HOST}`
- `Admin__Email`
- `Admin__Password`
- `BackgroundJobs__Enabled=true`
- `BackgroundJobs__TbankPricesDaily__Token`
- `BackgroundJobs__TbankInstrumentInfoDaily__Token`
- `Serilog__WriteTo__1__Args__uri=http://larchik-loki:3100`

## Что делает workflow
1. Собирает и пушит образы в GHCR:
- `ghcr.io/<owner>/<repo>/larchik-api:latest`
- `ghcr.io/<owner>/<repo>/larchik-jobs:latest`
- `ghcr.io/<owner>/<repo>/larchik-web:latest`
2. Копирует `docker-compose` и `observability`-конфиги на сервер в `/opt/apps/larchik`.
3. Создает docker-сети `proxy` и `observability`, если их нет.
4. Генерирует `.env` с переменными приложения.
5. Выполняет `docker compose pull` и `docker compose up -d`.
6. После старта контейнеров выполняет только обычный `docker compose up -d`, без принудительного `grafana cli admin reset-admin-password`.

## Маршрутизация
- frontend: `https://${WEB_HOST}`
- API: `https://${WEB_HOST}/api`
- Grafana: `https://${GRAFANA_HOST}`

## Frontend API base
- Frontend настроен так же, как в `Sportivity`: `VITE_API_BASE` по умолчанию равен `/api`.
- Поэтому endpoint paths в клиенте идут без префикса `/api`, а итоговый URL собирается как `${VITE_API_BASE}/...`.

## Проверка после деплоя
На сервере:

```bash
cd /opt/apps/larchik
docker compose -f docker-compose.prod.yml --env-file .env ps
docker ps | grep -E 'larchik|grafana|loki'
```

Проверить API:

```bash
curl -I https://<LARCHIK_WEB_HOST>/api/account/antiforgery
```

Проверить jobs:

```bash
docker logs --tail 200 larchik-jobs
```

Проверить Grafana:

```bash
docker logs --tail 200 larchik-grafana
```

## Замечания
- Production логирование сейчас уходит в файл и Loki, без console sink.
- Если `LARCHIK_TBANK_TOKEN` пустой, T-Bank jobs будут стартовать, но запросы к T-Bank API не пройдут.
- Если внешний доступ к PostgreSQL не нужен, можно убрать публикацию порта `5434` из compose.
- `GF_SECURITY_ADMIN_PASSWORD` в Grafana применяется надежно на первом старте с новым volume. На уже существующем `grafana-data-larchik` пароль автоматически не пересинхронизируется.
- Если нужно сменить пароль на уже развернутой Grafana, делай это отдельно: либо вручную через `grafana cli`, когда контейнер остановлен, либо через UI/API, либо пересозданием Grafana volume.
