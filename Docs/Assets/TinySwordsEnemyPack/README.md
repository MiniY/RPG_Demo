# Tiny Swords Enemy Pack: public engineering, private artwork

This isolated visual library does not register enemies in gameplay. The formal
SampleScene, ecology, TowerRun persistence, and frozen retained content stay unchanged.
The original Monster prefab examples use a `Visual` child; this library follows it.

The owner's final scope is **monster Prefabs only**: 22 main enemies plus the actual
Pig Rider Spear Goblin mounted variant. Auxiliary art remains part of the audited source
copy and recovery state, but boats, buildings, fences, decorations, standalone pigs,
projectiles and independent effects get no Prefab, controller, clip or preview entry.
Extra Hit/Guard/Dead/Row exports belonging to monsters extend those monsters' real states.
`Default` means the available exported artwork, not a synthesized color variant.
The audited library contains 23 monster Prefabs and 83 actual source animation states.

## License boundary

[Pixel Frog's official Tiny Swords page](https://pixelfrog-assets.itch.io/tiny-swords)
permits personal/commercial project use and modification, and prohibits redistribution,
resale, or repackaging of the artwork, including modified artwork. Enemy Pack is the paid
pack. The separately labelled old CC0 download is not the license for this pack.
Local source provenance is the owner's supplied directory, not an independently verified
purchase receipt. Obtain your own lawful copy and check terms for your purchase channel.

The public files contain original code, prefab hierarchy, animation references, importer
settings and IDs, never licensed pixel payloads. Paid PNGs, Aseprite sources, screenshots,
Player binaries and private backups must not be pushed, put in LFS, or published as CI or
release attachments. A public clone alone cannot run the visual preview.

## Restore BEFORE opening Unity

Required inputs are the same lawful source version, this engineering checkout, and the
version-matched private recovery kit. The kit is deliberately outside all Git checkouts.
It includes the golden import plan, metadata and GUID/sub-Sprite fileID map plus an offline
copy of restricted artwork. A separate source archive and duplicate kit are retained locally.
The exact kit locations and hashes belong in the private acceptance report, not public Git.
Do not open an artwork-missing checkout first: Unity may discard orphan metadata.

The recovery tool validates all inputs and all existing destinations before writing. It
only removes ASCII parentheses from source path components, preserves all other spelling,
and rejects collisions, missing files, wrong versions, GUID conflicts and destination
differences. It never replaces an already imported asset with a new GUID. Repeat restore
is a NO_OP. Source updates require a new audited plan and explicit migration; never swap
an updated purchase download underneath frozen prefab references.

```powershell
python Tools/TinySwordsEnemyPack/artwork.py restore --source <lawful-source-directory> --project <checkout> --kit <private-kit-directory> --receipt <new-receipt.json>
```

After restoration, use the project's existing Guard/Supervisor, with the installed Unity
2022.3.62f3c1 Editor, to call `TinySwordsEnemyPackAuthoring.Verify`. Pass `-enemyPlan` the
kit's import-plan.json, `-enemyReport` a new private output JSON, and `-enemyGolden` the kit's
asset-guid-map.json. Verification must compare actual imported Sprite GUIDs and local
fileIDs, clips and prefab references, not just image file hashes. Preserve the raw Editor
log, Supervisor Run ID, natural exit, Job cleanup and released locks.

## Authoring and preview

`Tools/TinySwordsEnemyPack/import-plan.json` is the source-audited structural plan. PNG
exports are imported through the existing Sprite package; editable Aseprite files remain
external. Frame dimensions, fixed body ground pivots and Aseprite durations are explicit.
Point filtering, PPU 64, FullRect meshes, no mipmaps or texture compression, and sufficiently
large maximum texture sizes avoid smoothing, clipped grids and scaled source frames.
No missing animation is invented. Source-facing artwork is distinguished from optional
mirroring. Auxiliary facing exports are recorded by the source audit only; auxiliary
objects are excluded from the monster catalog and preview.

Clip keys preserve integer-millisecond source timing, including unusually long last-frame
holds. The clip's 1000 Hz time grid is timestamp precision, not a generated 1000 FPS action.
Its final key holds the last Sprite one millisecond before the source endpoint, accounting
for Unity's implicit one-sample tail on object-reference curves. Inspector stop time,
effective clip length and actual Player frame order must all match source durations.

The Editor authoring tool uses Unity's Sprite Data Provider, AnimationUtility,
AnimatorController and PrefabUtility. It only creates this library's paths. A first Bear
pilot must pass before the full library is generated. Generation is distinct from
verification: a recovery verification must not quietly regenerate IDs or repair references.

The standalone preview scene is passed directly to BuildPipeline, never added to formal
EditorBuildSettings. The preview displays `Visual-ready / Gameplay not integrated`, browses
every prefab and state, plays/pauses, resets and selects loop/single playback. The private
report contains the actual executable, controls, Player screenshots and validation receipts.
It never opens formal scenes or calls TowerRun storage. Automated preview success is not
user acceptance; the latter remains NOT_RUN until the owner tries it.

## Reuse decisions and real call chain

| Function | Existing implementation | This implementation | Relation | Reason |
|---|---|---|---|---|
| Sprite import | SpriteSheetSlicerWindow / installed Sprite Data Provider 1.0.0 | Explicit audited rects + name/ID pairs via that provider | Extension | Existing interactive heuristic cannot freeze a complete audited batch or verify cold restoration |
| Monster hierarchy | Torch_Blue, HappySheep `Visual` child | Root + `Visual` with renderer/Animator, no behavior | Direct Reuse | Preserve the actual project convention without coupling incompatible combat scripts |
| Clip/controller/Prefab serialization | Unity AnimationUtility / AnimatorController / PrefabUtility | Narrow new-library batch adapter | Direct Reuse | Supported Editor APIs own all serialization; no hand-written Unity YAML |
| Process ownership | EngineeringAssurance unity_guard.run -> unity_supervisor.execute | Preview validator supplied through the existing command-policy seam | Direct Reuse | Same heavy/project locks, real admission, owned Job, natural-exit and cleanup evidence |
| Acceptance build | MapAcceptanceBuild explicit BuildPlayerOptions pattern | Minimal recovery-project scene array | Extension | Existing builder requires formal SampleScene and game defines, unsuitable for this isolated visual library |
| Recovery state | Existing Git/artifact boundaries and file hashes | Fail-closed source/metadata kit + cold GUID/fileID verification | Necessary New Module | No prior pack-specific source mapping or offline Sprite identity restoration contract exists |

Call flow: source audit -> import plan -> copy -> Guard/Supervisor -> Bear pilot ->
monster-only generation -> golden map/private kit -> public engineering export -> restore
BEFORE Unity -> Guard/Supervisor cold verification -> isolated Build -> same Guard/Supervisor
Preview adapter -> actual Player checks/screenshots -> reviewed exact Git allowlist.
These tools never run the old interactive slicer's heuristic or formal ecology path in
parallel; they do not compete for gameplay state ownership.

## Tool checks

Use Python with Pillow available for source PNG validation. The unit tests create their
own temporary fixtures and never modify the owner's source artwork:

```powershell
python -m unittest discover -s Tools/TinySwordsEnemyPack -p "test_*.py" -v
```

Unit tests cover the source/restore/publication boundaries. Actual Unity import, cold
recovery and Windows Player evidence are required separately; unit tests cannot replace
them. Keep failed runs alongside successful corrections in the private acceptance report.

The staged audit always runs the ordinary `git diff --cached --check` and retains its
raw exit code. Unity's serializer emits spaces after colons for empty YAML fields; these
are accepted only in `.meta`, `.anim`, `.controller`, `.prefab` and `.unity` when every
diagnostic matches that exact empty-field form. Any nonempty value, source-code whitespace
or other Git failure is rejected. This preserves already verified metadata bytes instead
of rewriting frozen imports to satisfy a generic whitespace check.
