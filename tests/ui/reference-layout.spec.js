const { test, expect } = require("@playwright/test");

test("reference proportions are preserved at the design canvas size", async ({
  page,
}) => {
  await page.setViewportSize({ width: 1107, height: 1421 });
  await page.goto("/");
  await page.evaluate(async () => {
    await document.fonts.ready;
    await SkinRenderer.ready;
    await SkinRenderer.paint();
  });
  const first = await page.locator(".task").first().boundingBox();
  const second = await page.locator(".task").nth(1).boundingBox();
  const headerButton = await page.locator("#skin").boundingBox();
  // Measured anchors from the approved 1107 × 1421 concept, not the old implementation.
  expect(Math.abs(first.x - 28)).toBeLessThanOrEqual(2);
  expect(Math.abs(first.y - 290)).toBeLessThanOrEqual(2);
  expect(Math.abs(first.height - 280)).toBeLessThanOrEqual(2);
  expect(Math.abs(second.y - 578)).toBeLessThanOrEqual(2);
  expect(Math.abs(headerButton.x - 507)).toBeLessThanOrEqual(2);
  await expect(page.locator("#toolbar")).toBeHidden();
  await expect(page.locator("#tools")).toBeVisible();
  expect(
    await page.evaluate(
      () =>
        document
          .querySelector("#skin-canvas")
          .getContext("2d")
          .getImageData(60, 60, 1, 1).data[3],
    ),
  ).toBe(255);
});

test("custom scrollbar supports keyboard and pointer, and local open shows task details", async ({
  page,
}) => {
  await page.setViewportSize({ width: 420, height: 420 });
  await page.goto("/");
  const scrollbar = page.getByRole("scrollbar");
  await scrollbar.focus();
  await page.keyboard.press("End");
  await expect
    .poll(() => page.locator("#tasklist").evaluate((n) => n.scrollTop))
    .toBeGreaterThan(0);
  await page.keyboard.press("Home");
  await expect
    .poll(() => page.locator("#tasklist").evaluate((n) => n.scrollTop))
    .toBe(0);
  const bounds = await scrollbar.boundingBox();
  await scrollbar.click({
    position: { x: bounds.width / 2, y: bounds.height - 5 },
  });
  await expect
    .poll(() => page.locator("#tasklist").evaluate((n) => n.scrollTop))
    .toBeGreaterThan(0);
  const noLink = page.locator(".task").filter({ hasText: "更新团队任务清单" });
  await noLink.getByRole("button", { name: "打开", exact: true }).click();
  await expect(page.locator("#task-title")).toHaveValue("更新团队任务清单");
});
