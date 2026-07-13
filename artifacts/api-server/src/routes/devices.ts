import { Router, type IRouter } from "express";
import { randomUUID } from "crypto";
import {
  CreateStreamDeckProfileBody,
} from "@workspace/api-zod";

const router: IRouter = Router();

// Mock device state — production reads from Windows HID/DirectInput
const streamDeckState = {
  connected: false,
  deviceName: null as string | null,
  buttonCount: 15,
  activeProfileId: "PROFILE-GF-DEFAULT",
  firmwareVersion: null as string | null,
};

const joystickState = {
  connected: false,
  deviceName: null as string | null,
  axisCount: 3,
  buttonCount: 12,
  vendorId: null as string | null,
  productId: null as string | null,
};

const streamDeckProfiles = new Map<string, any>([
  [
    "PROFILE-GF-DEFAULT",
    {
      profileId: "PROFILE-GF-DEFAULT",
      name: "Gaelic Football — Default",
      sport: "gaelic-football",
      isDefault: true,
      createdAt: "2026-01-01T00:00:00Z",
      buttons: [
        { buttonNumber: 1, label: "Home Score", icon: null, colour: "#1C8551", eventType: "score", team: "home", scoreEffect: "point", overlayEffect: "score", clipRequest: false, enabled: true },
        { buttonNumber: 2, label: "Away Score", icon: null, colour: "#1C8551", eventType: "score", team: "away", scoreEffect: "point", overlayEffect: "score", clipRequest: false, enabled: true },
        { buttonNumber: 3, label: "Goal", icon: null, colour: "#B6DC46", eventType: "goal", team: null, scoreEffect: "goal", overlayEffect: "goal", clipRequest: true, enabled: true },
        { buttonNumber: 4, label: "Point", icon: null, colour: "#B6DC46", eventType: "point", team: null, scoreEffect: "point", overlayEffect: "point", clipRequest: false, enabled: true },
        { buttonNumber: 5, label: "2-Point", icon: null, colour: "#B6DC46", eventType: "two-point-score", team: null, scoreEffect: "two-point", overlayEffect: "two-point", clipRequest: true, enabled: true },
        { buttonNumber: 6, label: "Kick-out", icon: null, colour: "#FFFFFF", eventType: "kick-out", team: null, scoreEffect: null, overlayEffect: null, clipRequest: false, enabled: true },
        { buttonNumber: 7, label: "Turnover", icon: null, colour: "#FFFFFF", eventType: "turnover", team: null, scoreEffect: null, overlayEffect: null, clipRequest: false, enabled: true },
        { buttonNumber: 8, label: "Shot", icon: null, colour: "#FFFFFF", eventType: "shot", team: null, scoreEffect: null, overlayEffect: null, clipRequest: false, enabled: true },
        { buttonNumber: 9, label: "Free", icon: null, colour: "#FFFFFF", eventType: "free", team: null, scoreEffect: null, overlayEffect: null, clipRequest: false, enabled: true },
        { buttonNumber: 10, label: "Mark", icon: null, colour: "#FFFFFF", eventType: "mark", team: null, scoreEffect: null, overlayEffect: null, clipRequest: false, enabled: true },
        { buttonNumber: 11, label: "Card", icon: null, colour: "#FFFFFF", eventType: "card", team: null, scoreEffect: null, overlayEffect: "card", clipRequest: true, enabled: true },
        { buttonNumber: 12, label: "Sub", icon: null, colour: "#FFFFFF", eventType: "substitution", team: null, scoreEffect: null, overlayEffect: "substitution", clipRequest: false, enabled: true },
        { buttonNumber: 13, label: "Incident", icon: null, colour: "#FFFFFF", eventType: "major-incident", team: null, scoreEffect: null, overlayEffect: null, clipRequest: true, enabled: true },
        { buttonNumber: 14, label: "Highlight", icon: null, colour: "#B6DC46", eventType: "highlight", team: null, scoreEffect: null, overlayEffect: null, clipRequest: true, enabled: true },
        { buttonNumber: 15, label: "Period", icon: null, colour: "#1C8551", eventType: "period-start", team: null, scoreEffect: null, overlayEffect: null, clipRequest: false, enabled: true },
      ],
    },
  ],
  [
    "PROFILE-HU-DEFAULT",
    {
      profileId: "PROFILE-HU-DEFAULT",
      name: "Hurling — Default",
      sport: "hurling",
      isDefault: true,
      createdAt: "2026-01-01T00:00:00Z",
      buttons: [
        { buttonNumber: 1, label: "Home Score", icon: null, colour: "#1C8551", eventType: "score", team: "home", scoreEffect: "point", overlayEffect: "score", clipRequest: false, enabled: true },
        { buttonNumber: 2, label: "Away Score", icon: null, colour: "#1C8551", eventType: "score", team: "away", scoreEffect: "point", overlayEffect: "score", clipRequest: false, enabled: true },
        { buttonNumber: 3, label: "Goal", icon: null, colour: "#B6DC46", eventType: "goal", team: null, scoreEffect: "goal", overlayEffect: "goal", clipRequest: true, enabled: true },
        { buttonNumber: 4, label: "Point", icon: null, colour: "#B6DC46", eventType: "point", team: null, scoreEffect: "point", overlayEffect: "point", clipRequest: false, enabled: true },
        { buttonNumber: 5, label: "Puck-out", icon: null, colour: "#FFFFFF", eventType: "puck-out", team: null, scoreEffect: null, overlayEffect: null, clipRequest: false, enabled: true },
        { buttonNumber: 6, label: "Shot", icon: null, colour: "#FFFFFF", eventType: "shot", team: null, scoreEffect: null, overlayEffect: null, clipRequest: false, enabled: true },
        { buttonNumber: 7, label: "Turnover", icon: null, colour: "#FFFFFF", eventType: "turnover", team: null, scoreEffect: null, overlayEffect: null, clipRequest: false, enabled: true },
        { buttonNumber: 8, label: "Free", icon: null, colour: "#FFFFFF", eventType: "free", team: null, scoreEffect: null, overlayEffect: null, clipRequest: false, enabled: true },
        { buttonNumber: 9, label: "Sideline", icon: null, colour: "#FFFFFF", eventType: "sideline-cut", team: null, scoreEffect: null, overlayEffect: null, clipRequest: false, enabled: true },
        { buttonNumber: 10, label: "Hook", icon: null, colour: "#FFFFFF", eventType: "hook", team: null, scoreEffect: null, overlayEffect: null, clipRequest: false, enabled: true },
        { buttonNumber: 11, label: "Block", icon: null, colour: "#FFFFFF", eventType: "block", team: null, scoreEffect: null, overlayEffect: null, clipRequest: false, enabled: true },
        { buttonNumber: 12, label: "Card", icon: null, colour: "#FFFFFF", eventType: "card", team: null, scoreEffect: null, overlayEffect: "card", clipRequest: true, enabled: true },
        { buttonNumber: 13, label: "Sub", icon: null, colour: "#FFFFFF", eventType: "substitution", team: null, scoreEffect: null, overlayEffect: "substitution", clipRequest: false, enabled: true },
        { buttonNumber: 14, label: "Highlight", icon: null, colour: "#B6DC46", eventType: "highlight", team: null, scoreEffect: null, overlayEffect: null, clipRequest: true, enabled: true },
        { buttonNumber: 15, label: "Period", icon: null, colour: "#1C8551", eventType: "period-start", team: null, scoreEffect: null, overlayEffect: null, clipRequest: false, enabled: true },
      ],
    },
  ],
]);

router.get("/devices", async (_req, res): Promise<void> => {
  res.json({
    streamDeck: streamDeckState,
    joystick: joystickState,
  });
});

router.get("/devices/stream-deck", async (_req, res): Promise<void> => {
  res.json(streamDeckState);
});

router.get("/devices/joystick", async (_req, res): Promise<void> => {
  res.json(joystickState);
});

router.get("/stream-deck/profiles", async (_req, res): Promise<void> => {
  res.json(Array.from(streamDeckProfiles.values()));
});

router.post("/stream-deck/profiles", async (req, res): Promise<void> => {
  const parsed = CreateStreamDeckProfileBody.safeParse(req.body);
  if (!parsed.success) {
    res.status(400).json({ error: parsed.error.message });
    return;
  }
  const profileId = `PROFILE-${randomUUID().substring(0, 8).toUpperCase()}`;
  const profile = {
    profileId,
    ...parsed.data,
    isDefault: parsed.data.isDefault ?? false,
    createdAt: new Date().toISOString(),
  };
  streamDeckProfiles.set(profileId, profile);
  res.status(201).json(profile);
});

export default router;
