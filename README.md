# AMHARC Match Capture

A hybrid, local-first sports video capture, recording, live-production, PTZ control, event-tagging and streaming platform for Gaelic games (Gaelic football, hurling, ladies' football, camogie).

---

## Architecture Overview

The system runs in two environments:

| Environment | Purpose |
|-------------|---------|
| **Windows 11 Laptop (Production)** | Local Windows Capture Agent, SQLite database, camera RTSP connection, PTZ control, Stream Deck, joystick, FFmpeg recording |
| **Replit (Development)** | Web operator interface, mock API, documentation, unit tests, architecture design |

```
Browser (Operator Interface)
    ↕ HTTP/WebSocket localhost:5000
Local Windows Capture Agent (C# / ASP.NET Core)
    ↕ RTSP / VAPIX / ONVIF
AXIS Q6128-E PTZ Camera
```

---

## Prerequisites

### Development (Replit)

- Node.js 24+ (provided by Replit)
- pnpm (provided by Replit)

### Production (Windows 11 Laptop)

- Windows 11
- .NET 8 SDK
- FFmpeg (on PATH)
- Elgato Stream Deck SDK or compatible HID library
- AXIS Q6128-E or compatible PTZ camera on a private Ethernet segment

---

## Development Setup

```bash
# Install dependencies
pnpm install

# Start the mock API server (development mode)
pnpm --filter @workspace/api-server run dev

# Start the operator interface (development mode)
pnpm --filter @workspace/operator-ui run dev

# Run codegen after changing the OpenAPI spec
pnpm --filter @workspace/api-spec run codegen

# Type-check all packages
pnpm run typecheck
```

---

## Local Execution

The Replit workspace runs two services:

| Service | URL | Description |
|---------|-----|-------------|
| Operator Interface | `/` | React + Vite operator control surface |
| Mock API | `/api` | Express mock of the local agent API |

The mock API provides realistic in-memory state for all endpoints. No camera or hardware is required to use the operator interface in development.

---

## Repository Structure

```
amharc-match-capture/
├── artifacts/
│   ├── operator-ui/          # React + Vite operator interface
│   └── api-server/           # Express mock API (dev) / production scaffold
├── lib/
│   ├── api-spec/             # OpenAPI spec (source of truth)
│   ├── api-client-react/     # Generated React Query hooks
│   ├── api-zod/              # Generated Zod validation schemas
│   └── db/                   # Drizzle ORM schema (PostgreSQL for cloud)
├── docs/
│   ├── requirements/         # Product requirements
│   ├── architecture/         # Solution architecture, components, camera adapters
│   ├── api/                  # Local API reference
│   ├── data-model/           # Entity definitions and ER diagram
│   ├── testing/              # Test strategy and acceptance tests
│   ├── security/             # Security model
│   ├── operations/           # Match day runbook
│   ├── decisions/            # Architecture Decision Records (ADRs)
│   ├── risk-register/        # Risk register
│   └── branding/             # AMHARC brand guidelines
├── samples/
│   ├── sample-matches/       # Test match fixtures (JSON)
│   ├── sample-events/        # Test event collections (JSON)
│   ├── sample-overlays/      # Overlay configuration examples
│   └── mock-streams/         # Mock RTSP stream configuration
└── scripts/
    └── development/          # Development utility scripts
```

---

## Known Limitations (Phase 0)

- The operator interface connects to a mock API with in-memory state. No real camera, joystick, Stream Deck, or recording pipeline is connected.
- The mock API state resets on server restart.
- The live video preview area in the Live Capture page shows a placeholder; real RTSP preview requires the Windows agent.
- Recording, PTZ, Stream Deck, and joystick functions are simulated in the mock API.

---

## Roadmap

| Phase | Description |
|-------|-------------|
| 0 | Design and project foundation (current) |
| 1 | Camera connection, RTSP preview, basic recording |
| 2 | PTZ control, joystick integration |
| 3 | Match management, Stream Deck, event tagging, export |
| 4 | Broadcast overlays, OBS browser source |
| 5 | Live streaming (RTMP) |
| 6 | Post-match review, clip generation, manifest export |
| 7 | Multi-camera support |

See `docs/architecture/SOLUTION_ARCHITECTURE.md` for the full phased plan.

---

## Documentation

| Document | Path |
|----------|------|
| Product Requirements | `docs/requirements/PRODUCT_REQUIREMENTS.md` |
| Solution Architecture | `docs/architecture/SOLUTION_ARCHITECTURE.md` |
| Component Definitions | `docs/architecture/COMPONENTS.md` |
| Camera Adapter Model | `docs/architecture/CAMERA_ADAPTER_MODEL.md` |
| Local API Reference | `docs/api/LOCAL_API.md` |
| Data Model | `docs/data-model/DATA_MODEL.md` |
| Test Strategy | `docs/testing/TEST_STRATEGY.md` |
| MVP Acceptance Tests | `docs/testing/MVP_ACCEPTANCE_TEST_PLAN.md` |
| Security Model | `docs/security/SECURITY_MODEL.md` |
| Match Day Runbook | `docs/operations/MATCH_DAY_RUNBOOK.md` |
| Brand Guidelines | `docs/branding/BRAND_GUIDELINES.md` |
| Risk Register | `docs/risk-register/RISK_REGISTER.md` |

---

## Product Name

All application titles, namespaces, manifests, documentation, and UI labels use:

**AMHARC Match Capture**

---

*AMHARC Match Capture — Phase 0 Foundation*
