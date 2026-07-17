import {
  StreamDeckButtonEditor,
  type StreamDeckButtonConfig,
} from "./StreamDeckButtonEditor";
import React, { useEffect, useMemo, useState } from "react";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  useGetStreamDeckStatus,
} from "@workspace/api-client-react";
import {
  Gamepad2,
  Plus,
  RefreshCw,
  Zap,
} from "lucide-react";
import { Skeleton } from "@/components/ui/skeleton";
import { useToast } from "@/hooks/use-toast";

type StreamDeckButton = StreamDeckButtonConfig;

type StreamDeckProfile = {
  profileId: string;
  name: string;
  sport: string;
  buttons: StreamDeckButton[];
  createdAt?: string;
  updatedAt?: string;
};

type StreamDeckStatus = {
  connected: boolean;
  deviceName?: string | null;
  activeProfileId?: string | null;
};

type DeckLayout = {
  name: string;
  keyCount: number;
  columns: number;
};

const STANDARD_LAYOUT: DeckLayout = {
  name: "Stream Deck 15",
  keyCount: 15,
  columns: 5,
};

const XL_LAYOUT: DeckLayout = {
  name: "Stream Deck XL",
  keyCount: 32,
  columns: 8,
};

function inferDeckLayout(
  status: StreamDeckStatus | undefined,
  profiles: StreamDeckProfile[] | undefined,
): DeckLayout {
  const deviceName = status?.deviceName?.toLowerCase() ?? "";

  if (deviceName.includes("xl")) {
    return XL_LAYOUT;
  }

  const highestConfiguredButton = profiles
    ?.flatMap((profile) => profile.buttons ?? [])
    .reduce(
      (highest, button) =>
        Math.max(highest, button.buttonNumber),
      -1,
    );

  if (
    highestConfiguredButton !== undefined &&
    highestConfiguredButton >= 15
  ) {
    return XL_LAYOUT;
  }

  return STANDARD_LAYOUT;
}

export default function StreamDeck() {
  const { toast } = useToast();

  const {
    data: rawStatus,
    isLoading: statusLoading,
    refetch: refetchStatus,
  } = useGetStreamDeckStatus({
    query: {
      refetchInterval: 2000,
    },
  });

  const [profiles, setProfiles] = useState<StreamDeckProfile[]>([]);
const [profilesLoading, setProfilesLoading] = useState(true);

const loadProfiles = async () => {
  try {
    const response = await fetch(
      "/api/devices/stream-deck/profiles",
    );

    if (!response.ok) {
      throw new Error(
        `Failed to load Stream Deck profiles: ${response.status}`,
      );
    }

    const data =
      (await response.json()) as StreamDeckProfile[];

    setProfiles(data);
  } catch (error) {
    console.error(error);

    toast({
      title: "Unable to load Stream Deck profiles",
      description:
        "The AMHARC Agent could not retrieve the configured Stream Deck profiles.",
      variant: "destructive",
    });
  } finally {
    setProfilesLoading(false);
  }
};

useEffect(() => {
  void loadProfiles();
}, []);

  const status = rawStatus as StreamDeckStatus | undefined;
  
  const [selectedProfileId, setSelectedProfileId] =
    useState<string>("");

  const [isActivating, setIsActivating] =
    useState(false);

  const [editingButtonNumber, setEditingButtonNumber] =
    useState<number | null>(null);

  const [isEditorOpen, setIsEditorOpen] =
    useState(false);

  
  const deckLayout = useMemo(
    () => inferDeckLayout(status, profiles),
    [status, profiles],
  );

  useEffect(() => {
    if (!profiles?.length) {
      return;
    }

    if (
      status?.activeProfileId &&
      profiles.some(
        (profile) =>
          profile.profileId === status.activeProfileId,
      )
    ) {
      setSelectedProfileId(status.activeProfileId);
      return;
    }

    setSelectedProfileId((current) => {
      if (
        current &&
        profiles.some(
          (profile) =>
            profile.profileId === current,
        )
      ) {
        return current;
      }

      return profiles[0].profileId;
    });
  }, [profiles, status?.activeProfileId]);

  const selectedProfile = useMemo(
    () =>
      profiles?.find(
        (profile) =>
          profile.profileId === selectedProfileId,
      ),
    [profiles, selectedProfileId],
  );

  const hardwareIndices = useMemo(
    () =>
      Array.from(
        { length: deckLayout.keyCount },
        (_, index) => index,
      ),
    [deckLayout.keyCount],
  );

  const activateProfile = async (
    profileId: string,
  ) => {
    if (!profileId) {
      return;
    }

    setIsActivating(true);

    try {
      const response = await fetch(
        `/api/devices/stream-deck/profiles/${encodeURIComponent(
          profileId,
        )}/activate`,
        {
          method: "POST",
        },
      );

      if (!response.ok) {
        throw new Error(
          `Failed to activate Stream Deck profile: ${response.status}`,
        );
      }

      await Promise.all([
        refetchStatus(),
        loadProfiles(),
      ]);

      const profile = profiles?.find(
        (item) =>
          item.profileId === profileId,
      );

      toast({
        title: "Stream Deck profile activated",
        description: profile
          ? `${profile.name} has been synchronised with the active Stream Deck.`
          : "The selected profile has been synchronised with the active Stream Deck.",
      });
    } catch (error) {
      console.error(error);

      toast({
        title: "Unable to activate Stream Deck profile",
        description:
          "The AMHARC Agent could not synchronise the selected profile with the Stream Deck.",
        variant: "destructive",
      });
    } finally {
      setIsActivating(false);
    }
  };

  const handleProfileChange = async (
    profileId: string,
  ) => {
    setSelectedProfileId(profileId);
    await activateProfile(profileId);
  };

  const handleEditButton = (
  hardwareIndex: number,
) => {
  setEditingButtonNumber(hardwareIndex);
  setIsEditorOpen(true);
};

const handleSaveButton = async (
  updatedButton: StreamDeckButtonConfig,
) => {
  if (!selectedProfile) {
    return;
  }


  try {
    const existingButtonIndex =
      selectedProfile.buttons.findIndex(
        (button) =>
          button.buttonNumber ===
          updatedButton.buttonNumber,
      );

    const updatedButtons =
      existingButtonIndex >= 0
        ? selectedProfile.buttons.map(
            (button) =>
              button.buttonNumber ===
              updatedButton.buttonNumber
                ? updatedButton
                : button,
          )
        : [
            ...selectedProfile.buttons,
            updatedButton,
          ];

    const updatedProfile: StreamDeckProfile = {
      ...selectedProfile,
      buttons: updatedButtons,
      updatedAt: new Date().toISOString(),
    };

    const response = await fetch(
      `/api/devices/stream-deck/profiles/${encodeURIComponent(
        selectedProfile.profileId,
      )}`,
      {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify(updatedProfile),
      },
    );

    if (!response.ok) {
      throw new Error(
        `Failed to update Stream Deck profile: ${response.status}`,
      );
    }

    const savedProfile =
      (await response.json()) as StreamDeckProfile;

    setProfiles((current) =>
      current.map((profile) =>
        profile.profileId ===
        savedProfile.profileId
          ? savedProfile
          : profile,
      ),
    );

    toast({
      title: "Stream Deck button saved",
      description: `Key ${
        updatedButton.buttonNumber + 1
      } has been updated successfully.`,
    });
  } catch (error) {
    console.error(error);

    toast({
      title: "Unable to save Stream Deck button",
      description:
        "The AMHARC Agent could not persist the button configuration.",
      variant: "destructive",
    });

    throw error;
  }
};

  const handleSync = async () => {
    if (!selectedProfileId) {
      toast({
        title: "No Stream Deck profile selected",
        description:
          "Select a profile before synchronising with the device.",
        variant: "destructive",
      });

      return;
    }

    await activateProfile(selectedProfileId);
  };

  const handleRefresh = async () => {
    await Promise.all([
      refetchStatus(),
      loadProfiles(),
    ]);
  };

  const isLoading =
    statusLoading || profilesLoading;

  return (
    <div className="p-6 md:p-8 max-w-7xl mx-auto space-y-6">
      <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
        <div>
          <h2 className="text-3xl font-bold tracking-tight text-white">
            Stream Deck
          </h2>

          <p className="text-neutral-400 mt-1">
            AMHARC hardware controller mapping and
            profile management
          </p>
        </div>

        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2 text-sm bg-neutral-900 border border-white/10 px-3 py-1.5 rounded-md">
            <span
              className={`w-2 h-2 rounded-full ${
                status?.connected
                  ? "bg-amharc-green animate-pulse-fast"
                  : "bg-neutral-600"
              }`}
            />

            {status?.connected
              ? status.deviceName ||
                "Stream Deck Connected"
              : "No Device Detected"}
          </div>

          <Button
            type="button"
            variant="outline"
            size="icon"
            onClick={() => void handleRefresh()}
            className="border-white/10 bg-black"
            title="Refresh Stream Deck status"
          >
            <RefreshCw className="w-4 h-4" />
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
        <Card className="bg-[#0f0f0f] border-white/10 lg:col-span-1">
          <CardHeader>
            <CardTitle className="text-lg">
              Profiles
            </CardTitle>

            <CardDescription>
              Select and activate an AMHARC button
              profile
            </CardDescription>
          </CardHeader>

          <CardContent className="space-y-4">
            {isLoading ? (
              <Skeleton className="h-10 w-full bg-neutral-800" />
            ) : (
              <Select
                value={selectedProfileId}
                onValueChange={(profileId) =>
                  void handleProfileChange(
                    profileId,
                  )
                }
                disabled={
                  !profiles?.length ||
                  isActivating
                }
              >
                <SelectTrigger className="bg-black border-white/10">
                  <SelectValue placeholder="Select profile" />
                </SelectTrigger>

                <SelectContent>
                  {profiles?.map((profile) => (
                    <SelectItem
                      key={profile.profileId}
                      value={profile.profileId}
                    >
                      {profile.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}

            <Button
              type="button"
              variant="outline"
              className="w-full border-white/10 bg-black"
              disabled
              title="Profile creation will be enabled in the next development stage"
            >
              <Plus className="w-4 h-4 mr-2" />
              New Profile
            </Button>

            <div className="mt-8 pt-6 border-t border-white/10 space-y-4">
              <div className="text-sm text-neutral-400">
                Properties
              </div>

              <div className="space-y-3 text-sm">
                <div className="flex justify-between gap-4">
                  <span className="text-neutral-500">
                    Sport:
                  </span>

                  <span className="text-white capitalize text-right">
                    {selectedProfile?.sport
                      ? selectedProfile.sport.replace(
                          "-",
                          " ",
                        )
                      : "—"}
                  </span>
                </div>

                <div className="flex justify-between gap-4">
                  <span className="text-neutral-500">
                    Buttons:
                  </span>

                  <span className="text-white">
                    {selectedProfile?.buttons
                      ?.length ?? 0}{" "}
                    configured
                  </span>
                </div>

                <div className="flex justify-between gap-4">
                  <span className="text-neutral-500">
                    Layout:
                  </span>

                  <span className="text-white text-right">
                    {deckLayout.name}
                  </span>
                </div>

                <div className="flex justify-between gap-4">
                  <span className="text-neutral-500">
                    Capacity:
                  </span>

                  <span className="text-white">
                    {deckLayout.keyCount} keys
                  </span>
                </div>

                <div className="flex justify-between gap-4">
                  <span className="text-neutral-500">
                    Active:
                  </span>

                  <span
                    className={
                      status?.activeProfileId ===
                      selectedProfileId
                        ? "text-amharc-lime"
                        : "text-neutral-500"
                    }
                  >
                    {status?.activeProfileId ===
                    selectedProfileId
                      ? "Yes"
                      : "No"}
                  </span>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="bg-[#0f0f0f] border-white/10 lg:col-span-3">
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <div>
              <CardTitle className="text-lg">
                Button Mapping
              </CardTitle>

              <CardDescription>
                Hardware positions use zero-based
                indices internally and human-readable
                key numbers in the operator interface
              </CardDescription>
            </div>

            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => void handleSync()}
              disabled={
                !selectedProfileId ||
                isActivating
              }
              className="text-amharc-lime hover:text-amharc-lime hover:bg-amharc-lime/10"
            >
              <Zap className="w-4 h-4 mr-2" />

              {isActivating
                ? "Syncing..."
                : "Sync to Device"}
            </Button>
          </CardHeader>

          <CardContent>
            <div className="bg-neutral-900 rounded-xl p-4 md:p-8 border border-white/5 overflow-x-auto">
              <div
                className="grid gap-3 md:gap-4 mx-auto min-w-fit"
                style={{
                  gridTemplateColumns: `repeat(${deckLayout.columns}, minmax(72px, 1fr))`,
                  maxWidth:
                    deckLayout.keyCount === 32
                      ? "1100px"
                      : "800px",
                }}
              >
                {hardwareIndices.map(
                  (hardwareIndex) => {
                    const button =
                      selectedProfile?.buttons?.find(
                        (item) =>
                          item.buttonNumber ===
                          hardwareIndex,
                      );

                    const displayKeyNumber =
                      hardwareIndex + 1;

                    return (
                      <button
                        key={hardwareIndex}
                        type="button"
                        onClick={() =>
                          handleEditButton(hardwareIndex)
                        }
                        className="aspect-square min-w-[72px] bg-black rounded-xl border border-white/10 flex flex-col items-center justify-center p-2 hover:border-white/30 transition-all group relative overflow-hidden"
                        style={{
                          borderColor: button?.colour
                            ? `${button.colour}80`
                            : undefined,

                          backgroundColor:
                            button?.colour
                              ? `${button.colour}18`
                              : undefined,
                        }}
                        title={
                          button
                            ? `Key ${displayKeyNumber} · Hardware index ${hardwareIndex} · ${button.label}`
                            : `Key ${displayKeyNumber} · Hardware index ${hardwareIndex} · Unconfigured`
                        }
                      >
                        <div className="absolute top-1 left-2 text-[9px] text-neutral-600 font-mono">
                          {displayKeyNumber}
                        </div>

                        {button ? (
                          <>
                            <div
                              className="w-8 h-8 mb-2 rounded-md flex items-center justify-center"
                              style={{
                                backgroundColor:
                                  button.colour ??
                                  "#1C8551",
                              }}
                            >
                              <span
                                className="text-xs font-bold"
                                style={{
                                  color:
                                    button.colour?.toUpperCase() ===
                                    "#B6DC46"
                                      ? "#000000"
                                      : "#FFFFFF",
                                }}
                              >
                                {button.label
                                  .substring(0, 2)
                                  .toUpperCase()}
                              </span>
                            </div>

                            <span className="text-[10px] font-bold text-center leading-tight tracking-wider uppercase text-white">
                              {button.label}
                            </span>

                            <span className="mt-1 text-[8px] font-mono text-neutral-600">
                              IDX {hardwareIndex}
                            </span>
                          </>
                        ) : (
                          <>
                            <Plus className="w-6 h-6 text-neutral-700 group-hover:text-neutral-400 transition-colors" />

                            <span className="mt-1 text-[8px] font-mono text-neutral-700">
                              IDX {hardwareIndex}
                            </span>
                          </>
                        )}
                      </button>
                    );
                  },
                )}
              </div>
            </div>

            <div className="mt-4 flex items-start gap-3 rounded-lg border border-white/5 bg-black/50 p-4">
              <Gamepad2 className="w-5 h-5 text-amharc-lime mt-0.5 shrink-0" />

              <div className="space-y-1">
                <p className="text-sm text-white font-medium">
                  Device-independent AMHARC profile
                  architecture
                </p>

                <p className="text-xs text-neutral-500 leading-relaxed">
                  AMHARC profiles retain physical
                  zero-based hardware indices. The
                  operator interface displays keys
                  starting at 1. The architecture is
                  being prepared for both the current
                  15-key Stream Deck and the 32-key
                  Stream Deck XL.
                </p>
              </div>
            </div>
          </CardContent>
        </Card>
       </div>

      <StreamDeckButtonEditor
        open={isEditorOpen}
        buttonNumber={editingButtonNumber}
        button={
          editingButtonNumber === null
            ? null
            : selectedProfile?.buttons.find(
                (button) =>
                  button.buttonNumber ===
                  editingButtonNumber,
              ) ?? null
        }
        onOpenChange={setIsEditorOpen}
        onSave={handleSaveButton}
      />
    </div>
  );
}