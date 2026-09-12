import concurrent.futures, json, tempfile, unittest
from pathlib import Path
from unittest.mock import patch
from app import Service, APIError
from key_pool import DeepLKeyPool

class EmailTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(); self.sent=[]; self.consumed=set()
        def captcha(token,ip):
            if token!='human' or token in self.consumed: raise APIError(400,'captcha','captcha rejected')
            self.consumed.add(token)
        self.service=Service(Path(self.temp.name)/'db','s'*40,mailer=lambda *args:self.sent.append(args),captcha=captcha)
        self.auth=self.service.email_auth; self.ip='127.0.0.1'
    def tearDown(self): self.temp.cleanup()
    def call(self,path,body,token=''):
        return self.service.dispatch('POST','/v2/'+path,body,token,self.ip)
    def send(self,email='hello@example.com',purpose='register',token=''):
        self.consumed.clear()
        return self.call(purpose+'/send',{'email':email,'password':'secure-password','captcha_token':'human'},token)
    def verify(self,challenge,email='hello@example.com',purpose='register',token='',code=None):
        return self.call(purpose+'/verify',{'email':email,'challenge_id':challenge['challenge_id'],'code':code or self.sent[-1][1],'new_password':'new-secure-password'},token)
    def error(self,code,fn):
        with self.assertRaises(APIError) as raised: fn()
        self.assertEqual(raised.exception.code,code)
    def test_only_verified_mailbox_gets_credit_and_login_does_not_regrant(self):
        pending=self.send()
        with self.service.db() as db:
            self.assertEqual(db.execute('SELECT COUNT(*) FROM users').fetchone()[0],0)
            record=db.execute('SELECT * FROM email_codes').fetchone()
            self.assertNotEqual(record['code_hash'],self.sent[-1][1]); self.assertNotEqual(record['password'],'secure-password')
        result=self.verify(pending)
        self.assertEqual(result['account']['remaining'],200000)
        self.consumed.clear()
        login=self.call('login',{'email':'HELLO@example.com','password':'secure-password','captcha_token':'human'})
        self.assertEqual(login['account']['granted'],200000)
        self.error('code',lambda:self.verify(pending))
    def test_captcha_required_replay_and_legacy_bypass(self):
        self.error('captcha',lambda:self.call('register/send',{'email':'x@example.com','password':'secure-password','captcha_token':''}))
        self.error('upgrade_required',lambda:self.service.dispatch('POST','/v1/register',{'username':'aaaa','password':'secure-password'},'',self.ip))
        self.send()
        self.error('captcha',lambda:self.call('register/send',{'email':'next@example.com','password':'secure-password','captcha_token':'human'}))
    def test_bad_expired_and_exhausted_codes(self):
        pending=self.send()
        for _ in range(5): self.error('code',lambda:self.verify(pending,code='wrong1'))
        # Invalid format is rejected before attempts; six-digit guesses are bounded.
        for _ in range(5): self.error('code',lambda:self.verify(pending,code='999999' if self.sent[-1][1]!='999999' else '888888'))
        self.error('code',lambda:self.verify(pending))
    def test_expiry(self):
        pending=self.send()
        with self.service.db() as db: db.execute('UPDATE email_codes SET expires=0')
        self.error('code',lambda:self.verify(pending))
    def test_reset_revokes_existing_session(self):
        account=self.verify(self.send()); self.ip='127.0.0.2'
        with self.service.db() as db: db.execute('DELETE FROM limits')
        pending=self.send(purpose='reset'); self.verify(pending,purpose='reset')
        self.error('login_required',lambda:self.service.authenticate(account['token']))
        self.consumed.clear()
        login=self.call('login',{'email':'hello@example.com','password':'new-secure-password','captcha_token':'human'})
        self.assertEqual(login['account']['remaining'],200000)
    def test_existing_account_binding_tops_up_once_and_keeps_spend(self):
        account=self.service.auth('register',{'username':'legacy','password':'secure-password'},self.ip)
        with self.service.db() as db: db.execute('UPDATE users SET credit=50000,spent=123')
        result=self.verify(self.send(purpose='bind',token=account['token']),purpose='bind',token=account['token'])
        self.assertEqual(result['account']['remaining'],199877)
        self.error('login_required',lambda:self.service.authenticate(account['token']))
    def test_mail_failure_does_not_create_account_or_usable_code(self):
        self.auth.mailer=lambda *args: (_ for _ in ()).throw(APIError(503,'mail_unavailable','failed'))
        self.error('mail_unavailable',lambda:self.send())
        with self.service.db() as db: self.assertEqual(db.execute('SELECT COUNT(*) FROM email_codes').fetchone()[0],0)
    def test_canonical_email_and_concurrent_verification(self):
        self.assertEqual(self.auth.email('A.b+test@googlemail.com'),'ab@gmail.com')
        pending=self.send()
        def verify(_):
            try: return self.verify(pending)['account']['remaining']
            except APIError: return 0
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as workers: self.assertEqual(sum(workers.map(verify,range(2))),200000)
    def test_browser_verification_ticket_is_single_use_and_expires(self):
        self.auth.captcha=self.auth.verify_captcha
        ticket=self.call('captcha/start',{})['ticket']
        self.error('captcha',lambda:self.auth.verify_captcha('browser:'+ticket,self.ip))
        with self.service.db() as db: db.execute('UPDATE browser_captcha SET verified=1')
        self.assertTrue(self.call('captcha/status',{'ticket':ticket})['verified'])
        self.auth.verify_captcha('browser:'+ticket,self.ip)
        self.error('captcha',lambda:self.auth.verify_captcha('browser:'+ticket,self.ip))
        second=self.call('captcha/start',{})['ticket']
        with self.service.db() as db: db.execute('UPDATE browser_captcha SET verified=1,expires=0')
        self.error('captcha',lambda:self.auth.verify_captcha('browser:'+second,self.ip))

    def test_siteverify_checks_hostname_action_and_success(self):
        self.auth.captcha=self.auth.verify_captcha
        class Response:
            def __init__(self,value): self.value=json.dumps(value).encode()
            def __enter__(self): return self
            def __exit__(self,*args): pass
            def read(self): return self.value
        with patch.dict('os.environ',{'TURNSTILE_SECRET':'test','YIKE_AUTH_HOST':'n5v1b.cn'}):
            for result in ({'success':False},{'success':True,'hostname':'attacker.test','action':'yike_auth'},{'success':True,'hostname':'n5v1b.cn','action':'wrong'}):
                with patch('urllib.request.urlopen',return_value=Response(result)): self.error('captcha',lambda:self.auth.verify_captcha('token',self.ip))
            with patch('urllib.request.urlopen',return_value=Response({'success':True,'hostname':'n5v1b.cn','action':'yike_auth'})): self.auth.verify_captcha('token',self.ip)

class KeyPoolTests(unittest.TestCase):
    def test_select_key_with_enough_quota_and_no_failure_fallback(self):
        pool=DeepLKeyPool(['test-a:fx','test-b:fx','test-c:fx','test-d:fx']); calls=[]
        def call(key,endpoint,body=None):
            if endpoint=='usage': return {'character_count':999999 if key=='test-a:fx' else 0,'character_limit':1000000}
            calls.append(key); raise TimeoutError()
        pool.call=call
        self.assertEqual(pool('usage')['character_limit'],1000000)
        with self.assertRaises(TimeoutError): pool('translate',{'text':['hello']})
        self.assertEqual(len(calls),1); self.assertNotEqual(calls[0],'test-a:fx')
    def test_reject_paid_or_duplicate_keys(self):
        for keys in (['paid-key'],['same:fx','same:fx']):
            with self.assertRaises(ValueError): DeepLKeyPool(keys)

if __name__=='__main__': unittest.main()
