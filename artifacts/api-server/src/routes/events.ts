import { Router, type IRouter } from "express";
import { randomUUID } from "crypto";
import {
  GetMatchEventsParams,
  CreateMatchEventParams,
  CreateMatchEventBody,
  UpdateMatchEventParams,
  UpdateMatchEventBody,
  DeleteMatchEventParams,
  UndoLastEventParams,
} from "@workspace/api-zod";

const router: IRouter = Router();

// In-memory event store keyed by matchId
const eventStore = new Map<string, any[]>([
  [
    "AMHARC-2026-000144",
    [
      { eventId: "EVT-000001", matchId: "AMHARC-2026-000144", eventType: "period-start", team: null, playerId: null, playerNumber: null, period: 1, matchClockSeconds: 0, recordingElapsedSeconds: 0, systemTimestamp: "2026-07-10T14:02:00Z", source: "operator-ui", operator: "Seán Ó Briain", note: null, scoreBefore: null, scoreAfter: null, clipRequested: false, reviewStatus: "reviewed", createdAt: "2026-07-10T14:02:00Z", updatedAt: "2026-07-10T14:02:00Z" },
      { eventId: "EVT-000002", matchId: "AMHARC-2026-000144", eventType: "puck-out", team: "home", playerId: null, playerNumber: 1, period: 1, matchClockSeconds: 45, recordingElapsedSeconds: 45, systemTimestamp: "2026-07-10T14:02:45Z", source: "stream-deck", operator: "Seán Ó Briain", note: null, scoreBefore: null, scoreAfter: null, clipRequested: false, reviewStatus: "unreviewed", createdAt: "2026-07-10T14:02:45Z", updatedAt: "2026-07-10T14:02:45Z" },
      { eventId: "EVT-000003", matchId: "AMHARC-2026-000144", eventType: "goal", team: "home", playerId: null, playerNumber: 13, period: 1, matchClockSeconds: 312, recordingElapsedSeconds: 312, systemTimestamp: "2026-07-10T14:07:12Z", source: "stream-deck", operator: "Seán Ó Briain", note: "Long-range effort from 65m", scoreBefore: "0-0 / 0-0", scoreAfter: "1-0 / 0-0", clipRequested: true, reviewStatus: "reviewed", createdAt: "2026-07-10T14:07:12Z", updatedAt: "2026-07-10T14:07:30Z" },
      { eventId: "EVT-000004", matchId: "AMHARC-2026-000144", eventType: "point", team: "away", playerId: null, playerNumber: 9, period: 1, matchClockSeconds: 498, recordingElapsedSeconds: 498, systemTimestamp: "2026-07-10T14:10:18Z", source: "stream-deck", operator: "Seán Ó Briain", note: null, scoreBefore: "1-0 / 0-0", scoreAfter: "1-0 / 0-1", clipRequested: false, reviewStatus: "unreviewed", createdAt: "2026-07-10T14:10:18Z", updatedAt: "2026-07-10T14:10:18Z" },
      { eventId: "EVT-000005", matchId: "AMHARC-2026-000144", eventType: "card", team: "away", playerId: null, playerNumber: 7, period: 2, matchClockSeconds: 2640, recordingElapsedSeconds: 2640, systemTimestamp: "2026-07-10T15:26:00Z", source: "operator-ui", operator: "Seán Ó Briain", note: "Yellow card — late challenge", scoreBefore: null, scoreAfter: null, clipRequested: true, reviewStatus: "flagged", createdAt: "2026-07-10T15:26:00Z", updatedAt: "2026-07-10T15:26:05Z" },
    ],
  ],
]);

let eventCounter = 5;

router.get("/matches/:matchId/events", async (req, res): Promise<void> => {
  const params = GetMatchEventsParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const events = eventStore.get(params.data.matchId) ?? [];
  res.json(events);
});

router.post("/matches/:matchId/events", async (req, res): Promise<void> => {
  const params = CreateMatchEventParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const parsed = CreateMatchEventBody.safeParse(req.body);
  if (!parsed.success) {
    res.status(400).json({ error: parsed.error.message });
    return;
  }
  eventCounter++;
  const eventId = `EVT-${String(eventCounter).padStart(6, "0")}`;
  const now = new Date().toISOString();
  const event = {
    eventId,
    matchId: params.data.matchId,
    ...parsed.data,
    playerId: parsed.data.playerId ?? null,
    playerNumber: parsed.data.playerNumber ?? null,
    operator: null,
    note: parsed.data.note ?? null,
    scoreBefore: null,
    scoreAfter: null,
    clipRequested: parsed.data.clipRequested ?? false,
    reviewStatus: "unreviewed",
    systemTimestamp: now,
    createdAt: now,
    updatedAt: now,
  };
  const events = eventStore.get(params.data.matchId) ?? [];
  events.push(event);
  eventStore.set(params.data.matchId, events);
  res.status(201).json(event);
});

router.put("/matches/:matchId/events/:eventId", async (req, res): Promise<void> => {
  const params = UpdateMatchEventParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const parsed = UpdateMatchEventBody.safeParse(req.body);
  if (!parsed.success) {
    res.status(400).json({ error: parsed.error.message });
    return;
  }
  const events = eventStore.get(params.data.matchId) ?? [];
  const idx = events.findIndex((e) => e.eventId === params.data.eventId);
  if (idx === -1) {
    res.status(404).json({ error: "Event not found" });
    return;
  }
  events[idx] = { ...events[idx], ...parsed.data, updatedAt: new Date().toISOString() };
  eventStore.set(params.data.matchId, events);
  res.json(events[idx]);
});

router.delete("/matches/:matchId/events/:eventId", async (req, res): Promise<void> => {
  const params = DeleteMatchEventParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const events = eventStore.get(params.data.matchId) ?? [];
  const idx = events.findIndex((e) => e.eventId === params.data.eventId);
  if (idx === -1) {
    res.status(404).json({ error: "Event not found" });
    return;
  }
  events.splice(idx, 1);
  eventStore.set(params.data.matchId, events);
  res.sendStatus(204);
});

router.post("/matches/:matchId/events/undo", async (req, res): Promise<void> => {
  const params = UndoLastEventParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const events = eventStore.get(params.data.matchId) ?? [];
  if (events.length === 0) {
    res.json({ success: false, message: "No events to undo" });
    return;
  }
  const removed = events.pop();
  eventStore.set(params.data.matchId, events);
  req.log.info({ matchId: params.data.matchId, undoneEventId: removed?.eventId }, "Event undone");
  res.json({ success: true, message: `Event ${removed?.eventId} undone` });
});

export default router;
