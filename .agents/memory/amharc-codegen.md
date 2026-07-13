---
name: AMHARC Codegen Setup
description: How the OpenAPI → Orval → React Query codegen is configured and a known collision fix
---

## Setup

OpenAPI spec: `lib/api-spec/openapi.yaml`
Codegen command: `pnpm --filter @workspace/api-spec run codegen`
Output: `lib/api-client-react/src/generated/` (React Query hooks) and `lib/api-zod/src/generated/` (Zod schemas)

## Known Orval Collision

**Problem:** Orval emits `GetMatchEventsParams` in both `api.ts` (query param schema) and `generated/types/` (TypeScript type). Barrel `export *` from both causes duplicate export TS2308.

**Fix:** Remove query parameters from the `getMatchEvents` operation in the OpenAPI spec. Filtering is done client-side. This fix is already applied.

**Why:** Orval derives `<OperationIdPascal>Params` automatically for query params. If a route has query params AND any component references the same name, it collides.

**How to apply:** If a new operation with query params causes the same error, either (a) remove/inline the query params, or (b) rename the operationId so Orval's derived name doesn't collide with any existing component schema name.
