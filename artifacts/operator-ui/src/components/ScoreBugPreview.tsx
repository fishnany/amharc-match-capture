import React from "react";

/**
 * Live preview of the server-side canonical Score Bug 1.1 SVG.
 * A changing query value prevents browser caching while the match clock runs.
 */
export function ScoreBugPreview({ visible = true }: { visible?: boolean }) {
  const [tick, setTick] = React.useState(0);

  React.useEffect(() => {
    if (!visible) return;
    const timer = window.setInterval(() => setTick((value) => value + 1), 1000);
    return () => window.clearInterval(timer);
  }, [visible]);

  if (!visible) return null;

  return (
    <img
      src={`/api/broadcast/score-bug.svg?v=${tick}`}
      alt="AMHARC live score bug"
      className="w-[92%] max-w-[1100px] h-auto drop-shadow-2xl"
      draggable={false}
    />
  );
}
