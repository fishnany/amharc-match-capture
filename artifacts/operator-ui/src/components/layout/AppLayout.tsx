import React from "react";
import { Link, useLocation } from "wouter";
import {
  LayoutDashboard,
  Video,
  MonitorPlay,
  ListVideo,
  RadioReceiver,
  Settings,
  Activity,
  Download,
  PlusSquare,
  Layers,
  Gamepad2,
  CalendarDays
} from "lucide-react";
import { cn } from "@/lib/utils";

const NAV_ITEMS = [
  { href: "/", label: "Dashboard", icon: LayoutDashboard },
  { href: "/match/new", label: "New Match", icon: PlusSquare },
  { href: "/capture", label: "Live Capture", icon: MonitorPlay },
  { href: "/cameras", label: "Cameras", icon: Video },
  { href: "/events/current", label: "Events", icon: ListVideo },
  { href: "/stream-deck", label: "Stream Deck", icon: Gamepad2 },
  { href: "/overlays", label: "Overlays", icon: Layers },
  { href: "/streaming", label: "Streaming", icon: RadioReceiver },
  { href: "/health", label: "System Health", icon: Activity },
  { href: "/exports", label: "Exports", icon: Download },
  { href: "/settings", label: "Settings", icon: Settings },
];

export function AppLayout({ children }: { children: React.ReactNode }) {
  const [location] = useLocation();

  const isLiveCapture = location === "/capture";

  if (isLiveCapture) {
    return <div className="min-h-screen bg-black text-white">{children}</div>;
  }

  return (
    <div className="flex min-h-screen bg-black text-white overflow-hidden selection:bg-amharc-lime selection:text-black">
      {/* Sidebar */}
      <aside className="w-64 bg-[#0a0a0a] border-r border-white/10 flex flex-col flex-shrink-0">
        <div className="h-16 flex items-center gap-3 px-4 border-b border-white/10">
          <img
            src="/branding/amharc-app-icon.png"
            alt="AMHARC"
            className="h-10 w-10 object-contain rounded-lg flex-shrink-0"
          />
          <div className="flex flex-col leading-none">
            <span className="text-sm font-bold tracking-widest text-white uppercase font-sans">
              AMHARC
            </span>
            <span className="text-[9px] text-amharc-lime uppercase tracking-widest font-mono mt-0.5">
              Match Capture
            </span>
          </div>
        </div>

        <nav className="flex-1 py-4 px-3 space-y-1 overflow-y-auto">
          {NAV_ITEMS.map((item) => {
            const isActive = location === item.href || (item.href !== "/" && location.startsWith(item.href));
            return (
              <Link
                key={item.href}
                href={item.href}
                className={cn(
                  "flex items-center gap-3 px-3 py-2 rounded-md transition-colors font-medium text-sm",
                  isActive
                    ? "bg-amharc-green/10 text-amharc-lime"
                    : "text-neutral-400 hover:text-white hover:bg-white/5"
                )}
              >
                <item.icon className={cn("w-5 h-5", isActive ? "text-amharc-lime" : "text-neutral-500")} />
                {item.label}
              </Link>
            );
          })}
        </nav>

        <div className="p-4 border-t border-white/10 text-xs font-mono text-neutral-500">
          <div>Operator: <span className="text-white">Admin</span></div>
          <div className="mt-1 flex items-center gap-2">
            <span className="w-2 h-2 rounded-full bg-amharc-green animate-pulse-fast"></span>
            System Online
          </div>
        </div>
      </aside>

      {/* Main Content */}
      <main className="flex-1 flex flex-col min-w-0 overflow-hidden bg-black">
        <div className="flex-1 overflow-y-auto">
          {children}
        </div>
      </main>
    </div>
  );
}
