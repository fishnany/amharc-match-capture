import React from "react";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { useGetStreamDeckStatus, useGetStreamDeckProfiles } from "@workspace/api-client-react";
import { Gamepad2, Plus, Edit2, Zap } from "lucide-react";
import { Skeleton } from "@/components/ui/skeleton";

export default function StreamDeck() {
  const { data: status, isLoading: statusLoading } = useGetStreamDeckStatus({
    query: { refetchInterval: 2000 }
  });
  const { data: profiles, isLoading: profilesLoading } = useGetStreamDeckProfiles();

  const activeProfile = profiles?.find(p => p.profileId === status?.activeProfileId) || profiles?.[0];

  const gridArray = Array.from({ length: 15 }, (_, i) => i + 1);

  return (
    <div className="p-6 md:p-8 max-w-6xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-3xl font-bold tracking-tight text-white">Stream Deck</h2>
          <p className="text-neutral-400 mt-1">Hardware controller mapping and profile editor</p>
        </div>
        <div className="flex items-center gap-4">
          <div className="flex items-center gap-2 text-sm bg-neutral-900 border border-white/10 px-3 py-1.5 rounded-md">
            <span className={`w-2 h-2 rounded-full ${status?.connected ? 'bg-amharc-green animate-pulse-fast' : 'bg-neutral-600'}`}></span>
            {status?.connected ? status.deviceName || "Stream Deck Connected" : "No Device Detected"}
          </div>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
        {/* Left Col: Profile Selection */}
        <Card className="bg-[#0f0f0f] border-white/10 lg:col-span-1">
          <CardHeader>
            <CardTitle className="text-lg">Profiles</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <Select value={activeProfile?.profileId}>
              <SelectTrigger className="bg-black border-white/10">
                <SelectValue placeholder="Select profile" />
              </SelectTrigger>
              <SelectContent>
                {profiles?.map(p => (
                  <SelectItem key={p.profileId} value={p.profileId}>{p.name}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Button variant="outline" className="w-full border-white/10 bg-black">
              <Plus className="w-4 h-4 mr-2" /> New Profile
            </Button>
            <div className="mt-8 pt-6 border-t border-white/10 space-y-4">
              <div className="text-sm text-neutral-400">Properties</div>
              <div className="space-y-2 text-sm">
                <div className="flex justify-between">
                  <span className="text-neutral-500">Sport:</span>
                  <span className="text-white capitalize">{activeProfile?.sport.replace('-', ' ')}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-neutral-500">Buttons:</span>
                  <span className="text-white">{activeProfile?.buttons?.length || 0} configured</span>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Right Col: Virtual Grid */}
        <Card className="bg-[#0f0f0f] border-white/10 lg:col-span-3">
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <div>
              <CardTitle className="text-lg">Button Mapping</CardTitle>
              <CardDescription>Click a button to edit its function</CardDescription>
            </div>
            <Button variant="ghost" size="sm" className="text-amharc-lime hover:text-amharc-lime hover:bg-amharc-lime/10">
              <Zap className="w-4 h-4 mr-2" /> Sync to Device
            </Button>
          </CardHeader>
          <CardContent>
            <div className="bg-neutral-900 rounded-xl p-8 border border-white/5 flex items-center justify-center">
              {/* Stream Deck 15-key representation */}
              <div className="grid grid-cols-5 gap-4 max-w-3xl w-full">
                {gridArray.map(buttonIndex => {
                  const btn = activeProfile?.buttons?.find(b => b.buttonNumber === buttonIndex);
                  
                  return (
                    <button 
                      key={buttonIndex}
                      className="aspect-square bg-black rounded-xl border border-white/10 flex flex-col items-center justify-center p-2 hover:border-white/30 transition-all group relative overflow-hidden"
                      style={{
                        borderColor: btn?.colour ? `${btn.colour}40` : '',
                        backgroundColor: btn?.colour ? `${btn.colour}10` : '',
                      }}
                    >
                      <div className="absolute top-1 left-2 text-[9px] text-neutral-600 font-mono">{buttonIndex}</div>
                      {btn ? (
                        <>
                          <div 
                            className="w-8 h-8 mb-2 rounded flex items-center justify-center text-white"
                            style={{ backgroundColor: btn.colour || '#333' }}
                          >
                            <span className="text-xs font-bold">{btn.label.substring(0, 2).toUpperCase()}</span>
                          </div>
                          <span className="text-[10px] font-bold text-center leading-tight tracking-wider uppercase text-white">
                            {btn.label}
                          </span>
                        </>
                      ) : (
                        <Plus className="w-6 h-6 text-neutral-700 group-hover:text-neutral-400 transition-colors" />
                      )}
                    </button>
                  );
                })}
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
