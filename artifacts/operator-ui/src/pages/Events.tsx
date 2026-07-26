import React, { useState } from "react";
import { useRoute } from "wouter";
import { 
  useGetMatches, 
  useGetMatchEvents, 
  useUpdateMatchEvent, 
  useDeleteMatchEvent, 
  useUndoLastEvent,
  MatchEvent
} from "@workspace/api-client-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Undo2, Flag, Video, Trash2, Edit2 } from "lucide-react";
import { toast } from "sonner";
import { Skeleton } from "@/components/ui/skeleton";

export default function Events() {
  const [matchRoute, matchParams] = useRoute("/events/:matchId");
  const [currentRoute] = useRoute("/events/current");

  const { data: matches } = useGetMatches({ query: { enabled: currentRoute } });
  const activeMatch = matches?.find(m => m.status === "active" || m.status === "ready");

  const matchId = matchRoute ? matchParams?.matchId : activeMatch?.matchId;

  const { data: events, isLoading, refetch } = useGetMatchEvents(matchId || "", {
    query: { enabled: !!matchId, refetchInterval: 2000 }
  });

  const updateEvent = useUpdateMatchEvent();
  const deleteEvent = useDeleteMatchEvent();
  const undoEvent = useUndoLastEvent();

  const [filterTeam, setFilterTeam] = useState<string>("all");
  const [filterType, setFilterType] = useState<string>("all");

  const formatTime = (seconds: number) => {
    const m = Math.floor(seconds / 60).toString().padStart(2, '0');
    const s = (seconds % 60).toString().padStart(2, '0');
    return `${m}:${s}`;
  };

  const handleUndo = () => {
    if(matchId) {
      undoEvent.mutate({ matchId }, {
        onSuccess: () => {
          toast.success("Last event undone");
          refetch();
        }
      });
    }
  };

  const handleDelete = (eventId: string) => {
    if(matchId && confirm("Delete this event?")) {
      deleteEvent.mutate({ matchId, eventId }, {
        onSuccess: () => {
          toast.success("Event deleted");
          refetch();
        }
      });
    }
  };

  const handleFlag = (eventId: string, currentStatus: string) => {
    if(matchId) {
      const newStatus = currentStatus === 'flagged' ? 'unreviewed' : 'flagged';
      updateEvent.mutate({ matchId, eventId, data: { reviewStatus: newStatus } }, {
        onSuccess: () => refetch()
      });
    }
  };

  if (currentRoute && !activeMatch) {
    return (
      <div className="p-8 text-center text-neutral-400">
        No active match running.
      </div>
    );
  }

  const filteredEvents = events?.filter(ev => {
    if (filterTeam !== "all" && ev.team !== filterTeam) return false;
    if (filterType !== "all" && ev.eventType !== filterType) return false;
    return true;
  });

  return (
    <div className="p-6 md:p-8 max-w-6xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-3xl font-bold tracking-tight text-white">Event Timeline</h2>
          <p className="text-neutral-400 mt-1">Review, edit, and flag tagged events</p>
        </div>
        <div className="flex gap-3">
          <Button 
            variant="outline" 
            className="border-white/10 bg-black text-white hover:bg-white/10"
            onClick={handleUndo}
            disabled={undoEvent.isPending || !events?.length}
          >
            <Undo2 className="w-4 h-4 mr-2" /> Undo Last
          </Button>
        </div>
      </div>

      <Card className="bg-[#0f0f0f] border-white/10">
        <CardHeader className="flex flex-row items-center justify-between py-4 border-b border-white/5">
          <div className="flex gap-4">
            <Select value={filterTeam} onValueChange={setFilterTeam}>
              <SelectTrigger className="w-[150px] bg-black border-white/10">
                <SelectValue placeholder="All Teams" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Teams</SelectItem>
                <SelectItem value="home">Home</SelectItem>
                <SelectItem value="away">Away</SelectItem>
              </SelectContent>
            </Select>
            <Select value={filterType} onValueChange={setFilterType}>
              <SelectTrigger className="w-[180px] bg-black border-white/10">
                <SelectValue placeholder="All Event Types" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Types</SelectItem>
                <SelectItem value="goal">Goal</SelectItem>
                <SelectItem value="point">Point</SelectItem>
                <SelectItem value="two-point-score">Two-point score</SelectItem>
                <SelectItem value="card">Card</SelectItem>
                <SelectItem value="substitution">Substitution</SelectItem>
                <SelectItem value="highlight">Highlight</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="text-sm text-neutral-500 font-mono">
            {filteredEvents?.length || 0} events
          </div>
        </CardHeader>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="p-6 space-y-4">
              <Skeleton className="h-12 w-full bg-white/5" />
              <Skeleton className="h-12 w-full bg-white/5" />
              <Skeleton className="h-12 w-full bg-white/5" />
            </div>
          ) : filteredEvents?.length === 0 ? (
            <div className="py-12 text-center text-neutral-500">
              No events found matching your filters.
            </div>
          ) : (
            <div className="divide-y divide-white/5">
              {filteredEvents?.slice().reverse().map(ev => (
                <div key={ev.eventId} className="flex items-center gap-4 p-4 hover:bg-white/5 transition-colors group">
                  <div className="w-20 text-center flex-shrink-0">
                    <Badge variant="outline" className="font-mono text-amharc-lime border-amharc-lime/30 bg-amharc-lime/5">
                      {formatTime(ev.matchClockSeconds)}
                    </Badge>
                  </div>
                  
                  <div className="w-16 flex-shrink-0 flex justify-center">
                    {ev.team === 'home' && <span className="w-3 h-3 rounded-full bg-blue-500" title="Home" />}
                    {ev.team === 'away' && <span className="w-3 h-3 rounded-full bg-red-500" title="Away" />}
                    {!ev.team && <span className="w-3 h-3 rounded-full bg-neutral-600" title="System" />}
                  </div>

                  <div className="flex-1 min-w-0">
                    <div className="flex items-baseline gap-2">
                      <span className="font-bold text-lg uppercase tracking-wide">
                        {ev.eventType.replace('-', ' ')}
                      </span>
                      {ev.scoreAfter && (
                        <span className="text-sm font-mono text-neutral-400">Score: {ev.scoreAfter}</span>
                      )}
                    </div>
                    {ev.note && <p className="text-sm text-neutral-400 mt-1">{ev.note}</p>}
                  </div>

                  <div className="flex items-center gap-6 text-sm text-neutral-500 font-mono flex-shrink-0">
                    <div className="flex items-center gap-1 w-24">
                      {ev.clipRequested && <Video className="w-4 h-4 text-amharc-lime" title="Clip requested" />}
                      {ev.reviewStatus === 'flagged' && <Flag className="w-4 h-4 text-amber-500" title="Flagged for review" />}
                    </div>
                    <div className="w-20">P{ev.period}</div>
                    
                    <div className="flex items-center gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
                      <Button 
                        variant="ghost" 
                        size="icon" 
                        className="w-8 h-8 hover:text-amber-500 hover:bg-amber-500/10"
                        onClick={() => handleFlag(ev.eventId, ev.reviewStatus)}
                      >
                        <Flag className="w-4 h-4" />
                      </Button>
                      <Button variant="ghost" size="icon" className="w-8 h-8 hover:text-white hover:bg-white/10">
                        <Edit2 className="w-4 h-4" />
                      </Button>
                      <Button 
                        variant="ghost" 
                        size="icon" 
                        className="w-8 h-8 hover:text-destructive hover:bg-destructive/10"
                        onClick={() => handleDelete(ev.eventId)}
                      >
                        <Trash2 className="w-4 h-4" />
                      </Button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
