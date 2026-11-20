# InventoryApi — a small store platform

A learning project: a **web API** for a shop. It runs on the .NET 10 platform, stores data in SQLite, and exposes JSON endpoints that a website, mobile app, or API client (like Postman) can call.

---

## 1. The short version (no technical background needed)

Imagine a shop with three kinds of people:

| Who | What they do in this app |
|---|---|
| **Customer** | Looks at products, buys things, and can see their own past orders |
| **Employee** | Sees all orders, marks them as paid/shipped/cancelled, and adjusts stock when goods arrive or go missing |
| **Manager** | Does everything an employee does, plus adds/edits products, creates staff accounts, and reads reports |

The app keeps track of:

- **Products** — name, barcode-like SKU, price, how many are in stock, and when to warn about low stock
- **Orders** — who bought what, when, the total, and where the order is in its journey (Pending → Paid → Shipped, or Cancelled)
- **Stock movements** — a history log of every time stock went up or down, and who did it
- **Users** — name, email, a securely hashed password, and their role

Nothing "renders pages". It is a behind-the-scenes engine. To see it working, you use an API tool or the built-in interactive documentation page.

**Security note:** people log in with an email and password. The server gives them a temporary digital "ID card" (a token). Every request must show that card, and the server checks whether that person is allowed to do what they're asking.

---

## 2. Run it

### Option A — just the API (locally)

```powershell
dotnet run
```

- API: `http://localhost:5241`
- Interactive docs (Swagger): `http://localhost:5241/swagger`
- Metrics text: `http://localhost:5241/metrics` — **protected** with HTTP Basic auth: user `metrics`, password `local-metrics-secret` (see `appsettings.json`)

### Option B — everything in Docker (API + Prometheus + Grafana)

```powershell
docker compose up --build
```

| Service | Address | What it is |
|---|---|---|
| Store API | `http://localhost:5241` | The application (Swagger at `/swagger`) |
| Prometheus | `http://localhost:9090` | Collects metrics every 10 seconds, using Basic auth for `/metrics` |
| Grafana | `http://localhost:3000` | Dashboards (login `admin` / `admin`) |

> Run **one option at a time** — both use port 5241.

Docker notes:
- Compose runs the API in **Production** mode. Swagger is opt-in via the `EnableSwagger` setting (currently `"true"` for learning — set it to `"false"` to hide it).
- Image tags are **pinned** (`prom/prometheus:v3.5.0`, `grafana/grafana:12.1.0`) so upgrades are deliberate, not accidental.
- Change the Grafana password with `$env:GRAFANA_ADMIN_PASSWORD = "something-strong"` before `docker compose up`.
- Prometheus is configured with `basic_auth` to match the protected `/metrics` endpoint.

**Demo accounts** (password is the prefix + `123!`):

| Email | Password | Role |
|---|---|---|
| `cx@shop.test` | `Cx123!` | Customer |
| `emp@shop.test` | `Emp123!` | Employee |
| `mgr@shop.test` | `Mgr123!` | Manager |

On every startup the database is deleted and recreated with sample data. Any orders you create disappear when the app restarts. This is intentional while learning (see *Limitations*).

---

## 3. Test it with Postman

Two ready-made files are in the project root:

- `InventoryApi.postman_collection.json` — 25 requests grouped by feature
- `InventoryApi.postman_environment.json` — variables like `baseUrl` and tokens

Steps:

1. Import **both** files into Postman.
2. Select the **InventoryApi Local** environment.
3. Run the three **Login** requests first — their test scripts automatically save tokens into `cxToken`, `empToken`, and `mgrToken`.
4. Run any other request. The correct `Authorization: Bearer ...` header is inserted automatically.

The collection includes deliberate "expect 403" requests so you can see role restrictions actually working.

---

## 4. For technical readers

### Stack

| Concern | Choice |
|---|---|
| Framework | ASP.NET Core Web API, .NET 10 |
| Language | C# (nullable enabled, implicit usings) |
| Database | SQLite, via Entity Framework Core 10 (Code First) |
| Authentication | JWT bearer tokens signed with HMAC-SHA256 |
| Passwords | PBKDF2-SHA256, 100 000 iterations, per-user salt |
| Authorization | Role claims + `[Authorize(Roles = ...)]`, plus per-record ownership checks |
| Request validation | Data annotations on DTOs (`[Range]`, `[EmailAddress]`, password regex) |
| Brute-force protection | Fixed-window rate limiter: 10 auth attempts per IP per minute (HTTP 429 after that) |
| Concurrency safety | Explicit DB transaction + atomic `UPDATE ... WHERE Quantity >= n` (no overselling) |
| Configuration | Connection string, JWT settings, and metrics credentials read from `appsettings.json` |
| API docs | Swagger / OpenAPI via Swashbuckle 11 (opt-in) |
| Metrics | `prometheus-net` middleware + custom Counter/Gauge, protected by HTTP Basic auth |
| Dashboards | Prometheus + Grafana in Docker Compose |
| Tests / CI | xUnit tests and a GitHub Actions workflow (`dotnet build` + `dotnet test`) |

### Project layout

```
Program.cs                  App startup: DI, auth, Swagger, DB init, metrics endpoint
Models/
  User.cs, Product.cs, Order.cs, OrderItem.cs, StockMovement.cs   DB entities
  Role.cs, OrderStatus.cs    Enums
  Dtos.cs                    Request/response shapes (never exposes PasswordHash)
Data/
  AppDbContext.cs            EF Core model: tables, keys, relationships, indexes
  DbSeeder.cs                Sample users/products/order on startup
Services/
  PasswordHelper.cs          Hashing and verification
  AppMetrics.cs              Custom Prometheus Counter + Gauge
  LowStockMetricsWorker.cs   BackgroundService polling the DB every 10s
Controllers/
  AuthController.cs          login / register / token creation
  ProductsController.cs      browse + manager CRUD (soft delete)
  OrdersController.cs        place/list/update orders, ownership rules
  StockController.cs         stock adjustments + history
  UsersController.cs         manager-only user administration
  ReportsController.cs       low-stock and sales aggregation
monitoring/
  prometheus/prometheus.yml  Scrape config (target: store-api:8080, Basic auth)
  grafana/provisioning/...   Auto-registers Prometheus datasource + dashboard
  grafana/dashboards/...     Prebuilt "Store API Metrics" dashboard
tests/InventoryApi.Tests/   xUnit tests (PasswordHelper + DTO validation)
.github/workflows/ci.yml    GitHub Actions: restore, build, test
.gitignore                  Keeps bin/obj/DB files out of git
update.sh                   Helper script to commit/push (uses git add .)
Dockerfile, docker-compose.yml, .dockerignore
```

### Authorization matrix

| Endpoint | Customer | Employee | Manager |
|---|:---:|:---:|:---:|
| `POST /api/auth/login`, `/register` | ✅ public | ✅ public | ✅ public |
| `GET /api/products` | ✅ active only | ✅ all | ✅ all |
| `POST/PUT /api/products`, `PATCH /api/products/{id}/active` | ❌ | ❌ | ✅ |
| `POST /api/orders` | ✅ | ❌ | ❌ |
| `GET /api/orders` | ✅ own only | ✅ all | ✅ all |
| `PATCH /api/orders/{id}/status` | ❌ | ✅ | ✅ |
| `POST /api/stock/movement`, `GET /api/stock/movements` | ❌ | ✅ | ✅ |
| `GET/POST /api/users`, `PATCH /api/users/{id}/role` | ❌ | ❌ | ✅ |
| `GET /api/reports/*` | ❌ | ❌ | ✅ |

`403 Forbidden` = you are logged in but lack the role. `401 Unauthorized` = no valid token.

### Data model notes

- One **User** has many **Orders**; one **Order** has many **OrderItems**.
- **OrderItem.UnitPrice** copies `Product.Price` at purchase time — changing the product price later does not rewrite history.
- Product deletion is **soft**: `PATCH .../active?active=false` sets `IsActive = false`, preserving old orders. Foreign keys use `DeleteBehavior.Restrict` so referenced rows cannot be deleted.
- Placing an order runs inside **one explicit database transaction**. Stock is reduced with an atomic `UPDATE ... WHERE Quantity >= n` so two simultaneous orders cannot both pass the check and oversell; if the update affects 0 rows, the order is rejected. Stock adjustments use the same pattern and reject a zero `Delta`.
- Money uses `decimal(18,2)`, never floating point.
- Email and SKU have unique indexes.
- Navigation properties use `[JsonIgnore]` to avoid cyclical JSON serialization.

### Metrics exposed at `/metrics`

`/metrics` is **not public**: a middleware checks HTTP Basic credentials before the endpoint responds. Without them you get `401 Unauthorized`; Prometheus sends `metrics` / `local-metrics-secret` via `basic_auth`. The credentials come from `appsettings.json` (`Metrics:User` / `Metrics:Password`) — change both the app config and `prometheus.yml` together when deploying.

| Metric | Type | Source | Grafana panel |
|---|---|---|---|
| `http_requests_received_total` | counter | prometheus-net middleware | Requests/min, Error rate % |
| `http_request_duration_seconds` | histogram | prometheus-net middleware | Latency p50 / p95 |
| `store_low_stock_products` | gauge | `LowStockMetricsWorker` | Low-stock products |
| `store_orders_total` | counter | `OrdersController.Place` | Orders/hour |

Example PromQL:

```promql
sum(rate(http_requests_received_total[1m])) * 60
100 * sum(rate(http_requests_received_total{code=~"4..|5.."}[5m]))
  / clamp_min(sum(rate(http_requests_received_total[5m])), 0.001)
histogram_quantile(0.95, sum by (le) (rate(http_request_duration_seconds_bucket[5m])))
sum(increase(store_orders_total[1h]))
```

Grafana provisioning auto-creates the datasource (`uid: prometheus`) and the dashboard titled **Store API Metrics** in the `.NET` folder. Login: `admin` / `admin`.

---

## 5. Limitations / good next steps

This is deliberately a learning project, not production-ready. Several review findings have already been fixed (see the next section); these remain:

1. **Database resets on every start** — `EnsureDeleted()` + `EnsureCreated()`. Learn **EF Core migrations** next to preserve data and evolve the schema safely.
2. **Secrets are still in `appsettings.json`** — the JWT signing key and the `/metrics` Basic credentials are dev-only defaults. In production use user secrets / environment variables / a key vault, and add token refresh handling.
3. **No login lockout** — repeated bad passwords are rate-limited (10 attempts/IP/minute), but an account is never temporarily disabled after N failed attempts. Add failed-attempt counters and lockout windows before deploying.
4. **No order state machine** — any staff member can set any status. Add allowed transitions (e.g. Pending → Paid → Shipped) and restock on cancellation.
5. **No pagination/filtering** on list endpoints.
6. **`WeatherForecast.cs` and its controller** are leftover Visual Studio template files; safe to delete.
7. **`update.sh` still runs `git add .`** — this is now safe because `.gitignore` excludes `bin/`, `obj/`, and SQLite files, but targeted `git add <paths>` is a better habit.

### Review issues that were fixed

For traceability, these are the problems that were identified and addressed:

| Problem | Fix |
|---|---|
| 88 `bin/`/`obj/`/SQLite files tracked in git | Added `.gitignore` and `git rm -r --cached bin obj inventory.db*` (files stay on disk) |
| Overselling under concurrent orders | Explicit transaction + atomic `ExecuteUpdateAsync(... WHERE Quantity >= n)` in orders and stock movements |
| Public `/metrics` endpoint | HTTP Basic auth middleware; Prometheus `basic_auth` scrape config |
| Compose ran API in `Development`, exposed Swagger, used `:latest` images | Compose sets `ASPNETCORE_ENVIRONMENT=Production`; Swagger is opt-in via `EnableSwagger`; images pinned to `v3.5.0` / `12.1.0` |
| No password policy / rate limiting / lockout | Password rules on register/create-user DTOs; 10 attempts/IP/minute rate limiter on auth endpoints (lockout still open — see above) |
| Weak request validation | `[Range]`, `[EmailAddress]`, `[Required]`, length and regex rules on DTOs; zero stock `Delta` rejected in controller |
| Hard-coded SQLite connection string in `Program.cs` | Moved to `ConnectionStrings:Default` in `appsettings.json` |
| No tests / no CI | xUnit test project with 6 passing tests and `.github/workflows/ci.yml` |

Run the tests locally with:

```powershell
dotnet test .\tests\InventoryApi.Tests\InventoryApi.Tests.csproj -c Release
```

---

## 6. Troubleshooting

| Symptom | Fix |
|---|---|
| `dotnet run` says port 5241 in use | Stop the Docker stack (`docker compose down`) or the other server |
| Grafana shows "No data" | Wait ~20s for the first scrape; check `http://localhost:9090/targets` shows `store-api` UP |
| 401 on all endpoints | Run a Login request and check the token is pasted after `Bearer ` |
| 401 on `/metrics` | Send HTTP Basic auth (`metrics` / `local-metrics-secret`), or use the Prometheus target instead |
| HTTP 429 on login/register | Too many auth attempts from this IP in one minute; wait for the window to reset |
| 403 on an action | Correct login, wrong role — see the matrix above |
| Build error about missing packages | `dotnet restore` |
| `CS0234` for `EntityFrameworkCore` | The EF Core SQLite package must be referenced in `.csproj` |
