const {test,expect}=require('@playwright/test');
test.beforeEach(async({page})=>{await page.goto('/');await page.evaluate(()=>document.fonts.ready);});
test('both skins use the embedded Chinese pixel font and persist selection',async({page})=>{
 await expect(page.locator('.task')).toHaveCount(3);
 expect(await page.evaluate(()=>document.fonts.check('24px Pixel'))).toBe(true);
 await page.getByRole('button',{name:'选择皮肤'}).click();await page.locator('.skin-option[data-skin=slate]').click();
 await expect(page.locator('body')).toHaveAttribute('data-skin','slate');await page.reload();await expect(page.locator('body')).toHaveAttribute('data-skin','slate');
 await page.screenshot({path:'test-results/slate.png'});
 await page.getByRole('button',{name:'选择皮肤'}).click();await page.locator('.skin-option[data-skin=oak]').click();await page.screenshot({path:'test-results/oak.png'});
});
test('create, edit, complete, restore, delete and reload local tasks',async({page})=>{
 await page.locator('#add').click();await page.locator('#task-title').fill('端到端测试任务');await page.locator('#task-due').fill('2026-12-31');await page.getByRole('button',{name:'保存任务'}).click();
 const card=page.locator('.task').filter({hasText:'端到端测试任务'});await expect(card).toBeVisible();await card.getByRole('button',{name:'编辑',exact:true}).click();await page.locator('#task-title').fill('修改后的任务');await page.getByRole('button',{name:'保存任务'}).click();
 const edited=page.locator('.task').filter({hasText:'修改后的任务'});await edited.getByRole('button',{name:'完成',exact:true}).click();await page.getByRole('button',{name:'确认',exact:true}).click();await expect(edited).toHaveCount(0);
 await page.getByRole('button',{name:'已完成',exact:true}).click();await expect(edited).toBeVisible();await edited.getByRole('button',{name:'恢复',exact:true}).click();await page.getByRole('button',{name:'确认',exact:true}).click();await page.getByRole('button',{name:'待办',exact:true}).click();await expect(edited).toBeVisible();
 await page.reload();await expect(edited).toBeVisible();await edited.getByRole('button',{name:'删除',exact:true}).click();await page.getByRole('button',{name:'确认',exact:true}).click();await expect(edited).toHaveCount(0);
});
test('untrusted titles are text and non-web URLs cannot be submitted',async({page})=>{
 await page.locator('#add').click();await page.locator('#task-title').fill('<img src=x onerror="window.pwned=true">');await page.locator('#task-url').fill('javascript:alert(1)');await page.getByRole('button',{name:'保存任务'}).click();await expect(page.locator('#toast')).toContainText('只支持');
 await page.locator('#task-url').fill('https://example.com');await page.getByRole('button',{name:'保存任务'}).click();await expect(page.locator('.task-title').filter({hasText:'<img'})).toBeVisible();expect(await page.evaluate(()=>window.pwned)).toBeUndefined();await expect(page.locator('.task img')).toHaveCount(0);
});
for(const width of [420,680,900])test(`layout remains usable at ${width}px`,async({page})=>{
 await page.setViewportSize({width,height:740});await page.locator('#add').click();await page.locator('#task-title').fill('很长的中文任务说明'.repeat(20));await page.getByRole('button',{name:'保存任务'}).click();
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
 const last=page.locator('.task').last();await last.scrollIntoViewIfNeeded();await last.getByRole('button',{name:'完成',exact:true}).scrollIntoViewIfNeeded();await expect(last.getByRole('button',{name:'完成',exact:true})).toBeInViewport();
 await page.locator('#settings').click();await expect(page.locator('#settings-dialog')).toBeInViewport();await page.screenshot({path:`test-results/settings-${width}.png`});
});
test('opacity persists, settings switch, empty state and fold',async({page})=>{
 await page.locator('#opacity').fill('64');await page.locator('#opacity').dispatchEvent('change');await page.reload();await expect(page.locator('#opval')).toHaveText('64%');
 await page.getByRole('button',{name:'已完成',exact:true}).click();await expect(page.locator('.empty')).toContainText('还没有');
 await page.locator('#settings').click();await page.locator('#mode').selectOption('larkcli');await expect(page.locator('#cli-options')).toBeVisible();await page.locator('#topmost').check();await page.getByRole('button',{name:'保存设置'}).click();await page.locator('#settings').click();await expect(page.locator('#mode')).toHaveValue('larkcli');await expect(page.locator('#topmost')).toBeChecked();await page.getByRole('button',{name:'关闭设置'}).click();
 await page.locator('#fold').click();await expect(page.locator('#tasklist')).toBeHidden();await page.locator('#fold').click();await expect(page.locator('#tasklist')).toBeVisible();
});
