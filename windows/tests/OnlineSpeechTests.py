"""Offline tests for connection selection and cancellation; no speech service calls."""
import asyncio
import importlib.util
import os
from pathlib import Path
import sys
import tempfile
import types
import unittest

sys.dont_write_bytecode = True
fake_module = types.ModuleType("edge_tts")
sys.modules["edge_tts"] = fake_module
spec = importlib.util.spec_from_file_location("online", Path(__file__).resolve().parents[1] / "src" / "Scripts" / "speech-online.py")
online = importlib.util.module_from_spec(spec)
spec.loader.exec_module(online)


class Connections(unittest.IsolatedAsyncioTestCase):
    async def asyncSetUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="YikeSpeechRoutes-")
        self.output = os.path.join(self.temp.name, "0.mp3")
        self.job = {"voice": "test", "rate": 0, "proxy": "http://stale.invalid"}
        self.calls = []
        self.behavior = {}
        owner = self

        class Speech:
            def __init__(self, text, voice, **options):
                self.route = options["proxy"]

            async def save(self, path):
                owner.calls.append(self.route)
                Path(path).write_bytes(b"partial")
                delay, succeeds = owner.behavior[self.route]
                await asyncio.sleep(delay)
                if not succeeds:
                    raise ConnectionError("controlled test failure")
                Path(path).write_bytes(b"complete audio")

        fake_module.Communicate = Speech

    async def asyncTearDown(self):
        self.temp.cleanup()

    async def test_dead_proxy_does_not_delay_direct_or_later_segments(self):
        self.behavior = {self.job["proxy"]: (5, False), None: (0.01, True)}
        selected = await asyncio.wait_for(online.synthesize(self.job, "test", self.output), 1)
        self.assertIsNone(selected)
        self.assertEqual(Path(self.output).read_bytes(), b"complete audio")
        self.assertEqual(list(Path(self.temp.name).glob("*.partial")), [])
        self.calls.clear()
        await online.synthesize(self.job, "next", self.output, True, selected)
        self.assertEqual(self.calls, [None])

    async def test_healthy_proxy_wins_and_unused_direct_attempt_is_canceled(self):
        self.behavior = {self.job["proxy"]: (0.01, True), None: (5, False)}
        self.assertEqual(await online.synthesize(self.job, "test", self.output), self.job["proxy"])
        self.assertEqual(self.calls, [self.job["proxy"]])
        self.assertEqual(list(Path(self.temp.name).glob("*.partial")), [])

    async def test_all_failures_do_not_publish_partial_audio(self):
        self.behavior = {self.job["proxy"]: (0.01, False), None: (0.01, False)}
        with self.assertRaises(ConnectionError):
            await online.synthesize(self.job, "test", self.output)
        self.assertEqual(list(Path(self.temp.name).iterdir()), [])

    async def test_cancel_removes_all_partial_audio(self):
        self.behavior = {self.job["proxy"]: (5, True), None: (5, True)}
        pending = asyncio.create_task(online.synthesize(self.job, "test", self.output))
        await asyncio.sleep(0.3)
        pending.cancel()
        with self.assertRaises(asyncio.CancelledError):
            await pending
        self.assertEqual(list(Path(self.temp.name).iterdir()), [])


if __name__ == "__main__":
    unittest.main()
