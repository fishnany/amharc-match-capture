import type { LiveReadinessStateV1 } from "@workspace/api-client-react";
import { Link } from "wouter";
import {
  AlertTriangle,
  CheckCircle2,
  CircleSlash2,
  RefreshCw,
  ShieldCheck,
  XCircle,
} from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/utils";

type LiveReadinessPanelProps = {
  readiness?: LiveReadinessStateV1;
  isLoading: boolean;
  isFetching: boolean;
  hasError: boolean;
  onRefresh: () => void;
};

const statusStyles = {
  Ready: {
    border: "border-amharc-lime/40",
    surface: "bg-amharc-lime/5",
    badge: "bg-amharc-lime text-black",
    icon: CheckCircle2,
  },
  Degraded: {
    border: "border-amber-500/40",
    surface: "bg-amber-500/5",
    badge: "bg-amber-500 text-black",
    icon: AlertTriangle,
  },
  Blocked: {
    border: "border-destructive/50",
    surface: "bg-destructive/5",
    badge: "bg-destructive text-white",
    icon: XCircle,
  },
} as const;

function formatObservedAt(value?: string): string {
  if (!value) {
    return "Not yet assessed";
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleTimeString([], {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  });
}

export function LiveReadinessPanel({
  readiness,
  isLoading,
  isFetching,
  hasError,
  onRefresh,
}: LiveReadinessPanelProps) {
  const status = readiness?.status ?? "Blocked";
  const presentation = statusStyles[status];
  const StatusIcon = presentation.icon;
  const canEnterCapture = readiness?.ready === true;

  const blockingFindings =
    readiness?.findings.filter((finding) => finding.severity === "Blocking") ?? [];

  const advisoryFindings =
    readiness?.findings.filter((finding) => finding.severity !== "Blocking") ?? [];

  return (
    <Card
      className={cn(
        "bg-[#0f0f0f] border",
        presentation.border,
        presentation.surface,
      )}
      data-readiness-status={status}
      data-readiness-ready={canEnterCapture ? "true" : "false"}
    >
      <CardHeader className="pb-4">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div className="flex items-start gap-3">
            <div
              className={cn(
                "mt-0.5 rounded-full border p-2",
                presentation.border,
                presentation.surface,
              )}
            >
              <StatusIcon className="h-5 w-5" />
            </div>
            <div>
              <div className="flex flex-wrap items-center gap-3">
                <CardTitle className="text-xl">Live Readiness</CardTitle>
                <Badge className={presentation.badge}>{status}</Badge>
              </div>
              <p className="mt-1 text-sm text-neutral-400">
                {isLoading
                  ? "Assessing the canonical match-capture readiness state..."
                  : hasError
                    ? "The readiness service is currently unavailable."
                    : readiness?.ready
                      ? "All required conditions permit entry to Live Capture."
                      : "One or more required conditions are preventing Live Capture."}
              </p>
              <p className="mt-2 font-mono text-xs text-neutral-500">
                Last assessed: {formatObservedAt(readiness?.observedAtUtc)}
              </p>
            </div>
          </div>

          <Button
            type="button"
            variant="outline"
            size="sm"
            className="border-white/10 bg-black/20"
            onClick={onRefresh}
            disabled={isFetching}
          >
            <RefreshCw
              className={cn("mr-2 h-4 w-4", isFetching && "animate-spin")}
            />
            Recheck
          </Button>
        </div>
      </CardHeader>

      <CardContent className="space-y-5">
        {readiness?.checks?.length ? (
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 xl:grid-cols-3">
            {readiness.checks.map((check) => {
              const CheckIcon =
                check.status === "Ready"
                  ? CheckCircle2
                  : check.status === "Degraded"
                    ? AlertTriangle
                    : XCircle;

              const stateClass =
                check.status === "Ready"
                  ? "text-amharc-lime"
                  : check.status === "Degraded"
                    ? "text-amber-500"
                    : "text-destructive";

              return (
                <div
                  key={check.dimension}
                  className="rounded-md border border-white/10 bg-black/25 p-3"
                >
                  <div className="flex items-start gap-2">
                    <CheckIcon className={cn("mt-0.5 h-4 w-4 shrink-0", stateClass)} />
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <span className="text-sm font-semibold">{check.dimension}</span>
                        {!check.required && (
                          <span className="text-[10px] uppercase tracking-wider text-neutral-500">
                            Advisory
                          </span>
                        )}
                      </div>
                      <p className="mt-1 text-xs text-neutral-400">{check.summary}</p>
                      {check.detail && (
                        <p className="mt-1 text-xs text-neutral-500">{check.detail}</p>
                      )}
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        ) : (
          <div className="flex items-center gap-3 rounded-md border border-white/10 bg-black/25 p-4 text-sm text-neutral-400">
            <CircleSlash2 className="h-4 w-4" />
            {isLoading
              ? "Readiness checks are loading."
              : "No readiness checks are currently available."}
          </div>
        )}

        {(blockingFindings.length > 0 || advisoryFindings.length > 0) && (
          <div className="space-y-3">
            {blockingFindings.length > 0 && (
              <div className="rounded-md border border-destructive/40 bg-destructive/5 p-4">
                <div className="mb-2 flex items-center gap-2 font-semibold text-destructive">
                  <XCircle className="h-4 w-4" />
                  Blocking findings
                </div>
                <div className="space-y-2">
                  {blockingFindings.map((finding) => (
                    <div key={`${finding.dimension}-${finding.code}`} className="text-sm">
                      <span className="font-medium">{finding.dimension}:</span>{" "}
                      <span className="text-neutral-300">{finding.message}</span>
                      <span className="ml-2 font-mono text-xs text-neutral-500">
                        {finding.code}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {advisoryFindings.length > 0 && (
              <div className="rounded-md border border-amber-500/30 bg-amber-500/5 p-4">
                <div className="mb-2 flex items-center gap-2 font-semibold text-amber-500">
                  <AlertTriangle className="h-4 w-4" />
                  Advisory findings
                </div>
                <div className="space-y-2">
                  {advisoryFindings.map((finding) => (
                    <div key={`${finding.dimension}-${finding.code}`} className="text-sm">
                      <span className="font-medium">{finding.dimension}:</span>{" "}
                      <span className="text-neutral-300">{finding.message}</span>
                      <span className="ml-2 font-mono text-xs text-neutral-500">
                        {finding.code}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        )}

        <div className="flex flex-col gap-3 border-t border-white/10 pt-4 sm:flex-row sm:items-center sm:justify-between">
          <div className="flex items-center gap-2 text-sm text-neutral-400">
            <ShieldCheck className="h-4 w-4" />
            The Agent is the authority for this decision. The console does not
            recalculate readiness.
          </div>

          {canEnterCapture ? (
            <Link
              href="/capture"
              className="inline-flex h-10 items-center justify-center rounded-md bg-amharc-green px-5 text-sm font-medium text-white transition-colors hover:bg-amharc-green/90"
            >
              Enter Live Capture
            </Link>
          ) : (
            <Button
              type="button"
              disabled
              className="bg-neutral-800 text-neutral-500"
            >
              {blockingFindings.length > 0
                ? "Resolve Blocking Checks"
                : "Readiness Requirements Not Met"}
            </Button>
          )}
        </div>
      </CardContent>
    </Card>
  );
}