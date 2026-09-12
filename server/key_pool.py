"""Official API Free keys only; never retry a translation against another key."""
import json, urllib.request

class DeepLKeyPool:
    def __init__(self, keys):
        if not 1<=len(keys)<=4 or len(set(keys))!=len(keys) or any(not isinstance(k,str) or not k.endswith(':fx') for k in keys):
            raise ValueError('Configure 1–4 distinct official API Free keys')
        self.keys=keys; self.available=[]

    def call(self,key,endpoint,body=None):
        req=urllib.request.Request('https://api-free.deepl.com/v2/'+endpoint,
            data=json.dumps(body).encode() if body is not None else None,
            headers={'Authorization':'DeepL-Auth-Key '+key,'Content-Type':'application/json'})
        with urllib.request.urlopen(req,timeout=12) as response: return json.load(response)

    def __call__(self,endpoint,body=None):
        if endpoint=='usage':
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
            if not self.available: raise RuntimeError('Unable to verify provider quota')
            # Requests are kept on one key; report the largest usable slot rather
            # than imply that a request can span small balances across keys.
            return {'character_count':0,'character_limit':max(item[1] for item in self.available)}
        cost=sum(len(t) for t in body['text'])
        eligible=[item for item in self.available if item[1]>=cost]
        if not eligible: raise RuntimeError('No key can cover this request')
        key,_=max(eligible,key=lambda item:item[1])
        return self.call(key,endpoint,body)
