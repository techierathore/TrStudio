"""Loopback TCP forwarder: 127.0.0.1:5101 -> 192.168.1.12:5101 (AppManager on the Windows box).

Lets TrStudioApi use https://localhost:5101/ so the NFR-05 loopback-only
self-signed-cert relaxation applies, while AppManager actually runs on the LAN.
Pure byte relay - TLS passes through untouched.
"""
import asyncio

TARGET_HOST, TARGET_PORT, LISTEN_PORT = "192.168.1.12", 5101, 5101


async def pump(reader, writer):
    try:
        while True:
            data = await reader.read(65536)
            if not data:
                break
            writer.write(data)
            await writer.drain()
    except (ConnectionResetError, BrokenPipeError, asyncio.IncompleteReadError):
        pass
    finally:
        try:
            writer.close()
        except Exception:
            pass


async def handle(client_reader, client_writer):
    try:
        remote_reader, remote_writer = await asyncio.open_connection(TARGET_HOST, TARGET_PORT)
    except OSError:
        client_writer.close()
        return
    await asyncio.gather(pump(client_reader, remote_writer), pump(remote_reader, client_writer))


async def main():
    server = await asyncio.start_server(handle, "127.0.0.1", LISTEN_PORT)
    print(f"forwarding 127.0.0.1:{LISTEN_PORT} -> {TARGET_HOST}:{TARGET_PORT}", flush=True)
    async with server:
        await server.serve_forever()


asyncio.run(main())
