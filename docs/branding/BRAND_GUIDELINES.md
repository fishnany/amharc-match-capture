# AMHARC Brand Guidelines

**Product:** AMHARC Match Capture  
**Version:** 1.0  
**Date:** July 2026

---

## 1. Official Assets

Two logo files are provided as canonical brand assets.

| File | Purpose |
|------|---------|
| `amharc-logo-transparent.png` | Primary logo for light backgrounds, reports, documentation, login screens, overlay graphics |
| `amharc-app-icon.png` | Application icon for dark backgrounds, Windows shortcut, favicon, splash screen, Stream Deck |

These assets are placed in:

```
apps/operator-ui/public/branding/
apps/overlay-renderer/public/branding/
apps/local-agent/Assets/Branding/
docs/branding/
```

---

## 2. Colour Palette

The canonical AMHARC core palette consists of exactly four colours.

| Name | Hex | Role |
|------|-----|------|
| Black | `#000000` | Primary dark backgrounds, headers, navigation, application chrome |
| AMHARC Green | `#1C8551` | Primary actions, connected states, recording active, match-ready, confirmed states |
| AMHARC Lime | `#B6DC46` | Live timing, active match clock, selected event states, highlights, period active |
| White | `#FFFFFF` | Primary text on dark backgrounds, light backgrounds, form fields, cards, reports |

**Do not introduce additional decorative colours without written approval.**

Functional accessibility colours (warning amber, error red) are permitted where required by WCAG 2.1 and must be documented as functional colours, not brand colours.

---

## 3. Colour Roles

### Black — `#000000`
- Primary dark backgrounds in live capture mode
- Header and navigation backgrounds
- Application chrome
- Video control surfaces
- Full-screen operator mode
- Broadcast graphics backgrounds

### AMHARC Green — `#1C8551`
- Primary action buttons
- Active navigation items
- Connected device state indicator
- Recording active indicator
- Successful export state
- Match-ready state

### AMHARC Lime — `#B6DC46`
- Active match clock display
- Current period indicator
- Selected Stream Deck button state
- Timeline markers
- Event highlight accents
- Live streaming indicator
- Focus indicator outlines

### White — `#FFFFFF`
- Primary text on black backgrounds
- Form field backgrounds
- Card backgrounds
- Report text
- Overlay text on dark video feeds

---

## 4. Logo Usage

### AMHARC [Transparent].png

Use on:
- Light backgrounds
- White report covers
- Exported PDF or HTML reports
- Transparent broadcast overlays (OBS browser source)
- Documentation headers
- About screen
- Login screen

### AMHARC.png

Use on:
- Windows application icon
- Favicon
- Windows executable icon
- Splash screen
- Dark operator dashboard header
- Stream Deck welcome screen
- Installer
- Desktop shortcut
- Compact navigation branding

---

## 5. Clear-Space Guidance

Maintain a minimum clear space around each logo equal to the height of the letter "A" in the AMHARC logotype.

Do not place any text, icon, control, or decorative element within this clear space.

---

## 6. Minimum Logo Sizes

| Context | Minimum width |
|---------|--------------|
| Web / UI header | 120 px |
| Broadcast overlay | 80 px |
| Favicon | 32 px |
| Print report | 30 mm |
| Stream Deck button | 48 px |

---

## 7. Dark-Background Usage

Use `AMHARC.png` (opaque icon variant) on dark backgrounds.

Do not place the dark application icon directly on a black background without a visible boundary (e.g. a subtle border or lighter card background).

If the dark icon must be placed on a black surface, add a thin `1px` border in AMHARC Lime (`#B6DC46`) or a dark card background (`#111111`) to maintain separation.

---

## 8. Light-Background Usage

Use `AMHARC [Transparent].png` on white or light-coloured backgrounds.

Ensure sufficient contrast between the logo and the background. Do not place the transparent logo on AMHARC Lime — the green elements of the logo may become illegible.

---

## 9. Overlay Usage

Broadcast overlays must:
- Use the transparent logo variant
- Place the logo within the title-safe area (10% inset from all edges)
- Never obstruct the match score, match clock, player identification, ball visibility, or team crests
- Support configurable visibility (operators must be able to hide the logo)

---

## 10. Stream Deck Usage

Stream Deck button backgrounds should use:
- Black (`#000000`) as the base background
- White (`#FFFFFF`) for button label text
- AMHARC Green (`#1C8551`) for primary action buttons (Record, Connect)
- AMHARC Lime (`#B6DC46`) for active or selected state buttons (Recording Active, Period Active)
- Reduced-opacity white text (`rgba(255,255,255,0.4)`) for disabled buttons

**Always combine colour with a text label. Never rely on colour alone to communicate button state.**

---

## 11. Accessibility Guidance

- All text must meet WCAG 2.1 AA contrast requirements (minimum 4.5:1 for normal text, 3:1 for large text).
- White text on Black (`#000000`): passes at 21:1.
- Black text on Lime (`#B6DC46`): passes at approximately 7.4:1.
- White text on AMHARC Green (`#1C8551`): approximately 4.6:1 — acceptable for large text and UI elements; validate before use on small text.
- Never use colour as the only indicator of state. Always combine colour with a text label, icon, or pattern.
- Provide visible keyboard focus states using AMHARC Lime as a focus ring colour.
- Critical warnings (storage full, camera disconnected, recording stopped unexpectedly) must display as text alerts, not colour indicators alone.

---

## 12. Prohibited Treatments

The following are explicitly prohibited:

- Redrawing, reinterpreting, or replacing the supplied logos
- Distorting, stretching, skewing, rotating, or cropping the logos
- Changing the logo colours
- Placing text, icons, or controls over the logos
- Placing the transparent logo on AMHARC Lime
- Using the opaque icon on a black background without a visible boundary
- Introducing decorative colours outside the four-colour palette without approval
- Using only colour to convey status or warnings

---

## 13. Asset Naming

| Asset | Filename |
|-------|---------|
| Primary logo (transparent) | `amharc-logo-transparent.png` |
| Application icon (opaque) | `amharc-app-icon.png` |

File names must remain consistent across all repositories and deployment artefacts.

---

## 14. Version Control Requirements

- Both logo files must be committed to the repository and version-controlled.
- Logo files must not be modified locally without updating this document.
- Any new brand asset additions must be documented in this file before deployment.
- Provide lossless originals (PNG) for all digital assets.

---

## 15. Design Tokens

Central design tokens are defined in `packages/shared-ui/src/theme.ts` and `packages/shared-ui/public/design-tokens.json`.

```json
{
  "brand": {
    "black": "#000000",
    "green": "#1C8551",
    "lime": "#B6DC46",
    "white": "#FFFFFF"
  },
  "semantic": {
    "backgroundPrimary": "#000000",
    "backgroundSecondary": "#FFFFFF",
    "textOnDark": "#FFFFFF",
    "textOnLight": "#000000",
    "actionPrimary": "#1C8551",
    "actionSecondary": "#B6DC46",
    "statusConnected": "#1C8551",
    "statusActive": "#B6DC46",
    "borderDark": "#000000",
    "borderLight": "#FFFFFF"
  }
}
```

All application code must reference semantic tokens, not hard-coded hex values.

---

*AMHARC Match Capture — Brand Guidelines v1.0*
