---
name: mudblazor-theming
description: 'Apply consistent Material Design UI theming in Egroo using MudBlazor. Use for: adding or updating colors, typography, elevation; ensuring new components follow the orange-on-dark palette; fixing visual inconsistencies; new MudBlazor components that need correct Color/Variant/Elevation props; CSS scoped overrides for theme-integrated layout. Covers the Egroo dark palette, component usage patterns, and the rules for keeping CSS and MudTheme in sync.'
argument-hint: 'Describe the component or UI area to style (e.g. "agent card grid", "nav active state", "new dialog form")'
---

# MudBlazor Theming — Egroo Design System

## When to Use

- Adding a new Razor component and unsure which MudBlazor `Color`, `Variant`, or `Elevation` to use
- Fixing a component whose colors clash with the dark palette
- Updating scoped `.razor.css` overrides so they match theme colors
- Choosing typography (`Typo.*`) for new text elements
- Defining a new elevation tier for a new surface type

---

## Palette Reference

Theme defined in [`src/Egroo.UI/Constants/Theme.cs`](../../../src/Egroo.UI/Constants/Theme.cs) — always edit that file for colour changes, never hardcode hex values in `.razor` files.

| Token | Value | Use on |
|---|---|---|
| `Primary` | `#F25922` | CTAs, active states, accent icons |
| `PrimaryContrastText` | `#FFFFFF` | Text/icons on Primary-colored surfaces |
| `Secondary` | `#F2B591` | Subtle highlights, badges, chips |
| `SecondaryContrastText` | `#1a1a1a` | Text on Secondary-colored surfaces |
| `Background` | `#32333d` | Page background |
| `Surface` | `#1e1e1e` | Cards, drawers, appbar |
| `DrawerBackground` | `#1e1e1e` | Left sidebar |
| `AppbarBackground` | `#1e1e1e` | Top navigation bar |
| `TextPrimary` | `rgba(255,255,255,0.90)` | Main readable text |
| `TextSecondary` | `rgba(255,255,255,0.70)` | Supporting/label text |
| `DrawerText` / `DrawerIcon` | `rgba(255,255,255,0.50)` | Inactive nav items |
| `Divider` | `rgba(255,255,255,0.12)` | Horizontal rules, borders |
| `LinesInputs` | `rgba(255,255,255,0.30)` | Text field underlines |
| `ActionDisabled` | `rgba(255,255,255,0.26)` | Disabled control icons |

> **CSS in `.razor.css` files**: Use the exact hex/rgba values from this table. Never invent new brand colors.
> **Sidebar/drawer** background = `#1e1e1e`. **Active nav link**: `rgba(242,89,34,0.20)` bg + `#F25922` text.

---

## Component Patterns

### Buttons

```razor
@* Primary CTA (filled orange) *@
<MudButton Variant="Variant.Filled" Color="Color.Primary">Save</MudButton>

@* Secondary action *@
<MudButton Variant="Variant.Outlined" Color="Color.Secondary">Cancel</MudButton>

@* Destructive *@
<MudButton Variant="Variant.Text" Color="Color.Error">Delete</MudButton>

@* Ghost/low-emphasis *@
<MudButton Variant="Variant.Text" Color="Color.Default">Close</MudButton>
```

**Rule**: Always specify both `Variant` and `Color`. Never leave either at default when intent matters.

### Text Fields / Forms

```razor
<MudTextField
    @bind-Value="model.Name"
    Label="Display Name"
    Variant="Variant.Outlined"
    Margin="Margin.Dense" />
```

Use `Variant.Outlined` throughout for consistency with the `LinesInputs` border color. `Margin.Dense` for inline/card forms.

### Cards & Surfaces

| Surface type | Elevation | Variant |
|---|---|---|
| Page section container | `0` | — |
| Standard card | `2` | — |
| Prominent card (agent) | `4` | — |
| Floating dialog card | `8` | — |
| Auth/login card | `25` | — |
| Clickable list item | `0` + `.clickable-paper` CSS class | `Outlined` |

```razor
<MudPaper Elevation="4" Class="pa-4">...</MudPaper>
```

### Typography Scale

| `Typo.*` | Intent |
|---|---|
| `h5` | Page/section title (FontWeight 600) |
| `h6` | Card heading, dialog title (FontWeight 600) |
| `body1` | Message bubbles, main content |
| `body2` | Form labels, secondary info |
| `caption` | Timestamps, metadata, helper text |
| `button` | Not used directly — MudButton handles it |

```razor
<MudText Typo="Typo.h6" Color="Color.Primary">Agent Name</MudText>
<MudText Typo="Typo.body2" Color="Color.Secondary">@description</MudText>
```

### Chips / Status Badges

```razor
@* Active *@
<MudChip T="string" Color="Color.Success" Size="Size.Small">Active</MudChip>

@* Inactive *@
<MudChip T="string" Color="Color.Default" Size="Size.Small">Inactive</MudChip>

@* Provider tag *@
<MudChip T="string" Color="Color.Secondary" Variant="Variant.Outlined" Size="Size.Small">@provider</MudChip>
```

### Icons

Use `Color.Primary` for action icons, `Color.Inherit` inside buttons, `Color.Default` for decorative icons.

```razor
<MudIcon Icon="@Icons.Material.Filled.SmartToy" Color="Color.Primary" />
```

### AppBar / Drawer

Already set by theme palette. In `.razor` files use:

```razor
<MudAppBar Elevation="0" Color="Color.Dark">   @* AppbarBackground from theme *@
<MudDrawer @bind-Open="_drawerOpen" Elevation="0" Variant="@DrawerVariant.Responsive">
```

`Elevation="0"` on both — depth comes from `Divider` border, not shadow.

---

## CSS Scoped Override Rules

When a `.razor.css` override is **unavoidable** (MudBlazor doesn't expose a theme token for that element), follow these rules:

1. **Use only values from the Palette Reference table above** — no new hex codes.
2. Always target via `::deep` for child component styles.
3. Group overrides by component in the comment header.

```css
/* === Nav: active link === */
.nav-item ::deep a.active {
    background-color: rgba(242,89,34,0.20); /* Primary @20% opacity */
    color: #F25922;                          /* Primary */
}
```

**Never override** in global `site.css` for component-specific rules — use scoped `.razor.css`.

---

## Elevation & Depth System

| Level | `Elevation=` | Usage |
|---|---|---|
| Flat | `0` | App bars, drawers, nav panels |
| Raised | `2` | Content cards, papers |
| Prominent | `4–8` | Agent/feature cards, dialogs |
| Modal | `25` | Auth forms, login cards |

---

## Adding a New Component — Checklist

- [ ] Colors use `Color.*` enum — no inline hex in `.razor` markup
- [ ] Typography uses `Typo.*` enum on `<MudText>`
- [ ] Elevation matches the tier table above
- [ ] Buttons have explicit `Variant` + `Color`
- [ ] Forms use `Variant.Outlined` text fields with `Margin.Dense`
- [ ] Any CSS overrides in `.razor.css` use only palette values
- [ ] No new brand colors introduced outside `Theme.cs`

---

## Theme File

See [`./references/theme-tokens.md`](./references/theme-tokens.md) for a one-page token cheat-sheet and MudBlazor docs links.
