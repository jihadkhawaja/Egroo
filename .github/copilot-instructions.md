# Egroo — Workspace Guidelines

Egroo is a self-hosted, open-source real-time chat platform built on .NET 10, Blazor Auto, ASP.NET Core, SignalR, and PostgreSQL.

## Architecture

The solution lives in `src/Egroo.slnx`. Projects and their roles:

| Project | Role |
|---|---|
| `Egroo/Egroo` | Blazor Server host; serves SSR + switches to WASM. No DB access — delegates to API. |
| `Egroo.Client` | Blazor WebAssembly client-side project |
| `Egroo.Server` | ASP.NET Core API: REST endpoints, SignalR hub, PostgreSQL via EF Core |
| `Egroo.UI` | Shared Razor component library (MudBlazor, BlazorDexie). Consumed by Blazor host & WASM. |
| `jihadkhawaja.chat.shared` | Internal NuGet package: models, interfaces, DTOs shared across client & server |
| `jihadkhawaja.chat.server` | Internal NuGet package: `ChatHub` (partial classes) + `InMemoryConnectionTracker` |
| `jihadkhawaja.chat.client` | Internal NuGet package: client-side services wrapping `HubConnection` and HTTP auth |
| `Egroo.Server.Test` | MSTest unit tests using in-memory EF Core |

**Dependency flow:**
```
Egroo (Blazor) → Egroo.UI → jihadkhawaja.chat.client → jihadkhawaja.chat.shared
Egroo.Server   → jihadkhawaja.chat.server → jihadkhawaja.chat.shared
Egroo.Server   → Repositories (IAuth, IUser, IChannel, IMessageRepository)
```

## Build & Test

```powershell
# Build everything
dotnet build src/Egroo.slnx --configuration Debug

# Run all tests
dotnet test src/Egroo.Server.Test/Egroo.Server.Test.csproj --verbosity normal

# Hot reload (separate terminals)
dotnet watch --project src/Egroo.Server/Egroo.Server.csproj
dotnet watch --project src/Egroo/Egroo/Egroo.csproj

# EF Core migrations (use PowerShell scripts)
.\scripts\add-migration.ps1 "<MigrationName>"
.\scripts\update-database.ps1
```

Migrations target `Egroo.Server`. Auto-migration runs at startup via `db.Database.MigrateAsync()`.

## Key Conventions

### Design / UX
- Preserve the existing dark chat UI with orange primary accents unless the feature requires a local exception.
- Chat readability takes priority over subtle styling. Sender bubbles, links, mentions, timestamps, and typing states must remain high-contrast.
- When styling message content, verify sender and receiver bubbles separately because palette inheritance differs.
- Mentions should render as semantic chips or tags, not plain text.
- Links should be visibly clickable and readable in all message states, especially the current-user bubble.

### Database
- **Lowercase naming convention** is applied to all EF Core table and column names via `LowerCaseNamingConvention`. Always expect lowercase in migrations and SQL.
- **Soft deletes**: All entities extend `EntityAudit` (`DateDeleted`, `DeletedBy`). Do not hard-delete tracked entities without good reason.
- **Primary keys**: `EntityBase` uses `Guid`; `EntityChildBase` uses `int`.
- **User model**: `User` (server-only) extends `UserDto` (shared). Nested owned types: `UserDetail`, `UserSecurity`.
- **Schema changes require migrations**: If you change a persisted entity, owned type, relationship, index, or EF mapping, add a migration with `./scripts/add-migration.ps1 "<MigrationName>"` and apply it with `./scripts/update-database.ps1`.

### API
- Endpoints use **Minimal API route groups** under `/api/v1/{Feature}`. Add new routes to the appropriate group file in `Egroo.Server/API/`.
- Rate limiting policy `"Api"` is applied globally (100 req/min fixed window). Unauthenticated routes use `.AllowAnonymous()`.
- Auth: JWT with `ClaimTypes.NameIdentifier` (GUID userId). SignalR also accepts token via `?access_token=` query param.

### Repository Pattern
- All DB access goes through a repository implementing a shared interface (e.g., `IAuth`, `IUser`).
- Extend `BaseRepository` for new repositories — use its protected helpers `GetConnectedUser()` / `GetConnectorUserId()` to resolve authenticated users; don't query claims manually.
- Repositories are registered as **scoped**.

### SignalR / ChatHub
- `ChatHub` is a **partial class** split by concern: `ChatHub.cs`, `ChatHubMessage.cs`, `ChatHubCall.cs`, `ChatHubChannel.cs`, `ChatHubUser.cs`. Keep this split when adding new hub methods.
- Hub is mapped at `/chathub` with **WebSockets-only** transport (no SSE or long-polling fallback).
- `IConnectionTracker` (default: `InMemoryConnectionTracker`) is not distributed. For multi-server deployments, inject a Redis-backed implementation.
- Client-side message event subscriptions should go through `ChatMessageService` rather than direct component-level `HubConnection.On(...)` handlers.

### Blazor / UI
- UI reusable components live in `Egroo.UI/Components/` (subdirs: `Base/`, `Layout/`, `View/`).
- Use **MudBlazor** components for all UI elements. Add `FluentValidation` validators for new forms.
- Client-side state caching uses **BlazorDexie** (IndexedDB wrapper `EgrooDB`). Auth token/session is stored in `SessionStorage`.
- Service calls from UI: use `jihadkhawaja.chat.client` services (`ChatMessageService`, `AuthService`, `ChatChannelService`, etc.) — don't call SignalR `HubConnection` directly.
- Treat markdown rendering as a presentation layer. Message content should stay transport-safe and portable.
- User mentions and agent mentions may share UI affordances, but only configured channel agents should trigger agent-response flows.
- Prefer HTTP upload plus durable links for files rather than binary SignalR payloads.

### Chat Behavior
- Human users and AI agents share the same channel timeline but should remain visually distinct.
- Typing indicators should support both users and agents and clear predictably when a real message is received.
- File links and pasted URLs should render as normal clickable anchors.
- When adjusting chat visuals, test current-user message bubbles in addition to other-user and agent bubbles.

### Encryption
- Message content in `UserPendingMessage` is **AES-encrypted** before storage. Keys (`Encryption:Key` 32-char, `Encryption:IV` 16-char) come from app secrets — never hardcode them.
- Messages are ephemeral: `UserPendingMessage` is deleted once the recipient acknowledges receipt via `UpdatePendingMessage`. The `Message` table stores only metadata.

### Testing
- Tests use an **in-memory EF Core database** isolated per test via `TestServiceProvider`.
- Mock HttpContext with claims is provided — use the existing `TestServiceProvider` pattern for new test classes.
- New features in `Egroo.Server` should have corresponding tests in `Egroo.Server.Test`.
- For EF Core schema changes, verify that the generated migration files and `DataContextModelSnapshot` are updated alongside the code change.

## Project-wide Settings (`Directory.Build.props`)
- **Target framework**: `net10.0`
- **Nullable**: `enable`
- **ImplicitUsings**: `enable`

## Pitfalls
- **`jihadkhawaja.chat.*` packages**: These are referenced as `ProjectReference` locally (not from NuGet feed). Changes to `jihadkhawaja.chat.shared` affect all three packages — rebuild in order: shared → server/client.
- **Blazor Auto Mode**: Components rendered interactively server-side first, then re-hydrated as WASM. Avoid direct server-only APIs (e.g., HttpContext) inside components without SSR guards.
- **Connection tracker is in-memory**: Not safe for horizontal scale-out without replacing `IConnectionTracker`.
- **CORS**: Allowed origins are configured in `Api:AllowedOrigins` — update `appsettings.json` when deploying to a new domain.
