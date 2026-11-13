let port=null, running=false, retry=null; let requestAt=0;
let connected=false, readyAt=0, reloaded=new Set();
let helperError=true, lastReload=0;
function show(text,color){chrome.action.setBadgeText({text});chrome.action.setBadgeBackgroundColor({color});}
function connect(){
 if(port)return;
 port=chrome.runtime.connectNative('com.chatgpt_happ.status');
 port.onMessage.addListener(async state=>{
  running=false; helperError=false;
  if(!state.connected){connected=false;readyAt=0;reloaded.clear();show('OFF','#666666');return;}
  if(!connected){connected=true;readyAt=Date.now()+3500;reloaded.clear();}
  show('VPN','#16803c');
  if(Date.now()<readyAt)return;
  const tabs=await chrome.tabs.query({url:['https://chatgpt.com/*','https://chat.openai.com/*']});
  for(const tab of tabs){
   if(reloaded.has(tab.id))continue;
   reloaded.add(tab.id);
   try {await chrome.tabs.reload(tab.id);lastReload=Date.now();await chrome.storage.local.set({lastReload});}
   catch(e){reloaded.delete(tab.id);console.error(e);}
  }
 });
 port.onDisconnect.addListener(()=>{
  console.warn(chrome.runtime.lastError?.message||'Disconnected');
  port=null;running=false;helperError=true;show('ERR','#bd2525');
  clearTimeout(retry);retry=setTimeout(connect,3000);
 });
 poll();
}
function poll(){if(running&&Date.now()-requestAt>5000){try{port?.disconnect();}catch{}port=null;running=false;}if(!port){connect();return;}if(running)return;running=true;requestAt=Date.now();try{port.postMessage({status:true});}catch{running=false;port=null;}}
chrome.runtime.onInstalled.addListener(()=>{chrome.alarms.create('status',{periodInMinutes:0.5});connect();});
chrome.runtime.onStartup.addListener(()=>{chrome.alarms.create('status',{periodInMinutes:0.5});running=false;connect();});
chrome.alarms.onAlarm.addListener(poll);
chrome.tabs.onRemoved.addListener(id=>reloaded.delete(id));
chrome.tabs.onUpdated.addListener((_id,change)=>{if(change.url)poll();});

setInterval(poll,1000);
connect();
chrome.storage.local.get('lastReload').then(s=>{lastReload=Math.max(lastReload,s.lastReload||0);});
chrome.runtime.onMessage.addListener((message,_sender,respond)=>{
 if(message.type!=='popupStatus'&&message.type!=='popupRefresh')return;
 (async()=>{
  const tabs=await chrome.tabs.query({url:['https://chatgpt.com/*','https://chat.openai.com/*']});
  if(message.type==='popupStatus'){respond({connected,helperError,tabs:tabs.length,lastReload});return;}
  let count=0;
  for(const tab of tabs){try{await chrome.tabs.reload(tab.id);count++;}catch(e){console.error(e);}}
  if(count){lastReload=Date.now();await chrome.storage.local.set({lastReload});}
  respond({count});
 })().catch(e=>respond({error:String(e)}));
 return true;
});
chrome.alarms.create('status',{periodInMinutes:0.5});
