import React from "react";
import { useGetOverlayTemplates, useGetOverlayState } from "@workspace/api-client-react";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Layers, Monitor, Play, Square, Settings2 } from "lucide-react";
import { Badge } from "@/components/ui/badge";

export default function Overlays() {
  const { data: templates } = useGetOverlayTemplates();
  const { data: state } = useGetOverlayState();

  const activeTemplate = templates?.find(t => t.templateId === state?.activeTemplateId) || templates?.[0];

  return (
    <div className="p-6 md:p-8 max-w-6xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-3xl font-bold tracking-tight text-white">Broadcast Overlays</h2>
          <p className="text-neutral-400 mt-1">Manage graphics and scoreboard outputs</p>
        </div>
        <div className="flex items-center gap-3">
          <Badge variant="outline" className={`font-mono ${state?.isVisible ? 'border-amharc-green text-amharc-green' : 'border-neutral-600 text-neutral-500'}`}>
            {state?.isVisible ? 'OVERLAYS LIVE' : 'OVERLAYS HIDDEN'}
          </Badge>
          <Button className={state?.isVisible ? "bg-destructive text-white hover:bg-destructive/90" : "bg-amharc-green text-white hover:bg-amharc-green/90"}>
            {state?.isVisible ? "Hide All" : "Show All"}
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <Card className="bg-[#0f0f0f] border-white/10 lg:col-span-2">
          <CardHeader className="border-b border-white/5 pb-4">
            <div className="flex items-center justify-between">
              <CardTitle>Program Output Preview</CardTitle>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" className="bg-neutral-900 border-white/10 h-8">
                  <Monitor className="w-4 h-4 mr-2" /> Pop out
                </Button>
              </div>
            </div>
          </CardHeader>
          <CardContent className="p-6">
            <div className="w-full aspect-video bg-neutral-900 rounded-lg border border-white/5 relative overflow-hidden shadow-inner">
              {/* Checkerboard background to show transparency */}
              <div className="absolute inset-0 opacity-10" style={{ backgroundImage: 'linear-gradient(45deg, #333 25%, transparent 25%), linear-gradient(-45deg, #333 25%, transparent 25%), linear-gradient(45deg, transparent 75%, #333 75%), linear-gradient(-45deg, transparent 75%, #333 75%)', backgroundSize: '20px 20px', backgroundPosition: '0 0, 0 10px, 10px -10px, -10px 0px' }}></div>
              
              {state?.isVisible && (
                <div className="absolute top-8 left-8 right-8 pointer-events-none">
                  <div className="bg-black/80 backdrop-blur border border-white/20 rounded shadow-2xl p-3 inline-flex gap-6 font-sans">
                    <div className="flex items-center gap-3">
                      <span className="font-bold text-lg">DUB</span>
                      <span className="text-2xl font-mono text-amharc-lime">1-12</span>
                    </div>
                    <div className="w-px bg-white/20"></div>
                    <div className="flex items-center gap-3">
                      <span className="text-2xl font-mono text-amharc-lime">0-14</span>
                      <span className="font-bold text-lg">KER</span>
                    </div>
                    <div className="w-px bg-white/20"></div>
                    <div className="flex items-center text-xl font-mono">
                      34:12
                    </div>
                  </div>
                </div>
              )}

              {!state?.isVisible && (
                <div className="absolute inset-0 flex items-center justify-center text-neutral-600">
                  <Layers className="w-12 h-12 mb-2 opacity-50" />
                  <p className="font-mono text-sm tracking-widest uppercase mt-4">Overlays Hidden</p>
                </div>
              )}
            </div>

            <div className="mt-6 flex items-center justify-between">
              <div className="space-y-1">
                <p className="text-sm font-medium">Output Mode</p>
                <p className="text-xs text-neutral-500">How the video feed is composed</p>
              </div>
              <div className="flex gap-2">
                <Button variant={state?.outputMode === 'clean' ? 'default' : 'outline'} className={state?.outputMode === 'clean' ? 'bg-white text-black' : 'bg-black border-white/10'}>
                  Clean
                </Button>
                <Button variant={state?.outputMode === 'programme' ? 'default' : 'outline'} className={state?.outputMode === 'programme' ? 'bg-white text-black' : 'bg-black border-white/10'}>
                  Programme
                </Button>
                <Button variant={state?.outputMode === 'overlay-only' ? 'default' : 'outline'} className={state?.outputMode === 'overlay-only' ? 'bg-white text-black' : 'bg-black border-white/10'}>
                  Key & Fill
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>

        <div className="space-y-6">
          <Card className="bg-[#0f0f0f] border-white/10">
            <CardHeader>
              <CardTitle>Template Selection</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-2">
                <label className="text-sm text-neutral-400">Scoreboard Style</label>
                <Select value={activeTemplate?.templateId}>
                  <SelectTrigger className="bg-black border-white/10">
                    <SelectValue placeholder="Select template" />
                  </SelectTrigger>
                  <SelectContent>
                    {templates?.map(t => (
                      <SelectItem key={t.templateId} value={t.templateId}>{t.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              
              <div className="pt-4 border-t border-white/10">
                <Button variant="outline" className="w-full border-white/10 bg-black justify-start">
                  <Settings2 className="w-4 h-4 mr-2" /> Configure Colours & Logos
                </Button>
              </div>
            </CardContent>
          </Card>

          <Card className="bg-[#0f0f0f] border-white/10">
            <CardHeader>
              <CardTitle>Manual Graphics</CardTitle>
              <CardDescription>Trigger full-screen or lower third graphics instantly.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-2">
              <Button variant="outline" className="w-full justify-between bg-black border-white/10 group hover:border-amharc-lime/50">
                <span>Lower Third (Custom)</span>
                <Play className="w-4 h-4 opacity-50 group-hover:opacity-100 group-hover:text-amharc-lime" />
              </Button>
              <Button variant="outline" className="w-full justify-between bg-black border-white/10 group hover:border-amharc-lime/50">
                <span>Starting Soon</span>
                <Play className="w-4 h-4 opacity-50 group-hover:opacity-100 group-hover:text-amharc-lime" />
              </Button>
              <Button variant="outline" className="w-full justify-between bg-black border-white/10 group hover:border-amharc-lime/50">
                <span>Half Time Stats</span>
                <Play className="w-4 h-4 opacity-50 group-hover:opacity-100 group-hover:text-amharc-lime" />
              </Button>
              <Button variant="outline" className="w-full justify-between bg-black border-white/10 group hover:border-amharc-lime/50">
                <span>Technical Fault</span>
                <Play className="w-4 h-4 opacity-50 group-hover:opacity-100 group-hover:text-amharc-lime" />
              </Button>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
