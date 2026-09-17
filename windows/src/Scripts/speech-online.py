import asyncio
import json
import os
import sys
import edge_tts

async def render(job, text, output, index, proxy, delay=0):
    await asyncio.sleep(delay)
    temporary = output + ".route-" + str(index) + ".partial"
    try:
        speech = edge_tts.Communicate(
            text, job["voice"], rate=f'{job["rate"]:+d}%',
            pitch="+0Hz", proxy=proxy,
            connect_timeout=3, receive_timeout=7)
        await asyncio.wait_for(speech.save(temporary), timeout=10)
        return proxy, temporary
    except BaseException:
        if os.path.exists(temporary):
            os.remove(temporary)
        raise


async def synthesize(job, text, output, has_route=False, route=None):
    proxy = job.get("proxy")
    if proxy and not has_route:
        # A stale local proxy must not hold up a healthy direct connection.
        tasks = [asyncio.create_task(render(job, text, output, 0, proxy)),
                 asyncio.create_task(render(job, text, output, 1, None, 0.25))]
        pending = set(tasks)
        error = None
        try:
            while pending:
                done, pending = await asyncio.wait(pending, return_when=asyncio.FIRST_COMPLETED)
                for task in done:
                    try:
                        selected, temporary = task.result()
                    except Exception as failure:
                        error = failure
                        continue
                    for other in pending:
                        other.cancel()
                    await asyncio.gather(*pending, return_exceptions=True)
                    os.replace(temporary, output)
                    return selected
            raise error
        finally:
            for task in tasks:
                task.cancel()
            await asyncio.gather(*tasks, return_exceptions=True)
            for index in range(2):
                temporary = output + ".route-" + str(index) + ".partial"
                if os.path.exists(temporary):
                    os.remove(temporary)
    attempts = [route, proxy if route is None else None] if has_route else [None, None]
    for index, candidate in enumerate(attempts):
        try:
            selected, temporary = await render(job, text, output, index, candidate)
            os.replace(temporary, output)
            return selected
        except Exception:
            if index == len(attempts) - 1:
                raise
            await asyncio.sleep(0.1)


async def main():
    with open(sys.argv[1], encoding="utf-8-sig") as stream:
        job = json.load(stream)
    if "chunks" not in job:
        await synthesize(job, job["text"], job["output"])
        return
    route = None
    for index, text in enumerate(job["chunks"]):
        output = os.path.join(job["folder"], str(index) + ".mp3")
        route = await synthesize(job, text, output, index > 0, route)
        print("READY " + str(index), flush=True)

if __name__ == "__main__":
    try:
        asyncio.run(main())
    except Exception as error:
        print(type(error).__name__ + ": " + str(error), file=sys.stderr)
        sys.exit(1)
