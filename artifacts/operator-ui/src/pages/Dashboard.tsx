import React from "react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { 
  useGetSystemStatus, 
  useGetMatches,
  useGetMatchClock,
  useGetRecordingStatus,
  SystemStatus
} from "@workspace/api-client-react";
import { Link } from "wouter";
import { 
  Activity, 
  Video, 
  HardDrive, 
  RadioReceiver, 
  Gamepad2, 
  AlertTriangle,
  PlayCircle
} from "lucide-react";
import { Skeleton } from "@/components/ui/skeleton";

export default function Dashboard() {
  const { data: systemStatus, isLoading: statusLoading } = useGetSystemStatus({ 
    query: { refetchInterval: 2000 } 
  });
  
  const { data: matches } = useGetMatches();
  const activeMatch = matches?.find(m => m.status === "active" || m.status === "ready");

  const { data: clock } = useGetMatchClock(activeMatch?.matchId || "", {
    query: { enabled: !!activeMatch?.matchId, refetchInterval: 500 }
  });

  const { data: recording } = useGetRecordingStatus(activeMatch?.matchId || "", {
    query: { enabled: !!activeMatch?.matchId, refetchInterval: 2000 }
  });

  const formatTime = (seconds: number) => {
    const m = Math.floor(seconds / 60).toString().padStart(2, '0');
    const s = (seconds % 60).toString().padStart(2, '0');
    return `${m}:${s}`;
  };

  const getStatusColor = (status?: string) => {
    switch(status) {
      case "connected":
      case "active":
      case "recording":
      case "streaming":
      case "ok":
        return "bg-amharc-green text-white";
      case "error":
      case "critical":
        return "bg-destructive text-white";
      case "warning":
        return "bg-amber-500 text-black";
      default:
        return "bg-neutral-800 text-neutral-300";
    }
  };

  return (
    <div className="p-6 md:p-8 max-w-7xl mx-auto space-y-8">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-3xl font-bold tracking-tight text-white">Dashboard</h2>
          <p className="text-neutral-400 mt-1">System overview and active match status</p>
        </div>
        {activeMatch ? (
          <Link href="/capture" className="flex items-center gap-2 bg-amharc-green hover:bg-amharc-green/90 text-white px-4 py-2 rounded-md font-medium transition-colors">
            <PlayCircle className="w-5 h-5" />
            Enter Live Capture
          </Link>
        ) : (
          <Link href="/match/new" className="flex items-center gap-2 bg-white text-black hover:bg-neutral-200 px-4 py-2 rounded-md font-medium transition-colors">
            Setup New Match
          </Link>
        )}
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <StatusCard 
          title="Camera" 
          icon={Video} 
          status={systemStatus?.camera.connectionState} 
          isLoading={statusLoading} 
        />
        <StatusCard 
          title="Recording" 
          icon={HardDrive} 
          status={systemStatus?.recording.state} 
          isLoading={statusLoading} 
        />
        <StatusCard 
          title="Streaming" 
          icon={RadioReceiver} 
          status={systemStatus?.streaming.state} 
          isLoading={statusLoading} 
        />
        <StatusCard 
          title="Stream Deck" 
          icon={Gamepad2} 
          status={
          systemStatus?.streamDeck.connected
          ? "connected"
          : "disconnected"
        } 
          isLoading={statusLoading} 
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <Card className="lg:col-span-2 bg-[#0f0f0f] border-white/10">
          <CardHeader>
            <CardTitle className="text-lg">Active Match</CardTitle>
          </CardHeader>
          <CardContent>
            {activeMatch ? (
              <div className="space-y-6">
                <div className="flex justify-between items-center p-4 bg-neutral-900 rounded-lg border border-white/5">
                  <div className="flex flex-col items-center">
                    <span className="text-sm text-neutral-400 font-medium uppercase tracking-wider mb-1">Home</span>
                    <span className="text-2xl font-bold">{activeMatch.homeTeam}</span>
                  </div>
                  <div className="flex flex-col items-center px-8 border-x border-white/10">
                    <span className="text-xs text-neutral-500 uppercase tracking-widest mb-2 font-mono">Period {clock?.currentPeriod || 1}</span>
                    <span className={`text-5xl font-mono tracking-wider ${clock?.isRunning ? 'text-amharc-lime' : 'text-white'}`}>
                      {formatTime(clock?.matchClockSeconds || 0)}
                    </span>
                  </div>
                  <div className="flex flex-col items-center">
                    <span className="text-sm text-neutral-400 font-medium uppercase tracking-wider mb-1">Away</span>
                    <span className="text-2xl font-bold">{activeMatch.awayTeam}</span>
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div className="p-4 bg-neutral-900 rounded-lg border border-white/5">
                    <h4 className="text-sm text-neutral-400 mb-2">Recording Status</h4>
                    <div className="flex items-center gap-3">
                      {recording?.isRecording ? (
                        <>
                          <span className="w-3 h-3 rounded-full bg-amharc-green animate-pulse-fast"></span>
                          <span className="font-mono text-xl">{formatTime(recording?.elapsedSeconds || 0)}</span>
                        </>
                      ) : (
                        <>
                          <span className="w-3 h-3 rounded-full bg-neutral-600"></span>
                          <span className="text-neutral-500">Not Recording</span>
                        </>
                      )}
                    </div>
                  </div>
                  <div className="p-4 bg-neutral-900 rounded-lg border border-white/5">
                    <h4 className="text-sm text-neutral-400 mb-2">Storage</h4>
                    <div className="flex items-center justify-between">
                      <span className="text-xl">{Math.floor((systemStatus?.storage.availableMinutes || 0) / 60)}h {(systemStatus?.storage.availableMinutes || 0) % 60}m</span>
                      <Badge className={getStatusColor(systemStatus?.storage.warningLevel)}>{systemStatus?.storage.warningLevel || "Unknown"}</Badge>
                    </div>
                  </div>
                </div>
              </div>
            ) : (
              <div className="py-12 flex flex-col items-center justify-center text-center">
                <p className="text-neutral-400 mb-4">No active match found.</p>
                <Link href="/match/new" className="bg-white text-black hover:bg-neutral-200 px-4 py-2 rounded-md font-medium transition-colors">
                  Create a Match
                </Link>
              </div>
            )}
          </CardContent>
        </Card>

        <Card className="bg-[#0f0f0f] border-white/10">
          <CardHeader>
            <CardTitle className="text-lg flex items-center gap-2">
              <AlertTriangle className="w-5 h-5 text-amber-500" />
              Active Warnings
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex flex-col items-center justify-center py-8 text-neutral-500">
              <Activity className="w-8 h-8 mb-2 opacity-50" />
              <p>No system warnings.</p>
              </div>
              <div className="flex flex-col items-center justify-center py-8 text-neutral-500">
                <Activity className="w-8 h-8 mb-2 opacity-50" />
                <p>System operating normally</p>
              </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

function StatusCard({ title, icon: Icon, status, isLoading }: { title: string, icon: any, status?: string, isLoading: boolean }) {
  const getStatusColor = (s?: string) => {
    switch(s) {
      case "connected":
      case "active":
      case "recording":
      case "streaming":
      case "ok":
        return "bg-amharc-green text-white";
      case "error":
      case "critical":
        return "bg-destructive text-white";
      case "warning":
        return "bg-amber-500 text-black";
      case "connecting":
      case "stopping":
        return "bg-blue-500 text-white";
      default:
        return "bg-neutral-800 text-neutral-300";
    }
  };

  return (
    <Card className="bg-[#0f0f0f] border-white/10">
      <CardContent className="p-4 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className={`p-2 rounded-lg bg-neutral-900 border border-white/5`}>
            <Icon className="w-5 h-5 text-neutral-400" />
          </div>
          <span className="font-medium">{title}</span>
        </div>
        {isLoading ? (
          <Skeleton className="w-20 h-6 bg-white/10 rounded-full" />
        ) : (
          <Badge className={`uppercase text-[10px] tracking-wider px-2 py-0.5 rounded-sm shadow-none ${getStatusColor(status)}`}>
            {status || "Unknown"}
          </Badge>
        )}
      </CardContent>
    </Card>
  );
}
