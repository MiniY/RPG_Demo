"""Chinese offline Shop UI acceptance view over immutable local evidence.

Reuses the established readable-delivery fields, link parser and byte hashing.
No Unity launch, Git operation, feedback inference or runtime state authority.
"""
from pathlib import Path
import argparse
import html
import json
import re
import sys
from urllib.parse import unquote, urlsplit

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'MapVNext'))
from deliver_readable import REQUIRED, Links, sha, tree


def validate_model(model):
    errors = []
    if not model.get('build') or not re.fullmatch('[0-9a-f]{40}', model.get('sourceCommit', '')):
        errors.append('缺少实际 Build 或源码提交身份')
    if model.get('humanStatus') != 'PENDING_USER':
        errors.append('最终成品未经用户确认，人工状态必须待验收')
    seen = set()
    for unit in model['units']:
        if unit['id'] in seen:
            errors.append('重复验收项目')
        seen.add(unit['id'])
        for field in REQUIRED:
            if not isinstance(unit.get(field), str) or not re.search('[\u4e00-\u9fff]', unit[field]):
                errors.append(unit['id'] + ': 缺少中文 ' + field)
        if unit.get('humanStatus') != 'PENDING_USER':
            errors.append(unit['id'] + ': 缺少本轮用户确认，不能填人工通过')
        if unit.get('autoStatus') == 'PASS' and not (unit.get('autoSubject') and unit.get('autoScope')):
            errors.append(unit['id'] + ': 自动通过缺少主体或范围')
        if unit['kind'] != 'runtime' and unit.get('autoStatus') == 'PASS':
            errors.append(unit['id'] + ': 素材或历史诊断不能计为本轮运行通过')
        for media in unit.get('media', []):
            record = model['files'].get(media['path'])
            if not record or any(media.get(k) != record.get(k) for k in ['build', 'runId']):
                errors.append(unit['id'] + ': 媒体与实际 Build/Run 记录不符')
            if media.get('role') != 'historical-reference' and media.get('build') != model['build']:
                errors.append(unit['id'] + ': 历史媒体不能冒充当前画面')
            if record and record.get('sampledReplay') and '采样' not in media.get('caption', ''):
                errors.append(unit['id'] + ': 采样回放必须明确标识')
    return errors


def check(root):
    root = Path(root).resolve()
    model = json.loads((root / 'manifest.json').read_text('utf-8'))
    errors = validate_model(model)
    for name, record in model['files'].items():
        path = (root / name).resolve()
        if not path.is_relative_to(root) or not path.is_file() or sha(path) != record['sha256']:
            errors.append('缺少资源、越界或哈希变化: ' + name)
    if tree(root / 'MVP_Windows_x64') != model['playerTree']:
        errors.append('完整 Windows Player 文件树不一致')
    if not model['playerTree'] or not any(p.endswith('.exe') for p in model['playerTree']) or 'UnityPlayer.dll' not in model['playerTree']:
        errors.append('验收包不是完整 Windows Player')
    parser = Links()
    parser.feed((root / 'index.html').read_text('utf-8'))
    for unit in model['units']:
        rendered = parser.article_text.get(unit['id'], '')
        if any(unit.get(field, '') not in rendered for field in REQUIRED):
            errors.append(unit['id'] + ': 页面遗漏中文判据')
    for link in parser.links:
        url = urlsplit(link)
        destination = (root / unquote(url.path)).resolve() if url.path else root / 'index.html'
        if url.scheme or url.netloc or not destination.is_relative_to(root) or not destination.is_file():
            errors.append('离线外部依赖、越界或断链: ' + link)
        elif url.fragment and destination == root / 'index.html' and unquote(url.fragment) not in parser.ids:
            errors.append('不存在的页面锚点: ' + link)
    return dict(status='PASS' if not errors else 'FAIL', errors=errors, checkedFiles=len(model['files']),
                playerFiles=len(model['playerTree']), checkedLinks=len(parser.links),
                scope='文件完整性与中文呈现契约检查；人对效果与可读性的确认仍待验收', browserPreview='SEPARATE_RECEIPT_REQUIRED')


def render(model):
    escape = lambda value: html.escape(str(value), quote=True)
    cards = []
    labels = dict(title='项目', purpose='用途', look='看点', actions='操作', normal='正常标准', abnormal='异常标准', verification='证据范围')
    for unit in model['units']:
        content = ''.join('<p><b>' + labels[field] + '：</b>' + escape(unit[field]) + '</p>' for field in REQUIRED if field != 'title')
        media = []
        for item in unit.get('media', []):
            path, caption = escape(item['path']), escape(item['caption'])
            media.append('<figure><a href="' + path + '"><img src="' + path + '" alt="' + caption + '"></a><figcaption>' + caption + '</figcaption></figure>')
        cards.append('<article id="' + escape(unit['id']) + '"><h2>' + escape(unit['title']) + '</h2><p class="pending">本轮人工：PENDING_USER</p>' + content + ''.join(media) + '</article>')
    body = '''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>正式场景商店验收</title>
<style>body{margin:0;background:#131217;color:#f2eef7;font:17px/1.7 system-ui,"Microsoft YaHei"}main{max-width:1180px;margin:auto;padding:30px}article,header,section{background:#211e29;border:1px solid #554568;border-radius:12px;padding:24px;margin-bottom:22px}h1,h2,b{color:#d7b8ff}a{color:#cbb7ff}img{max-width:100%;height:auto}figure{margin:20px 0}.pending{background:#493d22;padding:10px}code{overflow-wrap:anywhere}details{padding:12px;background:#18161d}button{padding:10px;font:inherit}</style>
<main><header><h1>正式 SampleScene · 商店 UI 集成验收</h1><p>用途：检查已接入主工程的商店外观、商人待机和既有购买／背包链路。请先运行旁边的完整 Windows Player，再按下方清单操作。</p>
<p class="pending">本轮最终成品人工验收：PENDING_USER。历史 V3.3 视觉和 V3.4 功能确认只保留原版本范围。</p>
<p>从开始新冒险进入，WASD 靠近商人，F 打开商店，点击商品购买；Esc／关闭按钮关店，Tab 打开背包。按一次只成交一次，售罄后金额和数量不变，关店后应恢复移动。</p>
<p>截图由同一源码的显式测试变体采集，自动输入和四商人摆位属于测试夹具；普通验收 Player 不带该商店自动测试宏。动画是原始真实游戏帧的采样回放。</p>
<p><a href="START-HERE.md">中文启动说明</a> · <a href="manifest.json">完整身份与原件校验清单</a></p>
<details><summary>源码、自动检查与发布证据</summary><pre>IDENTITY</pre></details></header>CARDS
<section><h2>反馈方式</h2><p>请注明分辨率、界面、具体操作与预期／实际区别。视觉、交互、交易和保存可分别给结论；局部通过不会自动扩为完整系统通过。</p><p>私有素材包须另行持有，本地完整包不随公开 Git 分发；公开冷克隆需先执行精确私有资源恢复。</p></section></main></html>'''
    identity = {k: model[k] for k in ['build', 'sourceCommit', 'humanStatus', 'checks']}
    return body.replace('IDENTITY', escape(json.dumps(identity, ensure_ascii=False, indent=2))).replace('CARDS', ''.join(cards))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('operation', choices=['generate', 'check'])
    parser.add_argument('root', type=Path)
    args = parser.parse_args()
    if args.operation == 'generate':
        model = json.loads((args.root / 'manifest.json').read_text('utf-8'))
        errors = validate_model(model)
        if errors:
            raise RuntimeError(errors)
        (args.root / 'index.html').write_text(render(model), encoding='utf-8')
    result = check(args.root)
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result['status'] == 'PASS' else 2


if __name__ == '__main__':
    sys.exit(main())
