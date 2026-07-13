import { Router, type IRouter } from "express";
import healthRouter from "./health";
import systemRouter from "./system";
import camerasRouter from "./cameras";
import matchesRouter from "./matches";
import eventsRouter from "./events";
import recordingRouter from "./recording";
import streamingRouter from "./streaming";
import storageRouter from "./storage";
import devicesRouter from "./devices";
import overlaysRouter from "./overlays";
import exportsRouter from "./exports";

const router: IRouter = Router();

router.use(healthRouter);
router.use(systemRouter);
router.use(camerasRouter);
router.use(matchesRouter);
router.use(eventsRouter);
router.use(recordingRouter);
router.use(streamingRouter);
router.use(storageRouter);
router.use(devicesRouter);
router.use(overlaysRouter);
router.use(exportsRouter);

export default router;
