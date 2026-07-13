# AMHARC Match Capture

A hybrid, local-first sports video capture, recording, PTZ control, event-tagging and streaming platform for Gaelic games.

## Project Overview

**Phase:** 0 — Foundation (complete)  
**Production target:** Windows 11 laptop with AXIS Q6128-E PTZ camera  
**Replit role:** Operator interface prototype, mock API, documentation, architecture

### What is built

- Operator interface (React + Vite) — 11 pages, AMHARC brand palette, full API integration
- Mock API (Express 5) — all 40+ endpoints with in-memory state
- OpenAPI spec — canonical `lib/api-spec/openapi.yaml`
- Generated React Query hooks — `lib/api-client-react/`
- Generated Zod schemas — `lib/api-zod/`
- TypeScript domain interfaces — `packages/domain-model/src/interfaces.ts`
- Mock implementations — `packages/domain-model/src/mock-implementations.ts`
- Full documentation suite — requirements, architecture, API, data model, security, testing, operations, ADRs
- Sample data — matches, events, overlay configs, mock stream setup

### Workflows

| Workflow | Service |
|----------|---------|
| `artifacts/operator-ui: web` | Operator interface (React + Vite, Wouter routing) |
| `artifacts/api-server: API Server` | Mock local agent API (Express 5) |

---

## Architecture

```
Browser (Operator Interface — React + Vite)
    ↕ HTTP/WebSocket
Mock API (Express — Replit dev only)
    |
    ↓ (production path, not in Replit)
Windows Local Agent (C# / ASP.NET Core)
    ↕ RTSP / VAPIX / ONVIF
AXIS Q6128-E PTZ Camera
```

---

## Key Technical Decisions

- **Local-first**: core match operations (recording, tagging, clock, PTZ) work without Internet
- **Camera adapter pattern**: `ICameraAdapter` + `IPtzController` isolate all camera-specific code
- **MKV segments**: recoverable recordings; final remux to MP4 after match
- **Dual clock model**: `matchClockSeconds` and `recordingElapsedSeconds` are always independent (ADR-004)
- **OpenAPI-first**: `lib/api-spec/openapi.yaml` → codegen → React Query hooks + Zod schemas
- **SQLite (local) / PostgreSQL (cloud)**: SQLite for Windows agent, Postgres available for cloud sync

---

## Brand

| Token | Hex | Role |
|-------|-----|------|
| Black | `#000000` | Dominant background |
| AMHARC Green | `#1C8551` | Primary actions, connected states, recording active |
| AMHARC Lime | `#B6DC46` | Live clock, active period, highlights |
| White | `#FFFFFF` | Text on dark |

---

## Development Commands

```bash
pnpm install                                     # Install dependencies
pnpm --filter @workspace/api-spec run codegen    # Regenerate hooks + schemas from OpenAPI
pnpm run typecheck                               # Type-check all packages
```

---

## User Preferences

- Product name is always **AMHARC Match Capture** (never abbreviated or varied)
- No emojis in the operator interface
- Dark theme is default; never use colour alone to convey status
- Score format: goals-points (e.g. 1-12 means 1 goal 12 points, total = 15)
- `matchClockSeconds` and `recordingElapsedSeconds` must always be treated as independent values
