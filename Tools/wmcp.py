#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""wmcp.py —— 直连 Windows-MCP 的极简 MCP stdio 客户端。

用途：WorkBuddy 客户端没有把 windows-mcp 加载进会话时（信任表为空），
本脚本自己起一个 stdio 会话，按 MCP 协议调用桌面自动化工具。

用法：
    python Tools/wmcp.py list
    python Tools/wmcp.py call Screenshot '{"path":"shot.png"}'
    python Tools/wmcp.py batch '[["Click",{"x":100,"y":200}],["Screenshot",{}]]'

注意：每次调用都会起一个短命子进程，用完即退，不在系统里留进程。
"""
import json
import os
import subprocess
import sys
import threading

EXE = r"E:\Document\Windows-MCP\.venv\Scripts\windows-mcp.exe"
TIMEOUT = 60


class Client:
    def __init__(self):
        env = dict(os.environ)
        env["ANONYMIZED_TELEMETRY"] = "false"
        env["PYTHONUNBUFFERED"] = "1"
        self.p = subprocess.Popen(
            [EXE, "serve", "--transport", "stdio"],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            env=env,
        )
        self._id = 0
        self._buf = {}
        self._lock = threading.Lock()
        self._cv = threading.Condition(self._lock)
        threading.Thread(target=self._reader, daemon=True).start()
        self._request("initialize", {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "wmcp-py", "version": "1.0"},
        })
        self._notify("notifications/initialized")

    def _reader(self):
        for raw in self.p.stdout:
            line = raw.decode("utf-8", "replace").strip()
            if not line:
                continue
            try:
                msg = json.loads(line)
            except Exception:
                continue
            mid = msg.get("id")
            if mid is None:
                continue
            with self._cv:
                self._buf[mid] = msg
                self._cv.notify_all()

    def _send(self, obj):
        self.p.stdin.write((json.dumps(obj) + "\n").encode("utf-8"))
        self.p.stdin.flush()

    def _notify(self, method, params=None):
        o = {"jsonrpc": "2.0", "method": method}
        if params is not None:
            o["params"] = params
        self._send(o)

    def _request(self, method, params=None, timeout=TIMEOUT):
        with self._lock:
            self._id += 1
            mid = self._id
        o = {"jsonrpc": "2.0", "id": mid, "method": method}
        if params is not None:
            o["params"] = params
        self._send(o)
        deadline = timeout
        with self._cv:
            while mid not in self._buf:
                if not self._cv.wait(0.2):
                    deadline -= 0.2
                    if deadline <= 0:
                        raise TimeoutError(f"{method} 超时（{timeout}s）")
            return self._buf.pop(mid)

    def list_tools(self):
        r = self._request("tools/list")
        return r.get("result", {}).get("tools", [])

    def call(self, name, args=None, timeout=TIMEOUT):
        return self._request("tools/call", {"name": name, "arguments": args or {}}, timeout)

    def close(self):
        try:
            self.p.terminate()
            self.p.wait(timeout=5)
        except Exception:
            try:
                self.p.kill()
            except Exception:
                pass


def text_of(result):
    """把 tools/call 结果压成可读文本。"""
    if "error" in result:
        return f"[ERR] {json.dumps(result['error'], ensure_ascii=False)}"
    res = result.get("result", {})
    parts = []
    for c in res.get("content", []) or []:
        t = c.get("type")
        if t == "text":
            parts.append(c.get("text", ""))
        elif t == "image":
            parts.append(f"[image {c.get('mimeType','')} {len(c.get('data',''))}B base64]")
        else:
            parts.append(json.dumps(c, ensure_ascii=False)[:300])
    if res.get("isError"):
        parts.insert(0, "[isError]")
    return "\n".join(parts) if parts else json.dumps(res, ensure_ascii=False)[:800]


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 1
    cmd = sys.argv[1].lower()
    c = Client()
    try:
        if cmd == "list":
            for t in c.list_tools():
                print(f"{t['name']:<24} {t.get('description','')[:90]}")
            return 0
        if cmd == "schema":
            for t in c.list_tools():
                if t["name"].lower() == sys.argv[2].lower():
                    print(json.dumps(t, ensure_ascii=False, indent=2))
                    return 0
            print(f"没找到工具 {sys.argv[2]}")
            return 1
        if cmd == "call":
            args = json.loads(sys.argv[3]) if len(sys.argv) > 3 else {}
            print(text_of(c.call(sys.argv[2], args)))
            return 0
        if cmd == "shot":
            out = sys.argv[2] if len(sys.argv) > 2 else "shot.png"
            region = json.loads(sys.argv[3]) if len(sys.argv) > 3 else None
            args = {"region": region} if region else {}
            r = c.call("Screenshot", args)
            res = r.get("result", {})
            saved = 0
            for content in res.get("content", []) or []:
                if content.get("type") == "image":
                    import base64
                    with open(out, "wb") as f:
                        f.write(base64.b64decode(content["data"]))
                    saved += 1
                    print(f"[saved] {out}")
                elif content.get("type") == "text":
                    print(content["text"][:1200])
            if not saved:
                print("[warn] 没有图片内容，原始返回：")
                print(json.dumps(res, ensure_ascii=False)[:600])
            return 0
        if cmd == "batch":
            for name, args in json.loads(sys.argv[2]):
                out = text_of(c.call(name, args))
                print(f"--- {name} ---\n{out}")
            return 0
        print(f"未知命令 {cmd}")
        return 1
    finally:
        c.close()


if __name__ == "__main__":
    sys.exit(main())
