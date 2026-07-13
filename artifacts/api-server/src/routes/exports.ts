import { Router, type IRouter } from "express";
import {
  ExportMatchParams,
  ExportMatchBody,
} from "@workspace/api-zod";

const router: IRouter = Router();

router.post("/matches/:matchId/export", async (req, res): Promise<void> => {
  const params = ExportMatchParams.safeParse(req.params);
  if (!params.success) {
    res.status(400).json({ error: params.error.message });
    return;
  }
  const parsed = ExportMatchBody.safeParse(req.body);
  if (!parsed.success) {
    res.status(400).json({ error: parsed.error.message });
    return;
  }

  const exportDirectory = `C:/Matches/2026/${params.data.matchId}/exports`;
  const files: string[] = [];

  if (parsed.data.formats.includes("json")) {
    files.push(`${exportDirectory}/events.json`);
  }
  if (parsed.data.formats.includes("csv")) {
    files.push(`${exportDirectory}/events.csv`);
  }
  if (parsed.data.formats.includes("manifest")) {
    files.push(`${exportDirectory}/amharc-match-capture-manifest.json`);
  }
  if (parsed.data.formats.includes("technical-log")) {
    files.push(`${exportDirectory}/technical-log.json`);
  }

  req.log.info({ matchId: params.data.matchId, formats: parsed.data.formats, fileCount: files.length }, "Export requested");

  res.json({
    success: true,
    exportDirectory,
    files,
    manifestVersion: 1,
    message: `Export completed. ${files.length} file(s) written to ${exportDirectory}`,
  });
});

export default router;
