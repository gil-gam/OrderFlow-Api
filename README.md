# OrderFlow

![.NET](https://img.shields.io/badge/.NET_10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-316192?style=for-the-badge&logo=postgresql&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=for-the-badge&logo=docker&logoColor=white)
[![CI](https://img.shields.io/github/actions/workflow/status/gil-gam/OrderFlow-Api/ci.yml?branch=main&style=for-the-badge&logo=githubactions&label=CI)](https://github.com/gil-gam/OrderFlow-Api/actions)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

**OrderFlow** is a production-grade order management system built with **Clean Architecture**, **CQRS**, and **test-first practices** on **.NET 10**. Designed for maintainability, scalability, and full observability.

---

## 📋 Table of Contents

- [Architecture Diagram](#architecture-diagram)
- [Quick Start](#quick-start)
- [For New Developers](#for-new-developers)
- [Implementation rules](#implementation-rules)
- [Architecture Decisions](#architecture-decisions)
- [Prerequisites](#prerequisites)
- [Docker](#docker)
- [Environment Variables](#environment-variables)
- [API Endpoints](#api-endpoints)
- [Testing](#testing)
- [Tech Stack](#tech-stack)
- [CI/CD](#cicd)
- [Project Structure](#project-structure)

---

## Architecture Diagram

```mermaid
graph TB
    subgraph Clients["HTTP Clients"]
        Web["Web Apps<br/>(SPA, React, etc.)"]
        Tools["curl / Postman<br/>(API consumers)"]
    end

    subgraph API["API Layer (OrderFlow.Api)"]
        Controllers["ASP.NET Core Controllers<br/>Categories, Customers, Products, Orders, Auth"]
        Auth["JWT Bearer Authentication"]
        Middleware["Middleware Pipeline<br/>Rate Limiting, OpenTelemetry, Serilog"]
        Swagger["Swagger / OpenAPI"]
        Versioning["API Versioning"]
    end

    subgraph Application["Application Layer (OrderFlow.Application)"]
        CQRS["CQRS with MediatR<br/>Commands, Queries, Handlers"]
        DTOs["DTOs and Mapping Profiles"]
        Events["Domain Events<br/>e.g. OrderCreatedEvent"]
        Pipeline["Pipeline Behaviors<br/>Logging, Validation, Transactions"]
    end

    subgraph Domain["Domain Layer (OrderFlow.Domain)"]
        Entities["Entities<br/>Order, Customer, Product, Category, User"]
        ValueObjects["Value Objects<br/>Money, Address"]
        Interfaces["Repository Interfaces"]
        Rules["Business Rules and Enums"]
    end

    subgraph Infrastructure["Infrastructure Layer (OrderFlow.Infrastructure)"]
        EF["Entity Framework Core<br/>DbContext, Migrations"]
        Repos["Repository Implementations"]
        Config["Entity Configurations<br/>Fluent API"]
        DI["Dependency Injection Registration"]
    end

    subgraph External["External Systems"]
        DB[("PostgreSQL 16<br/>Database")]
        Monitoring["Prometheus / Grafana<br/>Observability"]
    end

    subgraph DevOps["CI/CD & Testing"]
        GH["GitHub Actions<br/>CI Pipeline"]
        Docker["Docker / Docker Compose"]
        Tests["xUnit + Testcontainers<br/>108 Unit + 18 Integration"]
    end

    Clients -->|HTTPS| API
    API --> Application
    Application --> Domain
    Infrastructure --> Domain
    Application --> Infrastructure
    Infrastructure --> DB
    API --> Monitoring
    GH --> Docker
    Docker --> API
    Docker --> DB
 ```

## Quick Start
```bash
# 1. Clone
git clone https://github.com/gil-gam/OrderFlow-Api.git
cd OrderFlow-Api

# 2. Start PostgreSQL
docker compose up -d postgres

# 3. Apply migrations
dotnet ef database update --project src/OrderFlow.Infrastructure --startup-project src/OrderFlow.Api

# 4. Run the API
dotnet run --project src/OrderFlow.Api

# 5. Open Swagger

http://localhost:5220/swagger
```


## For New Developers

How to implement a new feature
The development flow follows Clean Architecture + CQRS. Here's the step-by-step to add a new feature:

```bash 
1. Domain Layer
   ├── Create the entity (e.g. `Product.cs`)
   ├── Create value objects if needed (e.g. `Money.cs`)
   ├── Create the repository interface (e.g. `IProductRepository.cs`)
   └── Add enums or business rules

2. Application Layer
   ├── Create the Command/Query (e.g. `CreateProductCommand.cs`)
   ├── Create the Handler (e.g. `CreateProductCommandHandler.cs`)
   ├── Create the DTO (e.g. `ProductDto.cs`)
   ├── Create the mapping profile (e.g. `ProductMapping.cs`)
   └── Create Events if needed (e.g. `ProductCreatedEvent.cs`)

3. Infrastructure Layer
   ├── Implement the repository (e.g. `ProductRepository.cs`)
   ├── Add EF Core configuration (e.g. `ProductConfiguration.cs`)
   └── Register dependencies in `DependencyInjection.cs`

4. API Layer
   ├── Create the Controller (e.g. `ProductsController.cs`)
   ├── Add Swagger annotations
   └── Map the routes

5. Tests
   ├── Write unit tests in OrderFlow.

UnitTests
   └── Write integration tests in OrderFlow.

IntegrationTests
```

## Implementation rules
- **Domain** never depends on any other layer
- **Application** depends only on Domain
- **Infrastructure** implements Domain interfaces
- **API** coordinates layers and contains zero business logic
- Commands **never return query data**
- Queries **never modify state**
- Unit tests test **business rules**
- Integration tests test **the full pipeline with a real database**

## Architecture Decisions

### Why Clean Architecture?
| Decision | Rationale |
| ------------- | ------ |
|Domain isolated at the center   | Business rules don't depend on databases, frameworks, or external services. Swapping EF Core for Dapper or PostgreSQL for SQL Server won't touch business logic. |
|Dependency Inversion         | Domain defines interfaces (repositories, services). Infrastructure implements them. Application orchestrates using abstractions. |
| Testability   | Each layer is testable in isolation. Domain has zero external dependencies. |


### Why CQRS with MediatR?
| Decision | Rationale |
| ------------- | ------ |
| Separation of reads and writes   | Queries never modify state. Commands never return query data. This prevents side-effect bugs and makes each endpoint's intent explicit. |
| Pipeline behaviors         | MediatR pipelines apply cross-cutting concerns (logging, validation, transactions) to all commands/queries without duplicated code. |
| Event-driven extensibility   | Domain events (OrderCreatedEvent) decouple side effects. Adding email notifications, invoice generation, or audit logging means adding a new event handler — zero changes to existing code. |


### Why EF Core + PostgreSQL?
| Decision | Rationale |
| ------------- | ------ |
| Mature ORM   | EF Core provides change tracking, migrations, compiled queries, and battle-tested PostgreSQL support via Npgsql. |
| Open source + no licensing         | PostgreSQL is free with excellent JSONB support for event sourcing if needed later. |
| Containerized        | Both run in Docker, making CI/CD reproducible. |

### Why Serilog?
- **Structured logging** — JSON-formatted logs searchable in Seq, Elasticsearch, or Grafana Loki
- **Configurable sinks** — Console for dev, File for production, Seq/Elastic for aggregation
- **Enrichment** — Automatically adds correlation IDs, timestamps, and application name to every log entry

### Why JWT Bearer?
- **Stateless authentication** — No server-side sessions. The client sends a signed token with each request.
- **Standard (RFC 7519)** — Widely supported across languages and platforms, essential for frontend/API integration.
- **Granular claims** — Encode user roles, permissions, and expiration without hitting a database.

### Why Testcontainers?
- **Real database testing** — Spins up a disposable PostgreSQL container for integration tests and destroys it afterward. No mocks, no in-memory providers, no flaky tests.
- **CI-friendly** — Works inside GitHub Actions without Docker-in-Docker complexity. The GitHub runner's Docker socket is used directly.

### Why OpenTelemetry?
- **Observability out of the box** — Distributed tracing, metrics, and logging from a single instrumentation library.
- **Vendor-neutral** — Export traces and metrics to Prometheus, Grafana, Datadog, New Relic, or any OpenTelemetry-compatible backend.
- **Runtime metrics** — GC collections, thread pool stats, and CPU usage available at GET /metrics.

## Prerequisites
- .NET 10 SDK
- Docker Desktop
- EF Core CLI (dotnet tool install --global dotnet-ef)

## Docker 
```bash
# Build and run everything (API + PostgreSQL)
docker compose up --build

# Services:
# - API:      http://localhost:7279/swagger
# - PostgreSQL: localhost:5432 (user=orderflow, password=${POSTGRES_PASSWORD}, db=orderflow)

## Reset Database

Data persists in the named Docker volume `postgres_data` across runs.
To start fresh:

# Stop containers (keeps data)
docker compose down

# Stop containers AND wipe the database (destructive)
docker compose down -v
```

## Environment Variables 

| Variable | Description | Required | Default | 
| ------------- | ------ | ------ | ------ |
| ConnectionStrings__DefaultConnection   | PostgreSQL connection string | ✅ Yes | Host=localhost;Port=5432;Database=OrderFlowDb;Username=postgres;Password=<set via env var> |
| Jwt__Key         | JWT signing secret key (min 32 chars) | ✅ Yes | (32+ chars, set via env var) |
| Jwt__Issuer   | JWT token issuer | ❌ No | OrderFlow.Api |
| Jwt__Audience        | JWT token audience | ❌ No | OrderFlow.Client |
| ASPNETCORE_ENVIRONMENT         | Runtime environment | ❌ No | Production |



The __ (double underscore) syntax is the .NET convention for mapping environment variables to nested **appsettings.json** keys. **ConnectionStrings__DefaultConnection** maps to **ConnectionStrings:DefaultConnection** in JSON.

**Setting variables in different environments:**

```bash
# Linux / macOS / GitHub Actions
export ConnectionStrings__DefaultConnection="Host=myhost;Port=5432;Database=mydb;..."

# Windows CMD
set ConnectionStrings__DefaultConnection=Host=myhost;Port=5432;Database=mydb;...

# Windows PowerShell
$env:ConnectionStrings__DefaultConnection = "Host=myhost;Port=5432;Database=mydb;..."
```

## API Endpoints

### Auth
| Method | Route | Description | Auth |
| ------------- | ------ |  ------ |  ------ |
| POST   | /api/Auth/register |  Register a new user |  ❌ |
| POST   | /api/Auth/login |  Authenticate and receive JWT |  ❌ |


### Categories
| Method | Route | Description | Auth |
| -------------- | ---------- |  ---------- |  ------- |
| POST   | /api/categories       |  Create a category   |  ✅ |
| GET    | /api/categories       |  List all categories	|  ✅ |
| GET    | /api/categories/{id}  |  Get category by ID  |  ✅ |
| PUT    | /api/categories/{id}  |  Update a category   |  ✅ |
| DELETE | /api/categories/{id}  |  Delete a category   |  ✅ |


### Customers
| Method | Route | Description | Auth |
| -------------- | ---------- |  ---------- |  ------- |
| POST   | /api/customers       |  Create a customer   |  ✅ |
| GET    | /api/customers       |  List all customers	|  ✅ |
| GET    | /api/customers/{id}  |  Get customer by ID  |  ✅ |
| PUT    | /api/customers/{id}  |  Update a customer   |  ✅ |
| DELETE | /api/customers/{id}  |  Delete a customer   |  ✅ |


### Orders
| Method | Route | Description | Auth |
| -------------- | ---------- |  ---------- |  ------- |
| POST   | /api/Orders       |  Create an order   |  ✅ |
| GET    | /api/Orders       |  List all orders	|  ✅ |
| GET    | /api/Orders/{id}  |  Get order by ID  |  ✅ |
| PUT    | /api/Orders/{id}  |  Update an order   |  ✅ |
| DELETE | /api/Orders/{id}  |  Cancel/delete an order   |  ✅ |


### Products
| Method | Route | Description | Auth |
| -------------- | ---------- |  ---------- |  ------- |
| POST   | /api/products       |  Create a product   |  ✅ |
| GET    | /api/products       |  List all products	|  ✅ |
| GET    | /api/products/{id}  |  Get product by ID  |  ✅ |
| PUT    | /api/products/{id}  |  Update a product   |  ✅ |
| DELETE | /api/products/{id}  |  Delete a product   |  ✅ |

### Health & Observability
| Method | Route | Description | Auth |
| -------------- | ---------- |  ---------- |  ------- |
| GET    | /api/Health       |  Database connectivity check	|  ❌ |
| GET    | /metrics  |  Prometheus metrics (OpenTelemetry)  |  ❌ |



## Testing
```bash
# Run all tests
dotnet test

# Unit tests only (108 tests)
dotnet test tests/OrderFlow.

UnitTests

# Integration tests only (18 tests)
dotnet test tests/OrderFlow.

IntegrationTests

# With coverage
dotnet test --settings .runsettings
```

- **Unit tests:** xUnit + FluentAssertions — test Domain entities, value objects, Application handlers, events, queries, and helpers
- **Integration tests:** xUnit + Testcontainers + WebApplicationFactory — spin up a real PostgreSQL container, run the full API pipeline with JWT authentication


## CI/CD

### **CI (Continuous Integration)**

Triggered on **push** and **pull_request** to **main**. Pipeline:

1. Checkout + Setup .NET 10
2. dotnet restore
3. dotnet build --configuration Release
4. dotnet test — Unit tests (108)
5. dotnet test — Integration tests (18) with PostgreSQL container

### **CD (Continuous Deployment)**

Triggered automatically when CI succeeds on main. Pipeline:

1. Login to GitHub Container Registry (GHCR)
2. Build and push Docker image to ghcr.io/gil-gam/orderflow-api:latest
3. Image tags: latest, {version}, {major}.{minor}, {sha}


## Tech Stack
| Category | Technology |
| -------------- | ---------- |
| Runtime | .NET 10 — ASP.NET Core Controllers  |
| Language    | C# 13 |
| ORM    | Entity Framework Core 10 + Npgsql |
| Database    | PostgreSQL 16 |
| CQRS    | MediatR |
| Auth    | JWT Bearer (Microsoft.AspNetCore.Authentication.JwtBearer) |
| Logging    | Serilog (Console + File sinks) |
| Documentation    | Swagger / Swashbuckle + Annotations |
| Observability    | OpenTelemetry (Traces + Metrics + Prometheus) |
| API Versioning    | Asp.Versioning.Mvc |
| Rate Limiting    | System.Threading.RateLimiting |
| Mapping    | Manual profiles (custom extension methods) |
| Testing    | Unit + FluentAssertions + Testcontainers + WebApplicationFactory |
| Container    | Docker + Docker Compose |
| CI/CD    | GitHub Actions |


## Project Structure 
```text


OrderFlow/
├── .github/workflows/
│   ├── ci.yml                    # Build + Test pipeline
│   └── cd.yml                    # Docker image push to GHCR
├── src/
│   ├── OrderFlow.

Domain/         # Entities, Value Objects, Enums, Interfaces
│   ├── OrderFlow.

Application/    # Commands, Queries, Handlers, DTOs, Events
│   ├── OrderFlow.

Infrastructure/ # DbContext, Repositories, Migrations
│   └── OrderFlow.

Api/            # Controllers, Middleware, JWT, Swagger
├── tests/
│   ├── OrderFlow.

UnitTests/      # 108 unit tests
│   └── OrderFlow.

IntegrationTests/ # 18 integration tests
├── docker-compose.yml
├── Dockerfile
└── .runsettings
```

## License
This project is licensed under the MIT License — see the LICENSE file for details.

by Gilberto Andreatta 

[![LinkedIn](https://img.shields.io/badge/LinkedIn-0077B5?style=for-the-badge&logo=linkedin&logoColor=white)](https://linkedin.com/in/gilbertoandreatta)
