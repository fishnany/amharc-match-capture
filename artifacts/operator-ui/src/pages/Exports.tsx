import React, { useState } from "react";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { 
  useGetMatches, 
  useExportMatch,
  ExportRequestFormatsItem
} from "@workspace/api-client-react";
import { Download, FileJson, FileSpreadsheet, FileCode2, FileText, CheckCircle2 } from "lucide-react";
import { toast } from "sonner";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";

export default function Exports() {
  const { data: matches } = useGetMatches();
  const exportMatch = useExportMatch();

  const [selectedMatch, setSelectedMatch] = useState<string>("");
  const [formats, setFormats] = useState<ExportRequestFormatsItem[]>(["json", "csv", "manifest"]);

  const toggleFormat = (fmt: ExportRequestFormatsItem) => {
    setFormats(prev => 
      prev.includes(fmt) 
        ? prev.filter(f => f !== fmt)
        : [...prev, fmt]
    );
  };

  const handleExport = () => {
    if (!selectedMatch) {
      toast.error("Please select a match to export");
      return;
    }
    if (formats.length === 0) {
      toast.error("Please select at least one export format");
      return;
    }

    exportMatch.mutate(
      { 
        matchId: selectedMatch,
        data: { formats }
      },
      {
        onSuccess: (res) => {
          if (res.success) {
            toast.success("Export completed successfully");
          } else {
            toast.error(res.message || "Export failed");
          }
        },
        onError: () => toast.error("Export request failed")
      }
    );
  };

  return (
    <div className="p-6 md:p-8 max-w-4xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-3xl font-bold tracking-tight text-white">Exports</h2>
          <p className="text-neutral-400 mt-1">Generate match data packages for post-production</p>
        </div>
      </div>

      <Card className="bg-[#0f0f0f] border-white/10">
        <CardHeader>
          <CardTitle>Data Export</CardTitle>
          <CardDescription>Export timeline events and match metadata</CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          <div className="space-y-3">
            <label className="text-sm font-medium">Select Match</label>
            <Select value={selectedMatch} onValueChange={setSelectedMatch}>
              <SelectTrigger className="bg-black border-white/10">
                <SelectValue placeholder="Choose a match to export..." />
              </SelectTrigger>
              <SelectContent>
                {matches?.map(m => (
                  <SelectItem key={m.matchId} value={m.matchId}>
                    {m.date} - {m.homeTeam} vs {m.awayTeam} ({m.status})
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-3">
            <label className="text-sm font-medium">Export Formats</label>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <label className="flex items-start gap-3 p-4 rounded-lg border border-white/10 bg-neutral-900 cursor-pointer hover:bg-white/5 transition-colors">
                <Checkbox 
                  checked={formats.includes("json")} 
                  onCheckedChange={() => toggleFormat("json")}
                  className="mt-1"
                />
                <div>
                  <div className="font-medium flex items-center gap-2">
                    <FileJson className="w-4 h-4 text-amharc-lime" /> AMHARC JSON
                  </div>
                  <p className="text-xs text-neutral-500 mt-1">Full event timeline with metadata</p>
                </div>
              </label>

              <label className="flex items-start gap-3 p-4 rounded-lg border border-white/10 bg-neutral-900 cursor-pointer hover:bg-white/5 transition-colors">
                <Checkbox 
                  checked={formats.includes("csv")} 
                  onCheckedChange={() => toggleFormat("csv")}
                  className="mt-1"
                />
                <div>
                  <div className="font-medium flex items-center gap-2">
                    <FileSpreadsheet className="w-4 h-4 text-blue-400" /> Standard CSV
                  </div>
                  <p className="text-xs text-neutral-500 mt-1">Spreadsheet compatible event log</p>
                </div>
              </label>

              <label className="flex items-start gap-3 p-4 rounded-lg border border-white/10 bg-neutral-900 cursor-pointer hover:bg-white/5 transition-colors">
                <Checkbox 
                  checked={formats.includes("manifest")} 
                  onCheckedChange={() => toggleFormat("manifest")}
                  className="mt-1"
                />
                <div>
                  <div className="font-medium flex items-center gap-2">
                    <FileCode2 className="w-4 h-4 text-purple-400" /> Video Manifest
                  </div>
                  <p className="text-xs text-neutral-500 mt-1">Links events to video timestamps</p>
                </div>
              </label>

              <label className="flex items-start gap-3 p-4 rounded-lg border border-white/10 bg-neutral-900 cursor-pointer hover:bg-white/5 transition-colors">
                <Checkbox 
                  checked={formats.includes("technical-log")} 
                  onCheckedChange={() => toggleFormat("technical-log")}
                  className="mt-1"
                />
                <div>
                  <div className="font-medium flex items-center gap-2">
                    <FileText className="w-4 h-4 text-neutral-400" /> Technical Log
                  </div>
                  <p className="text-xs text-neutral-500 mt-1">System health and connection history</p>
                </div>
              </label>
            </div>
          </div>

          <div className="pt-4 border-t border-white/10 flex justify-end">
            <Button 
              className="bg-amharc-green hover:bg-amharc-green/90 text-white"
              onClick={handleExport}
              disabled={!selectedMatch || formats.length === 0 || exportMatch.isPending}
            >
              <Download className="w-4 h-4 mr-2" />
              {exportMatch.isPending ? "Exporting..." : "Generate Export Package"}
            </Button>
          </div>

          {exportMatch.isSuccess && exportMatch.data?.success && (
            <div className="mt-4 p-4 bg-amharc-green/10 border border-amharc-green/20 rounded-lg flex items-start gap-3 text-sm">
              <CheckCircle2 className="w-5 h-5 text-amharc-green shrink-0" />
              <div>
                <strong className="text-amharc-green block mb-1">Export Complete</strong>
                <p className="text-neutral-300">Files saved to: <span className="font-mono text-xs">{exportMatch.data.exportDirectory}</span></p>
                <ul className="list-disc pl-4 mt-2 text-neutral-400 text-xs font-mono space-y-1">
                  {exportMatch.data.files.map((f, i) => <li key={i}>{f}</li>)}
                </ul>
              </div>
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
