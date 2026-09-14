# B2BShop

A B2B wholesale e-commerce platform.

---

## Stack

| Layer | Technology |
|---|---|
| Frontend | React 18, TypeScript, Tailwind CSS, Vite |
| Backend API | ASP.NET Core 10, C# |
| Background worker | .NET Generic Host |
| ORM | Entity Framework Core 10 |
| Database | PostgreSQL 16 |
| Container | Docker + Docker Compose |

---

## Quick Start

**Requirements:** Docker Desktop (running)

```bash
docker-compose up -d --build
```

| Service | URL |
|---|---|
| Frontend | http://localhost:3000 |
| Backend API | http://localhost:5000 |

First start takes ~60 seconds — the API creates the schema and seeds sample data automatically.

To reset all data:
```bash
docker-compose down -v && docker-compose up -d --build
```

---

## Test Credentials

See [TEST_USERS.md](TEST_USERS.md) for all login credentials. All accounts use password `Password1!`.

Quick reference:

| Email | Role |
|---|---|
| admin@b2bshop.local | Platform Admin |
| buyer1@acme.example | Buyer (ACME Corp) |
| approver@acme.example | Approver (ACME Corp) |
| buyer1@metro.example | Buyer (Metro GmbH) |

---

## Project Structure

```
├── backend/
│   ├── src/
│   │   ├── B2BShop.Api/          # REST API (controllers, auth, config)
│   │   ├── B2BShop.Application/  # Business logic (pricing, orders, catalog)
│   │   ├── B2BShop.Domain/       # Entities and domain types
│   │   ├── B2BShop.Infrastructure/  # EF Core, services, seeding
│   │   └── B2BShop.Worker/       # Background order-transfer worker
│   └── tests/
│       └── B2BShop.Tests/        # xUnit test suite
├── frontend/
│   └── src/
│       ├── components/           # Shared UI components
│       ├── pages/                # Route pages
│       ├── services/             # API client services
│       └── types/                # TypeScript type definitions
├── database/                     # SQL views, triggers, procedures (reference only)
├── docker-compose.yml
└── TEST_USERS.md
```

---

## Running Tests

```bash
cd backend
dotnet test tests/B2BShop.Tests
```

---

## Local Frontend Development (optional)

The Docker setup serves the pre-built frontend. For live reloading:

```bash
cd frontend
npm install
npm run dev     # http://localhost:5173
```

