"""Official API Free keys only; never retry a translation against another key."""
import json, urllib.error, http.client, threading, time

class DeepLKeyPool:
    def __init__(self, keys):
        if not 1<=len(keys)<=4 or len(set(keys))!=len(keys) or any(not isinstance(k,str) or not k.endswith(':fx') for k in keys):
            raise ValueError('Configure 1–4 distinct official API Free keys')
        self.keys=keys; self.available=[]
        self.condition=threading.Condition()
        self.active=set(); self.checked=0; self.ttl=30
        self.connections={}; self.locks={k:threading.Lock() for k in keys}

    def call(self,key,endpoint,body=None):
        with self.locks[key]:
            connection=self.connections.get(key)
            if connection is None:
                connection=http.client.HTTPSConnection('api-free.deepl.com',timeout=12)
                self.connections[key]=connection
            try:
                connection.request('POST' if body is not None else 'GET','/v2/'+endpoint,
                    body=json.dumps(body).encode() if body is not None else None,
                    headers={'Authorization':'DeepL-Auth-Key '+key,'Content-Type':'application/json'})
                response=connection.getresponse(); data=response.read()
                if response.status!=200:
                    raise urllib.error.HTTPError('https://api-free.deepl.com/v2/'+endpoint,response.status,'DeepL request failed',{},None)
                return json.loads(data)
            except Exception:
                connection.close(); self.connections.pop(key,None)
                raise  # Never resend a potentially billed translation.

    def refresh(self):
        # Condition held by caller. Sampling waits until all reservations settle.
        while self.active: self.condition.wait()
        if self.available and time.monotonic()-self.checked<self.ttl: return
        if True:
            from concurrent.futures import ThreadPoolExecutor
            def check(key):
                try:
                    result=self.call(key,'usage')
                    used,limit=result['character_count'],result['character_limit']
                    if type(used)!=int or type(limit)!=int or used<0 or limit<0: return None
                    return (key,max(0,limit-used))
                except Exception: return None
            with ThreadPoolExecutor(max_workers=len(self.keys)) as workers:
                self.available=[item for item in workers.map(check,self.keys) if item is not None]
            self.checked=time.monotonic()
            if not self.available: raise RuntimeError('Unable to verify provider quota')

    def __call__(self,endpoint,body=None):
        with self.condition:
            if not self.available or time.monotonic()-self.checked>=self.ttl: self.refresh()
            if endpoint=='usage':
                return {'character_count':0,'character_limit':max(item[1] for item in self.available)}
            cost=sum(len(t) for t in body['text'])
            eligible=[item for item in self.available if item[1]>=cost and item[0] not in self.active]
            if not eligible: raise RuntimeError('No available key can cover this request')
            key,_=max(eligible,key=lambda item:item[1])
            self.available=[(k,v-cost if k==key else v) for k,v in self.available]
            self.active.add(key)
        try:
            return self.call(key,endpoint,body)
        except Exception:
            with self.condition: self.checked=0
            raise
        finally:
            with self.condition:
                self.active.remove(key); self.condition.notify_all()
