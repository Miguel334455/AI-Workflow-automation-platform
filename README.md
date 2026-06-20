# AI Workflow Automation Platform

A mini n8n/Zapier-style workflow automation platform built as five .NET microservices, communicating asynchronously over RabbitMQ.

## Services

| Service | Port | Responsibility |
|---|---|---|
| Workflow Service | 5001 | CRUD for workflows (nodes + connections), JWT auth/login (dev) |
| Trigger Service | 5002 | Manual / Webhook / Scheduled (cron) triggers; publishes `WorkflowTriggeredEvent` |
| Execution Service | 5003 | Core engine — walks the workflow graph, dispatches nodes, tracks run state |
| AI Task Service | 5004 | Executes `ai` nodes (calls an LLM provider; stub response if no API key set) |
| Notification Service | 5005 | Executes `notification` nodes (Email via MailKit, Slack/Webhook via HTTP) |

## Stack

ASP.NET Core 8 · EF Core (SQL Server) · MassTransit + RabbitMQ · Redis (definition caching) · Docker Compose · Swagger · JWT Bearer auth

## Project Layout

```
WorkflowPlatform/
├── docker-compose.yml
├── scripts/generate-migrations.sh
├── test-flow.http
├── src/
│   ├── Shared/
│   │   ├── Shared.Contracts/   <- event & DTO contracts shared by all services
│   │   └── Shared.Messaging/   <- MassTransit/RabbitMQ + JWT wiring helpers
│   └── Services/
│       ├── WorkflowService/WorkflowService.API
│       ├── TriggerService/TriggerService.API
│       ├── ExecutionService/ExecutionService.API
│       ├── AITaskService/AITaskService.API
│       └── NotificationService/NotificationService.API
```

## How a workflow runs

1. **Define** a workflow in Workflow Service: a list of nodes (`trigger`, `http`, `ai`, `notification`, `condition`) and connections between them (a DAG).
2. **Trigger** it via Trigger Service — manually (`POST /api/triggers/{id}/fire`), via webhook (`POST /api/triggers/webhook/{id}`), or on a cron schedule (Quartz.NET polls every minute).
3. The Trigger Service publishes a `WorkflowTriggeredEvent` to RabbitMQ.
4. **Execution Service** consumes it, fetches the workflow definition (cached in Redis), creates a `WorkflowRun` + `NodeExecution` rows, and walks the graph:
   - `http` / `condition` nodes run synchronously inline.
   - `ai` nodes publish `AITaskRequestedEvent` and pause; **AI Task Service** processes it and publishes `AITaskCompletedEvent` back.
   - `notification` nodes publish `NotificationRequestedEvent` and pause; **Notification Service** sends it and publishes `NotificationCompletedEvent` back.
5. Execution Service resumes on each completion event, continuing to successor nodes until the run reaches a terminal state (`Succeeded`/`Failed`).
6. Query run status any time: `GET /api/runs/{runId}` on Execution Service.

## Running locally

### Prerequisites
- Docker + Docker Compose
- .NET 8 SDK (only needed once, to generate EF Core migrations — see below)

### 1. Generate EF Core migrations (one-time)

Migration files aren't checked into the repo. Run this once with the .NET SDK installed:

```bash
chmod +x scripts/generate-migrations.sh
./scripts/generate-migrations.sh
```

This creates `Migrations/` folders under WorkflowService.API, TriggerService.API, and ExecutionService.API. Each service calls `Database.Migrate()` on startup, so they'll auto-apply inside Docker.

### 2. Start everything

```bash
docker compose up --build
```

This brings up SQL Server, RabbitMQ (management UI at http://localhost:15672, guest/guest), Redis, and all five services.

### 3. Explore the APIs

Swagger UI is enabled on every service:
- Workflow Service: http://localhost:5001/swagger
- Trigger Service: http://localhost:5002/swagger
- Execution Service: http://localhost:5003/swagger
- AI Task Service: http://localhost:5004/swagger
- Notification Service: http://localhost:5005/swagger

### 4. Try the end-to-end flow

Open `test-flow.http` (works with the VS Code "REST Client" extension) and run requests top to bottom: log in → create workflow → activate → create trigger → fire trigger → check run status.

## Node types supported out of the box

| Type | Behavior | Config JSON shape |
|---|---|---|
| `trigger` | Marks workflow entry point; doesn't execute | `{}` |
| `http` | Calls an external URL synchronously | `{"method":"GET\|POST","url":"...","body":"..."}` |
| `condition` | Branches to `true`/`false` connections | `{"expression":"..."}` (placeholder evaluator — swap in NCalc for real logic) |
| `ai` | Dispatches to AI Task Service | `{"taskType":"summarize\|classify\|generate","prompt":"..."}` |
| `notification` | Dispatches to Notification Service | `{"channel":"Email\|Slack\|Webhook","target":"...","subject":"...","body":"..."}` |

## Configuration notes

- **JWT signing key** is shared across all services via `Jwt:SigningKey` (set identically in each `appsettings.json` / compose env vars). The Workflow Service's `/api/auth/login` endpoint is a **dev-only stub** — swap for a real identity provider before production.
- **AI provider**: AI Task Service falls back to a stub response if `AiProvider:ApiKey` is empty, so the whole pipeline runs without external dependencies. Set the key + adjust `AiClient.cs` request/response shape for your provider (OpenAI-compatible by default).
- **Email**: Notification Service logs instead of sending if `Smtp:Host` is empty — same dev-friendly fallback pattern.
- **Webhook triggers** are currently unauthenticated (`AllowAnonymous`) — add HMAC signature validation against a per-trigger secret before exposing publicly.

## What to build next

- API Gateway (YARP) to unify the 5 ports behind one entrypoint and aggregate Swagger docs
- Real expression evaluator for `condition` nodes (NCalc, JSONPath)
- Node retry policies / dead-letter handling on RabbitMQ queues
- A frontend workflow builder (drag-and-drop nodes/connections)
- Replace the dev login with real identity (IdentityServer / Entra ID / Auth0)
