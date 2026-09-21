export function resolveMatchId(
  search: string = window.location.search,
): string | null {
  const params =
    new URLSearchParams(search);

  const matchId =
    params.get("matchId");

  if (!matchId) {
    return null;
  }

  const trimmed =
    matchId.trim();

  return trimmed.length > 0
    ? trimmed
    : null;
}
