import React, { useEffect, useState } from "react";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Save } from "lucide-react";
import { useToast } from "@/hooks/use-toast";

type JoystickConfig = {
  deadZone: number;
  panSensitivity: number;
  tiltSensitivity: number;
  zoomSensitivity: number;
  ptzUpdateIntervalMs: number;
  responseCurveStrength: number;
};

const defaultJoystickConfig: JoystickConfig = {
  deadZone: 0.08,
  panSensitivity: 1,
  tiltSensitivity: 1,
  zoomSensitivity: 1,
  ptzUpdateIntervalMs: 50,
  responseCurveStrength: 1,
};

export default function Settings() {
  const { toast } = useToast();
  const [joystickConfig, setJoystickConfig] =
    useState<JoystickConfig>(defaultJoystickConfig);

  const [isLoadingJoystick, setIsLoadingJoystick] = useState(true);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    const loadJoystickSettings = async () => {
      try {
        const response = await fetch("/api/settings/joystick");

        if (!response.ok) {
          throw new Error(
            `Failed to load joystick settings: ${response.status}`,
          );
        }

        const data = (await response.json()) as JoystickConfig;
        setJoystickConfig(data);
      } catch (error) {
        console.error(error);
        toast({
        title: "Unable to load joystick settings",
        variant: "destructive",
        });
      } finally {
        setIsLoadingJoystick(false);
      }
    };

    void loadJoystickSettings();
  }, []);

  const updateJoystickField = (
    field: keyof JoystickConfig,
    value: number,
  ) => {
    setJoystickConfig((current) => ({
      ...current,
      [field]: value,
    }));
  };

  const handleSave = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setIsSaving(true);

    try {
      const response = await fetch("/api/settings/joystick", {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify(joystickConfig),
      });

      if (!response.ok) {
        throw new Error(
          `Failed to save joystick settings: ${response.status}`,
        );
      }

      const validatedConfig =
        (await response.json()) as JoystickConfig;

      setJoystickConfig(validatedConfig);
      toast({
      title: "Settings saved successfully",
      description: "Joystick and PTZ settings have been applied to the running Agent.",
      });
    } catch (error) {
      console.error(error);
      toast({
      title: "Unable to save joystick settings",
      description: "The AMHARC Agent could not update the joystick configuration.",
      variant: "destructive",
      });
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="p-6 md:p-8 max-w-4xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-3xl font-bold tracking-tight text-white">
            System Settings
          </h2>
          <p className="text-neutral-400 mt-1">
            Configure global application preferences
          </p>
        </div>
      </div>

      <form onSubmit={handleSave} className="space-y-6">
        <Card className="bg-[#0f0f0f] border-white/10">
          <CardHeader>
            <CardTitle>Joystick & PTZ Control</CardTitle>
            <CardDescription>
              Configure AXIS T8311 joystick response and camera movement
              behaviour
            </CardDescription>
          </CardHeader>

          <CardContent className="space-y-6">
            {isLoadingJoystick ? (
              <p className="text-sm text-neutral-400">
                Loading joystick settings...
              </p>
            ) : (
              <>
                <div className="grid gap-6 md:grid-cols-2">
                  <div className="space-y-2">
                    <Label htmlFor="deadZone">Dead Zone</Label>
                    <Input
                      id="deadZone"
                      type="number"
                      min="0"
                      max="0.5"
                      step="0.01"
                      value={joystickConfig.deadZone}
                      onChange={(e) =>
                        updateJoystickField(
                          "deadZone",
                          Number(e.target.value),
                        )
                      }
                      className="bg-black border-white/10"
                    />
                    <p className="text-xs text-neutral-500">
                      Ignore small joystick movements to prevent camera drift.
                    </p>
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="responseCurveStrength">
                      Response Curve Strength
                    </Label>
                    <Input
                      id="responseCurveStrength"
                      type="number"
                      min="0.1"
                      max="3"
                      step="0.1"
                      value={joystickConfig.responseCurveStrength}
                      onChange={(e) =>
                        updateJoystickField(
                          "responseCurveStrength",
                          Number(e.target.value),
                        )
                      }
                      className="bg-black border-white/10"
                    />
                    <p className="text-xs text-neutral-500">
                      Controls fine movement near centre and acceleration
                      towards full travel.
                    </p>
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="panSensitivity">
                      Pan Sensitivity
                    </Label>
                    <Input
                      id="panSensitivity"
                      type="number"
                      min="0.1"
                      max="2"
                      step="0.1"
                      value={joystickConfig.panSensitivity}
                      onChange={(e) =>
                        updateJoystickField(
                          "panSensitivity",
                          Number(e.target.value),
                        )
                      }
                      className="bg-black border-white/10"
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="tiltSensitivity">
                      Tilt Sensitivity
                    </Label>
                    <Input
                      id="tiltSensitivity"
                      type="number"
                      min="0.1"
                      max="2"
                      step="0.1"
                      value={joystickConfig.tiltSensitivity}
                      onChange={(e) =>
                        updateJoystickField(
                          "tiltSensitivity",
                          Number(e.target.value),
                        )
                      }
                      className="bg-black border-white/10"
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="zoomSensitivity">
                      Zoom Sensitivity
                    </Label>
                    <Input
                      id="zoomSensitivity"
                      type="number"
                      min="0.1"
                      max="2"
                      step="0.1"
                      value={joystickConfig.zoomSensitivity}
                      onChange={(e) =>
                        updateJoystickField(
                          "zoomSensitivity",
                          Number(e.target.value),
                        )
                      }
                      className="bg-black border-white/10"
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="ptzUpdateIntervalMs">
                      PTZ Update Interval
                    </Label>
                    <Input
                      id="ptzUpdateIntervalMs"
                      type="number"
                      min="20"
                      max="500"
                      step="10"
                      value={joystickConfig.ptzUpdateIntervalMs}
                      onChange={(e) =>
                        updateJoystickField(
                          "ptzUpdateIntervalMs",
                          Number(e.target.value),
                        )
                      }
                      className="bg-black border-white/10"
                    />
                    <p className="text-xs text-neutral-500">
                      Time between PTZ updates in milliseconds.
                    </p>
                  </div>
                </div>

                <div className="p-4 rounded-lg border border-white/5 bg-neutral-900">
                  <p className="text-sm text-neutral-400">
                    Changes to joystick and PTZ settings are applied to the
                    running Agent immediately after saving.
                  </p>
                </div>
              </>
            )}
          </CardContent>
        </Card>

        <Card className="bg-[#0f0f0f] border-white/10">
          <CardHeader>
            <CardTitle>Local Storage & Recording</CardTitle>
            <CardDescription>
              Default paths and recording behaviours
            </CardDescription>
          </CardHeader>

          <CardContent className="space-y-6">
            <div className="space-y-2">
              <Label>Default Recording Directory</Label>
              <Input
                defaultValue="D:\\AMHARC_Captures"
                className="bg-black border-white/10 font-mono"
              />
              <p className="text-xs text-neutral-500">
                Must be an absolute path with write permissions.
              </p>
            </div>

            <div className="flex items-center justify-between p-4 bg-neutral-900 border border-white/5 rounded-lg">
              <div className="space-y-0.5">
                <Label className="text-base">
                  Auto-segment Recording
                </Label>
                <p className="text-sm text-neutral-500">
                  Split video files when period changes
                </p>
              </div>
              <Switch defaultChecked />
            </div>

            <div className="flex items-center justify-between p-4 bg-neutral-900 border border-white/5 rounded-lg">
              <div className="space-y-0.5">
                <Label className="text-base">
                  Keep-Alive Recording
                </Label>
                <p className="text-sm text-neutral-500">
                  Do not stop recording on stream drop
                </p>
              </div>
              <Switch defaultChecked />
            </div>
          </CardContent>
        </Card>

        <Card className="bg-[#0f0f0f] border-white/10">
          <CardHeader>
            <CardTitle>Operator Preferences</CardTitle>
            <CardDescription>
              Interface and behaviour defaults
            </CardDescription>
          </CardHeader>

          <CardContent className="space-y-6">
            <div className="space-y-2">
              <Label>Operator Name</Label>
              <Input
                defaultValue="Admin"
                className="bg-black border-white/10"
              />
            </div>

            <div className="flex items-center justify-between p-4 bg-neutral-900 border border-white/5 rounded-lg">
              <div className="space-y-0.5">
                <Label className="text-base">
                  Confirm Destructive Actions
                </Label>
                <p className="text-sm text-neutral-500">
                  Require confirmation for deleting events and stopping
                  records
                </p>
              </div>
              <Switch defaultChecked />
            </div>

            <div className="flex items-center justify-between p-4 bg-neutral-900 border border-white/5 rounded-lg">
              <div className="space-y-0.5">
                <Label className="text-base">
                  Auto-clip Goals
                </Label>
                <p className="text-sm text-neutral-500">
                  Automatically mark 'Goal' events for clipping
                </p>
              </div>
              <Switch defaultChecked />
            </div>
          </CardContent>
        </Card>

        <Card className="bg-[#0f0f0f] border-white/10">
          <CardHeader>
            <CardTitle>About AMHARC</CardTitle>
          </CardHeader>

          <CardContent className="space-y-4 text-sm text-neutral-400">
            <div className="flex justify-between py-2 border-b border-white/5">
              <span>Version</span>
              <span className="text-white font-mono">0.1.0-beta</span>
            </div>

            <div className="flex justify-between py-2 border-b border-white/5">
              <span>License</span>
              <span className="text-white font-mono">Operator Pro</span>
            </div>

            <div className="flex justify-between py-2 border-b border-white/5">
              <span>System ID</span>
              <span className="text-white font-mono">SYS-882-91A</span>
            </div>
          </CardContent>
        </Card>

        <div className="flex justify-end pt-4">
          <Button
            type="submit"
            disabled={isSaving || isLoadingJoystick}
            className="bg-amharc-green text-white hover:bg-amharc-green/90 px-8"
          >
            <Save className="w-4 h-4 mr-2" />
            {isSaving ? "Saving..." : "Save Settings"}
          </Button>
        </div>
      </form>
    </div>
  );
}