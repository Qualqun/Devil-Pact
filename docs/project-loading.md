# Loading a fresh checkout in s&box

Keep each asset's `.meta` file in Git. The engine now stores asset references as
GUID/path pairs; these GUIDs are separate from scene object and component GUIDs.
Do not regenerate them to repair an incomplete scene.

The observed initial editor session contained 630 missing prefab instances even
though the files existed and their asset GUIDs matched. Reopening MainScene after
asset compilation restored the instances and the StartArea component reference.

`Editor/StartupResourceRecovery.cs` handles that startup ordering problem. On the
first editor frame with a source scene open, it detects missing prefab placeholders
and absent compiled resources. The engine's `IsCompiledAndUpToDate` can be true
while `IsCompiled` is false, so both flags are checked. It compiles local assets,
awaits completion (native compilation is queued), loads the prefabs, then reopens
affected source scenes. It never saves a broken scene
and skips scenes with unsaved edits. The same operation is available through
**Devil Pact > Recover Missing Prefabs** in the editor menu.

The MCP also exposes `devil_pact_recover_assets` for the same recovery operation.

`Map_Bulding.scene` also referenced the old player path. It now uses
`prefabs/player/player.prefab`, preserving the instance IDs and overrides.

Custom compiled shaders are an exception to the generated-file ignore rule.
Include `Assets/Art/Shaders/enemyhit.shader_c` when committing these changes, as
is already done for `bonus.shader_c`. Its shader source and graph remain available
for recompilation after an engine update. The duplicate Git ignore entries that
excluded this shader have been removed.

For verification, reopen both scenes, check for `[Missing Prefab]` objects, then
start MainScene and inspect new console output. Existing engine-content warnings
are separate from missing project prefabs.
