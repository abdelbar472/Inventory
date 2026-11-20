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
- Health/metrics text: `http://localhost:5241/metrics`

### Option B — everything in Docker (API + Prometheus + Grafana)

```powershell
docker compose up --build
```

| Service | Address | What it is |
|---|---|---|
| Store API | `http://localhost:5241` | The application (Swagger at `/swagger`) |
| Prometheus | `http://localhost:9090` | Collects metrics every 10 seconds |
| Grafana | `http://localhost:3000` | Dashboards (login `admin` / `admin`) |

> Run **one option at a time** — both use port 5241.

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
| API docs | Swagger / OpenAPI via Swashbuckle 11 |
| Metrics | `prometheus-net` middleware + custom Counter/Gauge |
| Dashboards | Prometheus + Grafana in Docker Compose |

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
  prometheus/prometheus.yml  Scrape config (target: store-api:8080)
  grafana/provisioning/...   Auto-registers Prometheus datasource + dashboard
  grafana/dashboards/...     Prebuilt "Store API Metrics" dashboard
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
- Placing an order updates product stock, writes `OrderItem` rows, writes a `StockMovement`, and saves the order in **one `SaveChangesAsync()` call** — EF wraps it in a single database transaction.
- Money uses `decimal(18,2)`, never floating point.
- Email and SKU have unique indexes.
- Navigation properties use `[JsonIgnore]` to avoid cyclical JSON serialization.

### Metrics exposed at `/metrics`

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

This is deliberately a learning project, not production-ready:

1. **Database resets on every start** — `EnsureDeleted()` + `EnsureCreated()`. Learn **EF Core migrations** next to preserve data and evolve the schema safely.
2. **JWT signing key is hard-coded in `appsettings.json`** — in production use user secrets / environment variables / a key vault, and add token expiry/refresh handling.
3. **No order state machine** — currently any staff member can set any status. Add allowed transitions (e.g. Pending → Paid → Shipped) and restock on cancellation.
4. **No pagination/filtering** on list endpoints.
5. **`WeatherForecast.cs` and its controller** are leftover Visual Studio template files; safe to delete.
6. **No automated tests yet** — a natural next exercise is unit tests for `PasswordHelper` and integration tests for role gates.

---

## 6. Troubleshooting

| Symptom | Fix |
|---|---|
| `dotnet run` says port 5241 in use | Stop the Docker stack (`docker compose down`) or the other server |
| Grafana shows "No data" | Wait ~20s for the first scrape; check `http://localhost:9090/targets` shows `store-api` UP |
| 401 on all endpoints | Run a Login request and check the token is pasted after `Bearer ` |
| 403 on an action | Correct login, wrong role — see the matrix above |
| Build error about missing packages | `dotnet restore` |
| `CS0234` for `EntityFrameworkCore` | The EF Core SQLite package must be referenced in `.csproj` |
