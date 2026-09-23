import base64, concurrent.futures, tempfile, unittest
from pathlib import Path
from unittest.mock import patch
from app import Service, APIError
from key_pool import DeepLKeyPool

class EmailTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(); self.sent=[]
        self.service=Service(Path(self.temp.name)/'db','s'*40,mailer=lambda *args:self.sent.append(args))
        self.auth=self.service.email_auth; self.ip='127.0.0.1'
    def tearDown(self): self.temp.cleanup()
    def call(self,path,body,token=''):
        return self.service.dispatch('POST','/v2/'+path,body,token,self.ip)
    def send(self,email='hello@example.com',purpose='register',token=''):
        return self.call(purpose+'/send',{'email':email,'password':'secure-password'},token)
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
        login=self.call('login',{'email':'HELLO@example.com','password':'secure-password'})
        self.assertEqual(login['account']['granted'],200000)
        self.error('code',lambda:self.verify(pending))
    def test_register_and_login_do_not_require_captcha_or_external_calls(self):
        with patch('urllib.request.urlopen',side_effect=AssertionError('unexpected network request')):
            pending=self.send(); result=self.verify(pending)
            login=self.call('login',{'email':'hello@example.com','password':'secure-password'})
            self.assertEqual(login['account']['remaining'],200000)
            self.assertEqual(login['account']['username'],result['account']['username'])
        self.error('upgrade_required',lambda:self.service.dispatch('POST','/v1/register',{'username':'aaaa','password':'secure-password'},'',self.ip))
    def test_send_limits_still_apply_without_captcha(self):
        self.send()
        self.error('rate_limit',lambda:self.send())
        for i in range(3): self.send(email=f'person{i}@example.com')
        self.error('rate_limit',lambda:self.send(email='another@example.com'))
    def test_password_and_login_limits_still_apply_without_captcha(self):
        self.verify(self.send())
        for _ in range(10):
            self.error('invalid_credentials',lambda:self.call('login',{'email':'hello@example.com','password':'incorrect-password'}))
        self.error('rate_limit',lambda:self.call('login',{'email':'hello@example.com','password':'secure-password'}))
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
        login=self.call('login',{'email':'hello@example.com','password':'new-secure-password'})
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
    def test_retired_captcha_endpoints_request_client_update(self):
        for method,path in [('GET','/captcha'),('POST','/v2/captcha/start'),('POST','/v2/captcha/status'),('POST','/v2/captcha/complete')]:
            self.error('upgrade_required',lambda:self.service.dispatch(method,path,{},'',self.ip))
    def test_update_manifest_is_public_and_validated(self):
        manifest=Path(self.temp.name)/'update.json'
        manifest.write_text('{"version":"1.6","build":69,"title":"Yike 1.6","notes":"test","download_url":"https://github.com/a17746168234-alt/Yike/releases/latest/download/Yike-macOS-arm64.dmg","sha256":"'+'a'*64+'"}')
        self.service.update_path=manifest
        self.assertEqual(self.service.dispatch('GET','/v1/update/macos',{},'',self.ip)['build'],69)
        mirror='https://n5v1b.cn/yike-download/Yike-macOS-arm64-v1.6-build69.dmg'
        manifest.write_text(manifest.read_text().replace('https://github.com/a17746168234-alt/Yike/releases/latest/download/Yike-macOS-arm64.dmg',mirror))
        self.assertEqual(self.service.dispatch('GET','/v1/update/macos',{},'',self.ip)['download_url'],mirror)
        manifest.write_text(manifest.read_text().replace('n5v1b.cn','untrusted.example'))
        self.error('update_unavailable',lambda:self.service.dispatch('GET','/v1/update/macos',{},'',self.ip))
        windows=Path(self.temp.name)/'update-windows.json'
        windows.write_text('{"version":"2.1.0","build":210,"title":"Yike Windows 2.1","notes":"test","download_url":"https://github.com/a17746168234-alt/Yike/releases/download/windows-v2.1.0/Yike-Setup.exe","sha256":"'+'b'*64+'","size":123}',encoding='utf-8')
        self.service.windows_update_path=windows
        self.assertEqual(self.service.dispatch('GET','/v1/update/windows',{},'',self.ip)['size'],123)
        windows.write_text(windows.read_text().replace('windows-v2.1.0','v2.1.0'),encoding='utf-8')
        self.error('update_unavailable',lambda:self.service.dispatch('GET','/v1/update/windows',{},'',self.ip))
        manifest.write_text('{}')
        self.error('update_unavailable',lambda:self.service.dispatch('GET','/v1/update/macos',{},'',self.ip))
    def test_profile_persists_across_login_and_rejects_bad_avatar(self):
        signed=self.verify(self.send())
        token=signed['token']
        avatar=base64.b64encode(b'\xff\xd8\xff'+b'photo').decode()
        saved=self.service.dispatch('POST','/v1/profile',{'profile_name':'测试User','avatar_data':avatar},token,self.ip)
        self.assertEqual(saved['account']['profile_name'],'测试User')
        self.assertEqual(saved['account']['avatar_data'],avatar)
        reloaded=Service(Path(self.temp.name)/'db','s'*40,mailer=lambda *args:self.sent.append(args))
        self.assertEqual(reloaded.dispatch('GET','/v1/me',{},token,self.ip)['account']['avatar_data'],avatar)
        self.error('login_required',lambda:reloaded.dispatch('POST','/v1/profile',{'profile_name':'Other','avatar_data':None},'',self.ip))
        self.error('avatar',lambda:reloaded.dispatch('POST','/v1/profile',{'profile_name':'Valid','avatar_data':'not-base64'},token,self.ip))
        self.error('profile_name',lambda:reloaded.dispatch('POST','/v1/profile',{'profile_name':'bad name','avatar_data':None},token,self.ip))
        cleared=reloaded.dispatch('POST','/v1/profile',{'profile_name':'新名','avatar_data':None},token,self.ip)
        self.assertIsNone(cleared['account']['avatar_data'])
    def test_migration_removes_only_challenge_table_and_preserves_accounts(self):
        signed=self.verify(self.send())
        with self.service.db() as db:
            db.execute('CREATE TABLE browser_captcha(id TEXT PRIMARY KEY,expires INTEGER,verified INTEGER)')
            db.execute("INSERT INTO browser_captcha VALUES ('obsolete',0,0)")
        reloaded=Service(Path(self.temp.name)/'db','s'*40,mailer=lambda *args:self.sent.append(args))
        self.assertEqual(reloaded.account(reloaded.authenticate(signed['token']))['remaining'],200000)
        with reloaded.db() as db:
            self.assertIsNone(db.execute("SELECT name FROM sqlite_master WHERE name='browser_captcha'").fetchone())

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
