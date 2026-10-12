#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
Luminous · 微光 —— Android APK 离线构建脚本

不依赖 Gradle / 网络：直接用 Android SDK build-tools 里的
aapt2 → javac → d8 → zipalign → apksigner 走完整流程。

用法:
    python android/build_apk.py            # 构建并签名（缺失密钥自动生成）
    python android/build_apk.py --debug    # 用 Android 调试密钥签名

可用环境变量覆盖路径:
    ANDROID_SDK / JAVA_HOME
"""

import os
import re
import shutil
import subprocess
import sys
import time
import zipfile

ROOT = os.path.dirname(os.path.abspath(__file__))            # android/
PROJECT = os.path.dirname(ROOT)                              # 仓库根
BUILD = os.path.join(ROOT, 'build')
ASSETS_WWW = os.path.join(ROOT, 'assets', 'www')
DIST = os.path.join(PROJECT, 'dist')

SDK = os.environ.get('ANDROID_SDK', r'D:\App\Android\Sdk')
JAVA_HOME = os.environ.get('JAVA_HOME', r'D:\App\Android\jdk11')
BUILD_TOOLS = os.path.join(SDK, 'build-tools', '34.0.0')
PLATFORM_JAR = os.path.join(SDK, 'platforms', 'android-34', 'android.jar')

PKG = 'com.zxxandqiaoqiao.luminoussss'
VERSION_NAME = '0.1'
VERSION_CODE = '1'
APK_NAME = f'LuminousSSS-v{VERSION_NAME}.apk'

KEYSTORE = os.path.join(ROOT, 'keystore', 'luminous.jks')
KEY_ALIAS = 'luminous'
KEY_PASS = 'luminous2026'

# 需要打进 assets/www 的网页资源
WEB_ITEMS = ['index.html', 'src', 'assets']


def log(msg):
    print(f'[build] {msg}', flush=True)


def run(cmd, **kw):
    log('> ' + ' '.join(str(c) for c in cmd[:3]) + (' ...' if len(cmd) > 3 else ''))
    env = dict(os.environ)
    env['JAVA_HOME'] = JAVA_HOME
    env['PATH'] = os.path.join(JAVA_HOME, 'bin') + os.pathsep + env.get('PATH', '')
    # 代理变量会让 java/aapt2 抽风，清掉
    for k in ('http_proxy', 'https_proxy', 'HTTP_PROXY', 'HTTPS_PROXY'):
        env.pop(k, None)
    r = subprocess.run(cmd, env=env, capture_output=True, **kw)
    out = r.stdout.decode('utf-8', 'replace') + r.stderr.decode('utf-8', 'replace')
    if r.returncode != 0:
        print(out)
        raise SystemExit(f'命令失败 ({r.returncode}): {cmd[0]}')
    return out


def tool(name):
    p = os.path.join(BUILD_TOOLS, name)
    if not os.path.exists(p):
        raise SystemExit(f'缺少构建工具: {p}')
    return p


def check_env():
    missing = [p for p in (PLATFORM_JAR, tool('aapt2.exe'), tool('d8.bat'), tool('apksigner.bat'))
               if not os.path.exists(p)]
    if missing:
        raise SystemExit('环境不完整，缺少:\n  ' + '\n  '.join(missing))
    if not os.path.exists(os.path.join(JAVA_HOME, 'bin', 'javac.exe')):
        raise SystemExit(f'找不到 JDK: {JAVA_HOME}')
    log(f'SDK   : {SDK}')
    log(f'JDK   : {JAVA_HOME}')


def sync_web_assets():
    """把根目录的网页游戏同步进 assets/www"""
    if os.path.exists(ASSETS_WWW):
        shutil.rmtree(ASSETS_WWW)
    os.makedirs(ASSETS_WWW, exist_ok=True)
    for item in WEB_ITEMS:
        src = os.path.join(PROJECT, item)
        dst = os.path.join(ASSETS_WWW, item)
        if os.path.isdir(src):
            shutil.copytree(src, dst)
        elif os.path.isfile(src):
            shutil.copy2(src, dst)
        else:
            raise SystemExit(f'网页资源缺失: {src}')
    total = sum(len(f) for _, _, f in os.walk(ASSETS_WWW))
    log(f'已同步网页资源 → assets/www（{total} 个文件）')


def clean():
    if os.path.exists(BUILD):
        shutil.rmtree(BUILD)
    os.makedirs(BUILD, exist_ok=True)


def compile_resources():
    res_zip = os.path.join(BUILD, 'res.zip')
    run([tool('aapt2.exe'), 'compile', '--dir', os.path.join(ROOT, 'res'), '-o', res_zip])

    base_apk = os.path.join(BUILD, 'base.apk')
    gen = os.path.join(BUILD, 'gen')
    os.makedirs(gen, exist_ok=True)
    run([
        tool('aapt2.exe'), 'link',
        '-o', base_apk,
        '-I', PLATFORM_JAR,
        '--manifest', os.path.join(ROOT, 'AndroidManifest.xml'),
        '-R', res_zip,
        '-A', os.path.join(ROOT, 'assets'),
        '--java', gen,
        '--min-sdk-version', '21',
        '--target-sdk-version', '34',
        '--version-code', VERSION_CODE,
        '--version-name', VERSION_NAME,
        '--auto-add-overlay',
    ])
    return base_apk


def compile_java():
    sources = []
    for base in (os.path.join(ROOT, 'java'), os.path.join(BUILD, 'gen')):
        for d, _, files in os.walk(base):
            sources += [os.path.join(d, f) for f in files if f.endswith('.java')]
    if not sources:
        raise SystemExit('没有找到 Java 源码')
    classes = os.path.join(BUILD, 'classes')
    os.makedirs(classes, exist_ok=True)
    src_list = os.path.join(BUILD, 'sources.txt')
    with open(src_list, 'w', encoding='utf-8') as f:
        f.write('\n'.join(sources))
    log(f'编译 Java：{len(sources)} 个源文件')
    run([
        os.path.join(JAVA_HOME, 'bin', 'javac.exe'),
        '-source', '1.8', '-target', '1.8', '-nowarn',
        '-bootclasspath', PLATFORM_JAR,
        '-encoding', 'UTF-8',
        '-d', classes,
        '@' + src_list,
    ])
    return classes


def make_dex(classes):
    dex_dir = os.path.join(BUILD, 'dex')
    os.makedirs(dex_dir, exist_ok=True)
    class_files = []
    for d, _, files in os.walk(classes):
        class_files += [os.path.join(d, f) for f in files if f.endswith('.class')]
    if not class_files:
        raise SystemExit('javac 没产出 class 文件')
    run([tool('d8.bat'), '--release', '--lib', PLATFORM_JAR,
         '--min-api', '21', '--output', dex_dir] + class_files)
    dex = os.path.join(dex_dir, 'classes.dex')
    if not os.path.exists(dex):
        raise SystemExit('d8 未生成 classes.dex')
    return dex


def pack(base_apk, dex):
    """把 classes.dex 塞进 aapt2 产出的 APK（保持其它条目的压缩方式）"""
    unsigned = os.path.join(BUILD, 'unsigned.apk')
    with zipfile.ZipFile(base_apk, 'r') as zin:
        with zipfile.ZipFile(unsigned, 'w') as zout:
            for item in zin.infolist():
                data = zin.read(item.filename)
                zout.writestr(item, data)  # 复用原 ZipInfo：保留 STORED/DEFLATED
            with open(dex, 'rb') as f:
                zout.writestr('classes.dex', f.read(), compress_type=zipfile.ZIP_DEFLATED)
    return unsigned


def sign(unsigned):
    os.makedirs(DIST, exist_ok=True)
    aligned = os.path.join(BUILD, 'aligned.apk')
    run([tool('zipalign.exe'), '-p', '-f', '4', unsigned, aligned])

    debug_mode = '--debug' in sys.argv
    if debug_mode:
        keystore = os.path.join(os.path.expanduser('~'), '.android', 'debug.keystore')
        if not os.path.exists(keystore):
            os.makedirs(os.path.dirname(keystore), exist_ok=True)
            run([os.path.join(JAVA_HOME, 'bin', 'keytool.exe'), '-genkeypair',
                 '-keystore', keystore, '-storepass', 'android', '-keypass', 'android',
                 '-alias', 'androiddebugkey', '-keyalg', 'RSA', '-keysize', '2048',
                 '-validity', '10000', '-dname', 'CN=Android Debug,O=Android,C=US',
                 '-storetype', 'JKS'])
        store_pass = key_pass = 'android'
        alias = 'androiddebugkey'
    else:
        keystore, alias = KEYSTORE, KEY_ALIAS
        store_pass = key_pass = KEY_PASS
        if not os.path.exists(keystore):
            os.makedirs(os.path.dirname(keystore), exist_ok=True)
            log('生成签名密钥（只生成一次，请勿删除，否则无法覆盖安装升级）')
            run([os.path.join(JAVA_HOME, 'bin', 'keytool.exe'), '-genkeypair',
                 '-keystore', keystore, '-alias', alias,
                 '-keyalg', 'RSA', '-keysize', '2048', '-validity', '10000',
                 '-storepass', store_pass, '-keypass', key_pass,
                 '-storetype', 'JKS',
                 '-dname', 'CN=LuminousSSS, OU=Game, O=LuminousSSS, L=Beijing, ST=Beijing, C=CN'])

    out_apk = os.path.join(DIST, APK_NAME)
    if os.path.exists(out_apk):
        os.remove(out_apk)
    run([tool('apksigner.bat'), 'sign',
         '--ks', keystore, '--ks-key-alias', alias,
         '--ks-pass', f'pass:{store_pass}', '--key-pass', f'pass:{key_pass}',
         '--v1-signing-enabled', 'true', '--v2-signing-enabled', 'true',
         '--out', out_apk, aligned])
    run([tool('apksigner.bat'), 'verify', '--print-certs', out_apk])
    return out_apk


def main():
    t0 = time.time()
    check_env()
    sync_web_assets()
    clean()
    base = compile_resources()
    classes = compile_java()
    dex = make_dex(classes)
    unsigned = pack(base, dex)
    apk = sign(unsigned)
    size = os.path.getsize(apk) / 1024 / 1024
    log(f'✅ 完成：{apk}（{size:.2f} MB，耗时 {time.time() - t0:.1f}s）')


if __name__ == '__main__':
    main()
