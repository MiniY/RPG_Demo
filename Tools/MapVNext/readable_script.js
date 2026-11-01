// Only original PNGs and their matching recorded frame rows are displayed.
document.querySelectorAll('.motion-player').forEach(p=>{
 const id=p.dataset.case,mode=p.querySelector('.mode'),slider=p.querySelector('.scrub'),frame=p.querySelector('.frame'),label=p.querySelector('.telemetry'),link=p.querySelector('.original'),button=p.querySelector('.play');
 let n=0,playing=false,timer=null;
 const list=()=>frameEvidence[id][mode.value];
 function show(){const xs=list(),r=xs[n];slider.max=xs.length-1;slider.value=n;frame.src='../Media/r3d/'+r.screenshot;link.href=frame.src;
 label.textContent=`${id}  采样帧 ${n+1}/${xs.length}\n真实按键 ${r.keys}  PlayerAction.MoveInput (${r.moveX}, ${r.moveY})\n脚底位置 (${r.footX}, ${r.footY})  物理时间 ${r.physicsTime}  游戏时间 ${r.gameTime}\n原 Frame ${r.frame}  Generation ${r.generation}\nBuild ${r.identity}\nUTC ${r.utc}`;
 }
 function stop(){playing=false;clearTimeout(timer);button.textContent='播放采样回放'}
 function tick(){if(!playing)return;const xs=list(),next=(n+1)%xs.length,delay=next?Math.max(20,(+xs[next].gameTime- +xs[n].gameTime)*1000):500;timer=setTimeout(()=>{n=next;show();tick()},Math.min(delay,3000))}
 button.onclick=()=>{if(playing)stop();else{playing=true;button.textContent='暂停采样回放';tick()}};
 slider.oninput=()=>{stop();n=+slider.value;show()};mode.onchange=()=>{stop();n=0;show()};show();
});
const query=document.getElementById('filter'),side=document.getElementById('side-filter'),context=document.getElementById('context-filter');
function filterCases(){let count=0;const q=query.value.trim().toLowerCase();document.querySelectorAll('#cases .sample').forEach(x=>{x.hidden=!!(q&&!x.dataset.search.toLowerCase().includes(q)||side.value&&x.dataset.side!==side.value||context.value&&x.dataset.context!==context.value);if(!x.hidden)count++});document.getElementById('filter-count').textContent=`显示 ${count} / 72 个可玩场景。筛选只影响网页，不会跳转游戏。`}
[query,side,context].forEach(x=>x.addEventListener('input',filterCases));document.getElementById('clear-filter').onclick=()=>{query.value=side.value=context.value='';filterCases()};filterCases();
document.addEventListener('click',event=>{const a=event.target.closest('a[href^="#"]');if(!a)return;const target=document.getElementById(a.getAttribute('href').slice(1));if(target&&target.classList.contains('sample')){query.value=side.value=context.value='';filterCases()}});
document.querySelectorAll('.negative-slots').forEach(p=>{(negativeEvidence[p.dataset.case]||[]).forEach(path=>{const a=document.createElement('a'),img=document.createElement('img');a.href='../'+path;a.target='_blank';img.src=a.href;img.loading='lazy';img.alt='原 r3d 纯横移反例采样截图；完整判据见本例轨迹';a.append(img);p.append(a)})});
document.getElementById('copy-feedback').onclick=async()=>{const box=document.querySelector('textarea');box.focus();box.select();let success=false;try{await navigator.clipboard.writeText(box.value);success=true}catch(e){}document.getElementById('copy-status').textContent=success?' 已复制。':' 已选中，请按 Ctrl+C 复制。'};
