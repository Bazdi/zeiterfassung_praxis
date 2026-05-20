# UI smoke tests

End-to-end browser tests that verify the Claude Design (warm cream + sage
green light theme) is rendered correctly across the key surfaces of the
Blazor app.

## What they cover

- `/setup` — Razor Page renders with the cream/sage palette and submitting
  the form lands the user on the authenticated dashboard.
- `/login` — Razor Page renders the AuthShell layout, the muted-red error
  state is shown on bad credentials, and the link to `/terminal` exists.
- `/` (dashboard) — CSS custom properties (`--bg`, `--accent`, …) match the
  design spec; the sidebar carries no leftover Blazor template gradient;
  brand mark, avatar, and the 4 area cards render with the design tokens.
- `/terminal` — kiosk renders the large serif clock, sage brand mark, and
  employee cards; clicking an employee opens the PIN screen with 6 dots
  and the 12-key numpad; tapping a digit fills a dot in sage.

The intent is to fail loudly the moment someone re-introduces a dark-theme
hex (`#0f1117`, `#4f8ef7`, etc.) into a global stylesheet.

## Run

The Playwright config boots the .NET app on `http://localhost:5101` with a
disposable SQLite DB (`zeiterfassung-test.db`) so `/setup` is always
reachable on a fresh run.

```bash
cd tests/UI
npm install
npx playwright install chromium
npm test            # headless
npm run test:headed # see the browser
npm run test:ui     # Playwright UI mode
```

## CI hint

The webServer block in `playwright.config.js` shells out to `dotnet run`,
so CI just needs the .NET 8 SDK and Node ≥18.
