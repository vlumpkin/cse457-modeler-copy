# CSE 457 Project 2: Modeler — Spec

**Course page:** https://courses.cs.washington.edu/courses/cse457/26sp/project/modeler/
**Deadline:** Tuesday, April 21 2026 @ 11:00 PM (PT) — **today**
**Skeleton:** `modeler-skeleton/` (cloned from gitlab.cs.washington.edu/cse457-26sp/modeler-skeleton)

---

## Stack

- **Engine:** Unity 6000.3.1f1 LTS (or later)
- **AR:** ARCore (Android) or ARKit (iOS) via Unity's AR Foundation
- **Language:** C# (single script to modify: `Assets/Scripts/SurfaceOfRevolution.cs`)
- **Scenes:** `SurfaceOfRevolution.unity`, `Hierarchical Modeling.unity`, `ARScene.unity`

## Deliverables (ZIP to Canvas, folder named `<netid>`)

1. `SurfaceOfRevolution.cs` — the modified script.
2. A ~4-minute demo video:
   - **~2:00** — Surface of revolution with all 5 sample curves (sample1–sample5), each shown in all 4 viewing modes, mesh rotating.
   - **~1:00** — Hierarchical scene: expand full hierarchy in Inspector; select each component to highlight the body part it drives.
   - **~0:30** — The 3 button-triggered animations.
   - **~1:00** — AR video with you visible in the frame, placing/interacting with the model in a real environment.

## Part 1 — Surface of Revolution

Implement `ComputeMeshData()` in [SurfaceOfRevolution.cs](modeler-skeleton/Assets/Scripts/SurfaceOfRevolution.cs) so the mesh is built from `curvePoints` (already-sampled 2D points) and `subdivisions` (radial slices).

Inputs:
- `curvePoints : List<Vector2>` — sampled points `(x_i, y_i)` on the profile curve, ordered along the curve. `x_i` is the radius at height `y_i`.
- `subdivisions : int` — number of radial slices around the axis of revolution (Y).

Outputs (class fields):
- `vertices : Vector3[]`
- `normals : Vector3[]` — must point *out* of the mesh.
- `UVs : Vector2[]` — `u,v ∈ [0,1]`. **Duplicate the θ=0 column of vertices at θ=2π** so UVs can wrap cleanly (this is stated explicitly in the spec).
- `triangles : int[]` — CCW when viewed from outside the surface.

Implementation order recommended by the spec: **vertices & triangles → normals → UVs.**

### Key formulas

Let `N = curvePoints.Count`, `S = subdivisions`. Vertex grid is `N × (S + 1)` (the extra column is the seam duplicate).

- Index: `idx(i, j) = i * (S + 1) + j`, with `i ∈ [0, N-1]`, `j ∈ [0, S]`.
- Angle: `θ_j = 2π * j / S`.
- Position: `vertices[idx(i,j)] = (x_i cos θ_j, y_i, x_i sin θ_j)`.
- UV: `UVs[idx(i,j)] = (j / S, i / (N-1))`.
- Triangle for each cell `(i, j)` with `i < N-1`, `j < S`:
  - CCW-from-outside quad split — confirm winding in editor and flip if the mesh renders inside-out. A sensible starting winding:
    - `(idx(i,j), idx(i+1,j), idx(i+1,j+1))`
    - `(idx(i,j), idx(i+1,j+1), idx(i,j+1))`
- Normal:
  1. 2D curve tangent `T_i = normalize(P_{i+1} - P_{i-1})` (use forward/backward diff at endpoints).
  2. 2D outward normal of the profile: `n2_i = (T_i.y, -T_i.x)` (rotate tangent 90° CW). Flip sign if it ends up pointing toward the Y-axis.
  3. 3D normal at `(i, j)`: `(n2_i.x cos θ_j, n2_i.y, n2_i.x sin θ_j)`, then normalize.

### Edge cases to handle

- **Apex / axis touch.** If `x_i == 0` (curve touches the axis), all `S+1` verts at that ring coincide; UVs still need to be laid out so the texture doesn't pinch badly — usually fine with standard `u = j/S`.
- **Seam.** The `j = S` column must be positionally identical to `j = 0` but have `u = 1.0` instead of `0.0`. Do *not* reuse indices across the seam.
- **Invalid input.** `Initialize()` already early-returns if control-point counts are too low for the curve mode — no need to re-check.

### Visual validation

The scene has 4 viewing modes (wireframe, flat, smooth, textured per the course slides). Rotate each of `sample1`–`sample5` in all modes; watch for:
- Black facets (normals flipped)
- Visible seam line (UV seam handled wrong)
- Self-intersection / twisted winding (triangle index order wrong)

## Part 2 — Hierarchical Modeling

In `Hierarchical Modeling.unity`:
- Build a humanoid (or humanoid-like character) with **hierarchy depth ≥ 3** in the GameObject tree (e.g. `Root → Torso → UpperArm → Forearm → Hand`).
- Use primitive GameObjects (Cube, Sphere, Cylinder) **plus at least one custom Surface-of-Revolution mesh** you exported from Part 1 as a prefab / `.asset` (there's an `ExportMesh` button on the SoR scene).
- Pick an SoR shape that earns its spot — e.g. a bell-shaped helmet, vase body, goblet-hand, torpedo torso.
- Parent transforms so rotating an arm rotates the forearm & hand with it.

## Part 3 — Animations

Three UI buttons in the Hierarchical scene, each triggers one animation. Use a coroutine per button or Unity's Animator — either is fine for a 1-night job; coroutines are simpler.

1. **Full body rotation** — 360° around Y.
2. **Walking cycle** — alternating hip/knee and shoulder/elbow rotations; ~2-second loop.
3. **Creative** — your pick (wave, jump, dance, faint). Must visibly exercise the hierarchy.

Reference: the MazeGame project (linked on course page) shows the button wiring pattern.

## Part 4 — AR Deployment

1. Export the Hierarchical model as a **prefab**.
2. Drop the prefab into `ARScene.unity` as the object instantiated on tap/plane detection.
3. Build & deploy to an Android (ARCore) or iOS (ARKit) device. Android is lower-friction if you don't have a Mac + Apple dev account.
4. Record the AR demo video with yourself visible in frame.

## Grading (as surfaced from the spec)

Not explicitly weighted on the page, but evaluation hits:
- SoR correctness (positions, normals, UVs, winding) — all 5 sample curves, 4 modes
- Hierarchy design & depth ≥ 3
- Animation functionality — all 3 buttons work
- AR deployment success + video documentation

## Out of scope (explicitly)

- No custom shaders, no lighting changes, no new curve interpolation methods required. Don't yak-shave.
