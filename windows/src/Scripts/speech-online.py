import asyncio
import json
import os
import sys
import edge_tts

async def main():
    with open(sys.argv[1], encoding="utf-8-sig") as stream:
        job = json.load(stream)
    configured_proxy = job.get("proxy")
    attempts = [configured_proxy, configured_proxy, None] if configured_proxy else [None, None, None]
    last_error = None
    for attempt, proxy in enumerate(attempts):
        try:
            if os.path.exists(job["output"]):
                os.remove(job["output"])
            speech = edge_tts.Communicate(
                job["text"], job["voice"], rate=f'{job["rate"]:+d}%',
                pitch="+0Hz", proxy=proxy)
            await asyncio.wait_for(speech.save(job["output"]), timeout=17)
            return
        except Exception as error:
            last_error = error
            if attempt < len(attempts) - 1:
                await asyncio.sleep(0.4 * (attempt + 1))
    raise last_error

if __name__ == "__main__":
    try:
        asyncio.run(asyncio.wait_for(main(), timeout=55))
    except Exception as error:
        print(type(error).__name__ + ": " + str(error), file=sys.stderr)
        sys.exit(1)
