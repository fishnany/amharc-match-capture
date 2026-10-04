# W1 targeted development composition

This candidate is development/conformance-only. It grants no operational
issuer, allocator, format, principal or field appointment. Controlled-main
integration, deployment and real-match activation require separate Owner decisions.

## Capture

Ordinary Beta behavior remains independent and legacy-qualified. The W1 path is
registered by the actual API Program only when both the host environment is
Development and `W1:DevelopmentOnly` is true. Required explicit configuration:

- `W1:Issuer`, `W1:Actor`, `W1:Build`: synthetic development attribution.
- `W1:DependencyDirectory`: byte-exact controlled `governance/wave-1/v1.0.0`.
- `W1:ScratchJournalPath`: a separate explicitly synthetic journal file.
- `W1:GovernancePublicKeyFile`: external development anchor, never inferred from a received envelope.
- `W1:DevelopmentSigningSeedFile`: externally provisioned synthetic Ed25519 seed
  in base64; no default key is included and no private key is exported.
- `W1:InitialPeriodKey`: explicit key admitted by the exact controlled format.

Only use synthetic/test-local Match storage while enabled. The application never
enables this configuration automatically. Missing configuration is unavailable,
not equivalent legacy authority.

The actual operator setup form retains one `Idempotency-Key` for its mounted
creation intent and retries. `POST /api/matches` in explicit W1 development uses
the transactional prospective creation repository and returns the existing local
Match shape. No historical matches acquire canonical IDs.

Development protocol under `/api/w1-development`:

1. `POST prepare/{localId}` supplies a resolution document matching the durable
   provisional occurrence/issuer/local correspondence. The host returns a new
   incarnation challenge; this grants no eligibility.
2. External synthetic governance signs a bundle naming that exact occurrence,
   resolution, incarnation and capabilities. `POST activate/{subject}/{operationKey}`
   verifies it against the explicitly configured anchor.
3. `POST command/{subject}` invokes only capability-checked W1 commands. Wrong
   subjects refuse before state/order/history changes. Snapshot cannot imply advance.
4. `GET history/{subject}` returns the exact current head and bounded membership.
   External governance signs an observation closure naming this head/chain.
   `POST closure/{subject}` verifies it, without signing governance inside Capture.
5. `GET clock/{subject}` returns the actual separately signed W1 envelope.
   `GET context` exports only public exact JSON dependency bytes.

The issuer-qualified allocation domain is limited to newly issued provisional
subjects in the explicit synthetic installation configuration; the configured
external anchor is scoped to the matched subject at preparation. This is not a
real appointment. Routine observations allocate ordering but no tick activities.
Their activityRef is the exact immutable material baseline/head. Historical
contexts retain the original grant reference; observation closures are separate.
Grant conflicts quarantine the composed instance until explicit fresh composition.

The journal refuses old/incoherent writers and material update/delete. Recovery
validates checkpoint head digest, unique graph head, every exact parent digest,
subject/context/dependencies, historical eligibility and governed baseline.
A legacy-assurance boundary is qualified as a gap, never filled with invented ancestry.

## Evidence harness

Run `W1ComposedHostTests` before the Tagger cross-language tests. It starts an
isolated actual ASP.NET DI/controller HTTP host with scratch SQLite, synthetic
keys and the same W1 composition extension used by Program. Only unrelated
hardware/overlay/readiness services are mocked. It performs eight simultaneous
operator-creation retries, binding, command, closure and producer HTTP calls.

The exact serialized successful response, exact negative HTTP receipts, public
dependencies and explicitly fault-derived signed negative envelopes are exported
to `capture-composed-output.json` beside the candidate directory. The seed is
temporary, never exported, and is zeroed/deleted afterward. Tagger tests require
this Capture output; absence fails rather than skipping or constructing a substitute.
The loopback replay transport is a harness, not a Windows field deployment.

## Compatibility

ClockSnapshotV1 and recordingElapsedSeconds keep their original meanings.
W1 does not invoke legacy clock/score/Event write paths. Old writers and destructive
identity-history rollback refuse. Native Windows distribution and real authority
remain later assurance gates, not targeted W1-1 exit requirements.