/****************************************************************************
 * Copyright ©2021 Khoa Nguyen and Quan Dang. Adapted from CSE 457 Modeler by
 * Brian Curless. All rights reserved. Permission is hereby granted to
 * students registered for University of Washington CSE 457.
 * No other use, copying, distribution, or modification is permitted without
 * prior written consent. Copyrights for third-party components of this work
 * must be honored.  Instructors interested in reusing these course materials
 * should contact the authors below.
 * Khoa Nguyen: https://github.com/akkaneror
 * Quan Dang: https://github.com/QuanGary
 ****************************************************************************/
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Mathf;

/// <summary>
/// SurfaceOfRevolution is responsible for generating a mesh given curve points.
/// </summary>

#if (UNITY_EDITOR)
public class SurfaceOfRevolution : MonoBehaviour
{
    private Mesh mesh;

    private List<Vector2> curvePoints;
    private int _mode;
    private int _numCtrlPts;
    private readonly string _curvePointsFile = "curvePoints.txt";
    private Vector3[] normals;
    private int[] triangles;
    private Vector2[] UVs;
    private Vector3[] vertices;

    private int subdivisions;
    public TextMeshProUGUI subdivisionText;

    private void Start()
    {
        subdivisions = 16;
        subdivisionText.text = "Subdivision: " + subdivisions.ToString();
    }

    private void Update()
    {
    }

    public void Initialize()
    {
        // Create an empty mesh
        mesh = new Mesh();
        mesh.indexFormat =
            UnityEngine.Rendering.IndexFormat.UInt32; // Set Unity's max number of vertices for a mesh to be ~4 billion
        GetComponent<MeshFilter>().mesh = mesh;

        // Load curve points
        ReadCurveFile(_curvePointsFile);

        // Invalid number of control points
        if (_mode == 0 && _numCtrlPts < 4 || _mode == 1 && _numCtrlPts < 2) return;
        
        // Calculate and draw mesh
        ComputeMeshData();
        UpdateMeshData();
    }

    
    /// <summary>
    /// Computes the surface revolution mesh given the curve points and the number of radial subdivisions.
    /// 
    /// Inputs:
    /// curvePoints : the list of sampled points on the curve.
    /// subdivisions: the number of radial subdivisions
    /// 
    /// Outputs:
    /// vertices : a list of `Vector3` containing the vertex positions
    /// normals  : a list of `Vector3` containing the vertex normals. The normal should be pointing out of
    ///            the mesh.
    /// UVs      : a list of `Vector2` containing the texture coordinates of each vertex
    /// triangles: an integer array containing vertex indices (of the `vertices` list). The first three
    ///            elements describe the first triangle, the fourth to sixth elements describe the second
    ///            triangle, and so on. The vertex must be oriented counterclockwise when viewed from the 
    ///            outside.
    /// </summary>
    private void ComputeMeshData()
    {
        // TODO: Compute and set vertex positions, normals, UVs, and triangle faces
        // You will want to use curvePoints and subdivisions variables, and you will
        // want to change the size of these arrays
        int rowCount = curvePoints.Count;
        if (rowCount < 2 || subdivisions < 1)
        {
            vertices = Array.Empty<Vector3>();
            normals = Array.Empty<Vector3>();
            UVs = Array.Empty<Vector2>();
            triangles = Array.Empty<int>();
            return;
        }

        // Dupe θ = 0 column at θ = 2π
        int columnCount = subdivisions + 1;
        int vertexTotal = rowCount * columnCount;

        vertices = new Vector3[vertexTotal];
        normals = new Vector3[vertexTotal];
        UVs = new Vector2[vertexTotal];

        float vStep = 1f / (rowCount - 1);
        float thetaStep = (2f * PI) / subdivisions;

        for (int row = 0; row < rowCount; row++)
        {
            Vector2 cp = curvePoints[row];
            float radius = cp.x;
            float height = cp.y;
            float v = row * vStep;
            int rowStart = row * columnCount;

            for (int col = 0; col < columnCount; col++)
            {
                float theta = col * thetaStep;
                float u = (float)col / subdivisions;
                int idx = rowStart + col;
                vertices[idx] = new Vector3(radius * Cos(theta), height, radius * Sin(theta));
                UVs[idx] = new Vector2(u, v);
            }
        }

        triangles = new int[(rowCount - 1) * subdivisions * 6];
        int writePos = 0;

        for (int row = 0; row < rowCount - 1; row++)
        {
            int lowerBase = row * columnCount;
            int upperBase = lowerBase + columnCount;

            for (int col = 0; col < subdivisions; col++)
            {
                int a = lowerBase + col;
                int b = a + 1;
                int c = upperBase + col;
                int d = c + 1;

                // this part hurt me, lowk maybe put debugging next time, my thing was inside out sometimes
                triangles[writePos++] = a;
                triangles[writePos++] = b;
                triangles[writePos++] = c;
                triangles[writePos++] = c;
                triangles[writePos++] = b;
                triangles[writePos++] = d;
            }
        }

        for (int t = 0; t < triangles.Length; t += 3)
        {
            int a = triangles[t];
            int b = triangles[t + 1];
            int c = triangles[t + 2];
            Vector3 faceNormal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            normals[a] += faceNormal;
            normals[b] += faceNormal;
            normals[c] += faceNormal;
        }

        for (int i = 0; i < vertexTotal; i++)
        {
            normals[i] = normals[i].sqrMagnitude > 1e-20f ? normals[i].normalized : Vector3.up;
        }
    }

    private void UpdateMeshData()
    {
        // Assign data to mesh
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.uv = UVs;
    }

    // Export mesh as an asset
    public void ExportMesh()
    {
        string path = EditorUtility.SaveFilePanel("Save Mesh Asset", "Assets/ExportedMesh/", mesh.name, "asset");
        if (string.IsNullOrEmpty(path)) return;
        path = FileUtil.GetProjectRelativePath(path);
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
    }

    public void SubdivisionValueChanged(Slider slider)
    {
        subdivisions = (int)slider.value;
        subdivisionText.text = "Subdivision: " + subdivisions.ToString();
    }
    
    private void ReadCurveFile(string file)
    {
        curvePoints = new List<Vector2>();
        string line;

        var f =
            new StreamReader(file);
        if ((line = f.ReadLine()) != null)
        {
            var curveData = line.Split(' ');
            _mode = Convert.ToInt32(curveData[0]);
            _numCtrlPts = Convert.ToInt32(curveData[1]);
        }

        while ((line = f.ReadLine()) != null)
        {
            var curvePoint = line.Split(' ');
            var x = float.Parse(curvePoint[0]);
            var y = float.Parse(curvePoint[1]);
            curvePoints.Add(new Vector2(x, y));
        }

        f.Close();
    }
}
#endif
