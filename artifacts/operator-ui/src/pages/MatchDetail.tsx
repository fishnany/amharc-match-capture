import React from "react";
import { useRoute, Link } from "wouter";
import { 
  useGetMatch, 
  useGetMatchClock, 
  useGetMatchScore, 
  useStartMatch,
  useStartMatchClock,
  usePauseMatchClock,
  useUpdateMatchScore
} from "@workspace/api-client-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Play, Pause, Square, PlayCircle, Trophy, Settings2 } from "lucide-react";
import { Skeleton } from "@/components/ui/skeleton";

export default function MatchDetail() {
  const [, params] = useRoute("/match/:matchId");
  const matchId = params?.matchId || "";

  const { data: match, isLoading } = useGetMatch(matchId, {
    query: { enabled: !!matchId }
  });

  const { data: clock } = useGetMatchClock(matchId, {
    query: { enabled: !!matchId, refetchInterval: 500 }
  });

  const { data: score } = useGetMatchScore(matchId, {
    query: { enabled: !!matchId, refetchInterval: 2000 }
  });

  const startMatch = useStartMatch();
  const startClock = useStartMatchClock();
  const pauseClock = usePauseMatchClock();
  const updateScore = useUpdateMatchScore();

  const formatTime = (seconds: number) => {
    const m = Math.floor(seconds / 60).toString().padStart(2, '0');
    const s = (seconds % 60).toString().padStart(2, '0');
    return `${m}:${s}`;
  };

  if (isLoading) {
    return <div className="p-8"><Skeleton className="h-64 w-full bg-white/10" /></div>;
  }

  if (!match) {
    return <div className="p-8 text-center text-neutral-400">Match not found.</div>;
  }

  return (
    <div className="p-6 md:p-8 max-w-5xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <div className="flex items-center gap-3">
            <h2 className="text-3xl font-bold tracking-tight text-white">{match.homeTeam} vs {match.awayTeam}</h2>
            <span className="px-2 py-1 text-xs font-mono tracking-widest uppercase bg-neutral-800 rounded text-neutral-300">
              {match.status}
            </span>
          </div>
          <p className="text-neutral-400 mt-1">{match.competition} • {match.date}</p>
        </div>
        <div className="flex gap-3">
          <Link href="/capture" className="flex items-center gap-2 bg-amharc-green hover:bg-amharc-green/90 text-white px-4 py-2 rounded-md font-medium transition-colors">
            <PlayCircle className="w-5 h-5" />
            Live Capture
          </Link>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Score & Clock Controls */}
        <Card className="bg-[#0f0f0f] border-white/10 md:col-span-2">
          <CardContent className="p-6">
            <div className="flex flex-col md:flex-row justify-between items-center gap-8">
              {/* Home Team Score */}
              <div className="flex-1 flex flex-col items-center">
                <h3 className="text-xl font-bold mb-4">{match.homeTeam}</h3>
                <div className="flex items-center gap-6">
                  <div className="text-center">
                    <div className="text-4xl font-mono mb-2">{score?.homeGoals || 0}</div>
                    <span className="text-neutral-500 uppercase text-xs font-bold tracking-wider">Goals</span>
                  </div>
                  {match.sport === 'gaelic-football' && (
                    <>
                      <span className="text-2xl text-neutral-600">-</span>
                      <div className="text-center">
                        <div className="text-4xl font-mono mb-2">{score?.homeTwoPointScores || 0}</div>
                        <span className="text-neutral-500 uppercase text-xs font-bold tracking-wider">2pt</span>
                      </div>
                    </>
                  )}
                  <span className="text-2xl text-neutral-600">-</span>
                  <div className="text-center">
                    <div className="text-4xl font-mono mb-2">{score?.homePoints || 0}</div>
                    <span className="text-neutral-500 uppercase text-xs font-bold tracking-wider">Points</span>
                  </div>
                  <div className="text-center pl-6 border-l border-white/10">
                    <div className="text-4xl font-mono font-bold text-white mb-2">({score?.homeTotal || 0})</div>
                    <span className="text-neutral-500 uppercase text-xs font-bold tracking-wider">Total</span>
                  </div>
                </div>
              </div>

              {/* Clock */}
              <div className="flex-shrink-0 flex flex-col items-center px-8 border-x border-white/10">
                <span className="text-sm text-neutral-500 uppercase tracking-widest mb-2 font-mono">Period {clock?.currentPeriod || 1}</span>
                <span className={`text-6xl font-mono tracking-wider mb-4 ${clock?.isRunning ? 'text-amharc-lime' : 'text-white'}`}>
                  {formatTime(clock?.matchClockSeconds || 0)}
                </span>
                <div className="flex gap-2">
                  {!clock?.isRunning ? (
                    <Button 
                      variant="outline" 
                      size="icon"
                      className="border-amharc-lime text-amharc-lime hover:bg-amharc-lime hover:text-black"
                      onClick={() => startClock.mutate({ matchId })}
                    >
                      <Play className="w-5 h-5 fill-current" />
                    </Button>
                  ) : (
                    <Button 
                      variant="outline" 
                      size="icon"
                      className="border-amber-500 text-amber-500 hover:bg-amber-500 hover:text-black"
                      onClick={() => pauseClock.mutate({ matchId })}
                    >
                      <Pause className="w-5 h-5 fill-current" />
                    </Button>
                  )}
                </div>
              </div>

              {/* Away Team Score */}
              <div className="flex-1 flex flex-col items-center">
                <h3 className="text-xl font-bold mb-4">{match.awayTeam}</h3>
                <div className="flex items-center gap-6">
                  <div className="text-center">
                    <div className="text-4xl font-mono font-bold text-white mb-2">({score?.awayTotal || 0})</div>
                    <span className="text-neutral-500 uppercase text-xs font-bold tracking-wider">Total</span>
                  </div>
                  <div className="text-center pr-6 border-r border-white/10">
                    <div className="text-4xl font-mono mb-2">{score?.awayPoints || 0}</div>
                    <span className="text-neutral-500 uppercase text-xs font-bold tracking-wider">Points</span>
                  </div>
                  {match.sport === 'gaelic-football' && (
                    <>
                      <span className="text-2xl text-neutral-600">-</span>
                      <div className="text-center">
                        <div className="text-4xl font-mono mb-2">{score?.awayTwoPointScores || 0}</div>
                        <span className="text-neutral-500 uppercase text-xs font-bold tracking-wider">2pt</span>
                      </div>
                    </>
                  )}
                  <span className="text-2xl text-neutral-600">-</span>
                  <div className="text-center">
                    <div className="text-4xl font-mono mb-2">{score?.awayGoals || 0}</div>
                    <span className="text-neutral-500 uppercase text-xs font-bold tracking-wider">Goals</span>
                  </div>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Quick Settings */}
        <Card className="bg-[#0f0f0f] border-white/10">
          <CardHeader>
            <CardTitle className="text-lg flex items-center gap-2">
              <Settings2 className="w-5 h-5" />
              Match Status
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="flex justify-between items-center p-3 bg-neutral-900 rounded-md border border-white/5">
              <span className="text-sm font-medium">State</span>
              <span className="text-sm uppercase font-mono tracking-widest">{match.status}</span>
            </div>
            {match.status === 'setup' && (
              <Button 
                className="w-full bg-white text-black hover:bg-neutral-200"
                onClick={() => startMatch.mutate({ matchId })}
                disabled={startMatch.isPending}
              >
                Mark as Ready
              </Button>
            )}
            <Button variant="outline" className="w-full">
              Edit Match Details
            </Button>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
