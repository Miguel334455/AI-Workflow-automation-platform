# AI Workflow Automation Platform

A lightweight, self-hosted workflow automation engine — a mini n8n/Zapier — built with .NET microservices, RabbitMQ, and Docker.

---

## What it does

Define workflows as a graph of nodes, connect them, fire a trigger, and the platform handles the rest — calling APIs, running AI tasks, sending notifications, and evaluating conditions — all tracked and resumable.

```
Trigger → HTTP Call → AI Summarize → Condition → Email Notification
```

---

## Services

| Service | Port | Responsibility |
|---|---|---|
| Workflow Service | 5001 | Create and manage workflow definitions |
| Trigger Service | 5002 | Manual, webhook, and scheduled (cron) triggers |
| Execution Service | 5003 | Core engine — walks the node graph, tracks run state |
| AI Task Service | 5004 | Executes AI nodes via an LLM provider |
| Notification Service | 5005 | Sends emails, Slack messages, and webhooks |

---

## Tech Stack

- **ASP.NET Core 8** — all five services
- **Entity Framework Core + SQL Server** — per-service databases
- **MassTransit + RabbitMQ** — async event-driven communication
- **Redis** — workflow definition caching
- **Quartz.NET** — cron-based scheduled triggers
- **JWT Bearer Auth** — shared across all services
- **Swagger** — auto-generated API docs on every service
- **Docker Compose** — one command to run the whole platform

---

## Node Types

| Type | What it does | Config |
|---|---|---|
| `trigger` | Marks the workflow entry point | `{}` |
| `http` | Calls an external URL | `{ "method": "POST", "url": "...", "body": "..." }` |
| `condition` | Branches on true/false | `{ "expression": "..." }` |
| `ai` | Runs an AI task (summarize, classify, generate) | `{ "taskType": "summarize", "prompt": "..." }` |
| `notification` | Sends email, Slack, or webhook | `{ "channel": "Email", "target": "...", "subject": "...", "body": "..." }` |

---

## Getting Started

### Prerequisites

- [Docker + Docker Compose](https://docs.docker.com/get-docker/)
- [.NET 8 SDK](https://dotnet.microsoft.com/download) — needed once to generate migrations

### 1. Generate database migrations (one-time)

```bash
chmod +x scripts/generate-migrations.sh
./scripts/generate-migrations.sh
```

### 2. Start everything

```bash
docker compose up --build
```

This brings up SQL Server, RabbitMQ, Redis, and all five services. Migrations apply automatically on startup.

### 3. Open Swagger

| Service | Swagger UI |
|---|---|
| Workflow Service | http://localhost:5001/swagger |
| Trigger Service | http://localhost:5002/swagger |
| Execution Service | http://localhost:5003/swagger |
| AI Task Service | http://localhost:5004/swagger |
| Notification Service | http://localhost:5005/swagger |

RabbitMQ management UI: http://localhost:15672 (guest / guest)

---

## Running a workflow end-to-end

Open `test-flow.http` with the VS Code [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension and run the requests top to bottom:

1. **Login** — get a JWT token from the dev auth endpoint
2. **Create a workflow** — define nodes and connections
3. **Activate it** — enable the workflow for execution
4. **Create a trigger** — attach a manual trigger to the workflow
5. **Fire the trigger** — start a run
6. **Check run status** — poll the Execution Service for live node-by-node results

---

## How execution works

```
Trigger fires
     │
     ▼
WorkflowTriggeredEvent ──► RabbitMQ
                                │
                                ▼
                      Execution Service
                      fetches definition
                      (cached in Redis)
                            │
               ┌────────────┼────────────┐
               ▼            ▼            ▼
          http node    condition     ai node ──► AITaskRequestedEvent
          (inline)      (inline)         │              │
                                         │    AI Task Service
                                         │              │
                                         ◄── AITaskCompletedEvent
                                         │
                                    continue graph
                                         │
                                         ▼
                                  notification node ──► NotificationRequestedEvent
                                                               │
                                                    Notification Service
                                                               │
                                                    NotificationCompletedEvent
                                                               │
                                                         run complete ✓
```

---

## Configuration

All config lives in each service's `appsettings.json` and can be overridden via Docker Compose environment variables.

**JWT** — all services share the same signing key (`Jwt__SigningKey`). Change it from the default before deploying anywhere.

**AI provider** — set `AiProvider__ApiKey` in the AI Task Service. Defaults to a stub response (no external call) if left empty, so the full pipeline runs in dev without an API key.

**Email** — set `Smtp__Host` in the Notification Service. Logs to console instead of sending if left empty.

**Webhook triggers** — currently unauthenticated. Add HMAC signature validation before exposing publicly.

---

## Project Structure

```
WorkflowPlatform/
├── docker-compose.yml
├── test-flow.http
├── scripts/
│   └── generate-migrations.sh
└── src/
    ├── Shared/
    │   ├── Shared.Contracts/       # Event records and DTOs shared by all services
    │   └── Shared.Messaging/       # MassTransit + RabbitMQ + JWT wiring helpers
    └── Services/
        ├── WorkflowService/
        ├── TriggerService/
        ├── ExecutionService/
        ├── AITaskService/
        └── NotificationService/
```

---

## What to build next

- **API Gateway** with YARP — unified entrypoint and aggregated Swagger
- **Real condition evaluation** — replace the placeholder with NCalc or JSONPath
- **Retry policies** — dead-letter queues and node-level retry config
- **Workflow versioning** — run a specific version of a definition
- **Frontend builder** — drag-and-drop visual workflow editor
- **Real identity provider** — swap the dev login stub for Auth0, Entra ID, or IdentityServer

---

## License

MIT