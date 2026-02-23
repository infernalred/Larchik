![example workflow](https://github.com/infernalred/Larchik/actions/workflows/larchik_workflow.yml/badge.svg)

# Larchik

Larchik is an ASP.NET Core (`net10.0`) portfolio accounting API with CQRS, EF Core + PostgreSQL, portfolio valuation strategies, market prices, FX rates, and broker report import.

## Stack

- ASP.NET Core Web API + Identity (cookie auth)
- MediatR + FluentValidation
- EF Core 10 + PostgreSQL
- Serilog
- NSwag (`/swagger` in Development)
- React + TypeScript client scaffold in `src/larchik_client/`

## Repository structure

- `/Users/alex/Repos/Larchik/src/Larchik.API` - API host, controllers, middleware, DI wiring
- `/Users/alex/Repos/Larchik/src/Larchik.Application` - application layer (CQRS handlers, DTOs, validation, business logic)
- `/Users/alex/Repos/Larchik/src/Larchik.Persistence` - EF Core context, entities, configurations, migrations
- `/Users/alex/Repos/Larchik/src/Larchik.Infrastructure` - cross-cutting services
- `/Users/alex/Repos/Larchik/src/Larchik.Jobs` - background jobs host (scheduler/executor)
- `/Users/alex/Repos/Larchik/src/larchik_client` - React client
- `/Users/alex/Repos/Larchik/Tests` - test projects placeholder

## Prerequisites

- .NET SDK 10
- PostgreSQL 14+
- trusted local HTTPS dev certificate (for secure auth/antiforgery cookies)

Trust dev cert if needed:

```bash
dotnet dev-certs https --trust
```

## Configuration for local run

Main local config file:

- `/Users/alex/Repos/Larchik/src/Larchik.API/appsettings.Development.json`
- `/Users/alex/Repos/Larchik/src/Larchik.Jobs/appsettings.Development.json` (jobs host)

Required keys:

- `ConnectionStrings:DefaultConnection` - PostgreSQL connection string
- `Cors:Origins` - comma-separated allowed frontend origins
- `Frontend:BaseUrl` - used in email confirmation/reset links
- `Admin:Email`, `Admin:Password` - optional admin seeding on startup
- `BackgroundJobs:*` - jobs scheduler/executor settings in `Larchik.Jobs`

`TokenKey`/`DaysTokenLife` are currently not used in the codebase.

## Run backend locally

1. Restore/build:

```bash
dotnet restore Larchik.sln
dotnet build Larchik.sln
```

2. Apply migrations:

```bash
dotnet ef database update --project src/Larchik.Persistence --startup-project src/Larchik.API
```

3. Run API:

```bash
dotnet run --project src/Larchik.API
```

4. Run jobs host (optional locally, required in environments where scheduled jobs are expected):

```bash
dotnet run --project src/Larchik.Jobs
```

5. Open Swagger (Development):

- [https://localhost:6001/swagger](https://localhost:6001/swagger)

## Run frontend locally (optional)

```bash
cd src/larchik_client
npm install
npm run dev
```

Default Vite URL `http://localhost:5173` is included in development CORS config.

## Useful API endpoints

- `GET /api/account/antiforgery` - get XSRF token
- `POST /api/account/register`
- `POST /api/account/login`
- `POST /api/portfolios/{id}/imports/tbank` - import broker report
- `POST /api/prices/sync` - sync prices (admin)
- `POST /api/fxrates/sync/cbr?date=YYYY-MM-DD` - sync CBR FX rates (admin)
- `GET /api/portfolios/{id}/summary?method=adjustingAvg|staticAvg|fifo|lifo`

Scheduled jobs in `Larchik.Jobs`:
- `fx_cbr_daily` (`fx.cbr.daily`) - daily FX import from CBR
- `moex_prices_daily` (`prices.moex.daily`) - daily prices import from public MOEX ISS (no API key)

Operation types include bond redemption flows:

- `BondPartialRedemption` - partial redemption of nominal
- `BondMaturity` - full bond maturity
- `Split` - stock split (use `Quantity` as split factor, e.g. `10` for `1:10`)
- `ReverseSplit` - reverse split (use `Quantity` as factor, e.g. `0.1` for `10:1`)

## Notes

- On startup API runs EF migrations automatically and seeds roles.
- If `Admin` credentials are configured, admin user is created/updated automatically.
- Background jobs are DB-backed (`job_definitions`, `job_runs`) with dedup keys, retries, and lock timeout recovery, and now run in `Larchik.Jobs` as a separate process.
