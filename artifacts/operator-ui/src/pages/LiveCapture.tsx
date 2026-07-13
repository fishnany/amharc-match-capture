import React, { useState } from "react";
import { Link } from "wouter";
import {
  useGetMatches,
  useGetMatchClock,
  useGetMatchScore,
  useGetRecordingStatus,
  useGetSystemStatus,
  useStartRecording,
  useStopRecording,
  useStartMatchClock,
  usePauseMatchClock,
  useUpdateMatchScore,
  useGetMatchEvents,
  useCreateMatchEvent
} from "@workspace/api-client-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { 
  ArrowLeft, 
  Circle, 
  Square, 
  Play, 
  Pause,
  Maximize,
  Radio,
  Gamepad2,
  HardDrive,
  MoveUp,
  MoveDown,
  MoveLeft,
  MoveRight,
  ZoomIn,
  ZoomOut
} from "lucide-react";
import { toast } from "sonner";

export default function LiveCapture() {
  const { data: matches } = useGetMatches();
  const activeMatch = matches?.find(m => m.status === "active" || m.status === "ready");
  const matchId = activeMatch?.matchId || "";

  const { data: systemStatus } = useGetSystemStatus({ query: { refetchInterval: 2000 } });
  const { data: clock } = useGetMatchClock(matchId, { query: { enabled: !!matchId, refetchInterval: 500 } });
  const { data: score } = useGetMatchScore(matchId, { query: { enabled: !!matchId, refetchInterval: 2000 } });
  const { data: recording } = useGetRecordingStatus(matchId, { query: { enabled: !!matchId, refetchInterval: 2000 } });
  const { data: events } = useGetMatchEvents(matchId, { query: { enabled: !!matchId, refetchInterval: 2000 } });

  const startRec = useStartRecording();
  const stopRec = useStopRecording();
  const startClock = useStartMatchClock();
  const pauseClock = usePauseMatchClock();
  const updateScore = useUpdateMatchScore();
  const createEvent = useCreateMatchEvent();

  const [confirmStop, setConfirmStop] = useState(false);

  const formatTime = (seconds: number) => {
    const m = Math.floor(seconds / 60).toString().padStart(2, '0');
    const s = (seconds % 60).toString().padStart(2, '0');
    return `${m}:${s}`;
  };

  const handleScore = (team: 'home' | 'away', type: 'goal' | 'point') => {
    updateScore.mutate({
      matchId,
      data: {
        team,
        scoreType: type,
        delta: 1
      }
    });
  };

  const handleStopRecording = () => {
    if (confirmStop) {
      stopRec.mutate({ matchId }, {
        onSuccess: () => {
          setConfirmStop(false);
          toast.success("Recording stopped");
        }
      });
    } else {
      setConfirmStop(true);
      setTimeout(() => setConfirmStop(false), 3000);
    }
  };

  if (!activeMatch) {
    return (
      <div className="min-h-screen flex flex-col items-center justify-center bg-black">
        <h2 className="text-2xl font-bold mb-4">No Active Match</h2>
        <Link href="/" className="bg-white text-black px-4 py-2 rounded font-medium">Return to Dashboard</Link>
      </div>
    );
  }

  return (
    <div className="h-screen w-full flex flex-col bg-black text-white overflow-hidden font-sans">
      {/* Top Status Bar */}
      <header className="h-14 bg-[#0a0a0a] border-b border-white/10 flex items-center justify-between px-4 shrink-0">
        <div className="flex items-center gap-4">
          <Link href="/" className="text-neutral-400 hover:text-white transition-colors">
            <ArrowLeft className="w-5 h-5" />
          </Link>
          <div className="flex items-center gap-3 border-l border-white/10 pl-4">
            <Badge variant="outline" className={`font-mono ${recording?.isRecording ? 'border-amharc-green text-amharc-green' : 'border-neutral-600 text-neutral-500'}`}>
              <Circle className={`w-3 h-3 mr-2 ${recording?.isRecording ? 'fill-current animate-pulse-fast' : ''}`} />
              {recording?.isRecording ? `REC ${formatTime(recording.elapsedSeconds)}` : 'READY'}
            </Badge>
            <Badge variant="outline" className="font-mono border-white/10">
              <Radio className={`w-3 h-3 mr-2 ${systemStatus?.streaming === 'streaming' ? 'text-amharc-lime' : 'text-neutral-500'}`} />
              LIVE
            </Badge>
          </div>
        </div>

        <div className="flex items-center gap-6 text-sm font-mono text-neutral-400">
          <div className="flex items-center gap-2">
            <HardDrive className={`w-4 h-4 ${systemStatus?.storage.warningLevel === 'critical' ? 'text-destructive' : 'text-neutral-500'}`} />
            {Math.floor((systemStatus?.storage.availableMinutes || 0)/60)}h {(systemStatus?.storage.availableMinutes || 0)%60}m
          </div>
          <div className="flex items-center gap-2">
            <Gamepad2 className={`w-4 h-4 ${systemStatus?.streamDeck === 'connected' ? 'text-amharc-green' : 'text-neutral-500'}`} />
            SD
          </div>
        </div>
      </header>

      {/* Main Workspace */}
      <div className="flex-1 flex overflow-hidden">
        {/* Left/Center: Video & Timeline */}
        <div className="flex-1 flex flex-col min-w-0 border-r border-white/10">
          {/* Video Preview Area */}
          <div className="flex-1 bg-[#050505] p-4 flex flex-col">
            <div className="w-full aspect-video bg-neutral-900 rounded-lg border border-white/5 relative overflow-hidden flex items-center justify-center group">
              <div className="absolute inset-0 flex flex-col items-center justify-center text-neutral-600">
                <Maximize className="w-12 h-12 mb-4 opacity-50" />
                <p className="font-mono text-sm tracking-widest uppercase">Live Camera Feed</p>
                <p className="text-xs mt-2 opacity-50">1080p60 • 8.5 Mbps</p>
              </div>
              
              {/* Overlay Preview */}
              <div className="absolute top-8 left-8 right-8 flex justify-between items-start pointer-events-none">
                <div className="bg-black/80 backdrop-blur-md border border-white/20 rounded shadow-2xl p-3 flex gap-6 font-sans">
                  <div className="flex items-center gap-3">
                    <span className="font-bold text-lg">{activeMatch.homeTeamShort || activeMatch.homeTeam.substring(0,3).toUpperCase()}</span>
                    <span className="text-2xl font-mono text-amharc-lime">{score?.homeGoals}-{score?.homePoints}</span>
                  </div>
                  <div className="w-px bg-white/20"></div>
                  <div className="flex items-center gap-3">
                    <span className="text-2xl font-mono text-amharc-lime">{score?.awayGoals}-{score?.awayPoints}</span>
                    <span className="font-bold text-lg">{activeMatch.awayTeamShort || activeMatch.awayTeam.substring(0,3).toUpperCase()}</span>
                  </div>
                  <div className="w-px bg-white/20"></div>
                  <div className="flex items-center text-xl font-mono">
                    {formatTime(clock?.matchClockSeconds || 0)}
                  </div>
                </div>
              </div>
            </div>

            {/* PTZ Controls */}
            <div className="mt-4 h-24 bg-[#0a0a0a] rounded-lg border border-white/5 p-2 flex items-center justify-center gap-8">
              <div className="grid grid-cols-3 grid-rows-3 gap-1">
                <div></div>
                <Button variant="ghost" size="icon" className="h-8 w-8 hover:bg-white/10"><MoveUp className="w-4 h-4" /></Button>
                <div></div>
                <Button variant="ghost" size="icon" className="h-8 w-8 hover:bg-white/10"><MoveLeft className="w-4 h-4" /></Button>
                <div className="flex items-center justify-center w-8 h-8 rounded-full bg-white/5 border border-white/10"></div>
                <Button variant="ghost" size="icon" className="h-8 w-8 hover:bg-white/10"><MoveRight className="w-4 h-4" /></Button>
                <div></div>
                <Button variant="ghost" size="icon" className="h-8 w-8 hover:bg-white/10"><MoveDown className="w-4 h-4" /></Button>
                <div></div>
              </div>
              <div className="flex flex-col gap-2">
                <Button variant="outline" size="sm" className="bg-neutral-900 border-white/10"><ZoomIn className="w-4 h-4 mr-2" /> Zoom In</Button>
                <Button variant="outline" size="sm" className="bg-neutral-900 border-white/10"><ZoomOut className="w-4 h-4 mr-2" /> Zoom Out</Button>
              </div>
              <div className="flex gap-2">
                {[1,2,3,4].map(preset => (
                  <Button key={preset} variant="outline" className="w-12 h-12 bg-neutral-900 border-white/10 font-mono text-xs">{preset}</Button>
                ))}
              </div>
            </div>
          </div>

          {/* Timeline Strip */}
          <div className="h-32 bg-[#0a0a0a] border-t border-white/10 p-2 overflow-x-auto whitespace-nowrap flex items-center gap-2">
            {events?.slice().reverse().map(ev => (
              <div key={ev.eventId} className="inline-flex flex-col min-w-[120px] p-2 bg-neutral-900 rounded border border-white/5 relative">
                <span className="text-[10px] font-mono text-neutral-500">{formatTime(ev.matchClockSeconds)}</span>
                <span className="text-sm font-bold truncate">{ev.eventType.replace('-', ' ').toUpperCase()}</span>
                {ev.team && <span className="text-xs text-neutral-400">{ev.team === 'home' ? activeMatch.homeTeamShort : activeMatch.awayTeamShort}</span>}
                {ev.scoreAfter && <span className="text-xs text-amharc-lime font-mono mt-1">{ev.scoreAfter}</span>}
              </div>
            ))}
            {(!events || events.length === 0) && (
              <div className="w-full text-center text-neutral-500 font-mono text-sm">No events logged</div>
            )}
          </div>
        </div>

        {/* Right Sidebar: Controls */}
        <div className="w-80 bg-[#0a0a0a] flex flex-col flex-shrink-0">
          {/* Match Clock */}
          <div className="p-6 border-b border-white/10 text-center">
            <div className="text-xs font-mono text-neutral-500 mb-2 uppercase tracking-widest">Period {clock?.currentPeriod || 1}</div>
            <div className={`text-6xl font-mono font-bold tracking-wider mb-6 ${clock?.isRunning ? 'text-amharc-lime' : 'text-white'}`}>
              {formatTime(clock?.matchClockSeconds || 0)}
            </div>
            <div className="flex gap-2 justify-center">
              {!clock?.isRunning ? (
                <Button 
                  size="lg" 
                  className="flex-1 bg-amharc-lime hover:bg-amharc-lime/90 text-black font-bold"
                  onClick={() => startClock.mutate({ matchId })}
                >
                  <Play className="w-5 h-5 mr-2 fill-current" /> Start Clock
                </Button>
              ) : (
                <Button 
                  size="lg" 
                  variant="outline" 
                  className="flex-1 border-amber-500 text-amber-500 hover:bg-amber-500 hover:text-black font-bold"
                  onClick={() => pauseClock.mutate({ matchId })}
                >
                  <Pause className="w-5 h-5 mr-2 fill-current" /> Pause
                </Button>
              )}
            </div>
          </div>

          {/* Quick Score */}
          <div className="p-4 border-b border-white/10 space-y-4">
            <div className="flex justify-between items-center mb-2">
              <span className="font-bold text-sm tracking-wider uppercase">{activeMatch.homeTeam}</span>
              <span className="font-mono text-xl text-amharc-lime">{score?.homeGoals}-{score?.homePoints}</span>
            </div>
            <div className="flex gap-2">
              <Button variant="outline" className="flex-1 bg-neutral-900 border-white/10" onClick={() => handleScore('home', 'goal')}>+ Goal</Button>
              <Button variant="outline" className="flex-1 bg-neutral-900 border-white/10" onClick={() => handleScore('home', 'point')}>+ Point</Button>
            </div>

            <div className="flex justify-between items-center mt-6 mb-2">
              <span className="font-bold text-sm tracking-wider uppercase">{activeMatch.awayTeam}</span>
              <span className="font-mono text-xl text-amharc-lime">{score?.awayGoals}-{score?.awayPoints}</span>
            </div>
            <div className="flex gap-2">
              <Button variant="outline" className="flex-1 bg-neutral-900 border-white/10" onClick={() => handleScore('away', 'goal')}>+ Goal</Button>
              <Button variant="outline" className="flex-1 bg-neutral-900 border-white/10" onClick={() => handleScore('away', 'point')}>+ Point</Button>
            </div>
          </div>

          {/* Recording Control */}
          <div className="mt-auto p-4 border-t border-white/10">
            {!recording?.isRecording ? (
              <Button 
                size="lg" 
                className="w-full bg-destructive hover:bg-destructive/90 text-white font-bold"
                onClick={() => startRec.mutate({ matchId })}
                disabled={startRec.isPending}
              >
                <Circle className="w-5 h-5 mr-2 fill-current" /> START RECORDING
              </Button>
            ) : (
              <Button 
                size="lg" 
                variant="outline" 
                className={`w-full font-bold transition-all ${confirmStop ? 'bg-destructive text-white border-destructive' : 'border-neutral-600 text-neutral-400 hover:text-white hover:border-white'}`}
                onClick={handleStopRecording}
                disabled={stopRec.isPending}
              >
                <Square className="w-5 h-5 mr-2 fill-current" /> 
                {confirmStop ? "CLICK TO CONFIRM STOP" : "STOP RECORDING"}
              </Button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
