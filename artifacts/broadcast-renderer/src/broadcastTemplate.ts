export type BroadcastRendererTemplate =
  | "standard-scoreboard"
  | "fullscreen-score"
  | "lower-third"
  | "unsupported";

export interface BroadcastTemplateResolution {
  requestedTemplateId: string | null;
  template: BroadcastRendererTemplate;
}

export function resolveBroadcastTemplate(
  activeTemplateId: string | null | undefined,
): BroadcastTemplateResolution {
  const requestedTemplateId =
    activeTemplateId?.trim() ||
    null;

  /*
   * No explicit template is the historic/default scoreboard state.
   * Preserve that behaviour while routing through the canonical
   * presentation contract.
   */
  if (!requestedTemplateId) {
    return {
      requestedTemplateId: null,
      template: "standard-scoreboard",
    };
  }

  switch (requestedTemplateId) {
    case "standard-scoreboard":
      return {
        requestedTemplateId,
        template: "standard-scoreboard",
      };

    /*
     * Retained as a compatibility alias because existing canonical
     * publication tests use "scoreboard". New presentation control
     * should prefer "standard-scoreboard".
     */
    case "scoreboard":
      return {
        requestedTemplateId,
        template: "standard-scoreboard",
      };

    case "fullscreen-score":
      return {
        requestedTemplateId,
        template: "fullscreen-score",
      };

    case "lower-third":
      return {
        requestedTemplateId,
        template: "lower-third",
      };

    default:
      return {
        requestedTemplateId,
        template: "unsupported",
      };
  }
}
