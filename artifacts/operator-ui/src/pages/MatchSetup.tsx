import React from "react";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Label } from "@/components/ui/label";
import { useCreateMatch, useGetCameras } from "@workspace/api-client-react";
import { useLocation } from "wouter";
import { toast } from "sonner";
import { z } from "zod";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";
import { MatchInputSport, MatchInputPeriodStructure } from "@workspace/api-client-react";

const matchSchema = z.object({
  sport: z.enum(["gaelic-football", "hurling", "ladies-football", "camogie"] as const),
  competition: z.string().min(1, "Competition is required"),
  season: z.string().min(1, "Season is required"),
  round: z.string().optional(),
  date: z.string().min(1, "Date is required"),
  venue: z.string().optional(),
  homeTeam: z.string().min(1, "Home team is required"),
  homeTeamShort: z.string().max(4).optional(),
  homeTeamColour: z.string().optional(),
  awayTeam: z.string().min(1, "Away team is required"),
  awayTeamShort: z.string().max(4).optional(),
  awayTeamColour: z.string().optional(),
  periodStructure: z.enum(["halves", "quarters", "custom"] as const).optional(),
  cameraId: z.string().optional(),
});

type MatchFormValues = z.infer<typeof matchSchema>;

export default function MatchSetup() {
  const [, setLocation] = useLocation();
  const createMatch = useCreateMatch();
  const { data: cameras } = useGetCameras();

  const form = useForm<MatchFormValues>({
    resolver: zodResolver(matchSchema),
    defaultValues: {
      sport: "gaelic-football",
      competition: "",
      season: new Date().getFullYear().toString(),
      date: new Date().toISOString().split('T')[0],
      homeTeam: "",
      homeTeamShort: "",
      awayTeam: "",
      awayTeamShort: "",
      periodStructure: "halves",
      cameraId: "",
    }
  });

  const onSubmit = (data: MatchFormValues) => {
    createMatch.mutate(
      { data },
      {
        onSuccess: (match) => {
          toast.success("Match created successfully");
          setLocation(`/match/${match.matchId}`);
        },
        onError: () => {
          toast.error("Failed to create match");
        }
      }
    );
  };

  return (
    <div className="p-6 md:p-8 max-w-4xl mx-auto space-y-6">
      <div>
        <h2 className="text-3xl font-bold tracking-tight text-white">New Match Setup</h2>
        <p className="text-neutral-400 mt-1">Configure match details before going live</p>
      </div>

      <Form {...form}>
        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
          <Card className="bg-[#0f0f0f] border-white/10">
            <CardHeader>
              <CardTitle>Core Details</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <FormField
                control={form.control}
                name="sport"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Sport</FormLabel>
                    <Select onValueChange={field.onChange} defaultValue={field.value}>
                      <FormControl>
                        <SelectTrigger className="bg-neutral-900 border-white/10">
                          <SelectValue placeholder="Select sport" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        <SelectItem value="gaelic-football">Gaelic Football</SelectItem>
                        <SelectItem value="hurling">Hurling</SelectItem>
                        <SelectItem value="ladies-football">Ladies Football</SelectItem>
                        <SelectItem value="camogie">Camogie</SelectItem>
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="date"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Date</FormLabel>
                    <FormControl>
                      <Input type="date" className="bg-neutral-900 border-white/10" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="competition"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Competition</FormLabel>
                    <FormControl>
                      <Input placeholder="e.g. Senior Championship" className="bg-neutral-900 border-white/10" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="season"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Season</FormLabel>
                    <FormControl>
                      <Input className="bg-neutral-900 border-white/10" {...field} />
                    </FormControl>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </CardContent>
          </Card>

          <Card className="bg-[#0f0f0f] border-white/10">
            <CardHeader>
              <CardTitle>Teams</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-1 md:grid-cols-2 gap-8">
              <div className="space-y-4">
                <h3 className="font-medium text-amharc-lime">Home Team</h3>
                <FormField
                  control={form.control}
                  name="homeTeam"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Name</FormLabel>
                      <FormControl>
                        <Input placeholder="Full name" className="bg-neutral-900 border-white/10" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="homeTeamShort"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Short Name (3-4 letters)</FormLabel>
                      <FormControl>
                        <Input placeholder="e.g. DUB" maxLength={4} className="bg-neutral-900 border-white/10 uppercase" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="homeTeamColour"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Primary Colour (Hex)</FormLabel>
                      <FormControl>
                        <div className="flex gap-2">
                          <Input type="color" className="w-12 p-1 bg-neutral-900 border-white/10" {...field} value={field.value || "#000000"} />
                          <Input placeholder="#RRGGBB" className="flex-1 bg-neutral-900 border-white/10" {...field} />
                        </div>
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>

              <div className="space-y-4">
                <h3 className="font-medium text-white">Away Team</h3>
                <FormField
                  control={form.control}
                  name="awayTeam"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Name</FormLabel>
                      <FormControl>
                        <Input placeholder="Full name" className="bg-neutral-900 border-white/10" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="awayTeamShort"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Short Name (3-4 letters)</FormLabel>
                      <FormControl>
                        <Input placeholder="e.g. KER" maxLength={4} className="bg-neutral-900 border-white/10 uppercase" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="awayTeamColour"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Primary Colour (Hex)</FormLabel>
                      <FormControl>
                        <div className="flex gap-2">
                          <Input type="color" className="w-12 p-1 bg-neutral-900 border-white/10" {...field} value={field.value || "#ffffff"} />
                          <Input placeholder="#RRGGBB" className="flex-1 bg-neutral-900 border-white/10" {...field} />
                        </div>
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
              </div>
            </CardContent>
          </Card>

          <Card className="bg-[#0f0f0f] border-white/10">
            <CardHeader>
              <CardTitle>Technical Setup</CardTitle>
            </CardHeader>
            <CardContent className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <FormField
                control={form.control}
                name="cameraId"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Primary Camera</FormLabel>
                    <Select onValueChange={field.onChange} defaultValue={field.value}>
                      <FormControl>
                        <SelectTrigger className="bg-neutral-900 border-white/10">
                          <SelectValue placeholder="Select camera" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        {cameras?.map(cam => (
                          <SelectItem key={cam.cameraId} value={cam.cameraId}>{cam.name}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />

              <FormField
                control={form.control}
                name="periodStructure"
                render={({ field }) => (
                  <FormItem>
                    <FormLabel>Match Structure</FormLabel>
                    <Select onValueChange={field.onChange} defaultValue={field.value}>
                      <FormControl>
                        <SelectTrigger className="bg-neutral-900 border-white/10">
                          <SelectValue placeholder="Select structure" />
                        </SelectTrigger>
                      </FormControl>
                      <SelectContent>
                        <SelectItem value="halves">Halves (2 x 35/30m)</SelectItem>
                        <SelectItem value="quarters">Quarters</SelectItem>
                      </SelectContent>
                    </Select>
                    <FormMessage />
                  </FormItem>
                )}
              />
            </CardContent>
          </Card>

          <div className="flex justify-end gap-4">
            <Button type="button" variant="outline" onClick={() => setLocation("/")}>
              Cancel
            </Button>
            <Button type="submit" disabled={createMatch.isPending} className="bg-amharc-green hover:bg-amharc-green/90 text-white">
              {createMatch.isPending ? "Creating..." : "Create Match"}
            </Button>
          </div>
        </form>
      </Form>
    </div>
  );
}
