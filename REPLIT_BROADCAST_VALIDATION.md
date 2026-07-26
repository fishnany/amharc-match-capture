# Replit Validation Gate — AMHARC Broadcast Lite 1.1–1.3

This package batches the approved Phase 1 broadcast work for validation before GitHub push.

## Included

- **1.1 Score Bug** — sport-aware score rendering, totals, elapsed time, red/black discipline state, replay/HT/FT/ET presentation, immutable AMHARC master logo usage.
- **1.2 Event Banner System** — reusable SVG renderer and event-driven transient graphic trigger for scores, outcomes, cards, substitutions, match-control and restart events.
- **1.3 Match Introduction Package** — reusable 1920×1080 ten-second SVG/SMIL intro driven by match metadata and the immutable AMHARC master logo.

## Mandatory validation

```bash
dotnet restore agent-windows/AmharcAgent.sln
dotnet build agent-windows/AmharcAgent.sln
dotnet test agent-windows/AmharcAgent.sln

pnpm install
pnpm --filter @workspace/api-spec codegen
pnpm typecheck
pnpm --filter @workspace/operator-ui build
pnpm verify:branding
```

## Runtime smoke tests

Start the Agent API and use a real or test match ID.

```text
GET /api/broadcast/score-bug.svg?matchId=<MATCH_ID>
GET /api/broadcast/event-banner.svg?matchId=<MATCH_ID>&eventType=goal&team=Home&playerNumber=14
GET /api/broadcast/event-banner.svg?matchId=<MATCH_ID>&eventType=two_point_score&team=Home&playerNumber=11
GET /api/broadcast/event-banner.svg?matchId=<MATCH_ID>&eventType=red_card&team=Away&playerNumber=5
GET /api/broadcast/match-intro.svg?matchId=<MATCH_ID>&throwIn=19:30
```

## Acceptance checks

1. Hurling/Camogie/LGFA render Goals–Points with total in brackets.
2. Men's Gaelic football renders Goals–2pt–1pt with total in brackets.
3. Two-point event is rejected by the scoring domain outside men's football.
4. Red-card indicator remains on the relevant team side of the score bug.
5. Black-card indicator displays only while the black-card state is active.
6. Event banners use the canonical AMHARC logo file; no logo is reconstructed in SVG/CSS/text.
7. Two-point banner is visibly distinct and shows `+2`.
8. Creating an event triggers transient `BroadcastState.CurrentGraphic` state.
9. Intro animation runs to 10 seconds and uses match competition, teams, venue and date.
10. `pnpm verify:branding` passes.

## Git gate

Do not push to the main branch until the build, tests and smoke checks above pass. Push this batch first to the existing feature branch or a dedicated broadcast feature branch for review.
