import React from "react";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Save } from "lucide-react";
import { toast } from "sonner";

export default function Settings() {
  const handleSave = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    toast.success("Settings saved successfully");
  };

  return (
    <div className="p-6 md:p-8 max-w-4xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-3xl font-bold tracking-tight text-white">System Settings</h2>
          <p className="text-neutral-400 mt-1">Configure global application preferences</p>
        </div>
      </div>

      <form onSubmit={handleSave} className="space-y-6">
        <Card className="bg-[#0f0f0f] border-white/10">
          <CardHeader>
            <CardTitle>Local Storage & Recording</CardTitle>
            <CardDescription>Default paths and recording behaviors</CardDescription>
          </CardHeader>
          <CardContent className="space-y-6">
            <div className="space-y-2">
              <Label>Default Recording Directory</Label>
              <Input defaultValue="D:\AMHARC_Captures" className="bg-black border-white/10 font-mono" />
              <p className="text-xs text-neutral-500">Must be an absolute path with write permissions.</p>
            </div>
            
            <div className="flex items-center justify-between p-4 bg-neutral-900 border border-white/5 rounded-lg">
              <div className="space-y-0.5">
                <Label className="text-base">Auto-segment Recording</Label>
                <p className="text-sm text-neutral-500">Split video files when period changes</p>
              </div>
              <Switch defaultChecked />
            </div>

            <div className="flex items-center justify-between p-4 bg-neutral-900 border border-white/5 rounded-lg">
              <div className="space-y-0.5">
                <Label className="text-base">Keep-Alive Recording</Label>
                <p className="text-sm text-neutral-500">Do not stop recording on stream drop</p>
              </div>
              <Switch defaultChecked />
            </div>
          </CardContent>
        </Card>

        <Card className="bg-[#0f0f0f] border-white/10">
          <CardHeader>
            <CardTitle>Operator Preferences</CardTitle>
            <CardDescription>Interface and behavior defaults</CardDescription>
          </CardHeader>
          <CardContent className="space-y-6">
            <div className="space-y-2">
              <Label>Operator Name</Label>
              <Input defaultValue="Admin" className="bg-black border-white/10" />
            </div>

            <div className="flex items-center justify-between p-4 bg-neutral-900 border border-white/5 rounded-lg">
              <div className="space-y-0.5">
                <Label className="text-base">Confirm Destructive Actions</Label>
                <p className="text-sm text-neutral-500">Require confirmation for deleting events and stopping records</p>
              </div>
              <Switch defaultChecked />
            </div>

            <div className="flex items-center justify-between p-4 bg-neutral-900 border border-white/5 rounded-lg">
              <div className="space-y-0.5">
                <Label className="text-base">Auto-clip Goals</Label>
                <p className="text-sm text-neutral-500">Automatically mark 'Goal' events for clipping</p>
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
          <Button type="submit" className="bg-amharc-green text-white hover:bg-amharc-green/90 px-8">
            <Save className="w-4 h-4 mr-2" />
            Save Settings
          </Button>
        </div>
      </form>
    </div>
  );
}
