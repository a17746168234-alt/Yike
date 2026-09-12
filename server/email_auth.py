"""Verified-email auth. No plaintext passwords or OTPs are stored."""
import hashlib, hmac, json, os, re, secrets, smtplib, ssl, time
from email.message import EmailMessage

class EmailAuth:
    def __init__(self, service, error, *, mailer=None):
        self.s, self.Error = service, error
        self.mailer = mailer or self.send_mail
        with self.s.db() as db:
            columns = {r['name'] for r in db.execute('PRAGMA table_info(users)')}
            if 'email' not in columns: db.execute('ALTER TABLE users ADD COLUMN email TEXT')
            db.execute('CREATE UNIQUE INDEX IF NOT EXISTS users_email ON users(email)')
            db.execute('DROP TABLE IF EXISTS browser_captcha')
            db.execute('''CREATE TABLE IF NOT EXISTS email_codes(
                id TEXT PRIMARY KEY,email TEXT NOT NULL,purpose TEXT NOT NULL,user_id TEXT,
                code_hash TEXT NOT NULL,salt TEXT,password TEXT,expires INTEGER NOT NULL,attempts INTEGER NOT NULL DEFAULT 0)''')

    def email(self, value):
        if not isinstance(value,str) or len(value)>254: raise self.Error(400,'email','请输入有效的邮箱地址。')
        value=value.strip().lower()
        if not re.fullmatch(r"[a-z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?\.[a-z]{2,63}",value):
            raise self.Error(400,'email','请输入有效的邮箱地址。')
        local,domain=value.rsplit('@',1)
        # Gmail aliases belong to the same mailbox and must not receive extra grants.
        if domain in ('gmail.com','googlemail.com'): value=local.split('+')[0].replace('.','')+'@gmail.com'
        return value

    def password(self, value):
        if not isinstance(value,str) or not 10<=len(value)<=128: raise self.Error(400,'password','密码需为 10–128 位。')
        return value

    def send_mail(self,email,code,purpose):
        host=os.environ.get('SMTP_HOST',''); password=os.environ.get('SMTP_PASSWORD','')
        sender=os.environ.get('SMTP_FROM','')
        if not host or not password or not sender: raise self.Error(503,'mail_unavailable','验证码邮件暂时无法发送，请稍后再试。')
        # Strict server cap prevents this app from using paid email overage.
        self.s.rate('mail:day',int(os.environ.get('YIKE_MAIL_DAILY_LIMIT','50')),86400)
        self.s.rate('mail:month',int(os.environ.get('YIKE_MAIL_MONTHLY_LIMIT','1000')),31*86400)
        message=EmailMessage(); message['From']='Yike <'+sender+'>'; message['To']=email
        message['Subject']='Yike 邮箱验证码'
        message.set_content('你的 Yike 验证码是：'+code+'\n\n10 分钟内有效，请勿分享给他人。如果不是你本人操作，请忽略本邮件。')
        try:
            with smtplib.SMTP_SSL(host,int(os.environ.get('SMTP_PORT','465')),timeout=15,context=ssl.create_default_context()) as smtp:
                smtp.login(os.environ.get('SMTP_USER',''),password); smtp.send_message(message)
        except Exception: raise self.Error(503,'mail_unavailable','验证码邮件发送失败，请稍后重试。') from None

    def issue(self,db,user):
        token=secrets.token_urlsafe(32)
        db.execute('INSERT INTO sessions VALUES (?,?,?)',(self.s.digest(token),user['id'],int(time.time())+30*86400))
        return {'token':token,'account':self.s.account(user),'message':'登录成功。'}

    def dispatch(self,path,body,token,ip):
        if path=='/v2/login':
            self.s.rate('email-login:'+self.s.digest(ip),20,900)
            email=self.email(body.get('email')); password=self.password(body.get('password'))
            self.s.rate('email-login-name:'+self.s.digest(email),10,900)
            if not self.s.auth_slots.acquire(blocking=False): raise self.Error(429,'busy','登录服务繁忙，请稍后再试。')
            try:
                with self.s.db() as db:
                    user=db.execute('SELECT * FROM users WHERE email=?',(email,)).fetchone()
                    actual=self.s.password_hash(password,user['salt'] if user else '00'*16)
                    if not user or not hmac.compare_digest(actual,user['password']): raise self.Error(401,'invalid_credentials','邮箱或密码不正确。')
                    return self.issue(db,user)
            finally: self.s.auth_slots.release()
        if path in ('/v2/register/send','/v2/reset/send','/v2/bind/send'):
            purpose=path.split('/')[2]
            email=self.email(body.get('email'))
            self.s.rate('email-send-ip:'+self.s.digest(ip),5,3600)
            self.s.rate('email-send:'+self.s.digest(email),1,60)
            user=self.s.authenticate(token) if purpose=='bind' else None
            salt=password=None
            if purpose=='register':
                raw=self.password(body.get('password'))
                if not self.s.auth_slots.acquire(blocking=False): raise self.Error(429,'busy','注册服务繁忙，请稍后再试。')
                try: salt=secrets.token_hex(16); password=self.s.password_hash(raw,salt)
                finally: self.s.auth_slots.release()
            with self.s.db() as db:
                existing=db.execute('SELECT * FROM users WHERE email=?',(email,)).fetchone()
                if purpose in ('register','bind') and existing: raise self.Error(409,'email_exists','该邮箱已经注册，请直接登录或找回密码。')
                if purpose=='bind' and user['email']: raise self.Error(409,'already_verified','该账号已经验证邮箱，请刷新账号信息。')
                if purpose=='reset': user=existing
            challenge=secrets.token_urlsafe(32); code=f'{secrets.randbelow(1000000):06d}'
            if purpose=='reset' and not user: return {'challenge_id':challenge,'message':'如果该邮箱已注册，验证码将发送至邮箱。'}
            with self.s.db() as db:
                db.execute('DELETE FROM email_codes WHERE email=? AND purpose=?',(email,purpose))
                db.execute('INSERT INTO email_codes VALUES (?,?,?,?,?,?,?,?,0)',(self.s.digest(challenge),email,purpose,user['id'] if user else None,self.s.digest(challenge+':'+code),salt,password,int(time.time())+600))
            try: self.mailer(email,code,purpose)
            except Exception:
                with self.s.db() as db: db.execute('DELETE FROM email_codes WHERE id=?',(self.s.digest(challenge),))
                raise
            return {'challenge_id':challenge,'message':'验证码已发送，请查看收件箱或垃圾邮件。'}
        if path in ('/v2/register/verify','/v2/reset/verify','/v2/bind/verify'):
            purpose=path.split('/')[2]; email=self.email(body.get('email'))
            self.s.rate('email-verify:'+self.s.digest(ip),20,900)
            challenge=body.get('challenge_id'); code=body.get('code')
            if not isinstance(challenge,str) or len(challenge)>128 or not isinstance(code,str) or not re.fullmatch(r'\d{6}',code): raise self.Error(400,'code','请输入邮件中的 6 位验证码。')
            bind_user=self.s.authenticate(token) if purpose=='bind' else None
            new_salt=new_password=None
            if purpose=='reset':
                raw=self.password(body.get('new_password'))
                if not self.s.auth_slots.acquire(blocking=False): raise self.Error(429,'busy','操作繁忙，请稍后再试。')
                try: new_salt=secrets.token_hex(16); new_password=self.s.password_hash(raw,new_salt)
                finally: self.s.auth_slots.release()
            with self.s.db() as db:
                db.execute('BEGIN IMMEDIATE')
                pending=db.execute('SELECT * FROM email_codes WHERE id=?',(self.s.digest(challenge),)).fetchone()
                if not pending or pending['email']!=email or pending['purpose']!=purpose or pending['expires']<time.time() or pending['attempts']>=5:
                    db.rollback(); raise self.Error(400,'code','验证码无效或已过期，请重新发送。')
                db.execute('UPDATE email_codes SET attempts=attempts+1 WHERE id=?',(pending['id'],))
                if not hmac.compare_digest(pending['code_hash'],self.s.digest(challenge+':'+code)):
                    db.commit(); raise self.Error(400,'code','验证码不正确，请检查后重新输入。')
                if purpose=='bind' and pending['user_id']!=bind_user['id']:
                    db.commit(); raise self.Error(403,'code','请在原账号中完成邮箱验证。')
                if purpose in ('register','bind') and db.execute('SELECT 1 FROM users WHERE email=?',(email,)).fetchone():
                    db.rollback(); raise self.Error(409,'email_exists','该邮箱已经注册，请直接登录。')
                user_id=pending['user_id'] or secrets.token_hex(16)
                if purpose=='register':
                    if db.execute('SELECT COUNT(*) FROM users').fetchone()[0]>=10000:
                        db.rollback(); raise self.Error(503,'registration_paused','注册暂时已满。')
                    db.execute('INSERT INTO users(id,name,salt,password,credit,spent,email) VALUES (?,?,?,?,?,0,?)',(user_id,email,pending['salt'],pending['password'],self.s.gift,email))
                elif purpose=='bind':
                    db.execute('UPDATE users SET email=?,credit=MAX(credit,?) WHERE id=?',(email,self.s.gift,user_id))
                else:
                    db.execute('UPDATE users SET salt=?,password=? WHERE id=?',(new_salt,new_password,user_id))
                db.execute('DELETE FROM sessions WHERE user_id=?',(user_id,))
                db.execute('DELETE FROM email_codes WHERE email=?',(email,))
                user=db.execute('SELECT * FROM users WHERE id=?',(user_id,)).fetchone()
                result=self.issue(db,user) if purpose!='reset' else {'message':'密码已重置，请使用新密码登录。'}
                if purpose!='reset': result['message']='邮箱验证成功，20 万字符额度已到账。'
                db.commit(); return result
        raise self.Error(404,'not_found','接口不存在。')
