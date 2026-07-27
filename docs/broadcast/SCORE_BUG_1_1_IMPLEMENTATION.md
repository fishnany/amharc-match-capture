# AMHARC Broadcast Lite — Score Bug 1.1

Status: IMPLEMENTED (renderer + API preview)

## Canonical display order

AMHARC master logo | Team A | Score A (Total) | Elapsed Time | Team B | Score B (Total)

## Sport-specific score formats

- Hurling: Goals-Points (Total)
- Camogie: Goals-Points (Total)
- Ladies Gaelic Football: Goals-Points (Total)
- Men's Gaelic Football: Goals-2pt scores-1pt scores (Total)

## Discipline indicators

- Red: team-side red bar remains once a red-card event exists.
- Black: team-side black bar remains only while a black-card event is active. It is cleared by `black_card_end`, `black_card_expired`, or `player_return` for the affected player/team.
- The renderer does not hard-code sanction duration.

## Brand integrity

The renderer references `/branding/amharc-logo-transparent.png` as an immutable master asset. It must never recreate the AMHARC wordmark or foreground GAA goalposts using SVG/text/CSS primitives.

## Endpoint

`GET /api/broadcast/score-bug.svg?matchId=<id>`

Returns a transparent, resolution-independent SVG suitable for the Operator Preview and future programme compositor.
