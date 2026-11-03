const {test,expect}=require('@playwright/test');
test('desktop bridge waits for host confirmation and displays pending/error state',async({page})=>{
 await page.addInitScript(()=>{
   window.messages=[];
   window.chrome={webview:{postMessage:m=>window.messages.push(m),addEventListener:(_,handler)=>window.deliver=data=>handler({data})}};
 });
 await page.goto('/');
 const state={tasks:[{guid:'remote-1',summary:'飞书任务确认测试',due:'',url:'https://example.com',status:'todo',source:'feishu'}],pending:[],settings:{skin:'slate',opacity:92,mode:'larkcli',cliPath:'lark-cli'},lastSync:'',notice:''};
 await page.evaluate(state=>window.deliver({type:'state',state,busy:false}),state);
 await page.getByRole('button',{name:'完成',exact:true}).click();await page.getByRole('button',{name:'确认',exact:true}).click();
 await expect(page.locator('.task')).toHaveCount(1);await expect.poll(()=>page.evaluate(()=>window.messages.some(m=>m.type==='complete'&&m.guid==='remote-1'))).toBe(true);
 state.pending=[{guid:'remote-1',attempts:1,error:'完成失败'}];state.notice='同步失败 · 已保留上次任务与待重试动作';
 await page.evaluate(state=>window.deliver({type:'state',state,busy:false}),state);
 await expect(page.locator('.pending-text')).toContainText('已尝试 1 次');await expect(page.locator('#status')).toContainText('同步失败');
 await page.evaluate(state=>window.deliver({type:'state',state,busy:true}),state);await expect(page.locator('#refresh')).toBeDisabled();await expect(page.getByRole('button',{name:'待重试',exact:true})).toBeDisabled();
});
