"""P2 data adapter for the maintained deliver_readable entry. Reads actual runs only."""
from __future__ import annotations
import csv,json,shutil,statistics,zipfile,math,subprocess
from pathlib import Path
from urllib.parse import unquote,urlsplit
import deliver_readable as common


def latest_layouts(evidence,seeds):
    result={}
    for row in evidence["layouts"]:
        if row["seed"] in seeds:result[row["seed"]]=row
    return [result.get(seed,{"seed":seed,"status":"NOT_RUN","reason":"No actual generation record","platforms":[],"stairs":[]}) for seed in seeds]


def summarize(layouts,physics,seeds):
    success=[p for p in layouts if p["status"]=="SUCCESS"]
    platforms=[p for m in success for p in m["platforms"]]
    ranges=lambda values:[min(values),max(values)] if values else []
    return dict(inputs=len(seeds),success=len(success),rejected=sum(m["status"]=="REJECTED" for m in layouts),not_run=sum(m["status"]=="NOT_RUN" for m in layouts),
        semantic_maps=len({m["semanticHash"] for m in success}),normalized_shapes=len({p["normalizedShapeHash"] for p in platforms}),
        normalized_shape_sets=len({tuple(sorted(p["normalizedShapeHash"] for p in m["platforms"])) for m in success}),
        nonrectangular_platforms=sum(p["turns"]>4 and p["footprintArea"]<p["width"]*p["height"] for p in platforms),
        platform_count=ranges([len(m["platforms"]) for m in success]),root_count=ranges([m["roots"] for m in success]),
        max_elevations=sorted({m["maxElevation"] for m in success}),platform_area=ranges([p["footprintArea"] for p in platforms]),
        total_platforms=len(platforms),stairs=sum(len(m["stairs"]) for m in success),
        stair_sides={side:sum(s["side"]==side for m in success for s in m["stairs"]) for side in ("West","East")},
        physics_maps=len(physics),physics_stairs=sum(p["stairsTotal"] for p in physics),walked_stairs=sum(p["stairsWalked"] for p in physics),
        horizontal_trials=sum(p["horizontalTrials"] for p in physics),horizontal_blocked=sum(p["horizontalBlocked"] for p in physics),
        cliff_trials=sum(p["cliffTrials"] for p in physics),cliff_blocked=sum(p["cliffBlocked"] for p in physics),
        generation_ms_median=statistics.median(m["milliseconds"] for m in success) if success else None,
        generation_ms_max=max((m["milliseconds"] for m in success),default=None),
        attempts=sum(len(m.get("attempts",[])) for m in layouts),rejected_attempts=sum(a["status"]=="REJECTED" for m in layouts for a in m.get("attempts",[])))


def movement_evidence(path,identity):
    fields=None;previous=None;records=0;resets=0;max_step=0.;max_speed=0.;violations=[];dual={};identities=set()
    for line in path.read_text('utf-8-sig').splitlines():
        if line.startswith('#'):resets+=1;previous=None;continue
        row=next(csv.reader([line]))
        if row[0]=='trial':fields=row;continue
        if fields is None or len(row)!=len(fields):raise ValueError('Malformed movement CSV '+str(path))
        d=dict(zip(fields,row));records+=1;identities.add(d['identity'])
        if previous is not None and (d['generation'],d['trial'])==(previous['generation'],previous['trial']):
            dt=float(d['physicsTime'])-float(previous['physicsTime'])
            distance=math.hypot(float(d['footX'])-float(previous['footX']),float(d['footY'])-float(previous['footY']))
            max_step=max(max_step,distance)
            if dt>0:max_speed=max(max_speed,distance/dt)
            if distance>max(0,dt)*5+.06:violations.append(dict(trial=d['trial'],phase=d['trialPhase'],dt=dt,distance=distance))
        if d['trial'].startswith('S-') and d['trialPhase'] in ('UPHILL','DOWNHILL'):
            key=(d['generation'],d['rampId'],d['trialPhase']);dual.setdefault(key,0)
            if abs(float(d['moveX']))>.5 and abs(float(d['moveY']))>.5:dual[key]+=1
        previous=d
    return dict(status='PASS' if records and identities=={identity} and not violations and dual and min(dual.values())>0 else 'FAIL',
        records=records,explicitResetMarkers=resets,maxObservedStep=max_step,maxObservedSpeed=max_speed,
        unmarkedStepBound='distance <= 5 * actual physicsTime delta + .06 world units; reset markers split segments; serialized Warrior_Blue moveSpeed=5 and Ctrl boost is never fed',
        unmarkedStepViolations=len(violations),firstViolations=violations[:20],randomSlopeDirections=len(dual),minimumTwoComponentSamples=min(dual.values()) if dual else 0,identities=sorted(identities),
        scope='Actual CSV cross-check. A bounded finite movement observation; not manual acceptance or proof about unexecuted paths.')


def generate(source,output,release_path=None):
    source=source.resolve();output=output.resolve();release_file=source/(release_path or "release.json");release=common.read(release_file)
    marker=output/".p2-readable-delivery.json"
    if output.exists() and any(output.iterdir()) and not marker.is_file():raise ValueError("Refuse nonempty unrelated output")
    if output==source or output in source.parents:raise ValueError("Output must be a separate delivery directory")
    output.mkdir(parents=True,exist_ok=True);common.save(marker,{"identity":release["identity"],"source":str(source)})
    for name in ("Report","Checks","Evidence","Media"): (output/name).mkdir(exist_ok=True)
    provenance={}
    def copy(src,rel):
        src=Path(src);dst=output/rel;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,dst)
        provenance[str(rel).replace("\\","/")]={"sha256":common.sha(src),"source":str(src)}
    def copytree(src,rel):
        for p in sorted(Path(src).rglob("*")):
            if p.is_file():copy(p,Path(rel)/p.relative_to(src))
    plan=common.read(source/"implementation-plan.json");seeds=plan["seed_batch"]
    build=source/release["build"];build_report=common.read(build/"BuildReport.json")
    if build_report["identity"]!=release["identity"] or build_report["status"]!="PASS":raise ValueError("Build identity/status mismatch")
    copytree(build/"MVP_Windows_x64","MVP_Windows_x64")
    expected=common.read(build/"build-tree.json")
    if common.tree(output/"MVP_Windows_x64")!=expected:raise ValueError("Incomplete Player tree")
    for name in ("BuildReport.json","build-tree.json","build-inputs.json"):copy(build/name,"Evidence/Build/"+name)
    all_ids=[]
    for key,value in release["runs"].items():all_ids.extend(value if isinstance(value,list) else [value])
    evidence={};receipts={}
    for run_id in all_ids:
        root=source/"Runs"/run_id
        receipt=common.read(root/"receipt.json");receipts[run_id]=receipt
        if receipt["status"]!="PASS":raise ValueError("Selected run is not lifecycle PASS: "+run_id)
        for p in root.iterdir():
            if p.is_file():copy(p,Path("Evidence/Runs")/run_id/p.name)
        if (root/"Player").is_dir():
            evidence[run_id]=common.read(root/"Player/p2-evidence.json")
            if evidence[run_id]["identity"]!=release["identity"]:raise ValueError("Run Build identity mismatch")
            copytree(root/"Player",Path("Media")/run_id)
    # Preserve failed-run identity and logs, while excluding obsolete binaries and duplicate media.
    for root in sorted((source/"Runs").iterdir()):
        if not root.is_dir() or root.name in all_ids:continue
        for name in ("command.json","supervisor.json","receipt.json","unity.log","player.log","source-fingerprint.txt","preparation-layouts.json","guard-before.json","guard-after.json","precondition.json","results.xml"):
            if (root/name).is_file():copy(root/name,Path("Evidence/History")/root.name/name)
    for name in ("implementation-plan.json",):copy(source/name,name)
    copy(source/release.get("human_feedback","human-feedback.json"),"human-feedback.json")
    copy(release_file,"release.json")
    for p in (source/"TaskInput").glob("*.md"):copy(p,Path("Evidence/TaskInput")/p.name)
    for name in ("source-state.json","scope-verification.json","p2.delta.diff","integration-files.json","architecture-check.json","architecture-check.log","main-integration-baseline.json"):
        if (source/release.get("authoring","Authoring")/name).is_file():copy(source/release.get("authoring","Authoring")/name,Path("Evidence/Authoring")/name)
    full=evidence[release["runs"]["full"]];pilot=evidence[release["runs"]["pilot"]]
    layouts=latest_layouts(full,seeds);stats=summarize(layouts,full["physics"],seeds)
    replays=[]
    for seed in plan["determinism"]:
        rows=[]
        for run_id in release["runs"]["replays"]:
            matched=latest_layouts(evidence[run_id],[seed])[0]
            rows.append(dict(run=run_id,status=matched["status"],semanticHash=matched.get("semanticHash"),spatialSemanticHash=matched.get("spatialSemanticHash"),surfaceLegal=matched.get("surfaceWalkableCells"),surfaceReachable=matched.get("surfaceReachableCells"),attempt=matched.get("attempt")))
        replays.append(dict(seed=seed,repeats=rows,status="PASS" if len(rows)>=3 and all(r["status"]=="SUCCESS" for r in rows) and len({r["semanticHash"] for r in rows})==1 and ("cliff" not in release or (all(r.get("spatialSemanticHash") for r in rows) and len({r["spatialSemanticHash"] for r in rows})==1 and len({r["surfaceLegal"] for r in rows})==1 and all(r["surfaceLegal"]==r["surfaceReachable"] for r in rows))) else "FAIL"))
    movement={name:movement_evidence(output/"Media"/run_id/"movement.csv",release["identity"]) for name,run_id in (("pilot",release["runs"]["pilot"]),("full",release["runs"]["full"]))}
    common.save(output/"movement-validation.json",movement)
    for layout in layouts:
        layout["physics"]=next((p for p in full["physics"] if p["seed"]==layout["seed"]),None)
    model=dict(identity=release["identity"],build=build_report,source=str(source),selection=release["runs"],statistics=stats,layouts=layouts,replays=replays,
        pilot=dict(status=pilot["status"],base_cases=pilot["baseRegression"],physics=pilot["physics"]),
        lifecycle=receipts,movement=movement,files=provenance,user_visual_acceptance="NOT_RUN",user_operation_acceptance="NOT_RUN",formal_integration="NOT_RUN",formal_save_compatibility="NOT_RUN")
    common.save(output/"display-metadata.json",model);common.save(output/"seed-statistics.json",stats);common.save(output/"seed-layouts.json",layouts);common.save(output/"deterministic-replay.json",replays)
    fields=("seed","status","semanticHash","roots","maxElevation","milliseconds","attempt")
    with (output/"seed-list.csv").open("w",newline="",encoding="utf-8-sig") as f:
        writer=csv.DictWriter(f,fieldnames=fields,extrasaction="ignore");writer.writeheader();writer.writerows(layouts)
    render(output,model,plan,release)
    if "cliff" in release:
        import cliff_report
        cliff_report.augment(output,source,release,model,copy)
        common.save(output/"display-metadata.json",model)
    result=check(output);common.save(output/"Checks/delivery-check.json",result);return result


def generate_gifs(root,frames,identity,group="DerivedGIF",directory="GIF",require_clean=True,native_size=(1280,720)):
    import imageio_ffmpeg
    from PIL import Image
    ffmpeg=imageio_ffmpeg.get_ffmpeg_exe();evidence=root/'Evidence'/group;evidence.mkdir(exist_ok=True)
    media=root/'Media'/directory;media.mkdir(exist_ok=True);manifest=[]
    for case in dict.fromkeys(f['case'] for f in frames):
        selected=[f for f in frames if f['case']==case and (not require_clean or f['screenshot'].endswith('-clean.png'))]
        if not selected:raise ValueError('No actual clean frames for GIF '+case)
        concat=evidence/(case+'.ffconcat')
        concat.write_text('ffconcat version 1.0\n'+''.join("file '"+str((root/f['path']).resolve()).replace('\\','/')+"'\nduration 0.20\n" for f in selected),encoding='utf-8')
        output=media/(case+'-clean-sampled.gif')
        command=[ffmpeg,'-nostdin','-hide_banner','-loglevel','error','-y','-f','concat','-safe','0','-i',str(concat),'-filter_complex','[0:v]split[a][b];[a]palettegen[p];[b][p]paletteuse','-loop','0',str(output)]
        with (evidence/(case+'-ffmpeg.log')).open('w',encoding='utf-8') as log:
            process=subprocess.run(command,stdout=log,stderr=subprocess.STDOUT,timeout=60)
        if process.returncode!=0:raise ValueError('GIF assembly failed '+case)
        with Image.open(output) as image:
            count=image.n_frames;size=image.size
            if count<2 or size!=native_size:raise ValueError('Invalid native-size GIF '+case)
            for i in range(count):image.seek(i);image.load()
        manifest.append(dict(case=case,identity=identity,path=output.relative_to(root).as_posix(),sha256=common.sha(output),sourceFrames=[dict(path=f['path'],sha256=common.sha(root/f['path']),physicsTime=f['physicsTime'],utc=f['utc'],keys=f['keys']) for f in selected],sourceFrameCount=len(selected),gifFrames=count,nativeSize=list(size),displaySecondsPerSample=.20,
            scope='Actual unedited runtime PNG samples assembled as GIF; native size, 256-color GIF palette. Timing is review playback, not continuous realtime screen recording. Original PNGs/CSV retained for exact colors and capture times.'))
    common.save(evidence/'manifest.json',manifest);return manifest


def write_trajectories(root,run_id,directory="Trajectories",exclude_phases=(),regions_by_case=None):
    fields=None;segment=[];last_key=None;segments={};counts={}
    def flush():
        nonlocal segment,last_key
        if last_key and segment:segments.setdefault(last_key[0],[]).append((last_key[1],segment))
        segment=[];last_key=None
    for line in (root/'Media'/run_id/'movement.csv').read_text('utf-8-sig').splitlines():
        if line.startswith('#'):flush();continue
        row=next(csv.reader([line]))
        if row[0]=='trial':fields=row;continue
        d=dict(zip(fields,row));case=d['trial']
        if d['trialPhase'] in exclude_phases:flush();continue
        if not case.startswith('S-'):flush();continue
        key=(case,d['trialPhase'],d['generation'])
        if key!=last_key:flush();last_key=key
        segment.append((float(d['footX']),float(d['footY'])));counts[case]=counts.get(case,0)+1
    flush();dest=root/'Media'/directory;dest.mkdir(exist_ok=True)
    for case,parts in segments.items():
        paths=[]
        for phase,points in parts:
            color='#f58ed4' if 'UPHILL' in phase else '#93bfff' if 'DOWNHILL' in phase else '#ff9a6c' if 'NEGATIVE' in phase else '#d6e5ef'
            points_text=' '.join(('M' if i==0 else 'L')+f'{x:.3f},{-y:.3f}' for i,(x,y) in enumerate(points))
            paths.append(f'<path d="{points_text}" fill="none" stroke="{color}" stroke-width=".14" stroke-linecap="round"/>')
        grid=''.join(f'<path d="M{x},0 V-72" stroke="#293b4b" stroke-width=".05"/><text x="{x}" y="1.2" fill="#b5c6d9" font-size=".8">{x}</text>' for x in range(0,97,8))
        grid+=''.join(f'<path d="M0,{-y} H96" stroke="#293b4b" stroke-width=".05"/><text x="-1.8" y="{-y}" fill="#b5c6d9" font-size=".8">{y}</text>' for y in range(0,73,8))
        rock=''.join('<polygon points="'+' '.join(str(v['x'])+','+str(-v['y']) for v in region['vertices'])+'" fill="#63343c" stroke="#e78475" stroke-width=".06"/>' for region in (regions_by_case or {}).get(case,[]))
        svg='<svg xmlns="http://www.w3.org/2000/svg" viewBox="-2 -74 100 76"><title>'+common.e(case)+' actual foot trace; explicit resets split paths</title><rect x="-2" y="-74" width="100" height="76" fill="#101b27"/>'+grid+rock+''.join(paths)+'</svg>'
        (dest/(case+'.svg')).write_text(svg,encoding='utf-8')
    return counts


def render(root,model,plan,release):
    e=common.e;image=common.image;stats=model["statistics"];ident=model["identity"];full_id=release["runs"]["full"];pilot_id=release["runs"]["pilot"]
    intro=release.get("intro") or "上一版主要验证楼梯零件，这一版检查这些零件在随机地形中能否正确组合。高地形状、数量、分布、退台和楼梯位置来自 Seed 与配置。"
    stats_text=f"固定 {stats['inputs']} 个输入：成功 {stats['success']}，合法拒绝 {stats['rejected']}，未生成 {stats['not_run']}。每图 {stats['root_count'][0]}–{stats['root_count'][1]} 组高地、{stats['platform_count'][0]}–{stats['platform_count'][1]} 个平台；本批各图最高高程有 {'、'.join('H'+str(h) for h in stats['max_elevations'])}。{stats['total_platforms']} 个平台中 {stats['nonrectangular_platforms']} 个非矩形，平移归一化后有 {stats['normalized_shapes']} 种轮廓、{stats['normalized_shape_sets']} 组平台形状组合。"
    physics_text=f"完整批次真实物理：{stats['physics_maps']}/12 张图，{stats['walked_stairs']}/{stats['physics_stairs']} 处实际楼梯均往返；水平反例 {stats['horizontal_blocked']}/{stats['horizontal_trials']}，闭崖反例 {stats['cliff_blocked']}/{stats['cliff_trials']}。24 图共 {stats['stairs']} 处楼梯，其余 {stats['stairs']-stats['physics_stairs']} 处未做本批次往返物理；3 张小样另计，重复不相加。"
    guide="""1. 双击 `MVP_Windows_x64/MapVNextRandom.exe`，保留同目录全部文件。默认是真正随机地图 Seed 21193。
2. 在右上 Seed 输入框输入有符号整数，点 `Generate Seed` 复现本页地图；`Same Seed / G` 重新生成同一输入，`Next map / N` 生成下一输入。
3. `Tab / Overview` 在跟随游玩镜头和整图概览之间切换。出生点在左下角；先看概览找到平台及左右斜坡，再切回游玩。`R / Reset` 只回安全出生点，不代表读档恢复。
4. WASD 正常探索；西侧坡 W+D 上、S+A 下；东侧坡 W+A 上、S+D 下。只按 D/A 不能完整穿过高差；上层同平台 A/D 应正常。
5. `Base samples` 进入旧基础回归参考，N/P 切换；`Back to random` 回随机地图。旧夹具是参考，不计为新随机布局。
6. `F1 / Clean` 隐藏或恢复诊断界面；Esc 退出。H0–H4 表示地形高度，不是游戏楼层：蓝青草、绿、淡黄、中黄、褐草依次对应高度。"""
    normal="应沿可见坡面连续行走；接口下格边界按邻居接续；低地不因更高邻居多围一圈；真正向下或临水的边缘保留。"
    abnormal="如看到缺边、额外草边、悬空或不支持的转角，或遇到卡口、瞬移、只按 A/D 完整上楼、同平台横走被阻，请记录 Seed、平台/楼梯、按键和现象。"
    trace_counts=write_trajectories(root,full_id)
    cards=[]
    for m in model["layouts"]:
        seed=m["seed"];base=f"Media/{full_id}/Screenshots/S-{seed}";success=m["status"]=="SUCCESS"
        title=f"Seed {seed} · "+(f"{len(m['platforms'])} 个平台 / H0–H{m['maxElevation']} / {len(m['stairs'])} 处楼梯" if success else m["status"])
        parts=[f'<article class="sample" id="seed-{seed}" data-seed="{seed}" data-search="{e(title)}"><h3>{e(title)}</h3>']
        if success:
            parts.append(image(base+"-overview-clean.png","同一新 Player 的真实整图概览；相机拉远，适合判断分布"))
            parts.append(f'<p><strong>哪里不同：</strong>{m["roots"]} 组从低地独立起步的高地，平台面积 '+e([p["footprintArea"] for p in m["platforms"]])+f' 格；非矩形平台 {sum(p["turns"]>4 for p in m["platforms"])} 个。请看不同位置的凹凸轮廓和上层偏移。</p>')
            for side in ("West","East"):
                rel=base+f"-close-{side}-clean.png"
                if (root/rel).is_file():parts.append(image(rel,("西侧" if side=="West" else "东侧")+"代表性梯口，真实游玩尺度镜头；不代表已逐口人工审图"))
            parts.append('<details><summary>所有平台、楼梯与诊断原图</summary><table><tr><th>平台</th><th>父平台/高度</th><th>位置/大小</th><th>面积/转折</th></tr>')
            for p in m["platforms"]:parts.append(f'<tr><td>P{p["id"]}</td><td>P{p["parentId"]} / H{p["elevation"]}</td><td>({p["x"]},{p["y"]}) / {p["width"]}×{p["height"]}</td><td>{p["footprintArea"]} 格 / {p["turns"]}</td></tr>')
            parts.append('</table><p>P0 表示低地。以下为 MapData 坐标，帮助定位；未制作不可达装饰高地。</p><ul>')
            for s in m["stairs"]:parts.append(f'<li>P{s["parentId"]} → P{s["platformId"]}，H{s["lowerHeight"]}→H{s["upperHeight"]}，'+('西侧' if s['side']=='West' else '东侧')+'；低口 '+e(s['lower'])+'，高口 '+e(s['upper'])+'</li>')
            parts.append('</ul>'+image(base+'-overview-debug.png','真实诊断概览，含当前身份/输入；较小文字可点击原图')+'</details>')
            phy=m["physics"]
            if phy:
                parts.append('<details><summary>本图真实移动轨迹与所有到达平台检查帧</summary>'+image('Media/Trajectories/S-'+str(seed)+'.svg',f'由完整实际 CSV 的 {trace_counts["S-"+str(seed)]} 条脚底采样绘制；辅助轨迹图，不是游戏截图。粉色上坡、蓝色下坡、白色同层、橙色反例；显式重置处断开，不连成通行')+'<p>下面是逐个平台实际走到上层后的检查帧；单张停留图不能代替连续运动，完整过程以本 Run 的输入/位置 CSV 为依据。</p>')
                for link in sorted(m["stairs"],key=lambda z:z["platformId"]):
                    rel=f'Media/{full_id}/Motion/S-{seed}-P{link["platformId"]}-upper.png'
                    parts.append(image(rel,f'实际到达 P{link["platformId"]} / H{link["upperHeight"]}，原始 Player 检查帧'))
                parts.append('</details>')
            scope="本图全部声明平台及楼梯由真实自动输入往返通过；人工视觉和操作待确认。" if phy else "本图已生成并采集画面、逻辑全可达；本批次没有实际往返物理，人工视觉和操作待确认。"
            parts.append('<dl><dt>怎样打开、怎样走</dt><dd>在程序 Seed 框输入 '+str(seed)+'，点 Generate Seed。Tab 找平台；从左下出生点自由探索，按表中方向顺着斜坡到各层再返回。P0 出发，先走父平台，再走子平台。</dd><dt>正常</dt><dd>'+normal+'</dd><dt>异常</dt><dd>'+abnormal+'</dd><dt>自动证据与人工范围</dt><dd>'+scope+'</dd></dl>')
            parts.append(f'<p>可达格 {m["reachableCells"]}/{m["walkableCells"]}；尝试 {m["attempt"]+1} 次；语义指纹 <code>{m["semanticHash"]}</code>。'+('重试拒绝原因在 seed-layouts.json。' if m['attempt'] else '')+'</p>')
        else:parts.append('<p>'+e(m["reason"])+"；该输入保留分母，没有替换为漂亮 Seed 或固定夹具。</p>")
        parts.append('</article>');cards.append(''.join(parts))
    base_cards=[]
    for base_index,c in enumerate(model["pilot"]["base_cases"]):
        rel=f'Media/{pilot_id}/BaseRegression/{c["id"]}-clean.png'
        base_title=f'基础示例 {base_index+1:02d}：'+('西侧' if c['side']=='West' else '东侧')+f' H{c["lower"]} → H{c["upper"]} · 自动回归 '+c['status']
        base_cards.append('<details><summary>'+e(base_title)+'</summary><p>原案例 ID：<code>'+e(c['id'])+'</code></p>'+image(rel,'新 P2 Build 的基础回归；固定夹具参考，不是随机地图')+'<p>真实上下坡、暂停/逆向、水平反例和闭崖检查详见 p2-evidence.json 与 movement.csv；用户原 r3d 反馈没有扩大到本版。</p></details>')
    frames=[]
    with (root/f'Media/{pilot_id}/frames.csv').open(encoding='utf-8-sig',newline='') as f:
        for row in csv.DictReader(f):
            if row['screenshot'].startswith('Motion/S-'):
                row['path']=f'Media/{pilot_id}/'+row['screenshot'];frames.append(row)
    common.save(root/'Report/frame-evidence.json',frames)
    gifs=generate_gifs(root,frames,ident)
    gif_html='<details><summary>独立打开的真实原帧采样 GIF（3 张小样）</summary><p>每帧按0.20秒审阅，来自本次真实Player干净原帧；采样不是连续实时录屏。GIF有256色限制，精确配色请看保留的原始PNG；真实时间/按键见帧CSV与GIF来源清单。</p>'+''.join(image(g['path'],g['case']+' 原生1280×720采样GIF；点击原图可独立播放') for g in gifs)+'<p><a href="../Evidence/DerivedGIF/manifest.json">每帧来源、原始SHA、采集时间与按键</a></p></details>'
    replay_json=json.dumps(frames,ensure_ascii=False).replace('</','<\\/')
    script="""const allFrames=FRAME_DATA;let frames=allFrames;let timer=null,idx=0;const $=s=>document.querySelector(s);function show(i){if(!frames.length)return;idx=(i+frames.length)%frames.length;const f=frames[idx];$('#motion-image').src='../'+f.path;$('#motion-info').textContent=JSON.stringify(f,null,2);$('#scrub').value=idx;$('#frame-label').textContent=(idx+1)+' / '+frames.length+' · '+f.case;}$('#scrub').max=Math.max(0,frames.length-1);$('#scrub').oninput=e=>show(+e.target.value);$('#play').onclick=()=>{if(timer){clearInterval(timer);timer=null;$('#play').textContent='播放采样帧';}else{timer=setInterval(()=>show(idx+1),180);$('#play').textContent='暂停';}};$('#filter').oninput=e=>{let q=e.target.value.trim();document.querySelectorAll('article.sample').forEach(c=>c.hidden=!c.dataset.search.includes(q));};const cases=[...new Set(allFrames.map(f=>f.case))];$('#motion-seed').innerHTML=cases.map(c=>'<option>'+c+'</option>').join('');$('#motion-seed').onchange=()=>{frames=allFrames.filter(f=>f.case===$('#motion-seed').value);$('#scrub').max=Math.max(0,frames.length-1);show(0);};$('#motion-seed').onchange();""".replace('FRAME_DATA',replay_json)
    css="""*{box-sizing:border-box}body{margin:0;background:#11151d;color:#e9edf3;font:17px/1.7 'Microsoft YaHei',sans-serif}main{max-width:1240px;margin:auto;padding:32px 26px 80px}h1{font-size:34px;line-height:1.35}h2{margin-top:46px;color:#e1d0ff}h3{margin-top:8px}a{color:#b7d4ff}nav{display:flex;flex-wrap:wrap;gap:20px}.note{background:#242236;border-left:5px solid #b59bdc;padding:18px}article,details{background:#1b2230;border:1px solid #3a465a;border-radius:9px;padding:22px;margin:22px 0}article details{background:#171d29}figure{margin:20px 0}img{display:block;width:100%;height:auto;background:#05070b}figcaption{font-size:14px;color:#bfc8d7;margin-top:6px}table{border-collapse:collapse;width:100%;font-size:14px}td,th{border:1px solid #485164;padding:8px;text-align:left}input,button{font:inherit;padding:8px 14px;color:#f4f1fc;background:#303b50;border:1px solid #75849f;border-radius:5px}input[type=range]{width:100%}dt{font-weight:bold;color:#d5bbff}dd{margin:6px 0 16px}code,pre{font:13px/1.6 Consolas,monospace;overflow-wrap:anywhere;white-space:pre-wrap}pre{background:#10151f;padding:18px}li{margin-bottom:10px}[hidden]{display:none!important}"""
    intro_links='<nav><a href="../MVP_Windows_x64/MapVNextRandom.exe">完整 Windows MVP</a><a href="#maps">24 张随机地图</a><a href="#motion">真实移动采样</a><a href="#base">8 个基础回归</a><a href="#evidence">证据与限制</a></nav>'
    evidence_links='<ul>'+''.join('<li><a href="../'+rel+'">'+label+'</a></li>' for rel,label in [('technical-report.md','技术记录、复用链与合入差距'),('seed-layouts.json','全部语义布局、重试与楼梯连接'),('seed-statistics.json','统计与覆盖分母'),('deterministic-replay.json','3 Seed × 3 干净进程复现'),('movement-validation.json','原始输入、双轴移动与未标记跳变的交叉核对'),('human-feedback.json','原 r3d 人工反馈的实际范围'),('Evidence/Build/BuildReport.json','本版真实 Build 报告'),('Evidence/Authoring/source-state.json','源码身份与变化集合'),('Evidence/Authoring/scope-verification.json','冻结内容、主工程与范围保全')])+'</ul>'
    for run in (pilot_id,full_id):evidence_links+=f'<p>{e(run)}：<a href="../Media/{e(run)}/movement.csv">完整输入与移动 CSV</a> · <a href="../Media/{e(run)}/frames.csv">截图帧对应</a> · <a href="../Media/{e(run)}/regeneration-lifecycle.csv">重生成清理</a> · <a href="../Evidence/Runs/{e(run)}/supervisor.json">自然退出/清理/锁回执</a></p>'
    guide_html='<ol>'+''.join('<li>'+e(line.split('. ',1)[1]).replace('`','')+'</li>' for line in guide.splitlines())+'</ol>'
    html=f'<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>P2 随机高地 · 中文验收</title><style>{css}</style><main><h1>随机高地与东西楼梯：P2 候选验收</h1><p>{intro}</p>{intro_links}<p class="note">新 Build：{ident}。本页是实际运行结果；P2 人工视觉与操作均待确认，尚未正式集成、未启用主线默认。</p><h2>这一轮哪些地方变了</h2><p>{e(stats_text)}</p><p>{e(physics_text)}</p><p>平台自身轮廓比较先去掉整体平移；不同面积、转折和形状组合共同说明变化，不使用 Seed 标签或 GenerationId 充当地形变化。首版参数是候选默认，仍待体验调整。</p><h2>照着做即可试玩</h2>{guide_html}<h2 id="maps">全部 24 个预定输入</h2><p>成功、合法拒绝与未执行均保留。按 Seed 搜索：</p><input id="filter" aria-label="按 Seed 筛选" placeholder="例如 21193 或 -2026">'+''.join(cards)+f'<h2 id="motion">真实移动采样回放</h2>{gif_html}<p>来自三张试验地图的实际 Player 原帧，配有该帧真实按键、MoveInput、脚底和高程对应 CSV。采样间有间隔，不是连续录像；重置单独标记，不算通行。</p><label>小样地图 <select id="motion-seed" aria-label="选择移动采样地图"></select></label> <button id="play">播放采样帧</button><span id="frame-label"></span><input id="scrub" type="range" min="0" value="0" aria-label="移动帧"><img id="motion-image" alt="实际移动原始采样帧"><details><summary>本帧真实输入与位置</summary><pre id="motion-info"></pre></details><h2 id="base">新 Build 的基础回归参考</h2>'+''.join(base_cards)+f'<h2 id="evidence">证据、限制与反馈</h2>{evidence_links}<p class="note">原 r3d 人工通过只属已确认范围。旧 EditMode TIMEOUT、架构基线 BLOCKED、字体 FAIL/UNKNOWN 保留。独立代码审阅尚未执行；正式 Controller/Ready、战斗/生态接入和旧存档兼容未验证。商店 v3.2 已排除，无提交、合并、推送。</p><p>反馈请写：Seed；平台/梯口位置；按键；实际与期望；截图或现象。人工确认前不将视觉标为 PASS，不进入下一阶段。</p></main><script>{script}</script></html>'
    (root/'Report/index.html').write_text(html,encoding='utf-8')
    (root/'index.html').write_text('<!doctype html><meta charset="utf-8"><meta http-equiv="refresh" content="0;url=Report/index.html"><a href="Report/index.html">打开中文验收页</a>',encoding='utf-8')
    (root/'START-HERE.md').write_text('# 先看这里：真正随机高地 P2 候选\n\n'+intro+'\n\n[中文验收首页](Report/index.html) · [完整 Windows MVP](MVP_Windows_x64/MapVNextRandom.exe) · [试玩步骤](how-to-review.md)\n\nBuild：`'+ident+'`。请保留 Player 同目录全部文件。\n\n'+stats_text+'\n\n'+physics_text+'\n\nP2 人工视觉与操作均 NOT_RUN；正式集成和旧档兼容 NOT_RUN。原 r3d 已确认范围不重复索取；本轮只请检查新随机组合。商店 v3.2 已排除。没有提交、合入 main 或推送。\n',encoding='utf-8')
    (root/'how-to-review.md').write_text('# 随机地图怎么玩、怎么看\n\n'+guide+'\n\n'+normal+'\n\n'+abnormal+'\n\n[返回中文首页](Report/index.html)\n',encoding='utf-8')
    integration=release.get('integration_gaps','随机策略已输出 MapData；尚欠正式 Controller/GameplayReady、生态/导航/战斗适配、明确存档版本策略和新旧档回归。受控集成及 Git 写入须另获授权。')
    tech='# P2 技术记录与合入准备\n\nBuild：`'+ident+'`；实际源码指纹：`'+str(model['build']['sourceFingerprint'])+'`。\n\n'+stats_text+'\n\n'+physics_text+'\n\n## 复用关系\n\n| 功能 | 旧实现 | 本次实现 | 关系 | 原因 |\n|---|---|---|---|---|\n| 随机平台/侧梯 | MapElevationPlanner 固定两级南梯；Fixture 固定矩阵 | MapVNextRandomLayout | Necessary New Module | 原规划器不能表达本轮多组不规则平台和东西梯，仍输出同一 MapData |\n| 地形事实 | MapData | 同一实例 | Direct Reuse | 不增加高程权威 |\n| 投影/碰撞 | VNextProjection、StairGeometry、MapTilemapRenderer | 同路径 | Direct Reuse | 继承 r3d 接口与单 Collider |\n| 玩家/输入/脚圆 | GameInput → PlayerAction → Rigidbody2D，.30 Foot | 同实例与资产 | Direct Reuse | 不改移动合同 |\n| 基础夹具 | MapVNextFixture | 保留回归入口 | Intentional Compatibility | 不计随机成绩 |\n| Demo/报告/启动 | 原 VNextDemo、Build、run_r3、deliver_readable | P2 路由与数据适配 | Extension | 保留旧路径，不复制生命周期实现 |\n\n## 实际链与边界\n\nSeed/配置 → 临时宏格轮廓和层级 → 支撑/净空与一级侧梯候选 → MapData → 既有投影/一个 GeneratedCliffEdges → 正式玩家真实输入 → 按当前碰撞构造同层路径。临时规划集合退出后不承担运行时高度。每组高地目前为单条退台链，多组组成森林；尚无高层分叉、桥连群岛或装饰性不可达高地。所有声明可玩格逻辑可达；真实物理只按明确分母记录。\n\n## 确定性与耗时\n\n'+json.dumps(model['replays'],ensure_ascii=False,indent=2)+'\n\n完整批次生成耗时中位数 '+str(stats['generation_ms_median'])+' ms，最大 '+str(stats['generation_ms_max'])+' ms；实际重试 '+str(stats['rejected_attempts'])+' 次。来自本机 Unity 2022.3.62f3c1 Player，含约束规划，不含导入/Build；旧固定夹具无同口径微基准，因此不声称性能提高或普遍毫秒达标。物理时间倍率保留原 1，固定步长以原始 p2-evidence 为准。\n\n## 历史与当前问题\n\n首次请求 HEAVY_MUTEX_OWNED 未启动，后有 RectInt 编译失败与 SideCliffBypass 选址失败，均保留本机原件与包内日志索引；只根据实际原因修复，不换 Seed 或降低净空。旧 EditMode TIMEOUT、全图架构基线 BLOCKED、主字体 FAIL/UNKNOWN 未被本次通过记录覆盖。独立代码审阅 NOT_RUN，P2 用户审图/操作 NOT_RUN。\n\n## 预计合入清单与剩余差距\n\n'+integration+'\n\n具体来源/必要未跟踪 .meta/与主线文档交集见 Evidence/Authoring/integration-files.json 和现有候选 HANDOFF 的轻量章节。源码进入 main 与正式启用分开；均未获本次授权。禁止夹入 UI/怪物/字体、Artifacts、缓存、付费源图、正式存档或 Shop v3.2。旧 MainV1 路由与冻结身份保留；新布局独立候选身份，不宣称旧档兼容。\n\n原始数据：seed-layouts.json、deterministic-replay.json、Media/<Run>/movement.csv/frames.csv/regeneration-lifecycle.csv、Evidence/Runs/<Run>/supervisor.json、Build 及源码身份。主工程只读比较为采集时点，不代表远端已刷新。\n'
    (root/'technical-report.md').write_text(tech,encoding='utf-8')
    (root/'archive-locations.txt').write_text('本机原始 P2 证据：'+model['source']+'\n旧 r3d 完整目录与失败 Build 仍保留，不塞入本包、不删除。包内 JSON 的绝对路径是来源记录；核心验收只依赖包内相对文件。\n',encoding='utf-8')


def check(root):
    from PIL import Image
    root=root.resolve();model=common.read(root/'display-metadata.json');errors=[]
    for rel,info in model['files'].items():
        p=root/rel
        if not p.is_file() or common.sha(p)!=info['sha256']:errors.append('Missing/changed '+rel)
    if common.tree(root/'MVP_Windows_x64')!=common.read(root/'Evidence/Build/build-tree.json'):errors.append('Player tree mismatch')
    count=0;images=0
    for page in [root/'index.html',*sorted((root/'Report').glob('*.html'))]:
        parser=common.Links();parser.feed(page.read_text('utf-8'))
        for link in parser.links:
            count+=1;u=urlsplit(link)
            if u.scheme or u.netloc:errors.append('External dependency '+link);continue
            dest=(page.parent/unquote(u.path)).resolve() if u.path else page
            if not dest.is_relative_to(root) or not dest.is_file():errors.append('Broken/outside '+link)
            elif u.fragment and dest.suffix=='.html':
                other=common.Links();other.feed(dest.read_text('utf-8'))
                if unquote(u.fragment) not in other.ids:errors.append('Missing anchor '+link)
    for p in (root/'Media').rglob('*.png'):
        try:
            with Image.open(p) as im: im.verify();images+=1
        except Exception as ex:errors.append(str(p)+': '+str(ex))
    frames=common.read(root/'Report/frame-evidence.json')
    for row in frames:
        if row['identity']!=model['identity'] or not (root/row['path']).is_file():errors.append('Replay frame mismatch')
    if not frames:errors.append('No actual random movement frames')
    gifs=common.read(root/'Evidence/DerivedGIF/manifest.json')
    if len(gifs)!=3:errors.append('Missing required standalone sampled GIFs')
    for gif in gifs:
        if common.sha(root/gif['path'])!=gif['sha256'] or gif['identity']!=model['identity']:errors.append('GIF identity or bytes mismatch')
        with Image.open(root/gif['path']) as im:
            if im.n_frames!=gif['gifFrames']:errors.append('GIF frame mismatch')
            for i in range(im.n_frames):im.seek(i);im.load()
        for original in gif['sourceFrames']:
            if common.sha(root/original['path'])!=original['sha256']:errors.append('GIF source frame changed')
    for name,run_id in (("pilot",model['selection']['pilot']),("full",model['selection']['full'])):
        actual=movement_evidence(root/'Media'/run_id/'movement.csv',model['identity'])
        if actual!=model['movement'][name] or actual['status']!='PASS':errors.append('Movement cross-check failed '+name)
    if 'cliff' in model and any(r.get('status')!='PASS' for r in model['replays']):errors.append('Cliff terrain/spatial determinism failed')
    if 'cliff' in model:
        import cliff_report
        errors.extend(cliff_report.check(root,model))
    if model['user_visual_acceptance']!='NOT_RUN' or model['user_operation_acceptance']!='NOT_RUN':errors.append('Expanded human acceptance')
    result=dict(status='PASS' if not errors else 'FAIL',errors=errors,links=count,png_decoded=images,raw_files=len(model['files']),player_files=len(common.tree(root/'MVP_Windows_x64')),replay_frames=len(frames),sampled_gifs=len(gifs),browser='SEPARATE_RECEIPT_REQUIRED',semantic_readability_by_user='NOT_RUN')
    if 'cliff' in model:result.update(cliff_sampled_gifs=len(model['cliff']['gifs']),cliff_trials=len(model['cliff']['data']['trials']),placement_trials=len(model['cliff']['data']['placements']))
    return result


def portable(root,zip_path,extracted):
    root=root.resolve();zip_path=zip_path.resolve();extracted=extracted.resolve()
    if zip_path.exists() or extracted.exists():raise ValueError('Use fresh ZIP and extraction paths')
    expected=common.tree(root)
    with zipfile.ZipFile(zip_path,'w',zipfile.ZIP_DEFLATED,compresslevel=6,allowZip64=True) as z:
        for rel in expected:z.write(root/rel,rel)
    with zipfile.ZipFile(zip_path) as z:
        bad=z.testzip()
        if bad:raise ValueError('ZIP CRC '+bad)
        for name in z.namelist():
            if not (extracted/name).resolve().is_relative_to(extracted):raise ValueError('ZIP path traversal')
        z.extractall(extracted)
    actual=common.tree(extracted);result=check(extracted)
    result.update(zipSha256=common.sha(zip_path),zipBytes=zip_path.stat().st_size,files=len(expected),treeMatched=actual==expected,extraction=str(extracted))
    if actual!=expected:result['status']='FAIL';result['errors'].append('Extracted tree mismatch')
    common.save(zip_path.with_suffix('.verification.json'),result);return result
