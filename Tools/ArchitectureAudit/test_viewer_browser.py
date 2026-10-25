"""Optional agent-browser regression for the generated offline viewer.

Run locally after `scan.py`; this script does not run Unity or change game assets.
"""

from __future__ import annotations

import os
import json
from pathlib import Path
import shutil
import subprocess
import time


ROOT = Path(__file__).resolve().parents[2]
VIEWER = ROOT / "Docs" / "architecture-audit" / "viewer.html"


def main() -> None:
    cli = shutil.which("agent-browser.cmd") or shutil.which("agent-browser")
    if not cli:
        raise SystemExit("agent-browser is not installed")
    env = os.environ.copy()
    env["AGENT_BROWSER_SESSION"] = f"rpg-viewer-check-{os.getpid()}"

    def browser(*args: str) -> str:
        # The CLI launches a daemon on `open`. It may inherit captured pipes,
        # so do not capture the opener/closer process output on Windows.
        quiet = args[0] in {"open", "close"}
        run = subprocess.run(
            [cli, *args], env=env, text=True, encoding="utf-8", errors="replace",
            stdout=subprocess.DEVNULL if quiet else subprocess.PIPE,
            stderr=subprocess.DEVNULL if quiet else subprocess.PIPE,
            timeout=35, check=False)
        if run.returncode:
            raise AssertionError(f"agent-browser {args[0]} failed: {run.stdout} {run.stderr}")
        return (run.stdout or "").strip()

    def assert_page(name: str, condition: str) -> None:
        last_error: Exception | None = None
        for _ in range(5):
            try:
                result = browser("eval", f"(()=>{{if(!({condition}))throw Error('{name}');return true}})()")
                if result == "true":
                    print(f"PASS {name}", flush=True)
                    return
                last_error = AssertionError(f"{name}: unexpected result {result}")
            except AssertionError as error:
                last_error = error
            time.sleep(0.25)
        raise AssertionError(f"{name} did not become true") from last_error

    try:
        browser("open", VIEWER.as_uri())
        browser("set", "viewport", "1440", "900")
        assert_page("overview is undirected",
                    "mode==='overview' && document.querySelectorAll('#viewport .node').length>5 && !document.querySelector('#viewport [marker-end]')")
        assert_page("minimal theme and visible start",
                    "getComputedStyle(document.body).backgroundColor==='rgb(24, 24, 24)' && !document.querySelector('#quickStart').open && document.querySelector('#search').getBoundingClientRect().top>document.querySelector('aside').getBoundingClientRect().top")
        browser("click", '[data-example="Torch_Blue"]')
        assert_page("Torch search identifies Prefab",
                    "!!document.querySelector('button.row[data-id=\"asset:Assets/Prefabs/Monsters/Torch/Torch_Blue.prefab\"]') && document.querySelector('aside').scrollTop===0")
        browser("click", 'button.row[data-id="asset:Assets/Prefabs/Monsters/Torch/Torch_Blue.prefab"]')
        assert_page("local graph and Visual arrow",
                    "mode==='focus' && selected==='asset:Assets/Prefabs/Monsters/Torch/Torch_Blue.prefab' && document.querySelectorAll('#viewport [marker-end]').length>0 && graph.edges.some(e=>e.source===selected&&e.relation==='contains GameObject'&&nodes.get(e.target)?.label==='Visual'&&!!directionMeaning(e)) && document.querySelector('#results .is-selected')?.getAttribute('aria-current')==='true'")
        browser("click", "#copySourcePath")
        assert_page("source path copy feedback",
                    "document.querySelector('#copyStatus')?.textContent==='已复制' || (document.querySelector('#copyStatus')?.textContent.startsWith('剪贴板不可用') && getSelection().toString()===document.querySelector('#sourcePathText').textContent)")
        visual_edge_id = json.loads(browser(
            "eval",
            "graph.edges.find(e=>e.source===selected&&e.relation==='contains GameObject'&&nodes.get(e.target)?.label==='Visual')?.id"))
        browser("click", f"#viewport [data-edge-id='{visual_edge_id}']")
        assert_page("Visual evidence opens",
                    "document.querySelector('#detailBody').className==='edge-detail' && document.querySelector('#detailBody').innerText.includes('Visual') && document.querySelector('#detailBody').innerText.includes('验证到哪一步')")
        browser("click", "#zoomIn")
        assert_page("zoom changes scale", "scale>1")
        browser("click", "#resetView")
        assert_page("reset restores scale", "scale===1 && dx===0 && dy===0")
        browser("click", "#area6 details summary")
        assert_page("Chinese help opens", "document.querySelector('#area6 details').open")
        browser("click", "#area6 details summary")
        assert_page("Chinese help closes", "!document.querySelector('#area6 details').open")
        assert_page("Variant arrow but text match undirected",
                    "graph.edges.some(e=>e.relation==='prefab source'&&e.source.includes('Torch_Purple.prefab')&&e.target.includes('Torch_Blue.prefab')&&!!directionMeaning(e)) && graph.edges.some(e=>e.validation==='textual only'&&!directionMeaning(e))")
        browser("click", 'button[data-direction="in"]')
        assert_page("upstream filter",
                    "directionFilter==='in' && document.querySelector('button[data-direction=in]').getAttribute('aria-pressed')==='true'")
        browser("select", "#issueJump", "AA-01")
        assert_page("AA-01 issue jump",
                    "document.querySelector('#issueJump').value==='AA-01' && document.querySelector('#detailBody').innerText.includes('AA-01')")
        browser("select", "#issueJump", "AA-02")
        assert_page("AA-02 issue jump",
                    "document.querySelector('#issueJump').value==='AA-02' && document.querySelector('#issueFilter').value==='Confirmed Defect' && document.querySelector('#detailBody').innerText.includes('AA-02')")
        browser("select", "#chainFilter", "inventory")
        assert_page("inventory chain resets issue",
                    "document.querySelector('#issueJump').value==='' && document.querySelector('#resultCount').innerText.startsWith('9 ') && document.querySelector('#chainNav summary').innerText.includes('8 条')")
        browser("fill", "#search", "NonexistentAuditThing_20261014")
        assert_page("empty search recovery",
                    "document.querySelector('#resultCount').innerText.startsWith('0 ') && !!document.querySelector('#emptyClear')")
        browser("click", "#emptyClear")
        assert_page("clear restores top and overview",
                    "mode==='overview' && document.querySelector('aside').scrollTop===0 && !document.querySelector('#results .is-selected')")
        browser("click", "#filterShortcut")
        assert_page("filter shortcut finds options",
                    "document.querySelector('aside').scrollTop>0 && document.querySelector('#area4').getBoundingClientRect().top<innerHeight")
        browser("set", "viewport", "1440", "900")
        assert_page("1440 desktop width", "document.documentElement.scrollWidth<=innerWidth && document.querySelector('.graphbar').scrollWidth<=document.querySelector('.graphbar').clientWidth")
        browser("set", "viewport", "1280", "720")
        assert_page("1280 desktop width", "document.documentElement.scrollWidth<=innerWidth && document.querySelector('.graphbar').scrollWidth<=document.querySelector('.graphbar').clientWidth")
        browser("set", "viewport", "1920", "1080")
        assert_page("1920 desktop width", "document.documentElement.scrollWidth<=innerWidth")
        browser("set", "viewport", "1024", "768")
        assert_page("1024 desktop width",
                    "document.documentElement.scrollWidth<=innerWidth && document.querySelector('#area7').getBoundingClientRect().top>document.querySelector('#area5').getBoundingClientRect().top")
        print("Viewer browser regression complete.", flush=True)
    finally:
        try:
            browser("close")
        except (AssertionError, subprocess.TimeoutExpired) as error:
            print(f"Own browser session close was not confirmed: {error}", flush=True)


if __name__ == "__main__":
    main()
