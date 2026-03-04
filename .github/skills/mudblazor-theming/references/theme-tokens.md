# MudBlazor Theming — Token Cheat Sheet

## Quick Reference

| Category | C# / Razor | Value |
|---|---|---|
| **Primary** | `Color.Primary` | `#F25922` |
| **Secondary** | `Color.Secondary` | `#F2B591` |
| **Error** | `Color.Error` | MudBlazor default red |
| **Success** | `Color.Success` | MudBlazor default green |
| **Surface BG** | `--mud-palette-surface` | `#1e1e1e` |
| **Page BG** | `--mud-palette-background` | `#32333d` |
| **Primary text** | `--mud-palette-text-primary` | `rgba(255,255,255,0.90)` |
| **Secondary text** | `--mud-palette-text-secondary` | `rgba(255,255,255,0.70)` |
| **Divider** | `--mud-palette-divider` | `rgba(255,255,255,0.12)` |

## Variant Usage Guide

| Context | `Variant` |
|---|---|
| Primary action button | `Variant.Filled` |
| Secondary / cancel button | `Variant.Outlined` |
| Ghost / low-emphasis | `Variant.Text` |
| Form inputs | `Variant.Outlined` |
| Clickable list items | `Variant.Outlined` (via `MudPaper`) |

## Elevation Reference

```
0   → AppBar, Drawer, NavPanel  (flat, border gives depth)
2   → Content cards, MudPaper blocks
4   → Feature/agent cards
8   → Dialogs, floating panels
25  → Auth / login card
```

## Typography Quick Map

```
Typo.h5      → Page/section title
Typo.h6      → Card / dialog heading
Typo.body1   → Content, messages
Typo.body2   → Labels, secondary info
Typo.caption → Timestamps, metadata
```

## MudBlazor Docs Links

- Theming: https://mudblazor.com/customization/theming
- Palette: https://mudblazor.com/customization/palette
- Typography: https://mudblazor.com/customization/typography
- Elevation: https://mudblazor.com/utilities/elevation
- Colors enum: https://mudblazor.com/api/mudcolor
- Button variants: https://mudblazor.com/components/button
- Text field: https://mudblazor.com/components/textfield
