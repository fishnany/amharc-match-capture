import React from "react";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { 
  useGetSystemStatus, 
  useGetStorageStatus,
  SystemStatus
} from "@workspace/api-client-react";
import { 
  Activity, 
  Video, 
  HardDrive, 
  RadioReceiver, 
  Gamepad2, 
  Mic, 
  Joystick,
  Layers,
  AlertTriangle,
  CheckCircle2,
  XCircle,
  RefreshCw
} from "lucide-react";
import { Button } from "@/components/ui/button";

export default function Health() {
  const { data: status, refetch, isRefetching } = useGetSystemStatus({
    query: { refetchInterval: 2000 }
  });

  const { data: storage } = useGetStorageStatus({
    query: { refetchInterval: 5000 }
  });

  const formatBytes = (bytes?: number) => {
    if (!bytes) return "0 GB";
    return `${(bytes / (1024 * 1024 * 1024)).toFixed(1)} GB`;
  };

  const getStatusColor = (s?: string) => {
    switch(s) {
      case "connected":
      case "active":
      case "recording":
      case "streaming":
      case "ok":
        return "text-amharc-green";
      case "error":
      case "critical":
        return "text-destructive";
      case "warning":
        return "text-amber-500";
      case "connecting":
      case "stopping":
        return "text-blue-500";
      case "idle":
      case "inactive":
      case "none":
        return "text-neutral-500";
      default:
        return "text-neutral-500";
    }
  };

  const StatusIcon = ({ s }: { s?: string }) => {
    if (["connected", "active", "recording", "streaming", "ok"].includes(s || "")) {
      return <CheckCircle2 className="w-5 h-5 text-amharc-green" />;
    }
    if (["error", "critical"].includes(s || "")) {
      return <XCircle className="w-5 h-5 text-destructive" />;
    }
    if (s === "warning") {
      return <AlertTriangle className="w-5 h-5 text-amber-500" />;
    }
    return <Activity className="w-5 h-5 text-neutral-500" />;
  };

  return (
    <div className="p-6 md:p-8 max-w-6xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-3xl font-bold tracking-tight text-white">System Health</h2>
          <p className="text-neutral-400 mt-1">Subsystem status and diagnostic logs</p>
        </div>
        <div className="flex items-center gap-4">
          <div className="text-sm font-mono text-neutral-500">
            Uptime: {Math.floor((status?.uptime || 0) / 3600)}h {Math.floor(((status?.uptime || 0) % 3600) / 60)}m
          </div>
          <Button 
            variant="outline" 
            className="bg-black border-white/10" 
            onClick={() => refetch()}
            disabled={isRefetching}
          >
            <RefreshCw className={`w-4 h-4 mr-2 ${isRefetching ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 grid grid-cols-1 sm:grid-cols-2 gap-4">
          <SubsystemCard 
            title="Camera" 
            icon={Video} 
            status={status?.camera} 
            color={getStatusColor(status?.camera)} 
          />
          <SubsystemCard 
            title="Recording" 
            icon={HardDrive} 
            status={status?.recording} 
            color={getStatusColor(status?.recording)} 
          />
          <SubsystemCard 
            title="Streaming" 
            icon={RadioReceiver} 
            status={status?.streaming} 
            color={getStatusColor(status?.streaming)} 
          />
          <SubsystemCard 
            title="Stream Deck" 
            icon={Gamepad2} 
            status={status?.streamDeck} 
            color={getStatusColor(status?.streamDeck)} 
          />
          <SubsystemCard 
            title="Joystick" 
            icon={Joystick} 
            status={status?.joystick} 
            color={getStatusColor(status?.joystick)} 
          />
          <SubsystemCard 
            title="Audio" 
            icon={Mic} 
            status={status?.audio} 
            color={getStatusColor(status?.audio)} 
          />
          <SubsystemCard 
            title="Overlays" 
            icon={Layers} 
            status={status?.overlay} 
            color={getStatusColor(status?.overlay)} 
          />
        </div>

        <div className="space-y-6">
          <Card className="bg-[#0f0f0f] border-white/10">
            <CardHeader className="pb-3 border-b border-white/5">
              <CardTitle className="text-lg flex items-center justify-between">
                Storage
                <StatusIcon s={storage?.warningLevel} />
              </CardTitle>
            </CardHeader>
            <CardContent className="pt-4 space-y-4">
              <div className="space-y-1">
                <div className="flex justify-between text-sm">
                  <span className="text-neutral-400">Available</span>
                  <span className="font-mono text-white">{formatBytes(storage?.availableBytes)}</span>
                </div>
                <div className="flex justify-between text-sm">
                  <span className="text-neutral-400">Total</span>
                  <span className="font-mono text-white">{formatBytes(storage?.totalBytes)}</span>
                </div>
                <div className="flex justify-between text-sm">
                  <span className="text-neutral-400">Est. Recording Time</span>
                  <span className="font-mono text-amharc-lime">
                    {Math.floor((storage?.availableMinutes || 0) / 60)}h {(storage?.availableMinutes || 0) % 60}m
                  </span>
                </div>
              </div>
              
              <div className="space-y-2">
                <div className="h-2 bg-neutral-900 rounded-full overflow-hidden border border-white/5">
                  <div 
                    className={`h-full rounded-full ${storage?.warningLevel === 'critical' ? 'bg-destructive' : storage?.warningLevel === 'warning' ? 'bg-amber-500' : 'bg-amharc-green'}`}
                    style={{ width: `${((storage?.usedBytes || 0) / (storage?.totalBytes || 1)) * 100}%` }}
                  ></div>
                </div>
                <div className="text-xs text-neutral-500 text-center font-mono">
                  {Math.round(((storage?.usedBytes || 0) / (storage?.totalBytes || 1)) * 100)}% Used
                </div>
              </div>
            </CardContent>
          </Card>

          <Card className="bg-[#0f0f0f] border-white/10">
            <CardHeader className="pb-3 border-b border-white/5">
              <CardTitle className="text-lg flex items-center gap-2 text-amber-500">
                <AlertTriangle className="w-5 h-5" />
                Active Warnings
              </CardTitle>
            </CardHeader>
            <CardContent className="pt-4">
              {status?.warnings && status.warnings.length > 0 ? (
                <ul className="space-y-3">
                  {status.warnings.map((warning, i) => (
                    <li key={i} className="bg-amber-500/10 border border-amber-500/20 text-amber-200 p-3 rounded-md text-sm flex items-start gap-2">
                      <AlertTriangle className="w-4 h-4 mt-0.5 shrink-0" />
                      {warning}
                    </li>
                  ))}
                </ul>
              ) : (
                <div className="py-6 text-center text-neutral-500">
                  <CheckCircle2 className="w-8 h-8 mx-auto mb-2 opacity-50 text-amharc-green" />
                  <p>No active warnings</p>
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}

function SubsystemCard({ title, icon: Icon, status, color }: { title: string, icon: any, status?: string, color: string }) {
  return (
    <Card className="bg-[#0f0f0f] border-white/10 hover:border-white/20 transition-colors">
      <CardContent className="p-4 flex items-center justify-between">
        <div className="flex items-center gap-4">
          <div className="p-3 rounded-lg bg-neutral-900 border border-white/5">
            <Icon className={`w-6 h-6 ${color}`} />
          </div>
          <div>
            <h4 className="font-bold">{title}</h4>
            <div className={`text-sm font-mono uppercase tracking-widest ${color}`}>
              {status || "Unknown"}
            </div>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
