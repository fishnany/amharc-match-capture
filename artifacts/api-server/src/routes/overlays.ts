import { Router, type IRouter } from "express";

const router: IRouter = Router();

const templates = [
  { templateId: "TMPL-001", name: "AMHARC Standard Scoreboard", type: "standard-scoreboard", isDefault: true, description: "Full scoreboard with team names, crest placeholders, match clock and period" },
  { templateId: "TMPL-002", name: "AMHARC Compact Scoreboard", type: "compact-scoreboard", isDefault: false, description: "Compact score bar suitable for widescreen placement" },
  { templateId: "TMPL-003", name: "AMHARC Lower Third", type: "lower-third", isDefault: false, description: "Player or event lower-third caption" },
  { templateId: "TMPL-004", name: "AMHARC Goal Graphic", type: "goal", isDefault: false, description: "Full-screen goal celebration graphic" },
  { templateId: "TMPL-005", name: "AMHARC Point Graphic", type: "point", isDefault: false, description: "Point scored notification" },
  { templateId: "TMPL-006", name: "AMHARC Two-Point Graphic", type: "two-point", isDefault: false, description: "Two-point score graphic" },
  { templateId: "TMPL-007", name: "AMHARC Card Graphic", type: "card", isDefault: false, description: "Yellow or red card graphic" },
  { templateId: "TMPL-008", name: "AMHARC Substitution Graphic", type: "substitution", isDefault: false, description: "Player substitution graphic" },
  { templateId: "TMPL-009", name: "AMHARC Half-Time Graphic", type: "half-time", isDefault: false, description: "Half-time score summary" },
  { templateId: "TMPL-010", name: "AMHARC Full-Time Graphic", type: "full-time", isDefault: false, description: "Final score and result graphic" },
  { templateId: "TMPL-011", name: "AMHARC Technical Interruption", type: "technical-interruption", isDefault: false, description: "Please stand by notice" },
  { templateId: "TMPL-012", name: "AMHARC Starting Soon", type: "starting-soon", isDefault: false, description: "Pre-match countdown and fixture information" },
];

const overlayState = {
  activeTemplateId: "TMPL-001" as string | null,
  isVisible: false,
  outputMode: "operator-preview" as string,
  currentGraphic: null as string | null,
  graphicVisible: false,
};

router.get("/overlays/templates", async (_req, res): Promise<void> => {
  res.json(templates);
});

router.get("/overlays/state", async (_req, res): Promise<void> => {
  res.json(overlayState);
});

export default router;
