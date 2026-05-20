// Playwright config for the Zeiterfassung Blazor app design smoke tests.
// Boots the .NET server on a clean SQLite DB so /setup is reachable.
const { defineConfig, devices } = require('@playwright/test');
const path = require('path');
const fs = require('fs');

const APP_ROOT  = path.resolve(__dirname, '..', '..');
const APP_PROJ  = path.join(APP_ROOT, 'src', 'Zeiterfassung.Web', 'Zeiterfassung.Web.csproj');
const TEST_DB   = path.join(APP_ROOT, 'src', 'Zeiterfassung.Web', 'zeiterfassung-test.db');
const PORT      = process.env.PORT || '5101';
const BASE_URL  = `http://localhost:${PORT}`;

// Remove any pre-existing test DB so /setup is always reachable on a fresh run.
for (const ext of ['', '-shm', '-wal']) {
  try { fs.unlinkSync(TEST_DB + ext); } catch { /* ignore */ }
}

module.exports = defineConfig({
  testDir: __dirname,
  timeout: 30_000,
  expect: { timeout: 5_000 },
  fullyParallel: false,
  workers: 1,
  reporter: [['list']],
  use: {
    baseURL: BASE_URL,
    headless: true,
    viewport: { width: 1440, height: 900 },
    screenshot: 'only-on-failure',
    video: 'off',
    trace: 'retain-on-failure',
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
  ],
  webServer: {
    command: `dotnet run --project "${APP_PROJ}" --urls "${BASE_URL}" --no-launch-profile`,
    url: BASE_URL,
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
    cwd: APP_ROOT,
    env: {
      ASPNETCORE_ENVIRONMENT: 'Development',
      // Point the app at an isolated test DB — must match the key used in
      // Program.cs (Configuration.GetConnectionString("DefaultConnection")).
      ConnectionStrings__DefaultConnection: `Data Source=${TEST_DB}`,
    },
  },
});
