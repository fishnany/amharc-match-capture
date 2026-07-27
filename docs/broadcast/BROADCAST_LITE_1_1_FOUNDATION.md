# AMHARC Broadcast Lite — Phase 1.1 Foundation

**Status:** Engineering foundation implemented for Score Bug v1.0  
**Baseline:** AMHARC Match Capture 0.1.0 Beta  
**Date:** July 2026

## Purpose

This change establishes the application/platform contracts required before implementing the visual Score Bug renderer. It deliberately separates scoring rules from graphics rendering so that the Score Bug consumes authoritative match state rather than calculating Gaelic Games rules itself.

## Canonical sport scoring models

| Sport | Score display | Total |
|---|---|---|
| Hurling | Goals-Points | Goals × 3 + Points |
| Camogie | Goals-Points | Goals × 3 + Points |
| Ladies Gaelic Football (LGFA) | Goals-Points | Goals × 3 + Points |
| Men's Gaelic Football | Goals-2pt scores-1pt scores | Goals × 3 + 2pt scores × 2 + 1pt scores |

Two-point scoring is explicitly invalid for Hurling, Camogie and LGFA.

## Twelve-step foundation

1. **Sport/scoring domain model** — Match now stores two-point score components separately and derives its scoring model from the selected sport.
2. **Canonical ScoreState** — one score contract is used by API, overlay and broadcast layers.
3. **Men's football two-point scoring** — supported as an independent score component, not folded into ordinary points.
4. **Traditional scoring preserved** — Hurling, Camogie and LGFA continue to render Goals-Points.
5. **Event score snapshots** — MatchEvent now captures structured before/after score snapshots as well as human-readable export text.
6. **API contract** — ScoreState exposes sport, scoring model, each score component, totals and canonical display strings.
7. **Generated client model alignment** — React and Zod generated models have been aligned to the ScoreState contract pending normal Orval regeneration.
8. **Operator score controls** — Live Capture exposes Goal/Point for all sports and 2pt only for men's Gaelic football.
9. **Stream Deck scoring contract** — score effects recognise goal, point and two-point; the existing men's football mock profile includes a 2-Point control.
10. **Broadcast control plane** — BroadcastState, BroadcastTheme and IBroadcastService establish the state consumed by the future Score Bug renderer.
11. **Brand governance** — canonical AMHARC logo assets are immutable; code must load the approved artwork and never reconstruct the mark.
12. **Automated scoring tests** — tests cover traditional scoring, men's football formatting, two-point calculation and LGFA rejection.

## Score Bug contract

The renderer will receive a ScoreState and Match Clock state. It must not implement scoring rules.

Examples:

- Hurling/Camogie/LGFA: `CLA 1-12 (15)   54:37   NAA 0-15 (15)`
- Men's Gaelic football: `CLA 2-3-8 (20)   54:37   NAA 1-4-7 (18)`

The canonical presentation order remains:

**Team A → Score A → Elapsed Time → Team B → Score B**

Discipline state (red-card and timed black-card indicators) is the next domain addition required by the visual Score Bug implementation.

## Architecture boundary

```text
Match / Event Engine
        │
        ├── ScoringService ──> ScoreState
        │                         │
MatchClockService ────────────────┤
                                  ▼
                          BroadcastService
                                  │
                                  ▼
                       Score Bug Renderer 1.1
```

The existing IOverlayService remains as a compatibility façade while Broadcast Lite is introduced incrementally.

## Validation note

The repository snapshot was modified in an environment without the .NET SDK and without installed pnpm workspace dependencies. The changes therefore require the normal repository validation commands on the Windows/Replit development environment before merge:

```text
dotnet restore
dotnet build
dotnet test
pnpm --filter @workspace/api-spec codegen
pnpm typecheck
pnpm --filter @workspace/operator-ui build
```

After Orval code generation, generated client files should be reviewed for semantic equivalence with the ScoreState contract before commit.

## Score Bug 1.1 renderer implementation

The foundation is now consumed by `ScoreBugSvgRenderer`, exposed through `GET /api/broadcast/score-bug.svg`. The renderer is resolution-independent and references the immutable AMHARC master logo asset rather than reconstructing the logo. Operator Preview uses this endpoint and refreshes once per second while visible.
