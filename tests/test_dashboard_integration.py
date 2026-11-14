"""Integration test: ASP.NET Core dashboard against simulated local device endpoints."""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import subprocess
import sys
import threading
import time
from urllib.error import URLError
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[1]
APP_URL = "http://127.0.0.1:5080"


def handler(name):
    class DeviceHandler(BaseHTTPRequestHandler):
        def do_GET(self):
            if self.path != "/info":
                self.send_error(404)
                return
            payload = json.dumps({
                "name": name,
                "address": "http://127.0.0.1",
                "metrics": {"Temperature": "24.5", "UptimeSeconds": "55"},
            }).encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(payload)))
            self.end_headers()
            self.wfile.write(payload)

        def log_message(self, fmt, *args):
            pass

    return DeviceHandler


def start_device(port, name):
    server = ThreadingHTTPServer(("127.0.0.1", port), handler(name))
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    return server, thread


def stop_device(server, thread):
    server.shutdown()
    server.server_close()
    thread.join(timeout=5)


def get_json(path):
    with urlopen(APP_URL + path, timeout=5) as response:
        assert response.status == 200
        return json.load(response)


def check_counts(online, offline):
    data = get_json("/api/dashboard")
    assert (data["totalDevices"], data["onlineDevices"], data["offlineDevices"]) == (2, online, offline), data
    assert sum(x["isOnline"] for x in data["devices"]) == online, data
    print(f"PASS: {online} online / {offline} offline")
    return data


def main():
    d1, t1 = start_device(1235, "Device1")
    d2, t2 = start_device(1237, "Device2")
    log_path = ROOT / "integration-server.log"
    with log_path.open("w", encoding="utf-8") as log:
        app = subprocess.Popen([
            "dotnet", "run",
            "--project", "SmartHomeDashboard/SmartHomeDashboard.csproj",
            "--configuration", "Release", "--no-build",
            "--no-launch-profile", "--urls", APP_URL,
        ], cwd=ROOT, stdout=log, stderr=subprocess.STDOUT)
        try:
            for _ in range(50):
                if app.poll() is not None:
                    raise AssertionError("Dashboard exited before ready")
                try:
                    assert get_json("/health")["status"] == "ok"
                    break
                except (URLError, TimeoutError, ConnectionError):
                    time.sleep(0.4)
            else:
                raise AssertionError("Dashboard did not become ready")

            with urlopen(APP_URL + "/", timeout=5) as response:
                assert "Smart Home" in response.read().decode("utf-8")
            with urlopen(APP_URL + "/app.js", timeout=5) as response:
                assert "onlineDevices" in response.read().decode("utf-8")
            with urlopen(APP_URL + "/styles.css", timeout=5) as response:
                assert response.status == 200

            data = check_counts(2, 0)
            assert {x["name"] for x in data["devices"]} == {"Device1", "Device2"}
            assert all(x["metrics"]["Temperature"] == "24.5" for x in data["devices"])
            assert len(get_json("/api/devices")) == 2

            stop_device(d2, t2)
            d2 = None
            check_counts(1, 1)

            stop_device(d1, t1)
            d1 = None
            check_counts(0, 2)
            print("All monitoring integration tests passed")
        finally:
            app.terminate()
            try:
                app.wait(timeout=5)
            except subprocess.TimeoutExpired:
                app.kill()
                app.wait(timeout=5)
            if d1 is not None:
                stop_device(d1, t1)
            if d2 is not None:
                stop_device(d2, t2)
            if app.returncode not in (0, -15, 143):
                print(log_path.read_text(encoding="utf-8"), file=sys.stderr)


if __name__ == "__main__":
    main()
