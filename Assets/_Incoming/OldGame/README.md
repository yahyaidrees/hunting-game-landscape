# OldGame (staging)

This is the staging area for assets copied from the old game, which uses the Built-in render pipeline. Nothing here is used in a game scene until it passes the gate.

## How to bring assets in
1. In the old project, use *Assets > Export Package*, or copy the files directly. **Untick all scripts.** Only models, textures, animations and audio come across.
2. Drop the files into the matching subfolder here: Animals, Props, Environment, Textures, Audio, Other.
3. Keep an untouched raw copy in `SourceAssets/OldGame/`.

## Quality gate (checked before an asset moves to Assets/_Game/...)
- [ ] Materials converted to URP (Window > Rendering > Render Pipeline Converter)
- [ ] Triangles and texture size within the plan's section 13 budgets (Blender pass if not)
- [ ] Scale, pivot and LODs are correct
- [ ] Licence is known and logged in `Assets/_Game/Data/Credits/LicenceLog.csv`
- [ ] Looks right next to the style sheet under the game's lighting and colour grade
- [ ] Runs inside budget on the Low tier phone

An asset that fails goes back for fixing or is dropped.
