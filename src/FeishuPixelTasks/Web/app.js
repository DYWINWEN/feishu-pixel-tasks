'use strict';
const $ = id => document.getElementById(id);
const native = Boolean(window.chrome?.webview);
const defaults = {skin:'oak',opacity:100,topMost:false,mode:'local',cliPath:'lark-cli',autoSync:false};
let state = {tasks:[],pending:[],settings:{...defaults},lastSync:'',notice:'本地模式'}, busy=false, hasCredentials=false, filter='todo', toastTimer;
const demos = [{guid:'demo-1',summary:'整理项目阶段性成果',due:'2026-12-15',url:'https://www.feishu.cn',status:'todo',source:'local'},{guid:'demo-2',summary:'更新团队任务清单',due:'2026-12-20',url:'',status:'todo',source:'local'},{guid:'demo-3',summary:'汇总本月工作进展，并安排下一阶段的协作计划。',due:'',url:'',status:'todo',source:'local'}];
function notify(message){$('toast').textContent=message;$('toast').hidden=false;clearTimeout(toastTimer);toastTimer=setTimeout(()=>$('toast').hidden=true,6500);}
function send(type, fields={}){if(native)window.chrome.webview.postMessage({type,...fields});else preview(type,fields);}
function saveSettings(next){send('settings',{settings:{...state.settings,...next}});}
function el(tag, className, text){const n=document.createElement(tag);if(className)n.className=className;if(text!==undefined)n.textContent=text;return n;}
function button(text, className, action){const b=el('button',className,text);b.type='button';b.onclick=action;return b;}
async function confirmAction(text, action){$('confirm-title').textContent='确认操作';$('confirm-text').textContent=text;const d=$('confirm-dialog');d.returnValue='';d.onclose=()=>{if(d.returnValue==='confirm')action();};d.showModal();}
function editTask(task){$('task-heading').textContent=task?'编辑本地任务':'新建本地任务';$('task-id').value=task?.guid||'';$('task-title').value=task?.summary||'';$('task-due').value=task?.due||'';$('task-url').value=task?.url||'';$('task-dialog').showModal();$('task-title').focus();}
function render(){
 document.body.dataset.skin=state.settings.skin;
 document.body.classList.toggle('busy',busy);
 $('opacity').value=state.settings.opacity;$('opval').textContent=state.settings.opacity+'%';$('opacity').disabled=busy;
 $('refresh').textContent=busy?'同步中':'刷新';$('refresh').disabled=busy;
 for(const id of ['add','settings','skin'])$(id).disabled=busy;
 document.querySelectorAll('[data-filter]').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.filter===filter)));
 const list=$('tasklist'),scroll=list.scrollTop;list.replaceChildren();
 const tasks=state.tasks.filter(t=>(t.status==='done')===(filter==='done')).slice().sort((a,b)=>(a.due||'9999').localeCompare(b.due||'9999'));
 for(const t of tasks){
  const p=state.pending.find(p=>p.guid===t.guid), card=el('article','task'+(p?' pending':'')+(t.status==='done'?' done':''));card.dataset.guid=t.guid;
  const row=el('div','task-row');row.append(el('h2','task-title',t.summary),el('span','task-due'+(!t.due?' none':''),t.due?t.due+' 截止':'未设截止'));card.append(row);
  const actions=el('div','task-actions');
  const done=button(p?'待重试':t.status==='done'?'恢复':'完成','green',()=>{
   if(p){send('refresh');return;}
   confirmAction(t.status==='done'?'将这条任务恢复为待办？':t.source==='feishu'?'将此任务标记完成并同步到飞书？':'将这条本地任务标记为已完成？',()=>send('complete',{guid:t.guid}));
  });done.disabled=busy;actions.append(done);
  const open=button('打开','',()=>send('open',{guid:t.guid}));open.disabled=!t.url;open.title=t.url?'在默认浏览器中打开':'未设置任务链接';actions.append(open);
  if(t.source==='local'){
   const edit=button('编辑','minor',()=>editTask(t));edit.disabled=busy;actions.append(edit);
   const del=button('删除','minor',()=>confirmAction('删除这条本地任务？',()=>send('delete-task',{guid:t.guid})));del.disabled=busy;actions.append(del);
  }else actions.append(el('span','source','飞书'));
  card.append(actions);
  if(p){const line=el('p','pending-text',p.error?`${p.error} · 已尝试 ${p.attempts} 次`:'等待飞书确认，任务仍保留');const cancel=button('取消重试','minor cancel',()=>send('cancel-pending',{guid:t.guid}));cancel.disabled=busy;line.append(' ',cancel);card.append(line);}
  list.append(card);
 }
 if(!tasks.length){const empty=el('div','empty');empty.append(el('div','empty-icon',filter==='done'?'✓':'◇'),el('p','',filter==='done'?'还没有已完成的任务':'当前没有待办任务'),el('small','',filter==='done'?'完成的本地任务会保留在这里':'新建本地任务，或在设置中连接飞书'));if(filter==='todo')empty.append(button('+ 新建任务','green',()=>editTask()));list.append(empty);}
 list.scrollTop=scroll;
 const count=state.tasks.filter(t=>t.status!=='done').length;
 const time=state.lastSync?new Date(state.lastSync).toLocaleTimeString('zh-CN',{hour:'2-digit',minute:'2-digit'}):'';
 const status=`待办 ${count} 条 · ${busy?'正在同步…':state.notice?.startsWith('同步失败')?'同步失败 · 缓存已保留':state.pending.length?state.pending.length+' 个动作待重试':state.settings.mode==='local'?'本地模式':time?'同步于 '+time:'尚未同步'}`;
 $('status').textContent=(native?'':'预览 · ')+status;$('status').title=state.notice||status;
 if($('settings-dialog').open){$('credential-state').textContent=hasCredentials?'已保存加密凭据（有效期以飞书为准）':'未保存凭据，可使用已登录 CLI 的环境';$('cli-path').value=state.settings.cliPath;}
 document.fonts.ready.then(()=>send('rendered'));
}
function receive(data){if(data.type==='state'){state=data.state;state.settings={...defaults,...state.settings};busy=data.busy;hasCredentials=data.hasCredentials;render();}else if(data.type==='error')notify(data.message);}
if(native)window.chrome.webview.addEventListener('message',e=>receive(e.data));
$('skin').onclick=()=>$('skin-dialog').showModal();
document.querySelectorAll('.skin-option').forEach(b=>b.onclick=()=>{saveSettings({skin:b.dataset.skin});$('skin-dialog').close();});
$('refresh').onclick=()=>send('refresh');$('add').onclick=()=>editTask();
document.querySelectorAll('[data-filter]').forEach(b=>b.onclick=()=>{filter=b.dataset.filter;render();});
$('opacity').oninput=e=>{$('opval').textContent=e.target.value+'%';};
$('opacity').onchange=e=>saveSettings({opacity:Number(e.target.value)});
$('close').onclick=()=>send('close');$('minimize').onclick=()=>send('minimize');
$('fold').onclick=()=>{document.body.classList.toggle('folded');$('fold').textContent=document.body.classList.contains('folded')?'+':'−';send('fold');};
$('resize').onmousedown=()=>send('resize');
let dragging=false;
$('titlebar').onpointerdown=e=>{if(e.target.closest('button')||e.button!==0)return;dragging=true;$('titlebar').setPointerCapture(e.pointerId);send('drag-start');};
$('titlebar').onpointermove=()=>{if(dragging)send('drag-move');};
$('titlebar').onpointerup=()=>{dragging=false;send('drag-end');};
$('titlebar').onlostpointercapture=()=>{dragging=false;send('drag-end');};
document.querySelectorAll('[data-dismiss]').forEach(b=>b.onclick=()=>b.closest('dialog').close());
$('task-form').onsubmit=e=>{e.preventDefault();const url=$('task-url').value.trim();if(url&&!/^https?:\/\//i.test(url)){notify('任务链接只支持 http/https。');return;}const title=$('task-title').value.trim();if(!title){notify('请填写任务内容。');return;}send('save-task',{task:{guid:$('task-id').value,summary:title,due:$('task-due').value,url}});$('task-dialog').close();};
function connectionFields(){$('cli-options').hidden=$('mode').value!=='larkcli';}
$('mode').onchange=connectionFields;
$('settings').onclick=()=>{$('mode').value=state.settings.mode;$('cli-path').value=state.settings.cliPath;$('topmost').checked=state.settings.topMost;$('auto-sync').checked=state.settings.autoSync;$('credential-state').textContent=hasCredentials?'已保存加密凭据（有效期以飞书为准）':'未保存凭据，可使用已登录 CLI 的环境';connectionFields();$('settings-dialog').showModal();};
$('settings-form').onsubmit=e=>{e.preventDefault();saveSettings({mode:$('mode').value,cliPath:$('cli-path').value.trim()||'lark-cli',topMost:$('topmost').checked,autoSync:$('auto-sync').checked});$('settings-dialog').close();};
for(const id of ['choose-cli','import-credentials','import','export'])$(id).onclick=()=>send(id);
$('clear-credentials').onclick=()=>confirmAction('清除本机保存的加密凭据？任务不受影响。',()=>send('clear-credentials'));
document.onkeydown=e=>{if(e.key==='F5'){e.preventDefault();send('refresh');}if(e.ctrlKey&&e.key==='n'&&!document.querySelector('dialog[open]')){e.preventDefault();editTask();}};
// Browser preview is explicitly local. It cannot execute CLI, access credentials, or claim a real sync.
function preview(type,m){
 switch(type){
  case 'ready':try{state=JSON.parse(localStorage.getItem('pixel-preview-state'))||{...state,tasks:structuredClone(demos)};}catch{state.tasks=structuredClone(demos);}render();return;
  case 'settings':state.settings=m.settings;break;
  case 'save-task':{const old=state.tasks.find(t=>t.guid===m.task.guid);state.tasks=state.tasks.filter(t=>t.guid!==m.task.guid);state.tasks.push({...m.task,guid:old?.guid||crypto.randomUUID(),status:old?.status||'todo',source:'local'});break;}
  case 'complete':{const task=state.tasks.find(t=>t.guid===m.guid);if(task.source==='feishu'){notify('预览不发送飞书操作，请使用 Windows 应用。');return;}task.status=task.status==='done'?'todo':'done';break;}
  case 'delete-task':state.tasks=state.tasks.filter(t=>t.guid!==m.guid);break;
  case 'open':{const url=state.tasks.find(t=>t.guid===m.guid)?.url;if(/^https?:\/\//i.test(url))window.open(url,'_blank','noopener');return;}
  case 'refresh':notify('浏览器预览只保存本地任务；飞书同步请使用 Windows 应用。');return;
  case 'import':case 'export':case 'choose-cli':case 'import-credentials':case 'clear-credentials':notify('此功能在 Windows 桌面应用中使用。');return;
  case 'close':notify('请关闭浏览器标签页。');return;
  default:return;
 }
 try{localStorage.setItem('pixel-preview-state',JSON.stringify(state));}catch{notify('浏览器存储不可用，当前修改未持久保存。');}render();
}
send('ready');
