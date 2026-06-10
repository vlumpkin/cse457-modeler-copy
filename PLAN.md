# Project 2 Modeler — Implementation Plan

**Deadline: today, 11:00 PM PT.** Plan is ordered by risk: ship Part 1 first (hardest, easiest to fail silently), then Part 2/3 (mostly scene work), then Part 4 (AR build — can fail at the device-deploy step, so finish the rest first).

Total realistic time: **6–9 hours** if this is your first Unity AR build. The AR build step (SDK + device cable) is the unknown — budget 2h for it alone.

---

## Phase 0 — Environment sanity check (~30 min)

- [ ] Install Unity Hub + **Unity 6000.3.1f1 LTS**. Include the **Android Build Support** module (and **OpenJDK**, **Android SDK & NDK Tools**) if targeting Android.
- [ ] Open `modeler-skeleton/` in Unity Hub. Let it import — first import is slow. Watch console for red errors.
- [ ] Load `SurfaceOfRevolution.unity`. Press Play. You should see a placeholder cube (the skeleton's 8-vertex stub) — proof the wiring works before you touch code.
- [ ] Download the [sample solution](https://courses.cs.washington.edu/courses/cse457/26sp/assets/solutions/modeler-solution.zip) and run it side-by-side. This is your ground truth for what each sample curve should look like.

**Go/no-go:** if Unity won't open the project, stop and fix that before anything else.

## Phase 1 — Implement `ComputeMeshData()` (2–3 hours)

Single file: [`Assets/Scripts/SurfaceOfRevolution.cs`](modeler-skeleton/Assets/Scripts/SurfaceOfRevolution.cs). The only TODO is inside `ComputeMeshData()` (lines 89–98).

Recommended substeps (matches the spec's suggested order):

1. **Vertices + triangles first, ignore normals/UVs.** Set all normals to `Vector3.up` and UVs to zero so the mesh renders (untextured, flat-shaded). Goal: silhouette of `sample1` looks correct and rotates without glitches.
   - Gotcha: use `(S + 1)` columns, not `S`. The extra column is the UV seam.
   - Gotcha: check triangle winding — if you see through the mesh from outside (black/invisible), swap two indices in each triangle.
2. **Normals.** Compute curve tangent via central difference; rotate 90° for 2D outward normal; rotate around Y for the 3D normal. Validate in "smooth shaded" mode — apex should look smooth, sides should shade correctly with the light.
   - Gotcha: forward diff at `i=0`, backward at `i=N-1`.
   - Gotcha: if your shading looks "inverted" (lit where it should be dark), flip `n2` sign.
3. **UVs.** `(j/S, i/(N-1))`. Switch to "textured" mode; look for visible seam — if seam shows, your seam vertex duplicate is wrong.

**Exit criteria:** all 5 sample curves render correctly in all 4 viewing modes (wireframe, flat, smooth, textured). Compare to the sample-solution build.

## Phase 2 — Hierarchical model (1.5–2 hours)

Open `Hierarchical Modeling.unity`.

1. Create an empty `Root` GameObject at origin.
2. Parent a primitive torso (Cube or Cylinder) to Root.
3. Under torso, add 4 limb roots (`L_Shoulder`, `R_Shoulder`, `L_Hip`, `R_Hip`) as empty GameObjects — rotation points.
4. Under each shoulder: `UpperArm → Forearm → Hand`. Same pattern for legs. That's depth 5 — exceeds the minimum 3.
5. Add a head. Swap **one** part for your exported SoR mesh — e.g. a bell/dome helmet, or replace a hand with a goblet.
   - In the SoR scene: design a shape, click **Export Mesh** to save as `.asset`. Drag it into the Hierarchical scene, attach a `MeshFilter` + `MeshRenderer` + material.
6. Rough-scale primitives so they read as a character. Doesn't need to be pretty — it needs to be *clearly hierarchical*.
7. Verify by rotating a shoulder in Scene view — the whole arm should follow.

## Phase 3 — Three animations (1–1.5 hours)

Add a `CharacterAnimator.cs` MonoBehaviour on `Root` with 3 public methods. Wire each to a UI Button's `OnClick`.

1. `PlaySpin()` — coroutine that rotates Root Y by 360° over ~2 sec.
2. `PlayWalk()` — coroutine with a `while` loop over time `t`, using `Mathf.Sin(t * freq)` to oscillate shoulder/elbow/hip/knee X rotations. Offset arms vs legs by π. Keep it 4–6 seconds; stop or loop N times.
3. `PlayCustom()` — pick something visibly distinct. A jumping jack (symmetric arm/leg spread) or a wave (one arm raise + hand oscillation) are both ~15 min of work.

Guard against button spam by tracking a `bool isAnimating` flag.

## Phase 4 — AR build (1.5–2 hours, highest risk)

1. Select the fully-built character in `Hierarchical Modeling.unity`. Drag from Hierarchy into `Assets/Prefabs/` to save as a prefab.
2. Open `ARScene.unity`. Find the spawn/placement script (Lean Touch examples in the skeleton likely drive this) and set your prefab as the object it instantiates.
3. **Player Settings:**
   - Android: `Minimum API Level 24`, `Target Architecture: ARM64`, `Scripting Backend: IL2CPP`.
   - Bundle ID like `com.yourname.modeler`.
4. Plug phone in, enable USB debug, `File → Build & Run`. First build is slow (5–15 min).
5. Open app on phone, point at a textured floor, tap to place, walk around, record screen.
   - Android screen record: built-in (pull-down quick settings → Screen recorder).

**Escape hatch:** if device deploy fails late at night, record via Unity Remote or as a last resort a video of the scene running in-editor with an AR camera mock — but the spec explicitly wants a real-world deployment, so exhaust device debug first.

## Phase 5 — Video + submission (45 min)

1. Record each section per the spec timings (SPEC.md §Deliverables).
   - OBS Studio (free) for the desktop parts. Phone screen recorder for AR.
2. Concat clips — DaVinci Resolve (free) or even `ffmpeg -f concat`. No fancy editing needed.
3. Put the final video + `SurfaceOfRevolution.cs` in a folder named `<your-netid>`. Zip it.
4. Upload to Canvas.

## Risk register

| Risk | Likelihood | Mitigation |
|---|---|---|
| Unity Android SDK install blows time | High | Start Phase 0 **right now**; it downloads in the background |
| Normals flipped / UV seam visible | Medium | Compare side-by-side to sample solution early (Phase 0) |
| Phone not detected for deploy | Medium | Enable Developer Options + USB Debug before coding; test with `adb devices` |
| Scope creep on character design | High | Box-character is fine. One SoR part is the requirement, not five |

## Stop conditions

If by **9:00 PM PT** Parts 1–3 aren't solid, drop character polish and prioritize getting *any* AR build on-device. A rough character in AR beats a beautiful character with no AR demo.
