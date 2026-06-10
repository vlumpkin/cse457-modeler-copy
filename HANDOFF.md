# CSE 457 Project 2 — Session Handoff

Pick this up in a new conversation by reading this file top to bottom. It's written to be self-contained.

## Context

- **Project:** CSE 457 Project 2 (Modeler), 26sp quarter
- **Course URL:** https://courses.cs.washington.edu/courses/cse457/26sp/project/modeler/
- **Stack:** Unity 6000.3.1f1 LTS, **Built-in Render Pipeline** (not URP — the skeleton's `Packages/manifest.json` has no URP/HDRP package)
- **Deadline:** Tuesday, April 21 2026, 11:00 PM PT (same day as this session)
- **Repo location:** `C:\Users\vmoos\Documents\Claude\Code\cse457-modeler`
  - `modeler-skeleton/` — cloned from `gitlab.cs.washington.edu/cse457-26sp/modeler-skeleton`
  - `SPEC.md`, `PLAN.md` — written at session start
  - `_disabled_plugins/GitHub/` — the broken "GitHub for Unity" plugin moved out of `Assets/Plugins/` because it was throwing `ArgumentException: System.Byte[] cannot be converted to System.ReadOnlySpan<byte>` in Unity 6 (API drift, the package is unmaintained). **Do not move it back.**

## Progress

### ✅ Part 1 — Surface of Revolution (done)

File: `modeler-skeleton/Assets/Scripts/SurfaceOfRevolution.cs`

User's final working version (file was edited by them at least twice; this is the committed state):

- Vertex grid `N × (subdivisions+1)` with seam duplicate column for UV wrap.
- Position: `(r·cosθ, y, r·sinθ)` where `r = curvePoints[i].x`, `y = curvePoints[i].y`, `θ = 2π·j/subdivisions`.
- UV: `(j/subdivisions, i/(N-1))`.
- **Triangle winding** (the bug that bit us): `(i0, i1, i2)` and `(i2, i1, i3)` — CW from outside. Unity is left-handed and wants CW-front-facing. The CSE 457 spec text ("CCW from the outside") appears to be stated from a right-handed / OpenGL mental model; what Unity actually renders correctly is CW. Our first pass used CCW and showed the inside of the mesh from outside angles.
- **Normals via face-averaging**, not analytical curve-tangent: cross-product each triangle's edges, accumulate into vertex normals, normalize. Robust to curve orientation — won't silently invert lighting if the curve is ordered unexpectedly.
- Fallback `Vector3.up` if accumulated normal magnitude is ~0.

**Known-good behavior:** renders all sample curves (sample1–5) correctly in all 4 viewing modes. `ExportMesh` button works, produces `.asset` files.

### ⚠ Material/lighting gotcha (for Part 2)

When the user dragged an exported SoR mesh into the Hierarchical Modeling scene, shading looked "baked in" / unresponsive to lights. Almost certainly a material-shader issue — the default material was unlit or matcap-style, not Standard. Fix: MeshRenderer → swap material to one using **Standard** (built-in RP) shader. Not re-verified end-to-end.

### 🔄 Part 2 — Hierarchical Modeling (in progress)

User's existing hierarchy in `Hierarchical Modeling.unity`:

```
Frame
├── Torso
├── Left Arm Container → Left Arm
├── Right Arm Container → Right Arm
├── Left Leg Container → Left Leg
├── Right Leg Container → Right Leg
└── HeadContainer
    ├── lego_head
    └── Antenna Container → antenna
```

Depth ≥ 3 ✓. Uses at least one SoR-derived piece (`lego_head.asset` exists in `Assets/Scenes/`, though the one in the hierarchy may be the skeleton's pre-built example — user should confirm they use their own SoR export to satisfy the "custom SoR component" requirement).

### 🔄 Part 3 — Animations (in progress)

File: `modeler-skeleton/Assets/Scripts/char_animation_temp.cs`
Class: `char_animation_temp` (named to match filename; Unity requires this for MonoBehaviours). User intends to rename to `CharacterAnimator` when cleaning up.

**Button 1 — Full body rotation: ✅ DONE.** Coroutine rotates `Frame` 360° around local Y over `rotationDuration` (default 2s). Caches rest rotation in `Start`, snaps back at end, `isAnimating` guard against click-spam.

**Button 2 — Walk: 🟡 partial.** Currently only animates the right arm. Session ended with us in "right arm only" state for debugging purposes (to isolate the crash issue — which turned out to be unrelated to the walk code). Ready to re-add the other limbs:

```csharp
// pattern for natural cross-lateral gait:
// rightArm: +swing     leftArm:  -swing
// rightLeg: -swing     leftLeg:  +swing
```

**Button 3 — Creative: ❌ not started.** Suggestions: wave (one-arm raise + hand oscillation), jump (torso Y offset via sin half-wave + slight limb bend), faint (gradual rotate forward + fall).

### ❌ Part 4 — AR Deployment (not started)

Export finished hierarchical model as prefab → drop into `ARScene.unity` → build to Android (ARCore) or iOS (ARKit). See `PLAN.md` Phase 4 for Player Settings.

### ❌ Video (not started)

~4 minutes total. Breakdown in `SPEC.md` §Deliverables.

## Known issues / workarounds

### 1. Unity Inspector `EditorStyles` crash

Symptom: repeated exceptions in Console, all 100% `UnityEditor.*` stack frames:
```
NullReferenceException at UnityEditor.EditorStyles.get_toolbarButtonRight
TypeInitializationException: The type initializer for 'Styles' threw an exception.
Unable to use a named GUIStyle without a current skin. [...] UnityEditor.InspectorWindow:RedrawFromNative
```

**Not caused by user code.** Stack trace has zero user frames. Once the static cctor for `UnityEditor.PropertyEditor+Styles` fails, it's permanently unusable for the entire process (C# spec). Every Inspector repaint rethrows. The user initially blamed adding a second UI button, but `InspectorWindow.ShowButton` in the stack is the Inspector's **pin/padlock** icon, not scene UI.

**Fix applied in this session:** deleted `modeler-skeleton/Library/` (was 415 MB) while Unity was fully closed. Verified no `Unity.exe` in tasklist before deleting. User to reopen Unity Hub — expect 2–5 min first-import time.

**If it recurs:** reset editor layout via `Window → Layouts → Default`, or as last resort reinstall the Unity 6000.3.1f1 LTS editor.

### 2. GitHub for Unity plugin

Moved from `Assets/Plugins/GitHub` → `_disabled_plugins/GitHub` (and `.meta` file). Do not move back. Reflection call in `GitHub.Unity.StreamExtensions.ToTexture2D` uses `Texture2D.LoadImage(byte[])` which now takes `ReadOnlySpan<byte>` in Unity 6 → `ArgumentException`, crashes Project window rendering, makes scene files "disappear" from the Project panel.

## File inventory

| Path | State |
|---|---|
| `SPEC.md` | Full project spec, distilled from course page |
| `PLAN.md` | 5-phase implementation plan with time budgets |
| `HANDOFF.md` | This file |
| `modeler-skeleton/Assets/Scripts/SurfaceOfRevolution.cs` | ✅ Working, user-edited |
| `modeler-skeleton/Assets/Scripts/char_animation_temp.cs` | 🟡 Button 1 done, Button 2 right-arm only |
| `modeler-skeleton/Assets/Scenes/SurfaceOfRevolution.unity` | ✅ Working |
| `modeler-skeleton/Assets/Scenes/Hierarchical Modeling.unity` | 🔄 Hierarchy built, needs SoR mesh verification + full animation wiring |
| `modeler-skeleton/Assets/Scenes/ARScene.unity` | ❌ Untouched |
| `modeler-skeleton/Library/` | 🗑 Deleted at session end; will regenerate on next Unity launch |
| `_disabled_plugins/GitHub/` | 🗑 Disabled — keep disabled |

## Next actions (in order)

1. **User reopens Unity** — wait for import (2–5 min). Verify no `EditorStyles` errors.
2. **Verify right-arm walk still works** in `char_animation_temp.cs`.
3. **Add left arm + both legs** to `WalkRoutine`, with phases:
   - rightArm `+swing`, leftArm `-swing`, rightLeg `-swing`, leftLeg `+swing`
4. **Implement Button 3** — pick one from suggestions above.
5. **Wire the 3 buttons** in `Hierarchical Modeling.unity` UI canvas. `MazeGame` project (linked on course page) is the button-wiring reference.
6. **Confirm Part 2 uses user's own exported SoR mesh**, not just the skeleton's `lego_head`.
7. **Part 4 AR** — prefab, build settings (Android: min API 24, ARM64, IL2CPP), device deploy.
8. **Record video** per breakdown in `SPEC.md` §Deliverables.
9. **Zip `<netid>/` folder, submit to Canvas.**

## User context (for a fresh assistant)

- Vernon Lumpkin, `vernonlumpkin@outlook.com`
- Prefers hands-on iteration and deep walkthroughs ("intense understanding") over quick answers.
- Has been burned by assistant overconfidence — verify claims about Unity conventions before asserting.
- Comfortable with code but is learning Unity's Editor conventions.

## Correction log (things I got wrong this session, save future me the loop)

1. **Triangle winding.** First said CCW-from-outside was correct (parroting the spec). User saw inside-out rendering. I flipped to CW-from-outside, which caused all-black surfaces because I hadn't updated normal direction. Reverted. User's own rewrite used CW winding + face-averaged normals, which works. Lesson: **Unity is CW-front-facing in world space; the spec's CCW language is wrong or using a right-handed mental model.**
2. **"EditorStyles bug is one-off, restart fixes it."** Said this twice — didn't. Library delete was actually needed. Lesson: don't be optimistic about Unity editor-state corruption; go straight to cache wipe after one restart fails.
3. **Normal direction math.** My analytical curve-tangent approach carried a silent "curve goes bottom-to-top, x ≥ 0" assumption. Face-averaging is safer for a general solution.
