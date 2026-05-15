---
version: "alpha"
name: Minimal Switcher
description: Compact developer tool UI for a fast Windows switcher and settings surface.
colors:
  primary: "#F5F6F8"
  secondary: "#AEB3BC"
  muted: "#737A86"
  surface: "#0B0D10"
  surfaceRaised: "#0F1319"
  surfaceInteractive: "#12151B"
  border: "#202633"
  borderStrong: "#3A4353"
  accent: "#E6FFFFFF"
  accentBlue: "#8BA7FF"
  danger: "#FF5F57"
typography:
  title:
    fontFamily: Segoe UI Variable
    fontSize: 28px
    fontWeight: 600
    lineHeight: 1.15
  section:
    fontFamily: Segoe UI Variable
    fontSize: 16px
    fontWeight: 600
    lineHeight: 1.2
  body:
    fontFamily: Segoe UI Variable
    fontSize: 13px
    fontWeight: 400
    lineHeight: 1.35
  label:
    fontFamily: Segoe UI Variable
    fontSize: 12px
    fontWeight: 600
    lineHeight: 1.2
rounded:
  sm: 12px
  md: 15px
  lg: 22px
  xl: 28px
spacing:
  xs: 6px
  sm: 10px
  md: 18px
  lg: 22px
components:
  button:
    backgroundColor: "{colors.surfaceInteractive}"
    textColor: "{colors.primary}"
    rounded: "{rounded.md}"
    height: 38px
  button-hover:
    backgroundColor: "#1B2028"
    textColor: "{colors.primary}"
    rounded: "{rounded.md}"
  card:
    backgroundColor: "{colors.surfaceRaised}"
    textColor: "{colors.primary}"
    rounded: "{rounded.lg}"
    padding: 18px
---

## Overview

Minimal Switcher uses a quiet, tool-like interface: dark matte surfaces, compact controls, and subtle interaction feedback. The UI should feel precise and calm, with enough polish to make repeated use pleasant without becoming decorative.

## Colors

The palette is built around near-black surfaces, soft white text, and restrained accents. Accent colors are used sparingly for selection, theme previews, and icon details.

## Typography

Use Segoe UI Variable for native Windows fit. Keep labels compact, section titles clear, and avoid large marketing-style type inside settings surfaces.

## Layout

Prefer two-column settings layouts with persistent context on the left and dense controls on the right. Controls should align to stable rows and keep predictable hit targets.

## Elevation & Depth

Use one outer shadow for the settings window and thin borders for inner panels. Avoid blur and shader effects in settings.

## Shapes

Rounded corners are part of the identity, but should remain functional: large panels use 22-28px radii, compact controls use 12-15px.

## Components

Buttons and list rows should not show native WPF highlight colors. Hover states use subtle surface changes, border emphasis, or scale feedback.

## Do's and Don'ts

- Do keep settings dense, readable, and immediately useful.
- Do make hover and pressed states consistent across buttons, cards, rows, and theme tiles.
- Don't use default system list selection chrome.
- Don't use shader or blur effects in the settings window.
