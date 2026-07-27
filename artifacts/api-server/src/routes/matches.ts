import { Router, type IRouter } from "express";
import { randomUUID } from "crypto";
import {
  CreateMatchBody,
  UpdateMatchBody,
  GetMatchParams,
  UpdateMatchParams,
  StartMatchParams,
  StopMatchParams,
  StartMatchClockParams,
  PauseMatchClockParams,
  ResumeMatchClockParams,
  CorrectMatchClockParams,
  CorrectMatchClockBody,
  GetMatchClockParams,
  GetMatchScoreParams,
  UpdateMatchScoreParams,
  UpdateMatchScoreBody,
} from "@workspace/api-zod";

const router: IRouter = Router();

let matchCounter = 145;

// In-memory mock stores
const matches = new Map<string, any>([
  [
    "AMHARC-2026-000144",
    {
      matchId: "AMHARC-2026-000144",
      humanId: "AMHARC-2026-000144",
      sport: "hurling",
      competition: "Senior Hurling Championship",
      season: "2026",
      round: "Round 2",
      date: "2026-07-10",
      venue: "Páirc na Féile",
      homeTeam: "St Brigid's",
      awayTeam: "Na Piarsaigh",
      homeTeamShort: "BRI",
      awayTeamShort: "PIR",
      homeTeamColour: "#1C8551",
      awayTeamColour: "#003366",
      operator: "Seán Ó Briain",
      scheduledStart: "2026-07-10T14:00:00Z",
      periodStructure: "halves",
      expectedDurationMinutes: 70,
      recordingDirectory: "C:/Matches/2026/2026-07-10_St-Brigids_v_Na-Piarsaigh",
      cameraId: "CAM-AXIS-001",
      streamProfile: "Quality",
      overlayTemplate: "amharc-standard-scoreboard",
      streamDestination: null,
      notes: "County final warm-up fixture.",
      status: "complete",
      currentPeriod: 2,
      createdAt: "2026-07-09T10:00:00Z",
      updatedAt: "2026-07-10T16:15:00Z",
    },
  ],
]);

const clocks = new Map<string, any>();
const scores = new Map<string, any>();


function scoringModelForSport(sport: string) {
  return sport === "gaelic-football" ? "goals-two-point-one-point" : "goals-points";
}

function createScoreState(matchId: string) {
  const match = matches.get(matchId);
  const sport = match?.sport ?? "gaelic-football";
  return {
    matchId,
    sport,
    scoringModel: scoringModelForSport(sport),
    homeGoals: 0,
    homeTwoPointScores: 0,
    homePoints: 0,
    homeTotal: 0,
    homeDisplay: sport === "gaelic-football" ? "0-0-0 (0)" : "0-0 (0)",
    awayGoals: 0,
    awayTwoPointScores: 0,
    awayPoints: 0,
    awayTotal: 0,
    awayDisplay: sport === "gaelic-football" ? "0-0-0 (0)" : "0-0 (0)",
    updatedAt: null as string | null,
  };
}

function refreshScoreState(score: any) {
  const isMensFootball = score.sport === "gaelic-football";
  score.scoringModel = scoringModelForSport(score.sport);
  score.homeTotal = score.homeGoals * 3 + score.homeTwoPointScores * 2 + score.homePoints;
  score.awayTotal = score.awayGoals * 3 + score.awayTwoPointScores * 2 + score.awayPoints;
  const homeCore = isMensFootball
    ? `${score.homeGoals}-${score.homeTwoPointScores}-${score.homePoints}`
    : `${score.homeGoals}-${score.homePoints}`;
  const awayCore = isMensFootball
    ? `${score.awayGoals}-${score.awayTwoPointScores}-${score.awayPoints}`
    : `${score.awayGoals}-${score.awayPoints}`;
  score.homeDisplay = `${homeCore} (${score.homeTotal})`;
  score.awayDisplay = `${awayCore} (${score.awayTotal})`;
  return score;
}

function getMatchSecondsFromNow(startedAt: string | null): number {
  if (!startedAt) return 0;
  return Math.floor((Date.now() - new Date(startedAt).getTime()) / 1000);
}

router.get("/matches", async (_req, res): Promise<void> => {
  res.json(Array.from(matches.values()));
});

router.post("/matches", async (req, res): Promise<void> => {
  const parsed = CreateMatchBody.safeParse(req.body);
  if (!parsed.success) {
    res.status(400).json({ error: parsed.error.message });
    return;
  }
  matchCounter++;
  const matchId = `AMHARC-${new Date().getFullYear()}-${String(matchCounter).padStart(6, "0")}`;
  const match = {
    matchId,
    humanId: matchId,
    ...parsed.data,
    status: "setup",
    currentPeriod: null,
    createdAt: new Date().toISOString(),
    updatedAt: null,
  };
  matches.set(matchId, match);
  // Initialise clock and score
  clocks.set(matchId, {
    matchId,
    matchClockSeconds: 0,
    recordingElapsedSeconds: 0,
    isRunning: false,
    currentPeriod: 1,
    clockMode: "count-up",
    updatedAt: new Date().toISOString(),
    _startedAt: null,
    _recordingStartedAt: null,
  });
  scores.set(matchId, createScoreState(matchId));
  res.status(201).json(match);
});

router.get("/matches/:matchId", async (req, res): Promise<void> => {
  const params = GetMatchParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const match = matches.get(params.data.matchId);
  if (!match) {
    res.status(404).json({ error: "Match not found" });
    return;
  }
  res.json(match);
});

router.put("/matches/:matchId", async (req, res): Promise<void> => {
  const params = UpdateMatchParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const match = matches.get(params.data.matchId);
  if (!match) {
    res.status(404).json({ error: "Match not found" });
    return;
  }
  const parsed = UpdateMatchBody.safeParse(req.body);
  if (!parsed.success) {
    res.status(400).json({ error: parsed.error.message });
    return;
  }
  const updated = { ...match, ...parsed.data, updatedAt: new Date().toISOString() };
  matches.set(params.data.matchId, updated);
  res.json(updated);
});

router.post("/matches/:matchId/start", async (req, res): Promise<void> => {
  const params = StartMatchParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const match = matches.get(params.data.matchId);
  if (!match) {
    res.status(404).json({ error: "Match not found" });
    return;
  }
  match.status = "active";
  match.currentPeriod = 1;
  match.updatedAt = new Date().toISOString();
  matches.set(params.data.matchId, match);
  res.json({ success: true, message: "Match started" });
});

router.post("/matches/:matchId/stop", async (req, res): Promise<void> => {
  const params = StopMatchParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const match = matches.get(params.data.matchId);
  if (!match) {
    res.status(404).json({ error: "Match not found" });
    return;
  }
  match.status = "complete";
  match.updatedAt = new Date().toISOString();
  matches.set(params.data.matchId, match);
  res.json({ success: true, message: "Match complete" });
});

// Clock endpoints
router.get("/matches/:matchId/clock", async (req, res): Promise<void> => {
  const params = GetMatchClockParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  let clock = clocks.get(params.data.matchId);
  if (!clock) {
    clock = {
      matchId: params.data.matchId,
      matchClockSeconds: 0,
      recordingElapsedSeconds: 0,
      isRunning: false,
      currentPeriod: 1,
      clockMode: "count-up",
      updatedAt: new Date().toISOString(),
      _startedAt: null,
      _recordingStartedAt: null,
    };
    clocks.set(params.data.matchId, clock);
  }
  if (clock.isRunning && clock._startedAt) {
    const elapsed = getMatchSecondsFromNow(clock._startedAt);
    clock.matchClockSeconds = clock._baseMatchSeconds + elapsed;
    clock.recordingElapsedSeconds = clock._baseRecordingSeconds + elapsed;
  }
  res.json({
    matchId: clock.matchId,
    matchClockSeconds: clock.matchClockSeconds,
    recordingElapsedSeconds: clock.recordingElapsedSeconds,
    isRunning: clock.isRunning,
    currentPeriod: clock.currentPeriod,
    clockMode: clock.clockMode,
    updatedAt: clock.updatedAt,
  });
});

router.post("/matches/:matchId/clock/start", async (req, res): Promise<void> => {
  const params = StartMatchClockParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  let clock = clocks.get(params.data.matchId) ?? {
    matchId: params.data.matchId,
    matchClockSeconds: 0,
    recordingElapsedSeconds: 0,
    isRunning: false,
    currentPeriod: 1,
    clockMode: "count-up",
    _startedAt: null,
    _baseMatchSeconds: 0,
    _baseRecordingSeconds: 0,
  };
  clock.isRunning = true;
  clock._startedAt = new Date().toISOString();
  clock._baseMatchSeconds = clock.matchClockSeconds;
  clock._baseRecordingSeconds = clock.recordingElapsedSeconds;
  clock.updatedAt = new Date().toISOString();
  clocks.set(params.data.matchId, clock);
  res.json({ matchId: clock.matchId, matchClockSeconds: clock.matchClockSeconds, recordingElapsedSeconds: clock.recordingElapsedSeconds, isRunning: true, currentPeriod: clock.currentPeriod, clockMode: clock.clockMode, updatedAt: clock.updatedAt });
});

router.post("/matches/:matchId/clock/pause", async (req, res): Promise<void> => {
  const params = PauseMatchClockParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const clock = clocks.get(params.data.matchId);
  if (clock && clock.isRunning && clock._startedAt) {
    const elapsed = getMatchSecondsFromNow(clock._startedAt);
    clock.matchClockSeconds = clock._baseMatchSeconds + elapsed;
    clock.recordingElapsedSeconds = clock._baseRecordingSeconds + elapsed;
    clock.isRunning = false;
    clock._startedAt = null;
    clock.updatedAt = new Date().toISOString();
    clocks.set(params.data.matchId, clock);
  }
  const c = clock ?? { matchId: params.data.matchId, matchClockSeconds: 0, recordingElapsedSeconds: 0, isRunning: false, currentPeriod: 1, clockMode: "count-up", updatedAt: new Date().toISOString() };
  res.json({ matchId: c.matchId, matchClockSeconds: c.matchClockSeconds, recordingElapsedSeconds: c.recordingElapsedSeconds, isRunning: false, currentPeriod: c.currentPeriod, clockMode: c.clockMode, updatedAt: c.updatedAt });
});

router.post("/matches/:matchId/clock/resume", async (req, res): Promise<void> => {
  const params = ResumeMatchClockParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const clock = clocks.get(params.data.matchId);
  if (clock && !clock.isRunning) {
    clock.isRunning = true;
    clock._startedAt = new Date().toISOString();
    clock._baseMatchSeconds = clock.matchClockSeconds;
    clock._baseRecordingSeconds = clock.recordingElapsedSeconds;
    clock.updatedAt = new Date().toISOString();
    clocks.set(params.data.matchId, clock);
  }
  const c = clock ?? { matchId: params.data.matchId, matchClockSeconds: 0, recordingElapsedSeconds: 0, isRunning: true, currentPeriod: 1, clockMode: "count-up", updatedAt: new Date().toISOString() };
  res.json({ matchId: c.matchId, matchClockSeconds: c.matchClockSeconds, recordingElapsedSeconds: c.recordingElapsedSeconds, isRunning: true, currentPeriod: c.currentPeriod, clockMode: c.clockMode, updatedAt: c.updatedAt });
});

router.post("/matches/:matchId/clock/correct", async (req, res): Promise<void> => {
  const params = CorrectMatchClockParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const parsed = CorrectMatchClockBody.safeParse(req.body);
  if (!parsed.success) {
    res.status(400).json({ error: parsed.error.message });
    return;
  }
  let clock = clocks.get(params.data.matchId);
  if (!clock) {
    clock = { matchId: params.data.matchId, matchClockSeconds: 0, recordingElapsedSeconds: 0, isRunning: false, currentPeriod: 1, clockMode: "count-up", _startedAt: null, _baseMatchSeconds: 0, _baseRecordingSeconds: 0 };
  }
  clock.matchClockSeconds = parsed.data.matchClockSeconds;
  clock._baseMatchSeconds = parsed.data.matchClockSeconds;
  if (clock._startedAt) clock._startedAt = new Date().toISOString();
  clock.updatedAt = new Date().toISOString();
  clocks.set(params.data.matchId, clock);
  req.log.info({ matchId: params.data.matchId, correctedTo: parsed.data.matchClockSeconds, reason: parsed.data.reason }, "Match clock corrected");
  res.json({ matchId: clock.matchId, matchClockSeconds: clock.matchClockSeconds, recordingElapsedSeconds: clock.recordingElapsedSeconds, isRunning: clock.isRunning, currentPeriod: clock.currentPeriod, clockMode: clock.clockMode, updatedAt: clock.updatedAt });
});

// Score endpoints
router.get("/matches/:matchId/score", async (req, res): Promise<void> => {
  const params = GetMatchScoreParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  let score = scores.get(params.data.matchId);
  if (!score) {
    score = createScoreState(params.data.matchId);
    scores.set(params.data.matchId, score);
  }
  res.json(refreshScoreState(score));
});

router.put("/matches/:matchId/score", async (req, res): Promise<void> => {
  const params = UpdateMatchScoreParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const parsed = UpdateMatchScoreBody.safeParse(req.body);
  if (!parsed.success) {
    res.status(400).json({ error: parsed.error.message });
    return;
  }
  let score = scores.get(params.data.matchId) ?? createScoreState(params.data.matchId);
  const { team, scoreType, delta } = parsed.data;

  if (scoreType === "two-point" && score.sport !== "gaelic-football") {
    res.status(400).json({ error: "Two-point scores are valid only for men's Gaelic football." });
    return;
  }

  if (team === "home") {
    if (scoreType === "goal") score.homeGoals = Math.max(0, score.homeGoals + delta);
    else if (scoreType === "point") score.homePoints = Math.max(0, score.homePoints + delta);
    else if (scoreType === "two-point") score.homeTwoPointScores = Math.max(0, score.homeTwoPointScores + delta);
  } else {
    if (scoreType === "goal") score.awayGoals = Math.max(0, score.awayGoals + delta);
    else if (scoreType === "point") score.awayPoints = Math.max(0, score.awayPoints + delta);
    else if (scoreType === "two-point") score.awayTwoPointScores = Math.max(0, score.awayTwoPointScores + delta);
  }

  refreshScoreState(score);
  score.updatedAt = new Date().toISOString();
  scores.set(params.data.matchId, score);
  res.json(score);
});

export default router;
export { clocks, scores };
