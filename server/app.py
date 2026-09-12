"""Yike account and shared DeepL trial service. Python 3.11+, standard library only."""
import contextlib, hashlib, hmac, http.server, json, os, re, secrets, sqlite3, threading, time, urllib.request, urllib.error, uuid
from pathlib import Path
from email_auth import EmailAuth
from key_pool import DeepLKeyPool

class APIError(Exception):
    def __init__(self, status, code, message):
        self.status, self.code, self.message = status, code, message

class Service:
    def __init__(self, path, pepper, key='', enabled=False, gift=200000, pool_limit=4000000, upstream=None, mailer=None,
                 update_path='/opt/yike-trial/update-macos.json'):
        self.path, self.pepper, self.key = str(path), pepper.encode(), key
        self.enabled, self.gift, self.pool_limit = enabled, gift, pool_limit
        self.update_path = Path(update_path)
        self.upstream = upstream or self.deepl
        self.translation_lock = threading.BoundedSemaphore(2)
        self.auth_slots = threading.BoundedSemaphore(2)
        Path(path).parent.mkdir(parents=True, exist_ok=True)
        with self.db() as db:
            db.executescript('''
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS users(id TEXT PRIMARY KEY, name TEXT UNIQUE NOT NULL, salt TEXT NOT NULL, password TEXT NOT NULL, credit INTEGER NOT NULL, spent INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS sessions(hash TEXT PRIMARY KEY, user_id TEXT NOT NULL, expires INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS limits(bucket TEXT NOT NULL, stamp INTEGER NOT NULL);
            CREATE INDEX IF NOT EXISTS limits_bucket ON limits(bucket,stamp);
            CREATE TABLE IF NOT EXISTS pool(period TEXT PRIMARY KEY, spent INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS requests(user_id TEXT NOT NULL, request_id TEXT NOT NULL, fingerprint TEXT NOT NULL, status TEXT NOT NULL, result TEXT, cost INTEGER NOT NULL, created INTEGER NOT NULL, PRIMARY KEY(user_id,request_id));
            ''')
            db.execute('BEGIN IMMEDIATE')
            for row in db.execute("SELECT user_id,SUM(cost) AS cost FROM requests WHERE status='pending' GROUP BY user_id").fetchall():
                db.execute('UPDATE users SET spent=MAX(0,spent-?) WHERE id=?',(row['cost'],row['user_id']))
            db.execute("UPDATE requests SET status='failed' WHERE status='pending'")
            db.commit()
        os.chmod(path, 0o600)
        self.email_auth=EmailAuth(self, APIError, mailer=mailer)

    @contextlib.contextmanager
    def db(self):
        db = sqlite3.connect(self.path, timeout=10, isolation_level=None)
        db.row_factory = sqlite3.Row
        try: yield db
        finally: db.close()

    def digest(self, text): return hmac.new(self.pepper, text.encode(), hashlib.sha256).hexdigest()
    def password_hash(self, password, salt):
        return hashlib.scrypt(password.encode(), salt=bytes.fromhex(salt), n=16384, r=8, p=1, dklen=32).hex()

    def rate(self, bucket, maximum, seconds):
        now = int(time.time())
        with self.db() as db:
            db.execute('BEGIN IMMEDIATE')
            db.execute('DELETE FROM limits WHERE stamp < ?', (now-32*86400,))
            count = db.execute('SELECT COUNT(*) FROM limits WHERE bucket=? AND stamp>?', (bucket, now-seconds)).fetchone()[0]
            if count >= maximum:
                db.rollback(); raise APIError(429,'rate_limit','操作过于频繁，请稍后再试。')
            db.execute('INSERT INTO limits VALUES (?,?)',(bucket,now)); db.commit()

    def account(self, user):
        return {'username':user['email'] or user['name'], 'email':user['email'], 'granted':user['credit'], 'used':user['spent'], 'remaining':max(0,user['credit']-user['spent']), 'grant_policy':'once'}

    def authenticate(self, token):
        if not isinstance(token,str) or len(token)>128: raise APIError(401,'login_required','请先登录 Yike 账号领取体验额度。')
        with self.db() as db:
            user=db.execute('SELECT users.* FROM sessions JOIN users ON sessions.user_id=users.id WHERE sessions.hash=? AND sessions.expires>?',(self.digest(token),int(time.time()))).fetchone()
        if not user: raise APIError(401,'login_required','登录已失效，请重新登录。')
        return user

    def config(self):
        with self.db() as db:
            pool=db.execute("SELECT spent FROM pool WHERE period='total'").fetchone()
        return {'pool_limit':self.pool_limit,'pool_remaining':max(0,self.pool_limit-(pool[0] if pool else 0)), 'enabled':self.enabled and bool(self.key), 'gift':self.gift, 'grant_policy':'once', 'message':'注册后一次领取体验字符，使用受公共池剩余额度限制。' if self.enabled and self.key else '公共翻译尚未开启，可先注册账号；Apple 翻译与自填密钥不受影响。'}

    def update_config(self):
        try:
            value=json.loads(self.update_path.read_text())
            required=('version','build','title','notes','download_url','sha256')
            if any(key not in value for key in required): raise ValueError()
            if not isinstance(value['build'],int) or value['build']<1: raise ValueError()
            if not value['download_url'].startswith('https://github.com/a17746168234-alt/Yike/releases/'): raise ValueError()
            if not re.fullmatch(r'[0-9a-f]{64}',value['sha256']): raise ValueError()
            return {key:value[key] for key in required}
        except Exception:
            raise APIError(503,'update_unavailable','暂时无法获取版本信息，请稍后重试。')

    def auth(self, action, body, ip):
        self.rate('auth:'+self.digest(ip),20,900)
        name, password=body.get('username'), body.get('password')
        if not isinstance(name,str) or not re.fullmatch(r'[A-Za-z0-9_]{4,24}',name) or not isinstance(password,str) or not 10<=len(password)<=128:
            raise APIError(400,'credentials_format','账号需为 4–24 位英文、数字或下划线，密码需为 10–128 位。')
        name=name.lower()
        self.rate('account:'+self.digest(name),10,900)
        if not self.auth_slots.acquire(blocking=False): raise APIError(429,'busy','登录服务繁忙，请稍后再试。')
        try:
            with self.db() as db:
                if action=='register':
                    self.rate('register:'+self.digest(ip),3,86400)
                    salt=secrets.token_hex(16)
                    hashed=self.password_hash(password,salt)
                    db.execute('BEGIN IMMEDIATE')
                    if db.execute('SELECT COUNT(*) FROM users').fetchone()[0]>=10000:
                        db.rollback(); raise APIError(503,'registration_paused','本轮体验注册已满，请稍后再试。')
                    try: db.execute('INSERT INTO users(id,name,salt,password,credit,spent) VALUES (?,?,?,?,?,0)',(str(uuid.uuid4()),name,salt,hashed,self.gift))
                    except sqlite3.IntegrityError:
                        db.rollback(); raise APIError(409,'username_exists','账号名已被使用，请换一个或直接登录。')
                    db.commit()
                user=db.execute('SELECT * FROM users WHERE name=?',(name,)).fetchone()
                salt=user['salt'] if user else '00'*16
                correct=self.password_hash(password,salt)
                if not user or not hmac.compare_digest(correct,user['password']): raise APIError(401,'invalid_credentials','账号或密码不正确。')
                token=secrets.token_urlsafe(32)
                db.execute('DELETE FROM sessions WHERE expires<?',(int(time.time()),))
                db.execute('INSERT INTO sessions VALUES (?,?,?)',(self.digest(token),user['id'],int(time.time())+30*86400))
                return {'token':token,'account':self.account(user),'message':'注册成功，体验额度已到账。' if action=='register' else '登录成功。'}
        finally: self.auth_slots.release()

    def deepl(self, endpoint, body=None):
        # Deliberately Free-only: never switch to a paid endpoint or paid key.
        if not self.key.endswith(':fx'): raise APIError(503,'shared_unavailable','共享服务尚未配置 API Free 密钥，请联系开发者。')
        req=urllib.request.Request('https://api-free.deepl.com/v2/'+endpoint, data=json.dumps(body).encode() if body is not None else None,
            headers={'Authorization':'DeepL-Auth-Key '+self.key,'Content-Type':'application/json'})
        with urllib.request.urlopen(req,timeout=20) as response: return json.load(response)

    def translate(self, user, body):
        if not user['email']: raise APIError(403,'email_required','请在账号与安全中验证邮箱后使用赠送额度。')
        if not self.enabled or not self.key: raise APIError(503,'shared_unavailable','公共 DeepL 尚未开启，请使用 Apple 翻译或填写自己的密钥。')
        texts, source, target=body.get('text'),body.get('source'),body.get('target')
        sources={'en':'EN','zh-CN':'ZH','ja':'JA','ko':'KO'}
        targets={'en':'EN-US','zh-CN':'ZH-HANS','ja':'JA','ko':'KO'}
        if not isinstance(source,str) or not isinstance(target,str) or source not in sources or target not in targets or source==target: raise APIError(400,'languages','请选择支持且不同的原文和目标语言。')
        if not isinstance(texts,list) or not 1<=len(texts)<=40 or any(not isinstance(t,str) or not t.strip() for t in texts): raise APIError(400,'text','请输入有效的翻译文字。')
        cost=sum(len(t) for t in texts)
        if cost>5000: raise APIError(413,'text_too_long','公共体验单次最多 5,000 字符，请缩短内容。')
        try: request_id=str(uuid.UUID(body.get('request_id','')))
        except (ValueError,TypeError,AttributeError): raise APIError(400,'request_id','请求编号无效，请更新应用后重试。')
        fingerprint=self.digest(json.dumps([texts,source,target],ensure_ascii=False))
        self.rate('translate:'+user['id'],5,60)
        if not self.translation_lock.acquire(blocking=False): raise APIError(429,'busy','公共翻译正在处理其他请求，请稍后再试。')
        try:
            period='total'
            with self.db() as db:
                db.execute('UPDATE requests SET result=NULL WHERE created<?',(int(time.time())-600,))
                old=db.execute('SELECT * FROM requests WHERE user_id=? AND request_id=?',(user['id'],request_id)).fetchone()
                if old:
                    if old['fingerprint']!=fingerprint: raise APIError(409,'request_conflict','请求编号已被其他内容使用。')
                    if old['status']=='done' and old['result']:
                        cached=json.loads(old['result'])
                        cached['account']=self.account(db.execute('SELECT * FROM users WHERE id=?',(user['id'],)).fetchone())
                        return cached
                    raise APIError(409,'request_processed','该请求已处理或状态未确定，未重复扣除体验额度。请刷新额度后重新提交。')
                # Fail closed if upstream usage cannot be checked. No paid fallback.
                try: usage=self.upstream('usage')
                except Exception: raise APIError(503,'usage_unavailable','暂时无法核实共享池额度，本次未发送翻译，请稍后重试。')
                if not isinstance(usage.get('character_count'),int) or not isinstance(usage.get('character_limit'),int):
                    raise APIError(503,'usage_unavailable','共享池额度信息异常，已暂停请求。')
                if usage['character_count']+cost>min(self.pool_limit,usage['character_limit']): raise APIError(429,'pool_empty','公共字符池本期已用完，请使用 Apple 翻译或自己的密钥。')
                db.execute('BEGIN IMMEDIATE')
                # Recheck after quota I/O so concurrent replays cannot reserve twice.
                if db.execute('SELECT 1 FROM requests WHERE user_id=? AND request_id=?',(user['id'],request_id)).fetchone():
                    db.rollback(); raise APIError(409,'request_processed','该请求正在处理或已处理，未重复扣额。')
                current=db.execute('SELECT * FROM users WHERE id=?',(user['id'],)).fetchone()
                if not current or current['credit']-current['spent']<cost:
                    db.rollback(); raise APIError(402,'trial_empty','你的体验额度不足，请缩短文字，或使用 Apple 翻译 / 自己的密钥。')
                db.execute('INSERT OR IGNORE INTO pool VALUES (?,0)',(period,))
                if db.execute('SELECT spent FROM pool WHERE period=?',(period,)).fetchone()[0]+cost>self.pool_limit:
                    db.rollback(); raise APIError(429,'pool_empty','公共字符池已用完，请使用 Apple 翻译或自己的密钥。')
                db.execute('UPDATE users SET spent=spent+? WHERE id=?',(cost,user['id']))
                db.execute('UPDATE pool SET spent=spent+? WHERE period=?',(cost,period))
                db.execute('INSERT INTO requests VALUES (?,?,?, ?,NULL,?,?)',(user['id'],request_id,fingerprint,'pending',cost,int(time.time())))
                db.commit()
                try:
                    result=self.upstream('translate',{'text':texts,'source_lang':sources[source],'target_lang':targets[target]})
                    translations=[t['text'] for t in result['translations']]
                    if len(translations)!=len(texts) or any(not isinstance(t,str) or not t.strip() for t in translations): raise ValueError('invalid response')
                except Exception as error:
                    # Restore the user's credit on failure; conservatively retain pool reservation
                    # when the upstream may have charged before the connection was lost.
                    db.execute('BEGIN IMMEDIATE')
                    db.execute('UPDATE users SET spent=spent-? WHERE id=?',(cost,user['id']))
                    db.execute("UPDATE requests SET status='failed' WHERE user_id=? AND request_id=?",(user['id'],request_id))
                    if isinstance(error,urllib.error.HTTPError) and error.code in (400,401,403,413,429,456):
                        db.execute('UPDATE pool SET spent=spent-? WHERE period=?',(cost,period))
                    db.commit()
                    raise APIError(503,'upstream_failed','共享翻译暂时失败，本次体验额度已退回。请稍后重试，或使用自己的密钥。') from None
                account=self.account(db.execute('SELECT * FROM users WHERE id=?',(user['id'],)).fetchone())
                response={'translations':translations,'account':account}
                db.execute("UPDATE requests SET status='done',result=? WHERE user_id=? AND request_id=?",(json.dumps(response),user['id'],request_id))
                return response
        finally: self.translation_lock.release()

    def dispatch(self, method, path, body, token, ip):
        if path=='/captcha' or path.startswith('/v2/captcha/'):
            raise APIError(410,'upgrade_required','人机验证已移除，请更新 Yike 后直接登录或注册。')
        if method=='GET' and path=='/health': return {'ok':True}
        if method=='GET' and path=='/v1/config': return self.config()
        if method=='GET' and path=='/v1/update/macos': return self.update_config()
        if method=='POST' and path.startswith('/v2/'): return self.email_auth.dispatch(path,body,token,ip)
        if method=='POST' and path in ('/v1/register','/v1/login'): raise APIError(426,'upgrade_required','请更新 Yike，使用邮箱验证注册或登录。')
        user=self.authenticate(token)
        if method=='GET' and path=='/v1/me':
            with self.db() as db: db.execute('UPDATE requests SET result=NULL WHERE created<?',(int(time.time())-600,))
            return {'account':self.account(user)}
        if method=='POST' and path=='/v1/logout':
            with self.db() as db: db.execute('DELETE FROM sessions WHERE hash=?',(self.digest(token),))
            return {'message':'已退出登录。'}
        if method=='POST' and path=='/v1/password':
            old,new=body.get('old_password'),body.get('new_password')
            if not isinstance(old,str) or not isinstance(new,str) or not 10<=len(new)<=128 or len(old)>128: raise APIError(400,'password','密码格式不正确。')
            self.rate('password:'+user['id'],5,900)
            if not self.auth_slots.acquire(blocking=False): raise APIError(429,'busy','请稍后重试。')
            try:
                if not hmac.compare_digest(self.password_hash(old,user['salt']),user['password']): raise APIError(401,'password','原密码不正确。')
                salt=secrets.token_hex(16); hashed=self.password_hash(new,salt)
                with self.db() as db:
                    db.execute('BEGIN IMMEDIATE')
                    db.execute('UPDATE users SET salt=?,password=? WHERE id=?',(salt,hashed,user['id']))
                    db.execute('DELETE FROM sessions WHERE user_id=?',(user['id'],)); db.commit()
                return {'message':'密码已更新，请重新登录。'}
            finally: self.auth_slots.release()
        if method=='POST' and path=='/v1/translate': return self.translate(user,body)
        raise APIError(404,'not_found','接口不存在。')

class Handler(http.server.BaseHTTPRequestHandler):
    def log_message(self,*args): pass  # Never log account data, text or authorization headers.
    def do_GET(self): self.handle_api()
    def do_POST(self): self.handle_api()
    def handle_api(self):
        self.connection.settimeout(8)
        try:
            if self.headers.get('Transfer-Encoding'): raise APIError(400,'body','请求格式不支持。')
            length=int(self.headers.get('Content-Length','0'))
            if not 0<=length<=32768: raise APIError(413,'body','请求内容过长。')
            body=json.loads(self.rfile.read(length)) if length else {}
            if not isinstance(body,dict): raise APIError(400,'body','请求格式不正确。')
            ip=self.headers.get('X-Real-IP',self.client_address[0]) if self.client_address[0]=='127.0.0.1' else self.client_address[0]
            token=self.headers.get('Authorization','').removeprefix('Bearer ')
            data=self.server.service.dispatch(self.command,self.path.split('?')[0],body,token,ip)
            status=200
        except APIError as e: status,data=e.status,{'code':e.code,'message':e.message}
        except (ValueError,UnicodeError): status,data=400,{'code':'body','message':'请求格式不正确。'}
        except Exception: status,data=500,{'code':'server','message':'服务暂时异常，请稍后重试。'}
        encoded=json.dumps(data,ensure_ascii=False).encode()
        try:
            self.send_response(status)
            self.send_header('Content-Type','application/json; charset=utf-8')
            self.send_header('Content-Length',str(len(encoded)))
            self.send_header('Cache-Control','no-store')
            self.send_header('X-Content-Type-Options','nosniff')
            self.end_headers(); self.wfile.write(encoded)
        except (BrokenPipeError,ConnectionResetError): pass

if __name__=='__main__':
    os.umask(0o077)
    pepper=os.environ.get('YIKE_SECRET','')
    if len(pepper)<32: raise SystemExit('YIKE_SECRET must contain at least 32 random characters')
    service=Service(os.environ.get('YIKE_DB','/var/lib/yike-trial/accounts.sqlite3'),pepper,
        key=os.environ.get('DEEPL_API_KEY',''),enabled=os.environ.get('YIKE_PUBLIC_ENABLED')=='1',gift=200000,pool_limit=4000000)
    keys=json.loads(os.environ.get('DEEPL_API_KEYS','[]'))
    if keys:
        service.upstream=DeepLKeyPool(keys); service.key='configured-pool'
    server=http.server.ThreadingHTTPServer(('127.0.0.1',int(os.environ.get('YIKE_PORT','8093'))),Handler)
    def cleanup():
        while True:
            with service.db() as db:
                db.execute('UPDATE requests SET result=NULL WHERE created<?',(int(time.time())-600,))
                db.execute('DELETE FROM email_codes WHERE expires<?',(int(time.time()),))
                db.execute('DELETE FROM sessions WHERE expires<?',(int(time.time()),))
                db.execute('DELETE FROM limits WHERE stamp<?',(int(time.time())-32*86400,))
            time.sleep(60)
    threading.Thread(target=cleanup,daemon=True).start()
    server.daemon_threads=True; server.service=service
    server.serve_forever()
