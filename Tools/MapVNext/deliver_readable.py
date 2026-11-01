"""Publish a readable layer over retained MapVNext evidence; never launches Unity.

Reuses deliver.py's case/gallery assembly and timeline.py's CSV-to-original-frame
binding. Those historical one-shot scripts and their r3d output stay immutable.
This is the maintained report entry; runtime height/content manifests stay owners.
"""
from __future__ import annotations

import argparse
import csv
from datetime import datetime
import hashlib
import html
import json
import pathlib
import re
import shutil
from html.parser import HTMLParser
from urllib.parse import unquote, urlsplit

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parents[1]
REQUIRED = ('title', 'purpose', 'look', 'actions', 'normal', 'abnormal', 'verification')


def read(p):
    return json.loads(pathlib.Path(p).read_text('utf-8-sig'))


def save(p, value):
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', 'utf-8')


def sha(p):
    h = hashlib.sha256()
    with pathlib.Path(p).open('rb') as stream:
        for block in iter(lambda: stream.read(1048576), b''):
            h.update(block)
    return h.hexdigest()


def tree(p):
    return {f.relative_to(p).as_posix(): sha(f) for f in sorted(p.rglob('*')) if f.is_file()}



def feedback_summary(feedback):
    """Derive the latest bounded view; legacy root fields remain the v1 snapshot."""
    event = feedback['events'][-1]
    return {'event_id': event['id'], 'build': event['build'],
            'reviewed_report_revision': event['report_revision'],
            'chinese_material_understanding': 'USER_UNDERSTOOD_SCOPED',
            'basic_connection_visual': 'USER_ACCEPTED_SCOPED',
            'height_grass_visual': 'USER_ACCEPTED_SCOPED',
            'visual_details': 'GROUP_BASIC_VISUAL_ONLY_NO_INDIVIDUAL_ANSWERS',
            'controlled_matrix': 'HISTORICAL_AUTO_RETAINED_NO_INDIVIDUAL_HUMAN_PASS',
            'human_operation': 'NO_NEW_HUMAN_PASS_R3D_AUTO_RETAINED',
            'random_layout': 'NOT_DELIVERED_OR_ACCEPTED_FOR_THIS_CANDIDATE',
            'formal_integration': 'UNCHANGED_NOT_VERIFIED',
            'p1_closed': False, 'p2_started': False}


def validate_feedback_append(feedback, model):
    errors = []
    events = feedback.get('events', [])
    if len(events) != 2 or not feedback.get('legacy_root_is_historical_snapshot'):
        return ['反馈追加必须保留原历史事件及其标记']
    if any(events[0].get(k) != feedback.get(k) for k in events[0]):
        errors.append('旧反馈事件被改写或脱离原快照')
    event = events[-1]
    if (event.get('build') != model['build'] or event.get('report_revision') != 'r3d-readable-v1'
            or event.get('source_type') != 'user_confirmation_quoted_in_requested_attachment'):
        errors.append('新反馈 Build、所看报告或用户来源不符')
    record = model['files'].get(event.get('source_copy'), {})
    if (not event.get('quote') or not event.get('scope') or not event.get('source_file')
            or not event.get('source_sha256') or record.get('sha256') != event.get('source_sha256')):
        errors.append('新反馈缺少原话、范围或原附件哈希绑定')
    try:
        synced = datetime.fromisoformat(event['synced_at_utc'])
        if synced.utcoffset() is None: raise ValueError('timezone missing')
    except (KeyError, ValueError, TypeError):
        errors.append('缺少带时区的实际同步时间')
    if event.get('user_reviewed_at', 'missing') is not None:
        errors.append('不得捏造用户精确审阅时间')
    accepted = ['chinese_material_understanding', 'basic_connection_visual', 'height_grass_visual']
    exclusions = ['individual_case_reviews', 'human_operation', 'random_layout', 'formal_integration',
                  'gameplay_ready', 'combat_height', 'save_compatibility', 'p1_closure', 'p2_implementation']
    if (event.get('accepted') != accepted or event.get('not_covered') != exclusions
            or event.get('individual_case_reviews') != [] or event.get('p1_closed') is not False
            or event.get('p2_started') is not False):
        errors.append('基础视觉确认被扩大为操作、随机、集成、逐例或阶段通过')
    if feedback.get('current') != feedback_summary(feedback):
        errors.append('最新汇总与有范围的追加反馈不一致')
    if model.get('p1_closed') is not False or model.get('p2_started') is not False:
        errors.append('本次反馈不得关闭 P1 或开始 P2')
    return errors


def status_rows():
    return [('中文材料可理解性', '用户已表示看懂；未扩大为所有页面/控件逐项测试'),
            ('基础东西侧楼梯—高地连接', '用户视觉通过；仅当前 r3d 专项基本连接与所见画面'),
            ('不同高程草地配色', '用户视觉通过；当前样例，继承 R17 色序'),
            ('低地草边等视觉细项', '整体基础视觉已通过；保留历史分项反馈，不补造逐个细项回答'),
            ('斜向通行与纯 A/D 阻挡', '保留原 r3d 自动证据；本次没有新增人工操作 PASS'),
            ('60 变种 / 12 Seed 受控矩阵', '保留原自动证据；没有新增逐例人工审阅'),
            ('随机高地与楼梯布局', '本专项未完成交付/验收；P2 仅登记，等待独立任务'),
            ('正式地图 / GameplayReady / 战斗高程 / 旧档兼容', '按原证据保留；本次未新增验证'),
            ('P1 / 后续阶段', 'P1 未关闭；P2 / F1 / 多风格未启动')]


def validate_model(model):
    """Check evidence semantics separately from readability judgements by a person."""
    errors = []
    build = model['build']
    feedback = model['feedback']
    expected = {
        'ISSUE01': 'PASS_SCOPED_TO_R3D_EXTENDED_MATRIX',
        'ISSUE02': 'AWAITING_USER_CONFIRMATION',
        'ISSUE03': 'AWAITING_USER_CONFIRMATION',
        'READABILITY': 'AWAITING_USER_CONFIRMATION',
    }
    if feedback.get('issues') != expected or feedback.get('p1_closed') is not False:
        errors.append('人工反馈范围被扩大或阶段被关闭')
    if not feedback.get('quote') or not feedback.get('source_file') or not feedback.get('source_sha256') or feedback.get('build') != build:
        errors.append('人工确认缺少原话、来源、哈希或适用 Build')
    if feedback.get('individual_case_reviews'):
        errors.append('分组反馈不能制造逐例人验')
    if 'events' in feedback:
        errors.extend(validate_feedback_append(feedback, model))
    seen = set()
    for unit in model['units']:
        if unit['id'] in seen:
            errors.append('重复案例 ' + unit['id'])
        seen.add(unit['id'])
        for name in REQUIRED:
            text = unit.get(name, '')
            if not isinstance(text, str) or not re.search('[\u4e00-\u9fff]', text):
                errors.append(unit['id'] + ': 缺少中文 ' + name)
        if unit.get('human_status') != 'AWAITING_REMAINING_ISSUES':
            errors.append(unit['id'] + ': 无逐例依据不能声称人工通过')
        if unit['kind'] != 'playable' and (unit.get('counts_for_traversal') or unit.get('auto_status') == 'PASS'):
            errors.append(unit['id'] + ': 素材/诊断混入通行')
        if unit.get('auto_status') == 'PASS' and not (unit.get('auto_subject') and unit.get('auto_scope')):
            errors.append(unit['id'] + ': 无主体或范围的 PASS')
        for media in unit.get('media', []):
            record = model['files'].get(media['path'])
            if not record or media['build'] != record.get('build') or media.get('run_id') != record.get('run_id'):
                errors.append(unit['id'] + ': 媒体来源或 Build 不符')
            if media.get('role') != 'before' and media['build'] not in (build, 'SOURCE_ASSET', 'USER_REFERENCE'):
                errors.append(unit['id'] + ': 旧图冒充当前 Build')
    playable = [u for u in model['units'] if u['kind'] == 'playable']
    if model['traversal_denominator'] != len(playable):
        errors.append('通行分母包含非可玩单元')
    return errors


class Links(HTMLParser):
    def __init__(self):
        super().__init__()
        self.links, self.ids = [], set()
        self.article_text, self.current_article = {}, None

    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if tag == 'article' and a.get('id'):
            self.current_article = a['id']
            self.article_text[self.current_article] = ''
        if 'id' in a:
            self.ids.add(a['id'])
        for name in ('href', 'src'):
            if a.get(name):
                self.links.append(a[name])
        if tag == 'span' and 'data-source' in a:
            self.links.append(a['data-source'])

    def handle_data(self, data):
        if self.current_article:
            self.article_text[self.current_article] += data

    def handle_endtag(self, tag):
        if tag == 'article':
            self.current_article = None


def check_package(root):
    model = read(root / 'display-metadata.json')
    errors = validate_model(model)
    for rel, info in model['files'].items():
        p = root / rel
        if not p.is_file() or sha(p) != info['sha256']:
            errors.append('资源缺失或原始哈希不符: ' + rel)
    expected = read(root / 'Evidence/r3d/build-tree.json')
    actual = tree(root / 'MVP_Windows_x64')
    if expected != actual:
        errors.append('完整 Player 文件树变化')
    count = 0
    for page in sorted((root / 'Report').glob('*.html')):
        parser = Links()
        parser.feed(page.read_text('utf-8'))
        if page.name == 'index.html':
            for unit in model['units']:
                actual_text = parser.article_text.get(unit['id'], '')
                for field in REQUIRED:
                    if unit.get(field, '') not in actual_text or not actual_text:
                        errors.append(unit['id'] + ': 页面没有呈现元数据字段 ' + field)
        for link in parser.links:
            count += 1
            url = urlsplit(link)
            if url.scheme or url.netloc:
                errors.append('离线页面包含外部依赖: ' + link)
                continue
            dest = (page.parent / unquote(url.path)).resolve() if url.path else page
            if not dest.is_relative_to(root.resolve()) or not dest.is_file():
                errors.append('断链或越界: ' + link)
            elif url.fragment and dest.suffix == '.html':
                target = parser
                if dest != page:
                    target = Links()
                    target.feed(dest.read_text('utf-8'))
                if unquote(url.fragment) not in target.ids:
                    errors.append('不存在的页面锚点: ' + link)
    # Replay data is inline so file:// needs no fetch/service. Verify every frame.
    for case, modes in read(root / 'Report/frame-evidence.json').items():
        for rows in modes.values():
            for row in rows:
                rel = 'Media/r3d/' + row['screenshot']
                if row['identity'] != model['build'] or rel not in model['files']:
                    errors.append('回放帧身份或资源缺失: ' + case)
    return {'status': 'PASS' if not errors else 'FAIL', 'errors': errors,
            'relative_links_checked': count, 'raw_files_checked': len(model['files']),
            'player_files': len(actual), 'units': len(model['units']),
            'semantic_readability_by_user': model['feedback'].get('current', {}).get('chinese_material_understanding', 'AWAITING_USER_CONFIRMATION'),
            'browser_preview': 'SEPARATE_RECEIPT_REQUIRED'}


def e(value):
    return html.escape(str(value), quote=True)


def image(rel, caption, alt=None):
    return f'<figure><a href="../{e(rel)}" target="_blank"><img loading="lazy" src="../{e(rel)}" alt="{e(alt or caption)}"></a><figcaption>{e(caption)} · 点击查看原图</figcaption></figure>'


def descriptions(u):
    names = [('purpose', '展示什么、为什么看'), ('look', '具体看哪里'),
             ('actions', '怎样操作'), ('normal', '正常表现'),
             ('abnormal', '发现这些请反馈'), ('verification', '目前验证到哪里')]
    return '<dl>' + ''.join(f'<dt>{label}</dt><dd>{e(u[key])}</dd>' for key, label in names) + '</dl>'


def generate(source, legacy, output, config_path, input_path):
    cfg = read(config_path)
    source, legacy, output = source.resolve(), legacy.resolve(), output.resolve()
    if output in (source, legacy) or output.is_relative_to(source) or output.is_relative_to(legacy):
        raise ValueError('Use a separate delivery directory; retained evidence is immutable')
    output.mkdir(parents=True, exist_ok=True)
    marker = output / '.readable-delivery'
    if not marker.exists() and any(p.name != 'Checks' for p in output.iterdir()):
        raise ValueError('Refuse to overwrite an unrelated directory')
    if marker.exists() and marker.read_text('utf-8') != cfg['revision']:
        raise ValueError('Report revision differs; use a new directory')
    marker.write_text(cfg['revision'], 'utf-8')
    manifest = read(source / 'case-manifest.json')
    runs = read(source / 'run-summary.json')
    assert manifest['identity'] == cfg['build'] == read(source / 'BuildReport.json')['identity']
    assert runs['player-r3d-full']['runId'] == cfg['player_run']
    assert runs['player-r3d-full']['naturalExit'] and runs['player-r3d-full']['exitCode'] == 0
    bindings = read(source / 'ReusedP0/height-binding.json')['levels']
    assert [x['family'] for x in bindings] == cfg['families']
    files = {}

    def take(src, rel, build, run_id='', role='evidence'):
        dst = output / rel
        digest = sha(src)
        if dst.exists() and sha(dst) != digest:
            raise ValueError('Existing copied evidence differs: ' + rel)
        dst.parent.mkdir(parents=True, exist_ok=True)
        if not dst.exists():
            shutil.copy2(src, dst)
        files[rel] = {'sha256': digest, 'original': str(src), 'build': build,
                      'run_id': run_id, 'role': role, 'reused': True}
        return {'path': rel, 'build': build, 'run_id': run_id, 'role': role}

    expected = read(source / 'build-tree.json')
    assert tree(source / 'MVP_Windows_x64') == expected, 'Source Player differs from actually played build'
    for rel in expected:
        take(source / 'MVP_Windows_x64' / rel, 'MVP_Windows_x64/' + rel, cfg['build'], cfg['player_run'], 'player')
    folders = ['Screenshots', 'Interfaces', 'Layers', 'Motion', 'Negatives', 'BeforeMotion', 'Trajectories', 'Videos']
    for folder in folders:
        for p in sorted((source / folder).rglob('*')):
            if p.is_file():
                take(p, 'Media/r3d/' + p.relative_to(source).as_posix(), cfg['build'], cfg['player_run'])
    for n in ['case-manifest.json', 'verdicts.json', 'run-summary.json', 'BuildReport.json', 'build-tree.json', 'movement.csv', 'frames.csv', 'approach-plans.csv', 'movement-validation.json', 'projection-role-validation.json', 'map-baseline-equivalence.json', 'timeline-validation.json', 'preservation-final.json', 'implementation-source-sha256.json', 'r3.delta.diff']:
        take(source / n, 'Evidence/r3d/' + n, cfg['build'], cfg['player_run'])
    # Preserve the old prose as raw text: its omitted historical links are not
    # offered as portable links in this focused package.
    take(source / 'report.md', 'Evidence/r3d/report-original.md.txt', cfg['build'])
    for run in ['play-r3d-approach', 'build-r3d-01', 'player-r3d-full', 'edit-r3-origin-fix']:
        for p in sorted((source / 'Runs' / run).rglob('*')):
            if p.is_file() and p.suffix.lower() in ('.json', '.xml', '.log', '.txt'):
                take(p, 'Evidence/r3d/Runs/' + run + '/' + p.relative_to(source / 'Runs' / run).as_posix(),
                     cfg['build'] if 'r3d' in run else 'r3-earlier-source', runs[run]['runId'])
    for name in ['height-binding.json', 'template-source-contract.json', 'input-fingerprint.json', 'runtime-import.json']:
        take(source / 'ReusedP0' / name, 'Evidence/P0/' + name, 'SOURCE_ASSET')
    take(input_path, 'Evidence/Input/' + input_path.name, 'USER_REFERENCE')
    if cfg.get('scope_note'):
        take(pathlib.Path(cfg['scope_report']), 'Evidence/ThisRevision/main-scope-report.json', 'DOCUMENT_SCOPE_AUDIT')
        take(pathlib.Path(cfg['scope_receipt']), 'Evidence/ThisRevision/main-scope-drift.json', 'DOCUMENT_SCOPE_AUDIT')
    previous = cfg['feedback']['previous']
    previous_path = pathlib.Path(previous['path'])
    assert sha(previous_path) == previous['sha256'], 'Historical feedback changed'
    feedback = read(previous_path)
    take(previous_path, 'Evidence/Feedback/readable-v1-human-feedback.json', 'USER_REFERENCE')
    take(pathlib.Path(previous['source_path']), 'Evidence/Input/' + feedback['source_file'], 'USER_REFERENCE')
    assert sha(pathlib.Path(previous['source_path'])) == feedback['source_sha256']
    event = dict(cfg['feedback']['append'], source_sha256=sha(input_path),
                 source_copy='Evidence/Input/' + input_path.name)
    assert input_path.name == event['source_file']
    assert event['quote'] in input_path.read_text('utf-8-sig'), 'User quote missing from actual input'
    feedback = dict(feedback, legacy_root_is_historical_snapshot=True,
                    events=[dict(feedback), event])
    feedback['current'] = feedback_summary(feedback)
    save(output / 'human-feedback.json', feedback)
    for reference in cfg['generation_sources']:
        src = REPO / reference['path']
        assert sha(src) == reference['sha256'], 'Static generation source changed'
        take(src, 'Evidence/Scope/' + src.name + '.txt', 'STATIC_SOURCE_REFERENCE')
    user_ref = take(source / 'TaskInput/Input/figure-03-user-handmade-templates.png',
                    'Media/References/figure-03-user-handmade-templates.png', 'USER_REFERENCE')
    for family in cfg['families']:
        take(REPO / f'Assets/_Resources/TinySwordsFreePack/Terrain/Tileset/Tilemap_color{family}.png',
             f'Media/Source/Tilemap_color{family}.png', 'SOURCE_ASSET')
    units = []
    for index, case in enumerate(manifest['cases']):
        chain = case['id'].startswith('S-')
        lower, upper = case['lower'], case['upper']
        if chain:
            title = f'固定地图 {case["seed"]}：从偏蓝草地逐级上到偏褐高地，再沿原路返回'
            actions = '沿地图上的四段连接依次往返；左侧坡 W+D / S+A，右侧坡 W+A / S+D。大地图 R 返回地图起点。'
        else:
            side = '左侧（西侧）' if case['side'] == 'West' else '右侧（东侧）'
            upper_colour = cfg['colours'][upper].removesuffix('草地')
            title = f'示例 {index+1:02}：从{cfg["colours"][lower]}走上{upper_colour}高地，{side}楼梯；{cfg["contexts"][case["context"]]}'
            actions = ('按 R 回到低地入口。上行 W+D，下行 S+A；只按 D 应挡住完整跨层。' if case['side'] == 'West' else '按 R 回到低地入口。上行 W+A，下行 S+D；只按 A 应挡住完整跨层。')
        media = []
        for kind in ['clean', 'debug']:
            if case.get(kind):
                rel = 'Media/r3d/' + case[kind]
                media.append({'path': rel, 'build': cfg['build'], 'run_id': cfg['player_run'], 'role': kind})
        if index < 8:
            before = legacy / 'Screenshots' / (case['id'] + '-clean.png')
            media.append(take(before, 'Media/r2/' + before.name, 'map-vnext-p1-stairs-20261028-r2', '', 'before'))
        units.append({'id': case['id'], 'title': title, 'kind': 'playable', 'index': index,
            'side': case['side'], 'context': case['context'], 'seed': case['seed'], 'lower': lower, 'upper': upper,
            'purpose': '检查四段相邻高度连接在组合地图中的实际往返。' if chain else '检查一段相邻高度连接的边界外观与实际上下楼。',
            'look': '沿每个高平台的外缘看低地是否多画草边；找到四个连接口，检查上口、下口和拐角。' if chain else '看斜坡接入高地的下端一格，以及低地贴到高平台脚下的接缝；真正朝外的草边应保留。',
            'actions': actions,
            'normal': '低地连续贴到高平台，真正外缘仍有边；沿斜坡同时横向、纵向移动后上楼，能原路返回。',
            'abnormal': '高平台脚下多围一圈低地草边，缺口断边，纯横移跨高差，或上下口卡住。',
            'verification': '复用 r3d 程序实际输入与物理轨迹：四段上下往返通过；本地图没有逐段水平负例。基础连接与按高程配色已有本专项分组视觉确认，中文材料已看懂；不是本地图逐段人工审阅，操作没有新增人工 PASS，随机布局未验收。' if chain else '复用 r3d 程序自动检查与实际移动轨迹；基础连接与按高程配色已有分组视觉确认，中文材料已看懂；没有制造本例独立人验或草边细项逐个回答，操作没有新增人工 PASS，随机布局未验收。',
            'auto_status': case['status'], 'auto_subject': 'r3d 历史程序检查',
            'auto_scope': '四段上行与原路下行' if chain else '双向上下楼与纯横移跨层阻挡',
            'human_status': 'AWAITING_REMAINING_ISSUES', 'counts_for_traversal': True,
            'media': media})
    for h, family in enumerate(cfg['families']):
        for side in ['West', 'East']:
            label = '左侧（西侧）' if side == 'West' else '右侧（东侧）'
            old = f'Media/r3d/Screenshots/Material-H{h}-{side}.png'
            units.append({'id': f'Material-H{h}-{side}', 'title': f'素材对照：{cfg["colours"][h]}，观察{label}外形',
                'kind': 'material', 'lower': h, 'family': family, 'side': side,
                'purpose': '辅助看素材颜色、草顶、正面岩壁与两侧肩片；不是一条完整可玩楼梯。',
                'look': f'观察网页模板的{label}肩片与草顶、正面岩壁接缝；整块模板保留两侧，不把观察侧说成独立几何。',
                'actions': '只看外观，无需操作；本项不计通行成绩。',
                'normal': '网页使用对应原素材色；草顶在上、岩壁在下，两侧肩片与手绘参考结构对应。网页拼贴是说明，实际通行看链接的可玩示例。',
                'abnormal': '旧十张图的大块绿色平台没有变色，局部露出的小色块不能代表完整五色模板；孤立岩块和截断边也不作为标准答案。',
                'verification': 'Codex 已打开十张旧图并对照生成源，登记旧展示缺陷。原素材与保存模板数据已核对；用户已表示新版材料看懂，不等于逐张素材卡或旧缺陷图独立通过。',
                'auto_status': 'NOT_APPLICABLE', 'human_status': 'AWAITING_REMAINING_ISSUES',
                'counts_for_traversal': False, 'known_media_issue': 'MATERIAL_SCREENSHOTS_UNSUITABLE',
                'media': [{'path': old, 'build': cfg['build'], 'run_id': cfg['player_run'], 'role': 'known_bad_showcase'},
                          {'path': f'Media/Source/Tilemap_color{family}.png', 'build': 'SOURCE_ASSET', 'run_id': '', 'role': 'source'}, user_ref]})
    model = {'revision': cfg['revision'], 'build': cfg['build'], 'player_run': cfg['player_run'],
             'source_evidence_root': str(source), 'legacy_evidence_root': str(legacy),
             'route': 'A', 'player_rebuilt': False, 'gameplay_changed': False, 'media_reused': True,
             'feedback': feedback, 'units': units, 'files': files,
             'traversal_denominator': len(manifest['cases']), 'p1_closed': False, 'p2_started': False}
    errors = validate_model(model)
    if errors:
        raise ValueError(errors)
    save(output / 'display-metadata.json', model)
    frames = list(csv.DictReader((source / 'frames.csv').open(encoding='utf-8')))
    timeline = {u['id']: {kind: [r for r in frames if r['case'] == u['id'] and r['screenshot'].startswith('Motion/') and r['screenshot'].endswith('-' + kind + '.png')] for kind in ['clean', 'debug']} for u in units[:8]}
    assert all(rows for modes in timeline.values() for rows in modes.values())
    (output / 'Report').mkdir(exist_ok=True)
    save(output / 'Report/frame-evidence.json', timeline)
    write_report(output, model, cfg, timeline, read(source / 'ReusedP0/template-source-contract.json'))
    check_path = output / 'Checks/delivery-check.json'
    if check_path.exists() and read(check_path).get('status') == 'FAIL':
        archived = output / ('Checks/failed-delivery-check-' + sha(check_path)[:12] + '.json')
        if not archived.exists():
            shutil.copy2(check_path, archived)
    # Bootstrap the linked receipt before validating links; a first generation
    # must work without relying on a previous run having created this file.
    save(check_path, {'status':'IN_PROGRESS','scope':'report structure, hashes and links'})
    receipt = check_package(output)
    save(check_path, receipt)
    if receipt['errors']:
        raise ValueError(receipt['errors'])
    return receipt


def template_board(unit, contract):
    h, family = unit['lower'], unit['family']
    group = next(g for g in contract['groups'] if g['cells'] == 19 and str(family) in g['colorFamilies'])
    cells = []
    for cell in group['localCells']:
        x, y, _ = cell['local']
        tile = cell['source']['tile']
        imported = next(t for t in contract['imported_tiles'] if t['tileGuid'] == tile['tileGuid'])
        assert imported['spriteFileId'] == int(tile['sprite']['fileId'])
        rect = imported['rect']
        if not rect:
            raise ValueError('Actual saved template sprite rect missing')
        px = float(rect['x']) / (576 - 64) * 100
        py = (384 - float(rect['y']) - 64) / (384 - 64) * 100
        cells.append(f'<span class="tile" data-source="../Media/Source/Tilemap_color{family}.png" style="grid-column:{x+1};grid-row:{5-y};background-image:url(../Media/Source/Tilemap_color{family}.png);background-position:{px}% {py}%" title="{e(tile.get("name", ""))}"></span>')
    return '<div class="board" role="img" aria-label="按保存模板坐标排布的原素材，网页说明拼贴，不是游戏截图">' + ''.join(cells) + '</div>'


def write_report(root, model, cfg, timeline, contract):
    contract = dict(contract, imported_tiles=read(root/'Evidence/P0/runtime-import.json')['tiles'])
    playable = [u for u in model['units'] if u['kind'] == 'playable']
    variants = [u for u in playable if u['id'].startswith('V')]
    maps = [u for u in playable if u['id'].startswith('S-')]
    contexts = len({(u['lower'], u['side'], u['context']) for u in variants})
    guide_html = '<ol class="guide">' + ''.join(f'<li><h3>{e(x["title"])}</h3><p>{e(x["text"])}</p></li>' for x in cfg['guide']) + '</ol>'
    guide_md = '\n\n'.join(f'{i+1}. **{x["title"]}**\n\n   {x["text"]}' for i, x in enumerate(cfg['guide']))
    feedback_html = '<textarea aria-label="可复制的分项反馈表" rows="12" readonly>' + e(cfg['feedback_form']) + '</textarea><button id="copy-feedback">复制反馈表</button><span id="copy-status" role="status"></span>'
    feedback_md = '```text\n' + cfg['feedback_form'] + '```'
    status = '| 项目 | 当前状态 |\n|---|---|\n' + '\n'.join('| '+name+' | '+value+' |' for name, value in status_rows())
    summary_html = '<table><thead><tr><th>项目</th><th>当前状态</th></tr></thead><tbody>' + ''.join('<tr><td>'+e(name)+'</td><td>'+e(value)+'</td></tr>' for name, value in status_rows()) + '</tbody></table>'
    scope_html = '<p>'+e(cfg['random_analysis'])+'</p><table><thead><tr><th>验收层次</th><th>含义</th><th>本版边界</th></tr></thead><tbody>' + ''.join('<tr><td>'+e(row['layer'])+'</td><td>'+e(row['meaning'])+'</td><td>'+e(row['status'])+'</td></tr>' for row in cfg['generation_scope']) + '</tbody></table>'
    scope_md = '# 样例变化与随机生成范围\n\n'+cfg['random_analysis']+'\n\n| 层次 | 含义 | 本版边界 |\n|---|---|---|\n' + '\n'.join('| '+r['layer']+' | '+r['meaning']+' | '+r['status']+' |' for r in cfg['generation_scope'])
    scope_md += '\n\n本轮只做静态调用核对，没有运行新地图。源码原件与 SHA：\n\n' + '\n'.join('- ['+r['path']+'](Evidence/Scope/'+pathlib.Path(r['path']).name+'.txt)；SHA-256 `'+r['sha256']+'`。' for r in cfg['generation_sources'])
    (root/'generation-scope.md').write_text(scope_md+'\n', 'utf-8')
    event = model['feedback']['events'][-1]
    feedback_scope = '确认所看报告：'+event['report_revision']+'；Player：'+event['build']+'。同步时间（不是试玩时间）：'+event['synced_at_utc']+'。用户原话：'+event['quote']
    cards = []
    for u in playable:
        clean = next(m['path'] for m in u['media'] if m['role'] == 'clean')
        travel = f'从刚启动开始：按 N {u["index"]} 次；或按 P {(len(playable)-u["index"])%len(playable)} 次。'
        cards.append(f'<article id="{e(u["id"])}" class="sample" data-side="{u["side"]}" data-context="{u["context"]}" data-search="{e(u["title"]+" "+u["id"]+" "+str(u["seed"]))}"><h3>{e(u["title"])}</h3><p class="sub">可玩样例 · 固定地图编号 {u["seed"]} · H{u["lower"]} 到 H{u["upper"]}</p>{image(clean, "原 r3d 实际运行干净画面，本次仅更新说明")}<p>{e(travel)}</p><p class="status">历史自动证据保留；基础视觉为分组确认，操作没有新增人验，随机布局未验收。</p><details><summary>看点、操作、判据和案例 ID</summary><p><code>{e(u["id"])}</code></p>{descriptions(u)}' + ''.join(f'<p><a href="../{e(m["path"])}">查看原始诊断图（带坐标）</a></p>' for m in u['media'] if m['role'] == 'debug') + '</details></article>')
    comparisons = []
    for u in playable[:8]:
        ident = u['id']
        before = next(m['path'] for m in u['media'] if m['role'] == 'before')
        after = f'Media/r3d/BeforeMotion/{ident}-clean.png'
        comparisons.append(f'<details class="comparison" id="compare-{ident}"><summary>{e(u["title"])}</summary><div class="pair">{image(before,"修复前：原 r2 Player；当时人工验收未通过")}{image(after,"修复后：原 r3d Player；接口下一格已有分组视觉确认")}</div>{descriptions(u)}<h4>楼梯接到高地的地方有没有缺边</h4><p>看图中斜坡接入平台处的下端一格：侧边应接上相邻边界，不能强行画成朝下终止的拐角。该项已有 r3d 扩展分组反馈，保留成果供对照。</p>{image("Media/r3d/Interfaces/"+ident+"-clean.png","接口局部：斜坡下端的侧边接缝，原 r3d 画面")}<details><summary>局部网格与上下层拆分（辅助诊断）</summary>' + image('Media/r3d/Interfaces/'+ident+'-grid.png', '网格用于定位格子，不是新的验收截图') + ''.join(image(f'Media/r3d/Layers/{ident}-{suffix}.png',label) for suffix,label in [('lower-only','低层与共享底图的绘制贡献；不是纯高程遮罩'),('upper-only','高平台投影的绘制贡献'),('composite','恢复所有图层后的合成画面')]) + '</details><h4>人物是不是沿斜坡走上去</h4><p>先看干净采样帧，按需要展开实际按键、坐标和物理轨迹。采样回放只展示保留的部分帧，不冒充连续实时录像。</p><div class="motion-player" data-case="'+ident+'"><p><button class="play">播放采样回放</button> <label>画面 <select class="mode"><option value="clean">干净原帧</option><option value="debug">轨迹诊断原帧</option></select></label></p><label class="scrub-label">采样帧 <input class="scrub" type="range" min="0" value="0"></label><a class="original" target="_blank"><img class="frame" alt="真实移动采样帧"></a><details><summary>本帧真实输入和位置（来自原 CSV）</summary><pre class="telemetry"></pre></details></div><details><summary>完整轨迹图、横移反例与原始数据</summary>'+image('Media/r3d/Trajectories/'+ident+'.svg','实际轨迹；图中旧 r2 红线只在原数据存在的案例显示')+'<p>纯 A/D 的反例有原始轨迹和部分截图，未保留连续反例录像；这里没有补造录像。</p><div class="negative-slots" data-case="'+ident+'"></div><a href="../Evidence/r3d/movement.csv">查看真实移动 CSV</a>；<a href="../Evidence/r3d/frames.csv">查看截图逐帧记录</a></details></details>')
    material = []
    for u in [u for u in model['units'] if u['kind'] == 'material']:
        h = u['lower']
        # Every source family appears as the upper material in a legal case except H0.
        match = next((c for c in variants if c['upper'] == h and c['side'] == u['side']), None)
        related = f'<a href="#{e(match["id"])}">查看用这种颜色作高地的可玩例：{e(match["title"].split("；")[0])}</a>' if match else '本项只有素材外形参考：H0 没有合法的更低草地作为上楼起点。<a href="#V00-H0-H1-West-OpenLanding">H0 作为低地参与的可玩示例 01</a>可另看。'
        material.append(f'<article id="{u["id"]}" class="material-card"><h3>{e(u["title"])}</h3><p class="status">辅助参考／不计通行成绩 · 只看外观，无需操作</p><p>候选高度 H{h}，原素材编号 color{u["family"]}。高度是地形级别，不是游戏楼层或绘制顺序。</p><div class="pair"><figure><a href="../Media/References/figure-03-user-handmade-templates.png" target="_blank"><img loading="lazy" src="../Media/References/figure-03-user-handmade-templates.png" alt="用户原手绘模板参考"></a><figcaption>左：用户原手绘参考（原图保留多组颜色与片段；不保证包含本色完整独立样板）</figcaption></figure><figure>{template_board(u,contract)}<figcaption>右：原素材按保存的 19 格模板坐标在网页排布；说明拼贴，非本轮新游戏截图。整块保留两侧，本卡请观察'+('左' if u['side']=='West' else '右')+f'肩片。</figcaption></figure></div>{descriptions(u)}<p>{related}</p><details><summary>原素材图、技术编号，以及旧展示为什么不能当标准答案</summary>'+image(f'Media/Source/Tilemap_color{u["family"]}.png',f'原素材 color{u["family"]}，未编辑像素')+image(f'Media/r3d/Screenshots/{u["id"]}.png','已知展示问题：旧图大块平台始终绿色，只有局部小块变色；不能作为完整五色或独立侧面展示，保留原件')+'<p>原生成入口 MaterialGallery 同时排两侧 19 格，循环内仅素材选择和标题变化。十图实际没有显示预期的五色切换，根因未经新 Unity 运行定位；本轮登记并用来源原图解释，没有修游戏规则。</p></details></article>')
    negative = {}
    for u in playable[:8]:
        negative[u['id']] = [p for p in model['files'] if p.startswith('Media/r3d/Negatives/') and u['id'] in p]
    css = (HERE / 'readable_style.css').read_text('utf-8')
    script = (HERE / 'readable_script.js').read_text('utf-8')
    template = (HERE / 'readable_template.html').read_text('utf-8')
    values = {'STYLE': css, 'SCRIPT': script, 'REVISION': e(model['revision']), 'BUILD': e(model['build']),
        'SCOPE_NOTE': ('<p class="warning">'+e(cfg['scope_note'])+' <a href="../Evidence/ThisRevision/main-scope-report.json">主项目原始范围检查（未通过）</a>；<a href="../Evidence/ThisRevision/main-scope-drift.json">变化记录与来源未知说明</a>。</p>') if cfg.get('scope_note') else '',
        'LATEST_STATUS': summary_html, 'GENERATION_SCOPE': scope_html, 'FEEDBACK_SCOPE': e(feedback_scope),
        'INTRO': e(cfg['intro']), 'COMPARISONS': '\n'.join(comparisons), 'CASES': '\n'.join(cards),
        'MATERIAL': '\n'.join(material), 'GUIDE': guide_html, 'FEEDBACK': feedback_html,
        'TIMELINE': json.dumps(timeline,ensure_ascii=False).replace('</','<\\/'),
        'NEGATIVES': json.dumps(negative,ensure_ascii=False), 'VARIANTS':str(len(variants)), 'MAPS':str(len(maps)), 'CONTEXTS':str(contexts)}
    for name, value in values.items():
        template = template.replace('@@'+name+'@@', value)
    assert not re.search(r'@@[A-Z]+@@', template)
    (root / 'Report/index.html').write_text(template, 'utf-8')
    (root/'START-HERE.md').write_text('# 先看这里：基础视觉已通过，随机布局尚未验收\n\n'+cfg['intro']+'\n\n先双击 [中文验收首页](Report/index.html)。三个主要入口：先看修复前后、查看全部样例、按说明试玩。若 Markdown 阅读器不支持链接，用文件夹打开 Report/index.html。\n\n'+status+'\n\n'+feedback_scope+'\n\n[样例变化与随机生成范围](generation-scope.md)\n\n素材区已登记旧十张图颜色未切换的问题；改用原素材与手绘参考解释，辅助参考不计通行成绩。\n\n'+f'报告修订：{model["revision"]}；Player：{model["build"]}。本轮只更新说明，原 Player 全目录与媒体均复用，未重新构建/运行 Unity 或 Player。\n\n[照着做的试玩步骤](how-to-review.md) · [技术记录与缺口](technical-report.md) · [本次人工反馈来源](human-feedback.json)\n\n停止点：已确认的基础视觉与中文可理解性不重复索取确认；操作人验及其余门禁保留原状态。随机布局尚未完成本次验收，P1 不关闭，P2/F1/多风格不启动，无提交、合并或推送。\n', 'utf-8')
    (root/'how-to-review.md').write_text('# 楼梯专项试玩：照着这份说明做\n\n'+f'适用 Player：{model["build"]}；说明修订：{model["revision"]}。本轮未重启 Player；按键依据原 r3d 实际输入记录、截图和当前源码逐项核对。\n\n'+cfg['intro']+'\n\n'+feedback_scope+'\n\n'+guide_md+'\n\n## 当前分项状态\n\n'+status+'\n\n## 可复制反馈表\n\n'+feedback_md+'\n\n[回到中文首页](Report/index.html)\n', 'utf-8')
    case_md = '# 全部样例中文索引\n\n前 8 个基础连接已经包含在前 60 个变种内；24 类是 4 组相邻高度×2侧×3种入口环境，并非另加 24 个案例。尺寸变体分布为 24/24/12。12 个固定地图另列，总共 72 个可玩场景。素材 10 项不加到分母。\n\n'
    for u in model['units']:
        case_md += f'## {u["title"]}\n\n原 ID：`{u["id"]}`；类型：'+('可玩样例' if u['kind']=='playable' else '素材辅助参考')+'。\n\n' + '\n\n'.join(f'**{name}**：{u[key]}' for key,name in [('purpose','用途'),('look','看哪里'),('actions','操作'),('normal','正常'),('abnormal','异常'),('verification','验证范围')])+'\n\n'
    (root/'case-index.md').write_text(case_md,'utf-8')
    (root/'technical-report.md').write_text('# 技术记录与尚未完成的工作\n\n路线 A：保留 r3d Player、地形、输入和原始媒体。所有历史运行成绩属于原 Run，本轮新执行的是报告生成、说明结构正反例、完整文件树与离线浏览器检查。\n\n'+f'- Build：`{model["build"]}`；报告：`{model["revision"]}`；原 Player Run：`{model["player_run"]}`。\n'+f'- [原始构建报告](Evidence/r3d/BuildReport.json)、[完整 Player 哈希树](Evidence/r3d/build-tree.json)、[逐 Run 生命周期](Evidence/r3d/run-summary.json)、[原始自动结果](Evidence/r3d/verdicts.json)。\n- [真实移动 CSV](Evidence/r3d/movement.csv)、[截图逐帧记录](Evidence/r3d/frames.csv)、[原始 r3d 报告文本](Evidence/r3d/report-original.md.txt)、[原 r2→r3d 代码差异](Evidence/r3d/r3.delta.diff)。\n- [当前分组人工反馈](human-feedback.json)：保留旧根字段为历史快照，events 追加本轮附件中的用户确认，current 汇总最新范围；绑定所看 v1 报告与 r3d Build，记录实际同步时间，不捏造试玩时间；不是新逐例试玩记录。原 verdicts 的 NOT_RUN 是当时快照，原字节保留。\n- [展示元数据与每个原件的来源/哈希](display-metadata.json)；[可执行交付检查](Checks/delivery-check.json)。\n\n旧 EditMode 78/78 但退出 TIMEOUT 属于较早 r3 源码；不能称为本轮新测试或精确 r3d 全量重测。原架构基线 BLOCKED 保留。正式 GameplayReady、战斗高程、生产全链配色和存档兼容均未获本专项验证；独立代码审阅未执行。\n\n素材旧十图的大块平台持续为绿色，只有中间小块随色组变化；不能构成完整五色模板或独立东西外形的有效展示；本轮只登记，不把错误图当标准，也不修改底层规则。网页原素材排布明确属于说明拼贴。\n\n本包省略旧失败 EXE、重复 RuntimeData、源码归档和完整架构归档，原件未删除。完整保留位置见 [历史归档位置与省略清单](archive-locations.txt)。包内核心图片、采样回放、Player 和说明离线完整；原始 JSON 中的旧绝对路径仅是证据来源记录。\n\n基础连接与按高程草色视觉已获用户确认，中文材料已看懂；操作无新增人工 PASS，随机布局本专项尚未完成交付/验收。P1 未关闭，P2/F1/多风格未启动。详见 generation-scope.md。无 Git 提交、合并或推送。\n','utf-8')
    with (root/'technical-report.md').open('a', encoding='utf-8') as stream:
        stream.write('\n## 本次确认与生成范围\n\n'+feedback_scope+'\n\n'+status+'\n\n[静态调用核对、源码原件与范围分层](generation-scope.md)；[原 v1 人工反馈原字节](Evidence/Feedback/readable-v1-human-feedback.json)。\n')
    source_root = pathlib.Path(model['source_evidence_root'])
    (root/'archive-locations.txt').write_text('以下是本机历史保留位置，不声称它们包含在本精简包中。\n原完整 r3d 证据：'+str(source_root)+'\n原完整 r3d ZIP：'+str(source_root.parent/'MapVNextStairs-20261028-r3d-Portable.zip')+'\n原 r2 证据：'+model['legacy_evidence_root']+'\n省略：RejectedBuilds 内早期失败 EXE、重复 RuntimeData、Source/BeforeR2Source、完整 Architecture/Assurance、非最终 Run 的重复媒体。所有旧原件继续保留。\n','utf-8')
    with (root/'archive-locations.txt').open('a', encoding='utf-8') as stream:
        stream.write('原中文 v1 包（原样保留）：'+str(pathlib.Path(cfg['feedback']['previous']['path']).parent)+'\n本轮不创建新 ZIP，完整 r3d Player 与核心媒体在本目录离线复用。\n')
    (root/'index.html').write_text('<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta http-equiv="refresh" content="0;url=Report/index.html"><title>打开中文验收页</title><a href="Report/index.html">打开中文验收页</a></html>','utf-8')
    if cfg.get('scope_note'):
        with (root/'technical-report.md').open('a',encoding='utf-8') as stream:
            stream.write('\n## 历史 v1 主项目范围检查（仍未解决）\n\n'+cfg['scope_note']+'\n\n[原始未通过报告](Evidence/ThisRevision/main-scope-report.json) · [变化记录](Evidence/ThisRevision/main-scope-drift.json)。\n')


def main():
    if len(__import__("sys").argv)>1 and __import__("sys").argv[1] in ("i1generate","i1check","i1portable"):
        import deliver_i1,sys
        sys.argv[1]=sys.argv[1][2:]
        return deliver_i1.main()
    ap = argparse.ArgumentParser(description=__doc__)
    sub = ap.add_subparsers(dest='command', required=True)
    gen = sub.add_parser('generate')
    gen.add_argument('--source', type=pathlib.Path, required=True)
    gen.add_argument('--legacy', type=pathlib.Path, required=True)
    gen.add_argument('--output', type=pathlib.Path, required=True)
    gen.add_argument('--config', type=pathlib.Path, default=HERE/'readable_delivery.json')
    gen.add_argument('--input', type=pathlib.Path, required=True)
    check = sub.add_parser('check')
    check.add_argument('root', type=pathlib.Path)
    p2 = sub.add_parser("p2generate")
    p2.add_argument("--source",type=pathlib.Path,required=True);p2.add_argument("--output",type=pathlib.Path,required=True)
    p2.add_argument("--release",type=str)
    pc = sub.add_parser("p2check");pc.add_argument("root",type=pathlib.Path)
    pp = sub.add_parser("p2portable");pp.add_argument("root",type=pathlib.Path);pp.add_argument("--zip",type=pathlib.Path,required=True);pp.add_argument("--extract",type=pathlib.Path,required=True)
    args = ap.parse_args()
    if args.command.startswith("p2"):
        import deliver_p2
        if args.command=="p2generate":result=deliver_p2.generate(args.source,args.output,getattr(args,"release",None))
        elif args.command=="p2portable":result=deliver_p2.portable(args.root,args.zip,args.extract)
        else:result=deliver_p2.check(args.root)
        print(json.dumps(result,ensure_ascii=False));return 0 if result["status"]=="PASS" else 2
    if args.command == 'generate':
        result = generate(args.source,args.legacy,args.output,args.config,args.input)
    else:
        result = check_package(args.root.resolve())
    print(json.dumps(result,ensure_ascii=False))
    return 0 if result['status']=='PASS' else 2


if __name__ == '__main__':
    raise SystemExit(main())
