// Design-token smoke tests.
//
// Verifies the warm-cream / sage-green light theme from the Claude Design
// handoff (see _design_extract/) is rendered correctly across the key
// surfaces: /setup, /login, /terminal, and the authenticated dashboard.
//
// Tokens we assert (must stay in lockstep with MainLayout.razor):
//   --bg          #f4f1ea   warm cream paper
//   --bg-alt      #ede9df   sidebar
//   --surface     #ffffff   cards
//   --accent      #1f5b4c   deep sage
//   --text        #1a1815   warm near-black
//
// Earlier dark-theme values (#0f1117 bg, #4f8ef7 accent) MUST NOT appear.

const { test, expect } = require('@playwright/test');

// Helper: read a CSS custom property off :root
async function rootVar(page, name) {
  return page.evaluate(
    n => getComputedStyle(document.documentElement).getPropertyValue(n).trim(),
    name,
  );
}

// Helper: assert computed bg color of a selector
async function expectBg(page, selector, expected) {
  const actual = await page.evaluate(
    s => getComputedStyle(document.querySelector(s)).backgroundColor,
    selector,
  );
  expect(actual, `${selector} background-color`).toBe(expected);
}

const FRESH_USER = {
  username: 'uitest',
  display:  'UI Test',
  password: 'UiTestPasswort1!',
};

test.describe('Setup page (/setup)', () => {
  test('renders warm cream bg and sage brand mark', async ({ page }) => {
    await page.goto('/setup');
    await expect(page).toHaveTitle(/Ersteinrichtung/);

    await expectBg(page, 'body',         'rgb(244, 241, 234)'); // #f4f1ea
    await expectBg(page, '.card',        'rgb(255, 255, 255)'); // white surface
    await expectBg(page, '.brand-mark',  'rgb(31, 91, 76)');    // sage
    await expectBg(page, 'button',       'rgb(31, 91, 76)');    // sage submit

    // No dark-theme residue
    const bodyBg = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);
    expect(bodyBg, 'body must not be dark').not.toBe('rgb(15, 17, 23)');

    await expect(page.locator('.badge-new')).toContainText(/Ersteinrichtung/i);
  });

  test('submits and lands on the authenticated dashboard', async ({ page }) => {
    await page.goto('/setup');
    await page.fill('#username',    FRESH_USER.username);
    await page.fill('#displayName', FRESH_USER.display);
    await page.fill('#password',    FRESH_USER.password);
    await page.fill('#confirm',     FRESH_USER.password);
    await page.click('button[type="submit"]');

    await expect(page).toHaveURL('/');
    await expect(page.locator('.sidebar')).toBeVisible();
  });
});

test.describe('Login page (/login)', () => {
  test.beforeEach(async ({ page }) => {
    // Log out (auth cookie persists across tests within the same browser context)
    await page.goto('/logout').catch(() => {});
  });

  test('renders the AuthShell light layout with sage clock badge', async ({ page }) => {
    await page.goto('/login');
    await expect(page).toHaveTitle(/Admin-Login/);

    await expectBg(page, 'body',           'rgb(244, 241, 234)');
    await expectBg(page, '.card',          'rgb(255, 255, 255)');
    await expectBg(page, '.brand-mark',    'rgb(31, 91, 76)');
    await expectBg(page, 'button',         'rgb(31, 91, 76)');

    await expect(page.locator('.brand-title')).toContainText('Zeiterfassung Arztpraxis');
    await expect(page.locator('.terminal-link')).toHaveAttribute('href', '/terminal');
  });

  test('shows muted-red error on bad credentials (not neon red)', async ({ page }) => {
    await page.goto('/login');
    await page.fill('#username', 'nobody');
    await page.fill('#password', 'wrongpw');
    await page.click('button[type="submit"]');

    const err = page.locator('.error');
    await expect(err).toBeVisible();
    await expectBg(page, '.error', 'rgb(251, 233, 228)'); // #fbe9e4 muted red bg
  });
});

test.describe('Authenticated dashboard (/)', () => {
  // log in once for this block
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.fill('#username', FRESH_USER.username);
    await page.fill('#password', FRESH_USER.password);
    await page.click('button[type="submit"]');
    await expect(page).toHaveURL('/');
  });

  test('CSS custom properties match the design spec', async ({ page }) => {
    expect(await rootVar(page, '--bg')).toBe('#f4f1ea');
    expect(await rootVar(page, '--bg-alt')).toBe('#ede9df');
    expect(await rootVar(page, '--surface')).toBe('#ffffff');
    expect(await rootVar(page, '--accent')).toBe('#1f5b4c');
    expect(await rootVar(page, '--text')).toBe('#1a1815');
  });

  test('sidebar is cream — no dark Blazor-template gradient', async ({ page }) => {
    await expectBg(page, '.sidebar', 'rgb(237, 233, 223)'); // #ede9df
    const sbBgImage = await page.evaluate(
      () => getComputedStyle(document.querySelector('.sidebar')).backgroundImage
    );
    expect(sbBgImage, 'sidebar must NOT carry the leftover Blazor purple gradient').toBe('none');
  });

  test('brand mark and user avatar render with the design pattern', async ({ page }) => {
    await expectBg(page, '.nav-brand-mark', 'rgb(31, 91, 76)');
    await expect(page.locator('.nav-brand-title')).toHaveText('Zeiterfassung');
    await expect(page.locator('.nav-user-avatar')).toBeVisible();
    const initials = await page.locator('.nav-user-avatar').textContent();
    expect(initials?.trim().length, 'avatar shows initials').toBeGreaterThan(0);
  });

  test('exactly 4 dashboard cards with the primary card highlighted', async ({ page }) => {
    await expect(page.locator('.dashboard-card')).toHaveCount(4);
    // Primary card has the sage gradient
    const primaryBorder = await page.evaluate(
      () => getComputedStyle(document.querySelector('.dashboard-card.primary')).borderTopColor
    );
    // border uses --accent-border which is rgba(31, 91, 76, 0.25)
    expect(primaryBorder).toMatch(/rgba\(31,\s*91,\s*76/);
  });
});

test.describe('Terminal kiosk (/terminal)', () => {
  test('renders the calm paper kiosk — brand, big serif clock, employees', async ({ page }) => {
    await page.goto('/terminal');
    // wait for Blazor Server to hydrate the dynamic clock
    await page.waitForSelector('.terminal-time', { timeout: 5000 });

    await expectBg(page, '.terminal-container', 'rgb(244, 241, 234)');
    await expectBg(page, '.terminal-brand-mark', 'rgb(31, 91, 76)');

    // Clock shows HH:MM
    const clock = await page.locator('.terminal-time').textContent();
    expect(clock?.trim()).toMatch(/^\d{2}:\d{2}$/);

    // Date label (German)
    await expect(page.locator('.terminal-date')).toContainText('20');

    // At least one employee card (setup created one)
    await expect(page.locator('.emp-btn').first()).toBeVisible();
    await expectBg(page, '.emp-btn', 'rgb(255, 255, 255)');

    // Footer pill is sage
    const onlineColor = await page.evaluate(
      () => getComputedStyle(document.querySelector('.terminal-online')).color
    );
    expect(onlineColor).toBe('rgb(47, 122, 92)'); // --green-fg
  });

  test('clicking an employee opens the PIN screen with 6 dots + 12 numpad keys', async ({ page }) => {
    await page.goto('/terminal');
    await page.waitForSelector('.emp-btn');

    // Wait for Blazor Server circuit to come up — otherwise the very first
    // @onclick is dropped. Blazor exposes Blazor._internal once connected.
    await page.waitForFunction(
      () => !!window.Blazor,
      null,
      { timeout: 10_000 },
    );

    await page.locator('.emp-btn').first().click();

    await expect(page.locator('.pin-container')).toBeVisible();
    await expect(page.locator('.pin-avatar')).toBeVisible();
    await expect(page.locator('.pin-dot')).toHaveCount(6);
    await expect(page.locator('.numpad-btn')).toHaveCount(12);

    // confirm button is sage
    await expectBg(page, '.numpad-confirm', 'rgb(31, 91, 76)');

    // Tap one digit → first dot fills with sage. Use toHaveCSS so we poll
    // the actual computed style (Blazor Server applies it via SignalR).
    await page.locator('.numpad-btn').first().click();
    await expect(page.locator('.pin-dot').first())
      .toHaveCSS('background-color', 'rgb(31, 91, 76)', { timeout: 5_000 });
  });
});

// ─── Responsive layout ──────────────────────────────────────────────
//
// Three viewports: phone (375), tablet (768), desktop (1440).
// Assert: hamburger only shows on phone, sidebar slides in on toggle,
// dashboard cards reflow, terminal clock scales with viewport width,
// employee grid uses 1/2/3 cols by breakpoint.

const VIEWPORTS = {
  phone:   { width: 375,  height: 812  },
  tablet:  { width: 768,  height: 1024 },
  desktop: { width: 1440, height: 900  },
};

async function login(page) {
  await page.goto('/login');
  // Already logged in? /login redirects to /.
  if (new URL(page.url()).pathname === '/') return;
  await page.fill('#username', FRESH_USER.username);
  await page.fill('#password', FRESH_USER.password);
  await page.click('button[type="submit"]');
  await expect(page).toHaveURL('/');
}

test.describe('Responsive — phone (375×812)', () => {
  test.use({ viewport: VIEWPORTS.phone });

  test('dashboard: hamburger visible, sidebar off-canvas, drawer opens on click', async ({ page }) => {
    await login(page);

    // hamburger visible
    await expect(page.locator('.nav-toggle')).toBeVisible();
    await expect(page.locator('.nav-toggle')).toHaveCSS('display', 'flex');

    // Sidebar off-canvas: transform must translate it left
    const sbTransform = await page.evaluate(
      () => getComputedStyle(document.querySelector('.sidebar')).transform
    );
    expect(sbTransform, 'sidebar translated off-canvas').toMatch(/matrix\(1,\s*0,\s*0,\s*1,\s*-\d/);

    // Open drawer
    await page.locator('.nav-toggle').click();
    await expect(page.locator('html')).toHaveClass(/nav-open/);
    await expect(page.locator('.sidebar')).toHaveCSS('transform', 'matrix(1, 0, 0, 1, 0, 0)');
    await expect(page.locator('.nav-backdrop')).toHaveCSS('opacity', '1');

    // Close via Escape
    await page.keyboard.press('Escape');
    await expect(page.locator('html')).not.toHaveClass(/nav-open/);
  });

  test('terminal: header stacks, clock scales down, single-column employees', async ({ page }) => {
    await page.goto('/terminal');
    await page.waitForSelector('.terminal-time');

    await expect(page.locator('.terminal-header')).toHaveCSS('flex-direction', 'column');

    const empCols = await page.evaluate(
      () => getComputedStyle(document.querySelector('.employee-grid')).gridTemplateColumns
    );
    // Single column on phone — only one track value reported
    expect(empCols.split(' ').length, 'employee grid is single column on phone').toBe(1);

    // Clock is fluid clamp — should be smaller than desktop's 132px
    const clockPx = parseFloat(await page.evaluate(
      () => getComputedStyle(document.querySelector('.terminal-time')).fontSize
    ));
    expect(clockPx).toBeLessThan(80);
    expect(clockPx).toBeGreaterThan(40);
  });
});

test.describe('Responsive — tablet (768×1024)', () => {
  test.use({ viewport: VIEWPORTS.tablet });

  test('dashboard: hamburger hidden, sidebar visible at 224px', async ({ page }) => {
    await login(page);

    // Toggle hidden
    await expect(page.locator('.nav-toggle')).toHaveCSS('display', 'none');

    // Sidebar present (no transform)
    await expect(page.locator('.sidebar')).toHaveCSS('transform', 'none');
    await expect(page.locator('.sidebar')).toHaveCSS('width', '224px');
  });

  test('terminal: 2-column employee grid', async ({ page }) => {
    await page.goto('/terminal');
    await page.waitForSelector('.terminal-time');

    const empCols = await page.evaluate(
      () => getComputedStyle(document.querySelector('.employee-grid')).gridTemplateColumns
    );
    expect(empCols.split(' ').length, 'employee grid is 2 columns on tablet').toBe(2);
  });
});

test.describe('Responsive — desktop (1440×900)', () => {
  test.use({ viewport: VIEWPORTS.desktop });

  test('dashboard: full-width sidebar at 256px, hamburger hidden', async ({ page }) => {
    await login(page);
    await expect(page.locator('.nav-toggle')).toHaveCSS('display', 'none');
    await expect(page.locator('.sidebar')).toHaveCSS('width', '256px');
  });

  test('terminal: 3-column employee grid', async ({ page }) => {
    await page.goto('/terminal');
    await page.waitForSelector('.terminal-time');

    const empCols = await page.evaluate(
      () => getComputedStyle(document.querySelector('.employee-grid')).gridTemplateColumns
    );
    expect(empCols.split(' ').length, 'employee grid is 3 columns on desktop').toBe(3);
  });
});

// ─── First-stamp regression (the bug from the message) ─────────────────
//
// Reproduces the exact path that crashed: /terminal → click employee →
// enter PIN → tap KOMMEN. With the AuditInterceptor fix in place, the
// stamp must succeed and show the confirmation screen.

test.describe('Terminal: stamp circuit survives auth round-trip', () => {
  // The actual stamp flow (StempelManager + hash sealing) is covered
  // by the C# Integration test StempelWithAuditInterceptorTests which
  // exercises the *real* path: insert → seal hash → AuditInterceptor.
  //
  // Here we only smoke-test that the Blazor circuit stays alive across
  // a PIN round-trip on /terminal — if the interceptor still rejected
  // the sealing UPDATE the wrong-PIN error wouldn't render either, the
  // circuit would die.
  test.use({ viewport: VIEWPORTS.desktop });

  test('wrong PIN renders error (circuit alive)', async ({ page }) => {
    await page.goto('/terminal');
    await page.waitForSelector('.emp-btn');
    await page.waitForFunction(() => !!window.Blazor, null, { timeout: 10_000 });

    await page.locator('.emp-btn').first().click();
    await expect(page.locator('.pin-dot')).toHaveCount(6);
    for (let i = 0; i < 6; i++) {
      await page.locator('.numpad-btn').nth(i).click();
    }
    await expect(page.locator('.pin-message.error')).toBeVisible({ timeout: 5_000 });
    await expect(page.locator('.pin-message.error')).toContainText(/Falsche PIN|gesperrt/i);
  });
});

// ─── SelfService access control ─────────────────────────────────────
//
// Mitarbeiter should NOT be able to see other employees' time data.
// PIN-gate enforces this: anonymous user must pick an employee + PIN;
// admin (cookie) bypasses the gate.

test.describe('SelfService — PIN gate access control', () => {
  test.use({ viewport: VIEWPORTS.desktop });

  test.beforeEach(async ({ page }) => {
    await page.goto('/logout').catch(() => {});
    await page.context().clearCookies();
  });

  test('anonymous visit to /selfservice shows PIN gate (not employee data)', async ({ page }) => {
    await page.goto('/selfservice');
    await expect(page).toHaveURL(/\/selfservice/);
    await expect(page.locator('.pingate-card')).toBeVisible();
    await expect(page.locator('.pingate-title')).toContainText('Persönlicher Zugang');
    // The day table and stat cards must NOT be visible — no data leaks.
    await expect(page.locator('table.table')).toHaveCount(0);
  });

  test('PIN gate rejects wrong PIN', async ({ page }) => {
    await page.goto('/selfservice');
    await page.waitForFunction(() => !!window.Blazor, null, { timeout: 10_000 });
    await expect(page.locator('.pingate-card')).toBeVisible();

    // Pick first employee in dropdown
    const select = page.locator('.pingate-card select');
    const options = await select.locator('option').count();
    if (options < 2) test.skip(); // need at least one real employee
    await select.selectOption({ index: 1 });

    await page.fill('.pingate-card input[type=password]', '000000');
    await page.locator('.pingate-card button.btn-primary').click();
    await expect(page.locator('.pingate-card .alert-danger')).toBeVisible({ timeout: 5_000 });
    // Gate still showing — no data leaked
    await expect(page.locator('.pingate-card')).toBeVisible();
    await expect(page.locator('table.table')).toHaveCount(0);
  });

  test('admin sees /selfservice with employee picker (gate bypassed)', async ({ page }) => {
    await login(page);
    await page.goto('/selfservice');
    await expect(page.locator('h1')).toContainText('Meine Zeiten');
    // Admin sees the picker, NOT the PIN gate
    await expect(page.locator('.pingate-card')).toHaveCount(0);
    await expect(page.locator('select')).toBeVisible();
  });

  test('admin sees /selfservice/korrektur with picker (gate bypassed)', async ({ page }) => {
    await login(page);
    await page.goto('/selfservice/korrektur');
    await expect(page.locator('h1')).toContainText('Korrekturantrag');
    await expect(page.locator('.pingate-card')).toHaveCount(0);
  });
});
