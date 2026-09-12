import threading
import unittest
from concurrent.futures import ThreadPoolExecutor
from unittest.mock import patch
from key_pool import DeepLKeyPool


class PoolSpeedTests(unittest.TestCase):
    def test_cache_deducts_and_expiry_refreshes(self):
        pool=DeepLKeyPool(['a:fx']); calls=[]
        def call(key,endpoint,body=None):
            calls.append(endpoint)
            return {'character_count':0,'character_limit':100} if endpoint=='usage' else {'translations':[{'text':'ok'}]}
        pool.call=call
        pool('usage'); pool('translate',{'text':['hello']})
        self.assertEqual(pool('usage')['character_limit'],95)
        self.assertEqual(calls.count('usage'),1)
        pool.checked=0; pool('usage')
        self.assertEqual(calls.count('usage'),2)

    def test_concurrent_requests_reserve_separate_keys(self):
        pool=DeepLKeyPool(['a:fx','b:fx']); barrier=threading.Barrier(2); selected=[]
        def call(key,endpoint,body=None):
            if endpoint=='usage': return {'character_count':0,'character_limit':100}
            selected.append(key); barrier.wait(timeout=3)
            return {'translations':[{'text':'ok'}]}
        pool.call=call; pool('usage')
        with ThreadPoolExecutor(max_workers=2) as workers:
            list(workers.map(lambda _:pool('translate',{'text':['hello']}),range(2)))
        self.assertEqual(len(set(selected)),2)
        self.assertEqual(sum(v for _,v in pool.available),190)

    def test_failure_invalidates_without_retry(self):
        pool=DeepLKeyPool(['a:fx']); calls=[]
        def call(key,endpoint,body=None):
            calls.append(endpoint)
            if endpoint=='usage': return {'character_count':0,'character_limit':100}
            raise TimeoutError()
        pool.call=call; pool('usage')
        with self.assertRaises(TimeoutError): pool('translate',{'text':['hello']})
        self.assertEqual(calls.count('translate'),1)
        pool('usage'); self.assertEqual(calls.count('usage'),2)

    def test_transport_reuses_connection_and_discards_failure(self):
        with patch('key_pool.http.client.HTTPSConnection') as factory:
            connection=factory.return_value
            connection.getresponse.return_value.status=200
            connection.getresponse.return_value.read.return_value=b'{}'
            pool=DeepLKeyPool(['a:fx'])
            pool.call('a:fx','usage'); pool.call('a:fx','usage')
            self.assertEqual(factory.call_count,1)
            connection.request.side_effect=TimeoutError()
            with self.assertRaises(TimeoutError): pool.call('a:fx','translate',{'text':['hello']})
            self.assertNotIn('a:fx',pool.connections)
            self.assertEqual(connection.request.call_count,3)
