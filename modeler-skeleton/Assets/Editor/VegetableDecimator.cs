using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;

// Editor utility for decimating the Tripo-generated vegetable meshes.
//
// Why this exists: Tripo exports raw high-density meshes (~60 MB FBX, often
// millions of triangles per vegetable). They're far too heavy to render at
// kitchen-prop scale.
//
// Two flows:
//   1. "Decimate from Source"     — bake fresh .asset meshes from the original
//                                    FBXs under Assets/Materials/Vegetables/.
//                                    Slow (input is huge), but highest quality
//                                    for a given target tri count.
//   2. "Re-decimate Existing"     — run the simplifier on the already-baked
//                                    .asset meshes in the Decimated/ folder.
//                                    Much faster because input is already
//                                    small. Updates the same asset GUIDs in
//                                    place, so prefab references survive — no
//                                    re-repoint needed.
//
// After the first flow you must also run "Repoint Container Prefabs" so the
// prefabs stop referencing the original FBX meshes. After the second flow you
// don't need to repoint — the asset is overwritten in place.
public static class VegetableDecimator
{
    const string VegetablesRoot = "Assets/Materials/Vegetables";
    const string DecimatedFolder = "Assets/Materials/Vegetables/Decimated";
    // Drop hand-picked mesh copies here (e.g. just the carrots) and use the
    // Tools/Vegetables/Custom menu to decimate them without touching the rest.
    const string CustomFolder = "Assets/Materials/Vegetables/Decimated/Custom";

    // Carrots-only source folders — for the "Carrots Only" submenu. These get
    // scanned recursively, so any *.fbx under them is fair game.
    static readonly string[] CarrotSourceFolders = new[]
    {
        "Assets/Materials/Vegetables/carrot+1",
        "Assets/Materials/Vegetables/carrot+2",
        "Assets/Materials/Vegetables/carrot+3",
        "Assets/Materials/Vegetables/carrot+4",
    };

    // -------------------------------------------------------- Decimate from source

    [MenuItem("Tools/Vegetables/Decimate from Source/10%")]
    public static void DecimateSource10() => DecimateFromSource(new[] { VegetablesRoot }, 0.10f);

    [MenuItem("Tools/Vegetables/Decimate from Source/5%")]
    public static void DecimateSource5() => DecimateFromSource(new[] { VegetablesRoot }, 0.05f);

    [MenuItem("Tools/Vegetables/Decimate from Source/2%")]
    public static void DecimateSource2() => DecimateFromSource(new[] { VegetablesRoot }, 0.02f);

    [MenuItem("Tools/Vegetables/Decimate from Source/0.5%")]
    public static void DecimateSourcePoint5() => DecimateFromSource(new[] { VegetablesRoot }, 0.005f);

    [MenuItem("Tools/Vegetables/Decimate from Source/0.1%")]
    public static void DecimateSourcePoint1() => DecimateFromSource(new[] { VegetablesRoot }, 0.001f);

    // -------------------------------------------------- Carrots only (from source)
    //
    // Re-bakes ONLY the carrot+1..carrot+4 source FBXs. Use this when you want
    // to push carrots further than the rest of the veg without touching them.
    // Overwrites the matching files in Decimated/ in place, so prefab refs survive.

    [MenuItem("Tools/Vegetables/Carrots Only/From Source 2%")]
    public static void DecimateCarrots2() => DecimateFromSource(CarrotSourceFolders, 0.02f);

    [MenuItem("Tools/Vegetables/Carrots Only/From Source 1%")]
    public static void DecimateCarrots1() => DecimateFromSource(CarrotSourceFolders, 0.01f);

    [MenuItem("Tools/Vegetables/Carrots Only/From Source 0.5%")]
    public static void DecimateCarrotsPoint5() => DecimateFromSource(CarrotSourceFolders, 0.005f);

    [MenuItem("Tools/Vegetables/Carrots Only/From Source 0.1%")]
    public static void DecimateCarrotsPoint1() => DecimateFromSource(CarrotSourceFolders, 0.001f);

    // -------------------------------------------------- Re-decimate existing assets
    //
    // These run on whatever is already in Decimated/. Quality is the fraction
    // kept relative to the *current* mesh, so it compounds with whatever pass
    // produced the existing assets.
    //
    // If your current Decimated/ is at 5% of source:
    //   40% of current = 2.0% of source
    //   20% of current = 1.0% of source
    //   10% of current = 0.5% of source
    //    2% of current = 0.1% of source

    [MenuItem("Tools/Vegetables/Re-decimate Existing/40%")]
    public static void Redecimate40() => RedecimateExisting(0.40f);

    [MenuItem("Tools/Vegetables/Re-decimate Existing/20%")]
    public static void Redecimate20() => RedecimateExisting(0.20f);

    [MenuItem("Tools/Vegetables/Re-decimate Existing/10%")]
    public static void Redecimate10() => RedecimateExisting(0.10f);

    [MenuItem("Tools/Vegetables/Re-decimate Existing/2%")]
    public static void Redecimate2() => RedecimateExisting(0.02f);

    // --------------------------------------------------------------- Custom folder
    //
    // Runs the simplifier on whatever .asset meshes are sitting in
    // CustomFolder. Use this when you want to push a specific subset (e.g. just
    // the carrots) further than the rest without re-running the global pass.
    //
    // Same in-place overwrite trick as Re-decimate Existing: the asset GUID is
    // preserved, so any prefab references survive.

    [MenuItem("Tools/Vegetables/Custom/Decimate Custom Folder (2%)")]
    public static void DecimateCustom2() => DecimateFolder(CustomFolder, 0.02f);

    [MenuItem("Tools/Vegetables/Custom/Decimate Custom Folder (5%)")]
    public static void DecimateCustom5() => DecimateFolder(CustomFolder, 0.05f);

    [MenuItem("Tools/Vegetables/Custom/Decimate Custom Folder (10%)")]
    public static void DecimateCustom10() => DecimateFolder(CustomFolder, 0.10f);

    [MenuItem("Tools/Vegetables/Custom/Decimate Custom Folder (1%)")]
    public static void DecimateCustom1() => DecimateFolder(CustomFolder, 0.01f);

    [MenuItem("Tools/Vegetables/Custom/Report Tri Counts of Custom/")]
    public static void ReportCustomTriCounts() => ReportTriCountsIn(CustomFolder);

    // ------------------------------------------------------------- Repoint prefabs

    [MenuItem("Tools/Vegetables/Repoint Container Prefabs to Decimated Meshes")]
    public static void RepointPrefabs()
    {
        if (!AssetDatabase.IsValidFolder(DecimatedFolder))
        {
            EditorUtility.DisplayDialog(
                "No decimated meshes found",
                $"Run 'Tools/Vegetables/Decimate from Source' first — '{DecimatedFolder}' doesn't exist yet.",
                "OK");
            return;
        }

        var swapMap = BuildSwapMap();
        if (swapMap.Count == 0)
        {
            EditorUtility.DisplayDialog("Nothing to repoint",
                "Couldn't find any baked decimated meshes that map back to a source mesh. " +
                "Re-run the decimation step.", "OK");
            return;
        }

        var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
        int totalSwaps = 0;
        int prefabsTouched = 0;

        foreach (var guid in prefabGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int swaps = SwapMeshFiltersIn(root, swapMap);
                if (swaps > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log($"[VegetableDecimator] Repointed {swaps} MeshFilter(s) in {path}");
                    totalSwaps += swaps;
                    prefabsTouched++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        Debug.Log($"[VegetableDecimator] Done. Repointed {totalSwaps} MeshFilter(s) across {prefabsTouched} prefab(s).");
        EditorUtility.DisplayDialog("Repoint complete",
            $"Repointed {totalSwaps} MeshFilter reference(s) across {prefabsTouched} prefab(s).",
            "OK");
    }

    // ------------------------------------------------------------------ Diagnostic

    [MenuItem("Tools/Vegetables/Report Tri Counts of Decimated/")]
    public static void ReportDecimatedTriCounts() => ReportTriCountsIn(DecimatedFolder);

    static void ReportTriCountsIn(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogWarning($"[VegetableDecimator] '{folder}' doesn't exist.");
            return;
        }

        var meshGuids = AssetDatabase.FindAssets("t:Mesh", new[] { folder });
        long total = 0;
        foreach (var guid in meshGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) continue;
            long tris = mesh.triangles.Length / 3;
            total += tris;
            Debug.Log($"[VegetableDecimator] {Path.GetFileName(path)}  {tris:N0} tris  ({mesh.vertexCount:N0} verts)");
        }
        Debug.Log($"[VegetableDecimator] Total across {folder}: {total:N0} tris.");
    }

    // ============================================================================
    // Source decimation
    // ============================================================================

    static void DecimateFromSource(string[] sourceFolders, float quality)
    {
        // Filter to folders that actually exist so a typo in the constants
        // doesn't silently make the whole pass a no-op.
        var validFolders = new List<string>();
        foreach (var f in sourceFolders)
        {
            if (AssetDatabase.IsValidFolder(f)) validFolders.Add(f);
            else Debug.LogWarning($"[VegetableDecimator] Source folder '{f}' doesn't exist — skipping.");
        }
        if (validFolders.Count == 0)
        {
            EditorUtility.DisplayDialog("No valid source folders",
                "None of the configured source folders exist. Check the constants in VegetableDecimator.cs.", "OK");
            return;
        }

        EnsureFolder(DecimatedFolder);

        var fbxGuids = AssetDatabase.FindAssets("t:Model", validFolders.ToArray());
        if (fbxGuids.Length == 0)
        {
            Debug.LogWarning($"[VegetableDecimator] No FBX models found under {string.Join(", ", validFolders)}.");
            return;
        }

        long totalTrisBefore = 0;
        long totalTrisAfter = 0;
        int meshesWritten = 0;

        try
        {
            for (int i = 0; i < fbxGuids.Length; i++)
            {
                var fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuids[i]);
                EditorUtility.DisplayProgressBar(
                    "Decimating Vegetables (from source)",
                    $"({i + 1}/{fbxGuids.Length}) {Path.GetFileName(fbxPath)}",
                    (float)i / fbxGuids.Length);

                var subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
                foreach (var sub in subAssets)
                {
                    var mesh = sub as Mesh;
                    if (mesh == null) continue;

                    long trisBefore = mesh.triangles.Length / 3;

                    var simplifier = new MeshSimplifier();
                    simplifier.Initialize(mesh);
                    simplifier.SimplifyMesh(quality);
                    var decimated = simplifier.ToMesh();
                    decimated.name = mesh.name + "_LOD";
                    decimated.RecalculateBounds();
                    decimated.Optimize();
                    // NOTE: do NOT call UploadMeshData(markNoLongerReadable: true) here.
                    // That seals the mesh and breaks any future Re-decimate pass on it.
                    // For runtime memory savings in a build, toggle Read/Write Enabled
                    // on the asset import inspector instead.

                    long trisAfter = decimated.triangles.Length / 3;

                    string assetPath = DecimatedAssetPathFor(fbxPath, mesh.name);
                    EnsureFolder(Path.GetDirectoryName(assetPath).Replace('\\', '/'));

                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                    if (existing != null)
                    {
                        // Overwrite in place so existing prefab references survive.
                        EditorUtility.CopySerialized(decimated, existing);
                    }
                    else
                    {
                        AssetDatabase.CreateAsset(decimated, assetPath);
                    }

                    totalTrisBefore += trisBefore;
                    totalTrisAfter += trisAfter;
                    meshesWritten++;

                    Debug.Log(
                        $"[VegetableDecimator] {Path.GetFileName(fbxPath)} :: {mesh.name}  " +
                        $"{trisBefore:N0} → {trisAfter:N0} tris  " +
                        $"({(trisAfter / (float)trisBefore) * 100f:F2}%)");
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log(
            $"[VegetableDecimator] Source decimate DONE. Wrote {meshesWritten} mesh(es). " +
            $"Total: {totalTrisBefore:N0} → {totalTrisAfter:N0} tris " +
            $"({(totalTrisAfter / (float)totalTrisBefore) * 100f:F2}%). " +
            $"Next: 'Tools/Vegetables/Repoint Container Prefabs to Decimated Meshes'.");
    }

    // ============================================================================
    // Re-decimate already-decimated assets
    // ============================================================================

    static void RedecimateExisting(float quality) => DecimateFolder(DecimatedFolder, quality);

    static void DecimateFolder(string folder, float quality)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            EditorUtility.DisplayDialog("Folder not found",
                $"'{folder}' doesn't exist. Create it and drop mesh .asset files inside, then re-run.", "OK");
            return;
        }

        var meshGuids = AssetDatabase.FindAssets("t:Mesh", new[] { folder });
        if (meshGuids.Length == 0)
        {
            Debug.LogWarning($"[VegetableDecimator] No meshes found in {folder}.");
            return;
        }

        long totalBefore = 0, totalAfter = 0;
        int meshesProcessed = 0;

        try
        {
            for (int i = 0; i < meshGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(meshGuids[i]);
                EditorUtility.DisplayProgressBar("Re-decimating existing",
                    $"({i + 1}/{meshGuids.Length}) {Path.GetFileName(path)}",
                    (float)i / meshGuids.Length);

                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null) continue;

                // Sealed meshes can't be re-simplified. For those, the recovery
                // path is to re-bake from source via the "Decimate from Source"
                // (or "Carrots Only / From Source") menu, which reads from FBX.
                if (!mesh.isReadable)
                {
                    Debug.LogWarning(
                        $"[VegetableDecimator] {Path.GetFileName(path)} is non-readable. " +
                        $"Re-bake from source via Tools/Vegetables/Decimate from Source.");
                    continue;
                }

                long before = mesh.triangles.Length / 3;

                var simplifier = new MeshSimplifier();
                simplifier.Initialize(mesh);
                simplifier.SimplifyMesh(quality);
                var newMesh = simplifier.ToMesh();
                newMesh.name = mesh.name;
                newMesh.RecalculateBounds();
                newMesh.Optimize();
                // No UploadMeshData seal — keep the mesh re-simplifiable on the next pass.

                // Overwrite the existing asset in place — preserves the GUID, so
                // every prefab reference keeps working without re-repointing.
                EditorUtility.CopySerialized(newMesh, mesh);

                long after = mesh.triangles.Length / 3;
                totalBefore += before;
                totalAfter += after;
                meshesProcessed++;

                Debug.Log(
                    $"[VegetableDecimator] {Path.GetFileName(path)}  " +
                    $"{before:N0} → {after:N0} tris  " +
                    $"({(after / (float)before) * 100f:F2}%)");
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log(
            $"[VegetableDecimator] Folder decimate DONE on '{folder}'. " +
            $"Processed {meshesProcessed} mesh(es). " +
            $"Total: {totalBefore:N0} → {totalAfter:N0} tris " +
            $"({(totalAfter / (float)totalBefore) * 100f:F2}%). " +
            $"Prefab references unchanged — no repoint needed.");
    }

    // ============================================================================
    // Path helpers
    // ============================================================================

    // Decimated mesh path for a given source FBX + mesh name.
    // Example: Assets/Materials/Vegetables/Decimated/carrot+1__carrot1.asset
    static string DecimatedAssetPathFor(string fbxPath, string meshName)
    {
        string parentFolder = Path.GetFileName(Path.GetDirectoryName(fbxPath));
        string safeMesh = SanitizeFileName(meshName);
        string safeParent = SanitizeFileName(parentFolder);
        return $"{DecimatedFolder}/{safeParent}__{safeMesh}.asset";
    }

    static string SanitizeFileName(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        return s;
    }

    // ============================================================================
    // Prefab repoint
    // ============================================================================

    static Dictionary<Mesh, Mesh> BuildSwapMap()
    {
        var map = new Dictionary<Mesh, Mesh>();
        var fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { VegetablesRoot });

        foreach (var guid in fbxGuids)
        {
            var fbxPath = AssetDatabase.GUIDToAssetPath(guid);
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var sub in subAssets)
            {
                var mesh = sub as Mesh;
                if (mesh == null) continue;

                string decimatedPath = DecimatedAssetPathFor(fbxPath, mesh.name);
                var decimated = AssetDatabase.LoadAssetAtPath<Mesh>(decimatedPath);
                if (decimated != null)
                {
                    map[mesh] = decimated;
                }
            }
        }

        return map;
    }

    static int SwapMeshFiltersIn(GameObject root, Dictionary<Mesh, Mesh> swapMap)
    {
        int swaps = 0;

        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(includeInactive: true))
        {
            if (mf.sharedMesh != null && swapMap.TryGetValue(mf.sharedMesh, out var replacement))
            {
                Undo.RecordObject(mf, "Repoint to decimated mesh");
                mf.sharedMesh = replacement;
                swaps++;
            }
        }

        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: true))
        {
            if (smr.sharedMesh != null && swapMap.TryGetValue(smr.sharedMesh, out var replacement))
            {
                Undo.RecordObject(smr, "Repoint to decimated mesh");
                smr.sharedMesh = replacement;
                swaps++;
            }
        }

        return swaps;
    }

    // ============================================================================
    // Misc
    // ============================================================================

    static void EnsureFolder(string folderPath)
    {
        folderPath = folderPath.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        var parts = folderPath.Split('/');
        string current = parts[0]; // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }
}
