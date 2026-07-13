import React, { useState } from "react";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { 
  useGetStreamingDestinations, 
  useGetMatches, 
  useGetStreamingStatus,
  useStartStreaming,
  useStopStreaming,
  useCreateStreamingDestination
} from "@workspace/api-client-react";
import { RadioReceiver, Plus, Play, Square, Wifi, Activity } from "lucide-react";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter } from "@/components/ui/dialog";
import { toast } from "sonner";

export default function Streaming() {
  const { data: destinations, refetch } = useGetStreamingDestinations();
  const { data: matches } = useGetMatches();
  const activeMatch = matches?.find(m => m.status === "active" || m.status === "ready");

  const { data: status } = useGetStreamingStatus(activeMatch?.matchId || "", {
    query: { enabled: !!activeMatch?.matchId, refetchInterval: 2000 }
  });

  const startStream = useStartStreaming();
  const stopStream = useStopStreaming();
  const createDest = useCreateStreamingDestination();

  const [isAddOpen, setIsAddOpen] = useState(false);

  const formatUptime = (seconds: number) => {
    const h = Math.floor(seconds / 3600).toString().padStart(2, '0');
    const m = Math.floor((seconds % 3600) / 60).toString().padStart(2, '0');
    const s = (seconds % 60).toString().padStart(2, '0');
    return `${h}:${m}:${s}`;
  };

  const handleAdd = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    const fd = new FormData(e.currentTarget);
    createDest.mutate(
      { data: {
        name: fd.get("name") as string,
        platform: fd.get("platform") as any,
        serverUrl: fd.get("serverUrl") as string,
        streamKey: fd.get("streamKey") as string,
      }},
      {
        onSuccess: () => {
          toast.success("Destination added");
          setIsAddOpen(false);
          refetch();
        }
      }
    );
  };

  const handleToggleStream = () => {
    if (!activeMatch?.matchId) {
      toast.error("No active match to stream");
      return;
    }
    
    if (status?.isStreaming) {
      stopStream.mutate({ matchId: activeMatch.matchId }, {
        onSuccess: () => toast.success("Stream stopped")
      });
    } else {
      startStream.mutate({ matchId: activeMatch.matchId }, {
        onSuccess: () => toast.success("Stream started")
      });
    }
  };

  return (
    <div className="p-6 md:p-8 max-w-6xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-3xl font-bold tracking-tight text-white">Streaming</h2>
          <p className="text-neutral-400 mt-1">Manage RTMP/SRT output destinations</p>
        </div>
        <div className="flex items-center gap-3">
          {status?.isStreaming && (
            <Badge className="bg-amharc-green text-white font-mono uppercase tracking-widest px-3 py-1 animate-pulse-fast">
              Live: {formatUptime(status.uptimeSeconds || 0)}
            </Badge>
          )}
          <Button 
            className={status?.isStreaming ? "bg-destructive text-white hover:bg-destructive/90" : "bg-amharc-green text-white hover:bg-amharc-green/90"}
            onClick={handleToggleStream}
            disabled={!activeMatch || startStream.isPending || stopStream.isPending}
          >
            {status?.isStreaming ? (
              <><Square className="w-4 h-4 mr-2 fill-current" /> Stop Stream</>
            ) : (
              <><Play className="w-4 h-4 mr-2 fill-current" /> Start Stream</>
            )}
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <Card className="bg-[#0f0f0f] border-white/10 lg:col-span-2">
          <CardHeader className="flex flex-row items-center justify-between pb-2 border-b border-white/5">
            <div>
              <CardTitle>Destinations</CardTitle>
              <CardDescription>Configured broadcast outputs</CardDescription>
            </div>
            <Dialog open={isAddOpen} onOpenChange={setIsAddOpen}>
              <DialogTrigger asChild>
                <Button variant="outline" size="sm" className="bg-black border-white/10 text-white">
                  <Plus className="w-4 h-4 mr-2" /> Add Target
                </Button>
              </DialogTrigger>
              <DialogContent className="bg-neutral-900 border-white/10 text-white">
                <DialogHeader>
                  <DialogTitle>Add Streaming Destination</DialogTitle>
                </DialogHeader>
                <form onSubmit={handleAdd} className="space-y-4">
                  <div className="space-y-2">
                    <Label>Name</Label>
                    <Input name="name" required placeholder="e.g. YouTube Live" className="bg-black border-white/10" />
                  </div>
                  <div className="space-y-2">
                    <Label>Platform</Label>
                    <Select name="platform" defaultValue="rtmp-custom">
                      <SelectTrigger className="bg-black border-white/10">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="youtube">YouTube</SelectItem>
                        <SelectItem value="vimeo">Vimeo</SelectItem>
                        <SelectItem value="rtmp-custom">Custom RTMP</SelectItem>
                        <SelectItem value="srt-custom">Custom SRT</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div className="space-y-2">
                    <Label>Server URL</Label>
                    <Input name="serverUrl" required placeholder="rtmp://a.rtmp.youtube.com/live2" className="bg-black border-white/10" />
                  </div>
                  <div className="space-y-2">
                    <Label>Stream Key</Label>
                    <Input name="streamKey" type="password" placeholder="••••••••••••" className="bg-black border-white/10" />
                  </div>
                  <DialogFooter className="mt-6">
                    <Button type="submit" disabled={createDest.isPending} className="bg-amharc-green text-white hover:bg-amharc-green/90">
                      {createDest.isPending ? "Adding..." : "Add Destination"}
                    </Button>
                  </DialogFooter>
                </form>
              </DialogContent>
            </Dialog>
          </CardHeader>
          <CardContent className="p-0">
            {destinations?.length === 0 ? (
              <div className="p-12 text-center text-neutral-500">
                <RadioReceiver className="w-12 h-12 mx-auto mb-4 opacity-20" />
                <p>No streaming destinations configured.</p>
              </div>
            ) : (
              <div className="divide-y divide-white/5">
                {destinations?.map(dest => (
                  <div key={dest.destinationId} className="p-6 flex items-center justify-between hover:bg-white/5 transition-colors">
                    <div className="flex items-center gap-4">
                      <div className="w-12 h-12 rounded-lg bg-neutral-900 border border-white/10 flex items-center justify-center">
                        <RadioReceiver className="w-6 h-6 text-neutral-400" />
                      </div>
                      <div>
                        <h4 className="font-bold text-lg flex items-center gap-2">
                          {dest.name}
                          {dest.isDefault && <Badge variant="outline" className="text-[10px] uppercase font-mono tracking-wider border-white/20">Default</Badge>}
                        </h4>
                        <p className="text-sm text-neutral-500 font-mono mt-1">{dest.serverUrl}</p>
                      </div>
                    </div>
                    <div>
                      {status?.isStreaming && status.destination === dest.destinationId ? (
                        <Badge className="bg-amharc-green text-white uppercase text-xs tracking-widest px-3 py-1">Active Output</Badge>
                      ) : (
                        <Button variant="outline" size="sm" className="bg-black border-white/10">Edit</Button>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </CardContent>
        </Card>

        <div className="space-y-6">
          <Card className="bg-[#0f0f0f] border-white/10">
            <CardHeader>
              <CardTitle>Stream Health</CardTitle>
            </CardHeader>
            <CardContent>
              {status?.isStreaming ? (
                <div className="space-y-6">
                  <div className="space-y-2">
                    <div className="flex justify-between text-sm">
                      <span className="text-neutral-400">Bitrate</span>
                      <span className="font-mono text-amharc-lime">{Math.round((status.outgoingBitRate || 0)/1024)} kbps</span>
                    </div>
                    <div className="h-2 bg-neutral-900 rounded-full overflow-hidden border border-white/5">
                      <div className="h-full bg-amharc-green w-[85%] rounded-full animate-pulse-fast"></div>
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-4 pt-4 border-t border-white/5">
                    <div>
                      <div className="text-xs text-neutral-500 mb-1">Dropped Frames</div>
                      <div className="font-mono text-xl">{status.droppedFrames || 0}</div>
                    </div>
                    <div>
                      <div className="text-xs text-neutral-500 mb-1">Reconnects</div>
                      <div className="font-mono text-xl">{status.reconnectCount || 0}</div>
                    </div>
                  </div>
                  
                  {status.error && (
                    <div className="p-3 bg-destructive/10 border border-destructive/30 rounded text-destructive text-sm mt-4">
                      {status.error}
                    </div>
                  )}
                </div>
              ) : (
                <div className="py-8 text-center text-neutral-500 flex flex-col items-center">
                  <Activity className="w-8 h-8 mb-2 opacity-50" />
                  <p>Stream offline</p>
                </div>
              )}
            </CardContent>
          </Card>

          <Card className="bg-[#0f0f0f] border-white/10">
            <CardHeader>
              <CardTitle>Encoder Settings</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4 text-sm text-neutral-400">
              <div className="flex justify-between">
                <span>Resolution</span>
                <span className="text-white font-mono">1080p</span>
              </div>
              <div className="flex justify-between">
                <span>Framerate</span>
                <span className="text-white font-mono">60 fps</span>
              </div>
              <div className="flex justify-between">
                <span>Video Codec</span>
                <span className="text-white font-mono">H.264 (Hardware)</span>
              </div>
              <div className="flex justify-between">
                <span>Audio Codec</span>
                <span className="text-white font-mono">AAC 192kbps</span>
              </div>
              <Button variant="outline" className="w-full mt-4 bg-black border-white/10">
                Advanced Settings
              </Button>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
