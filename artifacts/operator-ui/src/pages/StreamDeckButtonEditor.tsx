import { useEffect, useState } from "react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";

export type StreamDeckButtonConfig = {
  buttonNumber: number;
  label: string;
  icon?: string | null;
  colour?: string | null;
  eventType: string;
  team?: number | null;
  scoreEffect?: string | null;
  overlayEffect?: string | null;
  clipRequest: boolean;
  enabled: boolean;
};

type StreamDeckButtonEditorProps = {
  open: boolean;
  buttonNumber: number | null;
  button?: StreamDeckButtonConfig | null;
  onOpenChange: (open: boolean) => void;
  onSave: (button: StreamDeckButtonConfig) => Promise<void> | void;
};

const AMHARC_COLOURS = [
  {
    label: "AMHARC Green",
    value: "#1C8551",
  },
  {
    label: "AMHARC Lime",
    value: "#B6DC46",
  },
  {
    label: "AMHARC Black",
    value: "#000000",
  },
  {
    label: "White",
    value: "#FFFFFF",
  },
  {
    label: "Stop Red",
    value: "#CC3333",
  },
];

function createDefaultButton(
  buttonNumber: number,
): StreamDeckButtonConfig {
  return {
    buttonNumber,
    label: "",
    icon: null,
    colour: "#1C8551",
    eventType: "",
    team: null,
    scoreEffect: null,
    overlayEffect: null,
    clipRequest: false,
    enabled: true,
  };
}

export function StreamDeckButtonEditor({
  open,
  buttonNumber,
  button,
  onOpenChange,
  onSave,
}: StreamDeckButtonEditorProps) {
  const [draft, setDraft] =
    useState<StreamDeckButtonConfig | null>(null);

  const [isSaving, setIsSaving] =
    useState(false);

  useEffect(() => {
    if (!open || buttonNumber === null) {
      return;
    }

    setDraft(
      button
        ? {
            ...button,
          }
        : createDefaultButton(buttonNumber),
    );
  }, [open, buttonNumber, button]);

  if (!draft) {
    return null;
  }

  const updateField = <
    K extends keyof StreamDeckButtonConfig,
  >(
    field: K,
    value: StreamDeckButtonConfig[K],
  ) => {
    setDraft((current) =>
      current
        ? {
            ...current,
            [field]: value,
          }
        : current,
    );
  };

  const handleSave = async () => {
    setIsSaving(true);

    try {
      await onSave({
        ...draft,
        label: draft.label.trim(),
        eventType: draft.eventType.trim(),
        scoreEffect:
          draft.scoreEffect?.trim() || null,
        overlayEffect:
          draft.overlayEffect?.trim() || null,
        colour:
          draft.colour?.trim() || "#1C8551",
      });

      onOpenChange(false);
    } finally {
      setIsSaving(false);
    }
  };

  const displayKeyNumber =
    draft.buttonNumber + 1;

  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
    >
      <DialogContent className="sm:max-w-[640px] bg-[#0f0f0f] border-white/10 text-white">
        <DialogHeader>
          <DialogTitle>
            Configure Stream Deck Key{" "}
            {displayKeyNumber}
          </DialogTitle>

          <DialogDescription className="text-neutral-400">
            Hardware index {draft.buttonNumber}.
            Configure the AMHARC action, visual
            treatment and match behaviour for this
            physical key.
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-6 py-4">
          <div className="grid gap-2">
            <Label htmlFor="button-label">
              Button Label
            </Label>

            <Input
              id="button-label"
              value={draft.label}
              onChange={(event) =>
                updateField(
                  "label",
                  event.target.value,
                )
              }
              placeholder="e.g. HOME POINT"
              className="bg-black border-white/10"
            />
          </div>

          <div className="grid gap-2">
            <Label htmlFor="event-type">
              Event Type
            </Label>

            <Input
              id="event-type"
              value={draft.eventType}
              onChange={(event) =>
                updateField(
                  "eventType",
                  event.target.value,
                )
              }
              placeholder="e.g. point, goal, recording-start"
              className="bg-black border-white/10 font-mono"
            />

            <p className="text-xs text-neutral-500">
              This is the AMHARC event/action identifier
              raised when the physical key is pressed.
            </p>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <div className="grid gap-2">
              <Label>Team</Label>

              <Select
                value={
                  draft.team === null ||
                  draft.team === undefined
                    ? "none"
                    : String(draft.team)
                }
                onValueChange={(value) =>
                  updateField(
                    "team",
                    value === "none"
                      ? null
                      : Number(value),
                  )
                }
              >
                <SelectTrigger className="bg-black border-white/10">
                  <SelectValue placeholder="Select team" />
                </SelectTrigger>

                <SelectContent>
                  <SelectItem value="none">
                    None
                  </SelectItem>

                  <SelectItem value="0">
                    Home
                  </SelectItem>

                  <SelectItem value="1">
                    Away
                  </SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="grid gap-2">
              <Label>AMHARC Colour</Label>

              <Select
                value={draft.colour ?? "#1C8551"}
                onValueChange={(value) =>
                  updateField(
                    "colour",
                    value,
                  )
                }
              >
                <SelectTrigger className="bg-black border-white/10">
                  <SelectValue placeholder="Select colour" />
                </SelectTrigger>

                <SelectContent>
                  {AMHARC_COLOURS.map(
                    (colour) => (
                      <SelectItem
                        key={colour.value}
                        value={colour.value}
                      >
                        <span className="flex items-center gap-2">
                          <span
                            className="inline-block h-3 w-3 rounded-full border border-white/20"
                            style={{
                              backgroundColor:
                                colour.value,
                            }}
                          />

                          {colour.label}
                        </span>
                      </SelectItem>
                    ),
                  )}
                </SelectContent>
              </Select>
            </div>
          </div>

          <div className="grid gap-2">
            <Label htmlFor="custom-colour">
              Custom Colour
            </Label>

            <div className="flex gap-3">
              <Input
                id="custom-colour"
                type="color"
                value={draft.colour ?? "#1C8551"}
                onChange={(event) =>
                  updateField(
                    "colour",
                    event.target.value.toUpperCase(),
                  )
                }
                className="h-10 w-16 bg-black border-white/10 p-1"
              />

              <Input
                value={draft.colour ?? ""}
                onChange={(event) =>
                  updateField(
                    "colour",
                    event.target.value.toUpperCase(),
                  )
                }
                placeholder="#1C8551"
                className="bg-black border-white/10 font-mono"
              />
            </div>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <div className="grid gap-2">
              <Label htmlFor="score-effect">
                Score Effect
              </Label>

              <Input
                id="score-effect"
                value={draft.scoreEffect ?? ""}
                onChange={(event) =>
                  updateField(
                    "scoreEffect",
                    event.target.value,
                  )
                }
                placeholder="e.g. +1, +3"
                className="bg-black border-white/10 font-mono"
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="overlay-effect">
                Overlay Effect
              </Label>

              <Input
                id="overlay-effect"
                value={draft.overlayEffect ?? ""}
                onChange={(event) =>
                  updateField(
                    "overlayEffect",
                    event.target.value,
                  )
                }
                placeholder="Optional overlay action"
                className="bg-black border-white/10 font-mono"
              />
            </div>
          </div>

          <div className="flex items-center justify-between rounded-lg border border-white/5 bg-neutral-900 p-4">
            <div className="space-y-1">
              <Label className="text-base">
                Request Clip
              </Label>

              <p className="text-sm text-neutral-500">
                Mark the associated event for later
                clipping.
              </p>
            </div>

            <Switch
              checked={draft.clipRequest}
              onCheckedChange={(checked) =>
                updateField(
                  "clipRequest",
                  checked,
                )
              }
            />
          </div>

          <div className="flex items-center justify-between rounded-lg border border-white/5 bg-neutral-900 p-4">
            <div className="space-y-1">
              <Label className="text-base">
                Button Enabled
              </Label>

              <p className="text-sm text-neutral-500">
                Disabled keys remain in the profile but
                do not trigger an AMHARC action.
              </p>
            </div>

            <Switch
              checked={draft.enabled}
              onCheckedChange={(checked) =>
                updateField(
                  "enabled",
                  checked,
                )
              }
            />
          </div>

          <div className="rounded-lg border border-white/5 bg-black p-4">
            <div className="text-xs uppercase tracking-wider text-neutral-500 mb-3">
              Physical Key Preview
            </div>

            <div
              className="mx-auto flex aspect-square w-28 flex-col items-center justify-center rounded-xl border border-white/10 p-3"
              style={{
                backgroundColor:
                  draft.colour ?? "#1C8551",
              }}
            >
              <span
                className="text-sm font-bold text-center leading-tight uppercase"
                style={{
                  color:
                    draft.colour?.toUpperCase() ===
                    "#B6DC46"
                      ? "#000000"
                      : "#FFFFFF",
                }}
              >
                {draft.label || "UNASSIGNED"}
              </span>
            </div>
          </div>
        </div>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            onClick={() =>
              onOpenChange(false)
            }
            className="border-white/10 bg-black"
            disabled={isSaving}
          >
            Cancel
          </Button>

          <Button
            type="button"
            onClick={() =>
              void handleSave()
            }
            disabled={
              isSaving ||
              !draft.label.trim() ||
              !draft.eventType.trim()
            }
            className="bg-amharc-green text-white hover:bg-amharc-green/90"
          >
            {isSaving
              ? "Saving..."
              : "Save Button"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}