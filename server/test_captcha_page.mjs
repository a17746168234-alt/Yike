import {readFileSync} from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import assert from 'node:assert/strict';
const html=readFileSync(new URL('./captcha.html',import.meta.url),'utf8');
const scripts=[...html.matchAll(/<script>([\s\S]*?)<\/script>/g)].map(m=>m[1]);
function page({hash='#'+'a'.repeat(43),fetch=async()=>({ok:true,json:async()=>({})})}={}){
 const elements=new Map(),timers=new Map();let next=0,options,resetCount=0;
 const get=id=>{if(!elements.has(id))elements.set(id,{textContent:'',dataset:{},disabled:false,showModal(){this.open=true},close(){this.open=false}});return elements.get(id)};
 const ctx=vm.createContext({window:{},document:{getElementById:get,documentElement:{classList:{add(){}}}},location:{hash,reload(){}},fetch,AbortController,
 setTimeout(fn,ms){timers.set(++next,{fn,ms});return next},clearTimeout(id){timers.delete(id)},
 turnstile:{render(_,o){options=o;return 'widget'},reset(){resetCount++}}});
 for(const code of scripts)vm.runInContext(code,ctx);
 vm.runInContext('ready()',ctx);
 return {get,ctx,timers,get options(){return options},get resets(){return resetCount}};
}
test('a link without an app ticket cannot report verification success',()=>{
 const p=page({hash:''});assert.equal(p.options,undefined);assert.match(p.get('status').textContent,/开始验证/);
});
test('success waits for server validation and cannot be overwritten by expiry or slow warning',async()=>{
 let complete;const p=page({fetch:()=>new Promise(resolve=>complete=resolve)});
 const result=p.options.callback('test-token');assert.match(p.get('status').textContent,/正在确认/);
 complete({ok:true,json:async()=>({})});await result;
 assert.equal(p.get('status').dataset.kind,'success');assert.equal(p.timers.size,0);
 p.options['expired-callback']();p.options['error-callback']('600010');
 assert.equal(p.get('status').dataset.kind,'success');
});
test('provider errors stop retry loops and expose the code with manual recovery',()=>{
 const p=page();assert.equal(p.options.retry,'never');assert.equal(p.options['feedback-enabled'],false);
 p.options['error-callback']('600010');assert.match(p.get('status').textContent,/600010/);assert.equal(p.resets,0);
 p.get('retry').onclick();assert.equal(p.resets,1);assert.match(p.get('status').textContent,/正在重新验证/);
});
test('a rejected server token never becomes verified',async()=>{
 const p=page({fetch:async()=>({ok:false,json:async()=>({message:'验证链接已过期'})})});
 await p.options.callback('test-token');assert.equal(p.get('status').dataset.kind,'error');assert.equal(p.get('retry').disabled,false);
});
test('slow loading shows recovery and troubleshooting opens its own dialog',()=>{
 const p=page();for(const {fn,ms} of p.timers.values())if(ms===25000)fn();
 assert.match(p.get('status').textContent,/耗时较长/);
 p.get('help').onclick();assert.equal(p.get('troubleshooting').open,true);
 p.get('close-help').onclick();assert.equal(p.get('troubleshooting').open,false);
});
test('a late response from an earlier attempt cannot overwrite a new attempt',async()=>{
 let complete;const p=page({fetch:()=>new Promise(resolve=>complete=resolve)});
 const previous=p.options.callback('old-token');p.options['error-callback']('network');p.get('retry').onclick();
 complete({ok:true,json:async()=>({})});await previous;
 assert.match(p.get('status').textContent,/正在重新验证/);assert.notEqual(p.get('status').dataset.kind,'success');
});
