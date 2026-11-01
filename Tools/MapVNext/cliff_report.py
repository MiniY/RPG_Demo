"""South-cliff evidence extension of the existing P2 generator; no Unity/lifecycle ownership."""
import csv,json,html,statistics
from pathlib import Path
import deliver_readable as common

def validate(data,surfaces):
    errors=[]
    if data.get('identity')!=surfaces.get('identity'):errors.append('Cliff Build mismatch')
    if data.get('status')!='PASS':errors.append('Cliff run not PASS')
    trials=data.get('trials',[]);placements=data.get('placements',[])
    if {r.get('seed') for r in trials}!={42,21193,7727,314159}:errors.append('Missing frozen cliff seeds')
    required={'LOWER_W_HOLD','UPPER_S_EDGE','ALONG_D','ALONG_A','DIAGONAL_WD','DIAGONAL_WA'}
    for seed in [42,21193,7727,314159]:
        rows=[r for r in trials if r.get('seed')==seed]
        if not required.issubset({r.get('shape') for r in rows}):errors.append('Missing physical directions '+str(seed))
        if len({r.get('region') for r in rows})<2:errors.append('Missing boundary variation '+str(seed))
    for r in trials:
        if r.get('status')!='PASS' or r.get('footIntruded') is not False or r.get('centerCovered') is not True or r.get('steps',0)<24:errors.append('Unverified footprint trial')
    kinds={'small-no-Rigidbody','large-static-box','trigger-drop-probe','pool-reactivation-probe'}
    for seed in [42,21193,7727,314159]:
        for kind in kinds:
            rows=[r for r in placements if r.get('seed')==seed and r.get('category')==kind]
            if len(rows)!=2 or {r.get('expectedAllowed') for r in rows}!={True,False}:errors.append('Missing placement pair '+str(seed)+' '+kind)
    for r in placements:
        if r.get('allowed')!=r.get('expectedAllowed') or r.get('active')!=r.get('expectedAllowed') or not r.get('reason'):errors.append('Placement gate mismatch')
    rows=surfaces.get('surfaces',[])
    if len(rows)!=24 or len({r.get('seed') for r in rows})!=24:errors.append('Missing 24-seed surface coverage')
    for r in rows:
        if r.get('surfaceLegal')!=r.get('surfaceReachable') or r.get('projectedFaceCoreChecks',0)<=0 or r.get('southFaces',0)<=0 or r.get('projectedFaceCoreChecks')!=r.get('southFaces'):errors.append('Surface/physics/readback mismatch')
    return errors

def augment(root,source,release,model,copy):
    config=release['cliff'];run=config['run'];data=common.read(root/'Media'/run/'cliff-evidence.json');full=common.read(root/'Media'/release['runs']['full']/'cliff-evidence.json')
    if not common.read(root/"Media"/run/"p2-evidence.json").get("playerSpawnValidatedBeforeActivation"):raise ValueError("Candidate Player published before terrain check")
    errors=validate(data,full)
    if errors:raise ValueError('; '.join(errors))
    author=source/release['authoring']
    for name in ['entity-consumers.json','task-input.md','main-rule-review-only.patch','cliff-r2.delta.diff','baseline.json','assurance-final.json','assurance.record.json','r1-red-runtime-equivalence.json','main-rule-patch-base.json']:
        if (author/name).is_file():copy(author/name,Path('Evidence/CliffAuthoring')/name)
    historical=source/"Runs/cliff-wall-r6-01"
    for name in ["legacy-band-contact-diagnosis.json","independent-rock-measurement.json"]:
        if (historical/name).is_file():copy(historical/name,Path("Evidence/History/cliff-wall-r6-01")/name)
    red=source/'Runs'/config['baselineRun']
    for p in (red/'Cliff').iterdir():
        if p.is_file():copy(p,Path('Evidence/OriginalR1SourceProbe')/p.name)
    for name in ['receipt.json','results.xml','command.json','source-fingerprint.txt']:
        copy(red/name,Path('Evidence/OriginalR1SourceProbe')/name)
    copy(source.parents[2]/"Assets/Scripts/MapGeneration/VNext/MapVNextDemo.Cliff.cs","Evidence/CliffAuthoring/MapVNextDemo.Cliff.cs")
    # The original r1 EXE never supported this new probe. Keep that provenance explicit.
    feedback=common.read(root/"human-feedback.json")
    if feedback.get("current",{}).get("build")!=model["identity"] or feedback["current"].get("visual")!="NOT_RUN" or feedback["current"].get("operation")!="NOT_RUN":raise ValueError("Wrong Build or expanded human acceptance")
    baseline=common.read(root/'Evidence/OriginalR1SourceProbe/reproduction.json')
    if baseline.get('build')!='map-vnext-p2-random-20261029-r1' or not baseline.get('intrusion'):raise ValueError('Missing actual red baseline')
    import cliff_motion
    motion=cliff_motion.measure(root/"Media"/run)
    if motion["status"]!="PASS":raise ValueError("Independent cliff CSV/geometry measurement failed: "+str(motion))
    common.save(root/"cliff-motion-validation.json",motion)
    approach=cliff_motion.measure(root/"Media"/release["runs"]["full"],None)
    if approach["status"]!="PASS" or approach["skippedGenerationSamples"]:raise ValueError("Actual full-batch approach/stair feet entered rock or lacked geometry: "+str(approach))
    common.save(root/"full-motion-rock-validation.json",approach)
    consumers=common.read(author/'entity-consumers.json');common.save(root/'entity-consumers.json',consumers)
    replay=common.read(root/'Media'/run/'r1-collision-replay.json')
    if replay.get('actualBuild')!=model['identity'] or replay.get('mode')!='DIAGNOSTIC_RECONSTRUCTION_NOT_ORIGINAL_EXE' or not replay.get('expectedIntrusion'):raise ValueError('Unlabelled baseline reconstruction')
    common.save(root/'cliff-validation.json',dict(status='PASS',physicalTrials=len(data['trials']),physicsSteps=sum(r['steps'] for r in data['trials']),placementTrials=len(data['placements']),surfaceSeeds=len(full['surfaces']),originalCropSeed='UNKNOWN',originalR1BinaryProbe='NOT_RUN',r1SourceEquivalentProbe='FAIL_EXPECTED',sameCameraComparison='R1_COLLISION_RECONSTRUCTION_IN_NEW_BUILD',human='NOT_RUN'))
    old_layouts=common.read(source/"Delivery-r1/seed-layouts.json")
    old_by_seed={r["seed"]:r for r in old_layouts}
    common.save(root/"r1-layout-comparison.json",[dict(seed=r["seed"],oldTerrainHash=old_by_seed[r["seed"]]["semanticHash"],newTerrainHash=r["semanticHash"],terrainUnchanged=old_by_seed[r["seed"]]["semanticHash"]==r["semanticHash"],newSpatialHash=r.get("spatialSemanticHash"),oldLogicalCells=old_by_seed[r["seed"]]["walkableCells"],newSurfaceLegal=r.get("surfaceWalkableCells")) for r in model["layouts"]])
    model['cliff']=dict(run=run,data=data,surfaces=full['surfaces'],originalBaseline=baseline,replay=replay,consumerIntegration='CANDIDATE_ONLY')
    current=release.get("current_contracts")
    if current:
        common.save(root/"current-contracts.json",current)
        model["currentContracts"]=current
    common.save(root/"manifest.json",dict(kind="LOCAL_ACCEPTANCE_ARTIFACT_INDEX_NOT_GAME_CONTENT_MANIFEST",identity=model["identity"],status="AUTOMATED_DATA_READY_USER_PENDING",displayMetadata="display-metadata.json",build="Evidence/Build/BuildReport.json",playerTree="Evidence/Build/build-tree.json",sourceScope="Evidence/Authoring/scope-verification.json",human="human-feedback.json",currentTests=release.get("current_contracts",{"status":"NOT_RUN"}),formalDefaultChanged=False,mainRuleSync="PENDING_PATCH"))
    # Separate original PNGs are linked side by side; pixels are never rewritten into annotations.
    e=html.escape
    def image(rel,caption):return common.image(rel,caption)
    first=next(r for r in data['trials'] if r['seed']==21193 and r['shape']=='LOWER_W_HOLD' and r['region']==replay['region'])
    comparisons='<div class="cliff-comparison">'+image(f'Media/{run}/Comparison/S-21193-r1-collision-replay-clean.png','旧碰撞的独立诊断重现：新 Build 内恢复 r1 未改动的碰撞路径前缀，允许复现原错误；不是旧 EXE 新截图')+image(f'Media/{run}/'+first['clean'],'修复后：同 Seed、同镜头、正式脚圆和 W 输入；脚停在下方地面')+'</div>'
    import math
    shifts=[math.hypot(r["start"]["x"]-r["requestedStart"]["x"],r["start"]["y"]-r["requestedStart"]["y"]) for r in data["trials"]]
    common.save(root/"cliff-setup-adjustments.json",dict(adjusted=sum(v>0.00001 for v in shifts),maxWorldShift=max(shifts),candidateLimit=4,scope="Only explicit separate-trial setup, same surface height; no relocation during movement."))
    common.save(root/"boundary-band-contacts.json",dict(samples=sum(r.get("boundaryClearanceContactSamples",0) for r in data["trials"]),scope="Original narrow cliff-edge safety bands; recorded separately from rock-area intrusion. Rock tolerance remains .002."))
    info=f"本次岩壁实测 {len(data['trials'])}/{len(data['trials'])} 组，共 {sum(r['steps'] for r in data['trials'])} 个受检查物理采样；4 Seed 的 {len(data['placements'])} 次放置/激活尝试。原24个 Seed 的真实投影核心读回和可玩集合连通检查保留，下表给出新分母。"
    surface='<table><thead><tr><th>Seed</th><th>原逻辑可走格</th><th>0.31导航净空合法格心</th><th>本版可达节点</th><th>岩壁</th><th>真实投影核心</th></tr></thead><tbody>'+''.join('<tr>'+''.join('<td>'+str(r[k])+'</td>' for k in ['seed','logicalWalkable','surfaceLegal','surfaceReachable','southFaces','projectedFaceCoreChecks'])+'</tr>' for r in full['surfaces'])+'</tbody></table>'
    import deliver_p2
    backgrounds={"S-"+str(seed):common.read(root/"Media"/run/"CliffGeometry"/("S-"+str(seed)+".json"))["regions"] for seed in [42,21193,7727,314159]}
    cliff_trace_counts=deliver_p2.write_trajectories(root,run,"CliffTrajectories",["DIAGNOSTIC_R1_GEOMETRY_EXPECTED_FAIL"],backgrounds)
    probe_diagrams=probe_footprints(root,source,run,model,data)
    cards=[]
    for seed in [42,21193,7727,314159]:
        rows=[r for r in data['trials'] if r['seed']==seed];a=rows[0]
        cards.append(f'<article class="sample cliff-sample" data-cliff-seed="{seed}" data-search="Seed {seed} 岩壁专项"><h3>岩壁专项 · Seed {seed}</h3><p>用途：检查站在岩面与挤角；操作：下方 W、上层 S、沿壁 A/D、W+D/W+A。正常：脚圆留在草地，诊断红色岩面无落脚；异常：任何接地占地进入红面。自动 {len(rows)}/{len(rows)}，新 Build 人工待验。</p>'+image(f'Media/{run}/'+a['clean'],'真实运行干净画面：角色身体可能遮住岩壁，判据使用脚下占地')+image(f'Media/{run}/'+a['debug'],'独立诊断：红色岩面、黄色实际 Collider、青色实际脚圆、绿色0.30占位合法格心')+image(f'Media/{run}/Placement/S-{seed}-probes-debug.png','独立静态/Trigger/池复用探针：岩面请求被拒绝，蓝色标记只发布在合法地面；不是正式生态')+image('Media/CliffTrajectories/S-'+str(seed)+'.svg',str(cliff_trace_counts['S-'+str(seed)])+'条真实脚位的数据派生轨迹图，非游戏截图；红面为岩壁区域，独立重置处断开。旧碰撞诊断重现不计本版移动。')+probe_diagrams[seed]+'</article>')
    consumers_html='<table><thead><tr><th>类别</th><th>真实入口</th><th>当前范围</th><th>状态</th></tr></thead><tbody>'+''.join('<tr><td>'+e(r['category'])+'</td><td>'+e(r['file']+' / '+r['entry'])+'</td><td>'+e(r['candidateRuntime'])+'</td><td>'+e(r['status'])+'</td></tr>' for r in consumers)+'</tbody></table>'
    with (root/'Media'/run/'frames.csv').open(encoding='utf-8-sig',newline='') as f:raw_frames=list(csv.DictReader(f))
    selected=[]
    motion_seed=None;motion_region=None
    for candidate in [42,21193,7727,314159]:
        region=next(r['region'] for r in data['trials'] if r['seed']==candidate and r['shape']=='LOWER_W_HOLD')
        if all(any(f['screenshot'].startswith('CliffMotion/S-'+str(candidate)+'-'+region+'-'+label+'-') for f in raw_frames) for label in ['LOWER_W_HOLD','DIAGONAL_WD']):motion_seed=candidate;motion_region=region;break
    if motion_seed is None:raise ValueError('No complete actual wall/diagonal sampled sequence')
    for label in ['LOWER_W_HOLD','DIAGONAL_WD']:
        prefix='CliffMotion/S-'+str(motion_seed)+'-'+motion_region+'-'+label+'-'
        for frame in raw_frames:
            if frame['screenshot'].startswith(prefix):
                row=dict(frame);row['case']='S-'+str(motion_seed)+'-CLIFF-'+label;row['path']='Media/'+run+'/'+row['screenshot'];selected.append(row)
    import deliver_p2
    gifs=deliver_p2.generate_gifs(root,selected,model['identity'],'DerivedCliffGIF','CliffGIF',False)
    model['cliff']['gifs']=gifs
    model['cliff']['motionSelection']=dict(seed=motion_seed,region=motion_region,rule='First frozen cliff seed with actual complete W/WD source frames; the same-camera comparison separately uses Seed21193.')
    gif_html='<details id="cliff-motion-gifs"><summary>岩壁顶墙与斜向挤角：真实原帧采样 GIF</summary><p>来自本次真实输入/物理运行，Seed '+str(motion_seed)+'；同镜头对照单独使用21193。0.20秒审阅采样；不是连续实时录像。原始PNG和帧时间/按键均保留。</p>'+''.join(image(g['path'],g['case']+' 原生1280×720采样回放') for g in gifs)+'</details>'
    section='<section id="cliff-review"><h1>南侧岩壁禁止占用：P2专项返修</h1><p>Build：'+e(model['identity'])+'；新Build人工复验尚未执行。</p><h2>本次先看：南侧石壁不能走，也不能放实体</h2><p>'+info+'</p><p>上下楼梯保留 r1 用户所检范围的认可；本版仍需人工复验。按 <b>C</b> 切到安全岩壁检查点，<b>F2</b> 显示独立几何诊断，<b>O</b> 演示放置拒绝；R 回出生点。现有楼梯与 Seed 控件保留。</p><p>诊断画面的粉线来自真实脚中心，但旧绘制也会连接明确的测试重置；不能把跨重置的连线当成走过岩壁。下方CSV轨迹图已在重置处分段，青圈表示当时真实脚圆。</p><h3>同镜头定位对照</h3>'+comparisons+gif_html+'<details><summary>原始 r1 源码等价探针（真实失败）与来源边界</summary>'+image('Evidence/OriginalR1SourceProbe/south-wall-clean.png','修复前原始失败：r1 生产运行源码 + 新增测试探针。旧 EXE 未增加专项自动化接口，未冒称直接在旧二进制采集。跟随镜头和上方严格对照的固定镜头口径不同。')+'<p><a href="../Evidence/OriginalR1SourceProbe/reproduction.json">原失败坐标/脚圆/高程/Collider</a> · <a href="../Evidence/OriginalR1SourceProbe/results.xml">失败 XML</a> · <a href="../Media/'+run+'/r1-collision-replay.json">同镜头诊断重现的明确身份</a></p></details>'+''.join(cards)+'<details><summary>24 Seed 的实际新分母</summary>'+surface+'</details><details><summary>全实体消费者与尚未接入的正式系统</summary><p>生产未接入项须在正式启用前完成。下表的探针只验证公共合同；不代表怪物 AI、商人、掉落或存档恢复已上线。</p>'+consumers_html+'</details><p><a href="../Evidence/CliffAuthoring/cliff-r2.delta.diff">本轮精确源码差异</a> · <a href="../Evidence/Authoring/integration-files.json">必要文件与.meta/接入集合</a> · <a href="../cliff-validation.json">专项检查分母</a> · <a href="../Media/'+run+'/cliff-evidence.json">原始岩壁/占位结果</a> · <a href="../Media/'+run+'/movement.csv">真实输入与位置 CSV</a> · <a href="../Evidence/CliffAuthoring/main-rule-review-only.patch">主项目规则待审补丁</a></p></section>'
    gate_message=""
    if current:
        gate_message=("最终 PlayMode：22/22 断言通过；完整生命周期 FAIL（DESCENDANTS_OUTLIVED_ROOT）。Unity 主进程自然退出 0，但 Supervisor 强制回收了仍存活的自有子进程；无残留、双锁释放、Guard FREE。此 Run 不计为完整 PASS，也未被加入通过 Run 集合。后续修改仅涉及报告工具和说明，运行输入仍与 Build-r7 一致。")
        test_root="../Evidence/History/"+current["run"]+"/"
        gate_html='<aside id="current-test-status" style="border:2px solid #eebc69;padding:1rem;margin:1rem 0"><h3>当前门禁：断言通过，生命周期失败</h3><p>'+e(gate_message)+'</p><p><a href="'+test_root+'results.xml">22例原始XML</a> · <a href="'+test_root+'receipt.json">生命周期回执</a> · <a href="'+test_root+'supervisor.json">Supervisor原始结果</a> · <a href="../current-contracts.json">源码指纹与分项状态</a></p></aside>'
        section=section.replace('<h2>本次先看',gate_html+'<h2>本次先看',1)
    page=root/'Report/index.html';text=page.read_text('utf-8');pos=text.find('<main>')
    if pos<0:raise ValueError('P2 template missing main seam')
    pos+=len('<main>');text=text[:pos]+section+text[pos:]
    text=text.replace('<h1>随机高地与东西楼梯：P2 候选验收</h1>','<h2>原 P2 随机与基础楼梯回归</h2>').replace('原 r3d 人工反馈的实际范围','历次人工反馈及适用 Build 范围')
    text=text.replace('<main>','<style>.cliff-comparison{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:1rem}.cliff-comparison figure{margin:0}.cliff-probe-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:16px;align-items:start}.cliff-probe-grid figure{margin:0}.cliff-probe-grid img{height:360px;object-fit:contain;background:#142324}table{display:block;max-width:100%;overflow:auto}@media(max-width:800px){.cliff-comparison{grid-template-columns:1fr}.cliff-probe-grid{grid-template-columns:1fr}.cliff-probe-grid img{height:300px}}</style><main>',1)
    page.write_text(text,'utf-8')
    purpose='上下楼梯沿用已通过规则；本版主要检查高地南面的石壁不能走，也不能放任何游戏实体。'
    start=root/'START-HERE.md';start.write_text('# 先看这里：南侧岩壁禁止占用专项\n\n'+purpose+'\n\nBuild：`'+model['identity']+'`。 [中文首页](Report/index.html) · [完整 Windows Player](MVP_Windows_x64/MapVNextRandom.exe)\n\n'+info+'\n\n先在 Seed21193 按 C 到检查点，再 W 顶住石壁；F2 看实际脚圆、岩面和碰撞；O 看无物理/Trigger/池探针放置结果。按 R 回正常游玩，沿东西梯往返。原随机图与8基础回归仍可用。\n\n新 Build 人工验收 NOT_RUN；P2 总体待复验。正式消费者 NOT_INTEGRATED、默认未改、旧档兼容 NOT_RUN、MAIN_RULE_SYNC=PENDING_PATCH、提交/合并/推送均 NOT_RUN。\n','utf-8')
    if gate_message:
        with start.open('a',encoding='utf-8') as f:f.write('\n## 当前门禁限制\n\n'+gate_message+'\n')
    guide=root/'how-to-review.md';guide.write_text('# 岩壁怎么验收\n\n'+purpose+'\n\n1. 启动完整 Player，Seed21193 默认可复现。\n2. C 切安全岩壁检查点；W 持续顶住下侧，A/D沿壁，W+A/W+D挤角。正常：脚停在草地；异常：脚圆落在石壁。\n3. 经真实楼梯到上层，用 S 贴边；合法草顶仍可走。\n4. F2 显示岩壁禁区、真实碰撞、实际脚圆和公共占位合法格心（0.30）；导航批次额外保留0.01净空；F1 隐藏UI/粉色轨迹以看干净画面。O 显示独立放置探针，蓝色标记只出现在合法地面。诊断对象不是正式怪物/商人。\n5. R 回安全出生点；原 Seed输入、Tab概览、G同Seed、N下一图和基础样例入口保留。\n6. 西梯 W+D/S+A；东梯 W+A/S+D。纯A/D不能完整跨层，同层横移正常。记录 Seed、区域ID、按键和异常现象。\n\n图集有原始PNG和部分采样回放；采样不是连续实时录像。自动成绩不能代替你审图试玩。\n','utf-8')
    if gate_message:
        with (root/'technical-report.md').open('a',encoding='utf-8') as f:f.write('\n\n'+gate_message+'\n')
    with (root/'technical-report.md').open('a',encoding='utf-8') as f:f.write('\n\n## 本轮 R18 专项\n\n'+purpose+'\n\nMapData → VNextProjection → 原StairGeometry/单GeneratedCliffEdges → CliffExclusion派生岩面 → 同owner SurfaceQuery；导航、占位/扫掠、候选生成与重置共用。纯查询形状按各对象实际足迹；不同尺寸动态实体、无物理/Trigger/池复用由独立夹具验证。正式消费者精确清单见 entity-consumers.json，不声称正式全实体完成。\n\n地形/楼梯语义哈希可与 r1 相同，空间语义增加禁区身份及新 spatialHash，不冒称两版本等价。原逻辑可走格与排除岩面后的脚圆合法/可达节点分别报告。原始红测不是原二进制注入；严格同镜头对照是明确标记的 r1 碰撞路径前缀诊断重建，原 r1 源码真实红测另保留。\n')

def check(root,model):
    run=model['cliff']['run'];data=common.read(root/'Media'/run/'cliff-evidence.json');surfaces=common.read(root/'Media'/model['selection']['full']/'cliff-evidence.json')
    errors=validate(data,surfaces)
    import cliff_motion
    motion=cliff_motion.measure(root/"Media"/run)
    if motion!=common.read(root/"cliff-motion-validation.json") or motion["status"]!="PASS":errors.append("Independent cliff motion check failed")
    text=(root/'Report/index.html').read_text('utf-8')
    for label in ['cliff-review','DIAGNOSTIC','本次先看','原始 r1','全实体消费者']:
        if label not in text and label!='DIAGNOSTIC':errors.append('Missing cliff explanation '+label)
    from PIL import Image
    gifs=common.read(root/'Evidence/DerivedCliffGIF/manifest.json')
    if len(gifs)!=2:errors.append('Missing actual cliff motion GIFs')
    for gif in gifs:
        if common.sha(root/gif['path'])!=gif['sha256'] or gif['identity']!=model['identity']:errors.append('Cliff GIF identity/hash')
        with Image.open(root/gif['path']) as im:
            for i in range(im.n_frames):im.seek(i);im.load()
        for frame in gif['sourceFrames']:
            if common.sha(root/frame['path'])!=frame['sha256']:errors.append('Cliff GIF source changed')
    import xml.etree.ElementTree as ET
    for svg in (root/'Media/ProbeFootprints').glob('*.svg'):
        try:ET.parse(svg)
        except Exception as ex:errors.append('Invalid footprint SVG '+str(ex))
    contracts=common.read(root/'probe-footprint-contracts.json')
    if common.sha(root/'Evidence/CliffAuthoring/MapVNextDemo.Cliff.cs')!=contracts['sourceSha256']:errors.append('Probe source binding changed')
    if not (root/'entity-consumers.json').is_file():errors.append('Missing consumer audit')
    if model.get("currentContracts"):
        current=common.read(root/"current-contracts.json")
        history=root/"Evidence/History"/current["run"]
        xml=ET.parse(history/"results.xml").getroot();receipt=common.read(history/"receipt.json")
        if current!=model["currentContracts"] or current["lifecycle"]!=receipt["status"] or int(xml.attrib["passed"])!=current["passed"] or int(xml.attrib["total"])!=current["total"] or xml.attrib["result"]!="Passed":errors.append("Current XML/lifecycle disclosure mismatch")
        if common.sha(history/"results.xml")!=current["xmlSha256"]:errors.append("Current XML hash mismatch")
        if current["lifecycle"]=="FAIL" and ("current-test-status" not in text or "生命周期失败" not in text):errors.append("Current lifecycle failure hidden")
    return errors

def probe_footprints(root,source,run,model,data):
    """Derive a labelled auxiliary diagram from actual requests and Build-bound placement contracts."""
    code=source.parents[2]/'Assets/Scripts/MapGeneration/VNext/MapVNextDemo.Cliff.cs'
    inputs=common.read(root/'Evidence/Build/build-inputs.json')
    expected=inputs['Assets']['Scripts/MapGeneration/VNext/MapVNextDemo.Cliff.cs']
    if common.sha(code)!=expected:raise ValueError('Probe footprint source differs from Build input')
    text=code.read_text('utf-8')
    definitions={'small-no-Rigidbody':dict(circle=.12,token='("small-no-Rigidbody",MapVNextGroundFootprint.Circle(.12f))'),
        'large-static-box':dict(box=[.65,.45],token='("large-static-box",MapVNextGroundFootprint.Box(new Vector2(.65f,.45f)))'),
        'trigger-drop-probe':dict(circle=.22,token='("trigger-drop-probe",MapVNextGroundFootprint.Circle(.22f))'),
        'pool-reactivation-probe':dict(box=[.3,.25],token='("pool-reactivation-probe",MapVNextGroundFootprint.Box(new Vector2(.3f,.25f)))')}
    if not all(v['token'] in text for v in definitions.values()):raise ValueError('Changed probe footprint definitions; inspect source instead of guessing')
    common.save(root/'probe-footprint-contracts.json',dict(source='Assets/Scripts/MapGeneration/VNext/MapVNextDemo.Cliff.cs',sourceSha256=expected,definitions=definitions,scope='Actual no-physics placement contract parameters bound to this Build. Diagram is derived, not a game screenshot.'))
    names={'small-no-Rigidbody':'小占地静态对象','large-static-box':'大占地矩形装饰','trigger-drop-probe':'Trigger掉落探针','pool-reactivation-probe':'池激活探针'}
    dest=root/'Media/ProbeFootprints';dest.mkdir(exist_ok=True);output={}
    for seed in [42,21193,7727,314159]:
        geometry=common.read(root/'Media'/run/'CliffGeometry'/f'S-{seed}.json');rows=[r for r in data['placements'] if r['seed']==seed]
        details=[]
        for number,(category,spec) in enumerate(definitions.items()):
            bad=next(r for r in rows if r['category']==category and not r['expectedAllowed']);good=next(r for r in rows if r['category']==category and r['expectedAllowed'])
            bx,by=bad['requested']['x'],bad['requested']['y'];gx,gy=good['requested']['x'],good['requested']['y'];scale=75
            lo=min(bx,gx)-1.3;bottom=min(by,gy)-1;hi=max(bx,gx)+1.3;top=max(by,gy)+1.2
            width=(hi-lo)*scale;height=(top-bottom)*scale
            def xy(x,y):return (x-lo)*scale,(top-y)*scale
            parts=[f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}"><rect width="100%" height="100%" fill="#142324"/>']
            for region in geometry['regions']:
                p=region['vertices']
                if max(v['x'] for v in p)<lo or min(v['x'] for v in p)>hi or max(v['y'] for v in p)<bottom or min(v['y'] for v in p)>top:continue
                points=' '.join(f'{xy(v["x"],v["y"])[0]:.2f},{xy(v["x"],v["y"])[1]:.2f}' for v in p)
                parts.append(f'<polygon points="{points}" fill="#58303b" stroke="#ff625b" stroke-width="1"/>')
            for request,color,dash in [(bad,'#ffb0a8','4 3'),(good,'#70d8ff','')]:
                x,y=xy(request['requested']['x'],request['requested']['y'])
                if 'circle' in spec:parts.append(f'<circle cx="{x}" cy="{y}" r="{spec["circle"]*scale}" fill="none" stroke="{color}" stroke-width="2" stroke-dasharray="{dash}"/>')
                else:
                    hx,hy=spec['box'];parts.append(f'<rect x="{x-hx*scale}" y="{y-hy*scale}" width="{hx*2*scale}" height="{hy*2*scale}" fill="none" stroke="{color}" stroke-width="2" stroke-dasharray="{dash}"/>')
                parts.append(f'<circle cx="{x}" cy="{y}" r="2" fill="{color}"/>')
            parts.append('</svg>');path=dest/f'S-{seed}-{number}.svg';path.write_text(''.join(parts),'utf-8')
            dimensions=('半径 '+str(spec['circle'])) if 'circle' in spec else '宽高 '+str(spec['box'][0]*2)+' × '+str(spec['box'][1]*2)
            details.append('<figure><img src="../'+path.relative_to(root).as_posix()+'"/><figcaption>'+html.escape(names[category]+' ('+category+') / '+dimensions+' 世界单位；请求 ('+format(bx,'.2f')+','+format(by,'.2f')+')，合法 ('+format(gx,'.2f')+','+format(gy,'.2f')+')；红虚线请求被拒且未激活，蓝线为合法激活。拒绝：'+bad['reason']+'（占地触及固体）；合法：'+good['reason']+'（可激活）')+'</figcaption></figure>')
        output[seed]='<details><summary>各探针实际放置合同的占地与结果</summary><p>数据派生示意，不是游戏截图。参数来自本 Build 绑定的源码，位置与拒绝/激活结果来自本次运行；蓝色场景小方块是探针标记，不能当成它的真实占地尺寸。</p><div class="cliff-probe-grid">'+''.join(details)+'</div></details>'
    return output

