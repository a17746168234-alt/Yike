import concurrent.futures, hashlib, json, tempfile, threading, unittest, uuid
from pathlib import Path
from app import Service, APIError

class TrialTests(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory()
        self.calls=[]
        def upstream(endpoint,body=None):
            self.calls.append(endpoint)
            return {'character_count':0,'character_limit':1000000} if endpoint=='usage' else {'translations':[{'text':'translated '+t} for t in body['text']]}
        self.service=Service(Path(self.temp.name)/'accounts.db','a'*40,key='test-only:fx',enabled=True,upstream=upstream)
        self.response=self.service.auth('register',{'username':'test_user','password':'long-test-password'},'127.0.0.1')
        self.token=self.response['token']
        with self.service.db() as db: db.execute("UPDATE users SET email='test@example.com'")
        self.user=self.service.authenticate(self.token)
    def tearDown(self): self.temp.cleanup()
    def payload(self,text='你好😀'):
        return {'text':[text],'source':'zh-CN','target':'en','request_id':str(uuid.uuid4())}
    def assertCode(self,code,operation):
        with self.assertRaises(APIError) as error: operation()
        self.assertEqual(error.exception.code,code)
    def test_grant_and_login_never_grants_again(self):
        self.assertEqual(self.response['account']['remaining'],200000)
        self.assertCode('username_exists',lambda:self.service.auth('register',{'username':'TEST_USER','password':'long-test-password'},'127.0.0.2'))
        result=self.service.auth('login',{'username':'TEST_USER','password':'long-test-password'},'127.0.0.3')
        self.assertEqual(result['account']['granted'],200000)
        self.assertCode('invalid_credentials',lambda:self.service.auth('login',{'username':'test_user','password':'wrong-password'},'127.0.0.4'))
        with self.service.db() as db:
            row=db.execute('SELECT * FROM users').fetchone()
            self.assertNotEqual(row['password'],'long-test-password')
            self.assertNotEqual(db.execute('SELECT hash FROM sessions LIMIT 1').fetchone()[0],self.token)
    def test_real_character_count_and_replay(self):
        body=self.payload(); first=self.service.translate(self.user,body)
        self.assertEqual(first['account']['used'],3)
        self.assertEqual(self.service.translate(self.user,body),first)
        self.assertEqual(self.calls.count('translate'),1)
        body['text']=['不同文字']
        self.assertCode('request_conflict',lambda:self.service.translate(self.user,body))
    def test_german_french_translation_both_directions(self):
        requests=[]
        def upstream(endpoint,body=None):
            if endpoint=='usage': return {'character_count':0,'character_limit':1000000}
            requests.append(body)
            return {'translations':[{'text':'translated'} for _ in body['text']]}
        self.service.upstream=upstream
        for source,target in [('de','en'),('en','de'),('fr','en'),('en','fr')]:
            body=self.payload('Bonjour Guten Tag'); body.update(source=source,target=target)
            result=self.service.translate(self.user,body)
            self.assertEqual(result['translations'],['translated'])
            self.assertEqual(requests[-1]['source_lang'],source.upper())
            self.assertEqual(requests[-1]['target_lang'],'EN-US' if target=='en' else target.upper())
    def test_no_key_or_disabled_never_calls_deepl(self):
        self.service.enabled=False
        self.assertCode('shared_unavailable',lambda:self.service.translate(self.user,self.payload()))
        self.assertEqual(self.calls,[])
    def test_actual_provider_quota_overrides_configured_pool(self):
        self.service.upstream=lambda *a: {'character_count':13999999,'character_limit':2000000}
        self.assertCode('pool_empty',lambda:self.service.translate(self.user,self.payload()))
        self.assertEqual(self.service.authenticate(self.token)['spent'],0)
    def test_pool_is_total_not_monthly(self):
        with self.service.db() as db: db.execute("INSERT INTO pool VALUES ('total',3999999)")
        self.assertCode('pool_empty',lambda:self.service.translate(self.user,self.payload()))
        self.assertEqual(self.service.config()['pool_remaining'],1)
    def test_account_quota_is_server_enforced(self):
        with self.service.db() as db: db.execute('UPDATE users SET spent=199999')
        self.assertCode('trial_empty',lambda:self.service.translate(self.user,self.payload()))
        self.assertNotIn('translate',self.calls)
    def test_unknown_failure_refunds_user_and_preserves_pool_reservation(self):
        def fail(endpoint,body=None):
            if endpoint=='usage': return {'character_count':0,'character_limit':1000000}
            raise TimeoutError()
        self.service.upstream=fail
        body=self.payload()
        self.assertCode('upstream_failed',lambda:self.service.translate(self.user,body))
        self.assertEqual(self.service.authenticate(self.token)['spent'],0)
        self.assertEqual(self.service.config()['pool_remaining'],3999997)
        self.assertCode('request_processed',lambda:self.service.translate(self.user,body))
    def test_atomic_account_reservation_under_concurrency(self):
        with self.service.db() as db: db.execute('UPDATE users SET credit=3')
        def attempt(_):
            try: self.service.translate(self.user,self.payload()); return True
            except APIError: return False
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool: results=list(pool.map(attempt,range(2)))
        self.assertEqual(sum(results),1)
        self.assertEqual(self.service.authenticate(self.token)['spent'],3)
    def test_logout_and_password_change_revoke_sessions(self):
        result=self.service.dispatch('POST','/v1/password',{'old_password':'long-test-password','new_password':'another-long-password'},self.token,'127.0.0.1')
        self.assertCode('login_required',lambda:self.service.authenticate(self.token))
        login=self.service.auth('login',{'username':'test_user','password':'another-long-password'},'127.0.0.2')
        self.service.dispatch('POST','/v1/logout',{},login['token'],'127.0.0.2')
        self.assertCode('login_required',lambda:self.service.authenticate(login['token']))
    def test_registration_ip_limit(self):
        for i in range(2): self.service.auth('register',{'username':'new_user_'+str(i),'password':'long-test-password'},'127.0.0.1')
        self.assertCode('rate_limit',lambda:self.service.auth('register',{'username':'new_user_3','password':'long-test-password'},'127.0.0.1'))
    def test_malformed_or_oversized_inputs(self):
        body=self.payload('x'*5001)
        self.assertCode('text_too_long',lambda:self.service.translate(self.user,body))
        body=self.payload(); body['source']=[]
        self.assertCode('languages',lambda:self.service.translate(self.user,body))
        self.assertCode('login_required',lambda:self.service.dispatch('POST','/v1/translate',self.payload(),'invalid','127.0.0.1'))

if __name__=='__main__': unittest.main()
