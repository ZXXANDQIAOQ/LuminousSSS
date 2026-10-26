#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""publish_release.py —— 把本地编译产物发布成 GitHub Release 附件。

为什么需要这个脚本：
    GitHub 连接器（MCP）只提供「读」release 的接口，创建 release 和上传附件
    必须走 REST API。附件上限 2GB/个，正好解决 APK 超仓库 100MB 限制的问题。

凭据从哪来（按优先级）：
    1. 环境变量 GITHUB_TOKEN / GH_TOKEN
    2. 工程根目录的 .github_token 文件（单行，已加进 .gitignore）
    token 只需要 fine-grained PAT，权限勾：Repository access = 该仓库，
    Contents = Read and write。不需要任何其它权限。

用法：
    # 创建 v0.22 的 release 并上传两个包
    python Tools/publish_release.py --tag v0.22 --name "v0.22 铁匠铺实体 + 烁的行走动画" \
        --asset release/LuminousSSS_v0.22_arm64_dev.apk

    # 只上传附件到一个已存在的 release
    python Tools/publish_release.py --tag v0.22 --asset release/另一个.apk --skip-create

    # 先看看会发生什么（不发请求）
    python Tools/publish_release.py --tag v0.22 --asset release/x.apk --dry-run
"""
import argparse
import json
import os
import re
import subprocess
import sys
import urllib.error
import urllib.parse
import urllib.request

REPO_SLUG = "ZXXANDQIAOQ/LuminousSSS"
API = "https://api.github.com"
UPLOAD = "https://uploads.github.com"
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# 这些内容类型让 GitHub 下载时以二进制处理，不会当成文本预览
CONTENT_TYPES = {
    ".apk": "application/vnd.android.package-archive",
    ".zip": "application/zip",
    ".aab": "application/octet-stream",
    ".exe": "application/vnd.microsoft.portable-executable",
}


def find_token():
    for key in ("GITHUB_TOKEN", "GH_TOKEN"):
        val = os.environ.get(key)
        if val and val.strip():
            return val.strip(), "环境变量 " + key
    path = os.path.join(ROOT, ".github_token")
    if os.path.isfile(path):
        with open(path, "r", encoding="utf-8-sig") as f:
            val = f.read().strip()
        if val:
            return val, ".github_token 文件"
    return None, None


def api_request(method, url, token, payload=None):
    """返回 (status, 解析后的 json 或原始文本)。网络异常直接抛。"""
    data = None
    headers = {
        "Authorization": "Bearer " + token,
        "Accept": "application/vnd.github+json",
        "X-GitHub-Api-Version": "2022-11-28",
        "User-Agent": "LuminousSSS-release-publisher",
    }
    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"

    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            body = resp.read().decode("utf-8", "replace")
            return resp.status, (json.loads(body) if body else None)
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", "replace")
        try:
            return e.code, json.loads(body)
        except Exception:
            return e.code, body


def get_release_by_tag(tag, token):
    status, body = api_request("GET", "%s/repos/%s/releases/tags/%s" % (API, REPO_SLUG, tag), token)
    if status == 200:
        return body
    if status == 404:
        return None
    raise RuntimeError("查询 release 失败 HTTP %s: %s" % (status, body))


def create_release(tag, name, notes, token, target="main"):
    status, body = api_request("POST", "%s/repos/%s/releases" % (API, REPO_SLUG), token, {
        "tag_name": tag,
        "name": name,
        "body": notes,
        "target_commitish": target,
        "draft": False,
        "prerelease": False,
    })
    if status not in (200, 201):
        raise RuntimeError("创建 release 失败 HTTP %s: %s" % (status, body))
    return body


def upload_asset(release_id, path, token, label=None):
    """用 curl 上传：标准库做 100MB+ 的流式 multipart 又慢又容易出岔子。"""
    name = os.path.basename(path)
    ext = os.path.splitext(name)[1].lower()
    ctype = CONTENT_TYPES.get(ext, "application/octet-stream")
    size = os.path.getsize(path)

    # 已经传过同名附件就跳过，方便重跑
    status, body = api_request(
        "GET", "%s/repos/%s/releases/%s/assets?per_page=100" % (API, REPO_SLUG, release_id), token)
    if status == 200 and isinstance(body, list):
        for a in body:
            if a.get("name") == name:
                print("  [跳过] 已存在同名附件 %s（%s）" % (name, a.get("browser_download_url")))
                return a

    cmd = [
        "curl", "-sS", "--fail-with-body",
        "--max-time", "1800",
        "-X", "POST",
        "-H", "Authorization: Bearer " + token,
        "-H", "Accept: application/vnd.github+json",
        "-H", "Content-Type: " + ctype,
        "--data-binary", "@" + path,
        "%s/repos/%s/releases/%s/assets?name=%s" % (UPLOAD, REPO_SLUG, release_id, name),
    ]
    if label:
        cmd[-1] += "&label=" + urllib.parse.quote(label)

    print("  [上传] %s（%.1f MB，%s）..." % (name, size / 1048576.0, ctype))
    proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if proc.returncode != 0:
        raise RuntimeError("上传 %s 失败（curl exit %s）: %s" % (name, proc.returncode, proc.stdout[:500]))

    try:
        return json.loads(proc.stdout)
    except Exception:
        raise RuntimeError("上传返回无法解析: %s" % proc.stdout[:500])


def guess_notes(tag):
    """没给 --notes 时，从 Docs/更新记录.md 里摘该版本段落。"""
    doc = os.path.join(ROOT, "Docs", "更新记录.md")
    if not os.path.isfile(doc):
        return ""
    ver = re.escape(tag.lstrip("vV"))
    with open(doc, "r", encoding="utf-8", errors="replace") as f:
        text = f.read()
    # 匹配 "## v0.22 — ..." 到下一个 "## v" 之间
    m = re.search(r"^##\s*v?%s\b.*?$(.*?)(?=^##\s*v?\d|\Z)" % ver, text, re.S | re.M)
    if not m:
        return ""
    body = m.group(0).strip()
    # GitHub Release 里标题行用 # 更清晰
    return body.replace("## ", "# ", 1)


def main():
    ap = argparse.ArgumentParser(description="发布 GitHub Release 并上传编译产物")
    ap.add_argument("--tag", required=True, help="tag 名，如 v0.22")
    ap.add_argument("--name", default=None, help="release 标题，默认与 tag 相同")
    ap.add_argument("--notes", default=None, help="发布说明；不给则从 Docs/更新记录.md 摘")
    ap.add_argument("--notes-file", default=None, help="从文件读发布说明（优先于 --notes）")
    ap.add_argument("--asset", action="append", default=[], help="要上传的文件，可重复")
    ap.add_argument("--target", default="main", help="tag 指向的分支（默认 main）")
    ap.add_argument("--skip-create", action="store_true", help="release 已存在，只传附件")
    ap.add_argument("--dry-run", action="store_true", help="只打印计划，不发请求")
    args = ap.parse_args()

    token, src = find_token()
    if not token and not args.dry_run:
        print("[错误] 找不到 GitHub token。")
        print("       任选其一：")
        print("       1) 把 fine-grained PAT 写进工程根的 .github_token（单行）")
        print("       2) 设置环境变量 GITHUB_TOKEN")
        print("       权限只需 Contents: Read and write（针对 " + REPO_SLUG + "）")
        return 2

    # 校验附件
    assets = []
    for a in args.asset:
        p = a if os.path.isabs(a) else os.path.join(ROOT, a)
        if not os.path.isfile(p):
            print("[错误] 找不到文件: " + p)
            return 2
        assets.append(p)
    if not assets:
        print("[错误] 至少给一个 --asset")
        return 2

    name = args.name or args.tag
    if args.notes_file:
        nf = args.notes_file if os.path.isabs(args.notes_file) else os.path.join(ROOT, args.notes_file)
        if not os.path.isfile(nf):
            print("[错误] 找不到说明文件: " + nf)
            return 2
        with open(nf, "r", encoding="utf-8") as f:
            notes = f.read()
    elif args.notes is not None:
        notes = args.notes
    else:
        notes = guess_notes(args.tag)

    print("仓库   : " + REPO_SLUG)
    print("tag    : " + args.tag + "  →  " + args.target)
    print("标题   : " + name)
    print("说明   : %d 字" % len(notes))
    print("凭据   : " + (src or "(dry-run，未读取)"))
    for p in assets:
        print("附件   : %s（%.1f MB）" % (os.path.basename(p), os.path.getsize(p) / 1048576.0))
    if args.dry_run:
        print("\n[dry-run] 未发送任何请求。")
        return 0

    release = get_release_by_tag(args.tag, token)
    if release:
        print("\n[1/2] release %s 已存在，复用（id=%s）" % (args.tag, release["id"]))
    else:
        if args.skip_create:
            print("[错误] --skip-create 但 %s 不存在" % args.tag)
            return 2
        print("\n[1/2] 创建 release ...")
        release = create_release(args.tag, name, notes, token, args.target)
        print("      ok  id=%s" % release["id"])

    print("\n[2/2] 上传附件 ...")
    for p in assets:
        info = upload_asset(release["id"], p, token)
        print("      ok  %s" % info.get("browser_download_url", "?"))

    print("\n完成：" + release.get("html_url", ""))
    print("下载页：https://github.com/%s/releases/tag/%s" % (REPO_SLUG, args.tag))
    return 0


if __name__ == "__main__":
    sys.exit(main())
