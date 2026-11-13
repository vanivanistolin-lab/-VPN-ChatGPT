const $=id=>document.getElementById(id);
let busy=false;
async function update(){
 try{
  const s=await chrome.runtime.sendMessage({type:'popupStatus'});
  if(!s)throw new Error('No status');
  $('tabs').textContent=String(s.tabs);
  $('updated').textContent=s.lastReload?new Date(s.lastReload).toLocaleTimeString('ru-RU',{hour:'2-digit',minute:'2-digit',second:'2-digit'}):'Ещё не было';
  $('status').textContent=s.helperError?'Нет связи с помощником':s.connected?'VPN подключён':'Ожидание VPN';
  $('dot').className='dot '+(s.helperError?'error':s.connected?'on':'off');
  $('detail').textContent=s.helperError?'Помощник недоступен. Автообновление временно не работает.':s.connected?'Вкладки ChatGPT обновляются после подключения.':'После подключения Happ обновим вкладки ChatGPT.';
  $('refresh').disabled=busy||s.tabs===0;
 }catch{
  $('status').textContent='Нет связи с расширением';$('dot').className='dot error';$('detail').textContent='Перезагрузи расширение на странице chrome://extensions.';
 }
}
$('refresh').addEventListener('click',async()=>{
 busy=true;$('refresh').disabled=true;$('feedback').textContent='Обновляем…';
 try{const result=await chrome.runtime.sendMessage({type:'popupRefresh'});if(result.error)throw new Error(result.error);$('feedback').textContent=result.count?'Обновлено вкладок: '+result.count:'Нет открытых вкладок ChatGPT';}
 catch{$('feedback').textContent='Не удалось обновить страницу';}
 finally{busy=false;update();}
});
update();setInterval(update,1000);