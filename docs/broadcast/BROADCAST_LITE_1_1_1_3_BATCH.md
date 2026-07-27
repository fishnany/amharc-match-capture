# AMHARC Broadcast Lite — Phase 1 Batch

Status: implementation candidate for Replit validation.

## 1.1 Score Bug
Sport-aware SVG score bug, canonical AMHARC logo asset, totals, elapsed clock, replay state, red-card permanent indicator and active black-card indicator.

## 1.2 Event Banner System
Resolution-independent SVG event banners for scoring, outcomes, discipline, personnel, match control and restart/award events. Event creation triggers the transient broadcast graphic state. Men’s football two-point scoring has a distinct TWO-POINT SCORE +2 treatment.

Endpoint: `GET /api/broadcast/event-banner.svg?matchId=...&eventType=goal&team=Home&playerNumber=14`

## 1.3 Match Introduction Package
A reusable 1920x1080 SVG/SMIL ten-second intro timeline driven by match metadata. It uses the immutable master AMHARC logo asset rather than recreating the logo.

Endpoint: `GET /api/broadcast/match-intro.svg?matchId=...&throwIn=19:30`

## Validation gate
Run in Replit/Windows environment:

```bash
dotnet restore
dotnet build
dotnet test
pnpm --filter @workspace/api-spec codegen
pnpm typecheck
pnpm --filter @workspace/operator-ui build
pnpm verify:branding
```

The SVG intro is an application-native preview/rendering primitive; production video compositing/export remains a later Broadcast Lite pipeline step.
