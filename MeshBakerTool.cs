using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class MeshBakerTool : EditorWindow
{
    // === Variables for the tool window ===
    private GameObject targetRoot;
    
    private string meshName = "CombinedBakedMesh";
    private string meshSavePath = "Assets/BakedMeshes/";

    // Variables for frame-based input
    private int frameNumber = 0;
    private int framesPerSecond = 30; // Default FPS

    // === 1. Menu Item Setup ===
    [MenuItem("Tools/Mesh Baker/Bake Combined Mesh")]
    public static void ShowWindow()
    {
        MeshBakerTool window = GetWindow<MeshBakerTool>("Combined Mesh Baker");
        window.minSize = new Vector2(300, 380); 
    }

    // === 2. Drawing the UI ===
    private void OnGUI()
    {
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        EditorGUILayout.LabelField("Combined Animated Mesh Baker", titleStyle);
        EditorGUILayout.Space(10);
        
        // --- Target Input ---
        targetRoot = (GameObject)EditorGUILayout.ObjectField(
            "Target Parent Object (Root)", targetRoot, typeof(GameObject), true);

        EditorGUILayout.Space(5);

        // --- Frame Input Only ---
        EditorGUILayout.LabelField("Frame Input", EditorStyles.boldLabel);
        
        framesPerSecond = EditorGUILayout.IntField("Frame Rate (FPS)", framesPerSecond);
        frameNumber = EditorGUILayout.IntField("Frame Number", frameNumber);
        
        if (framesPerSecond > 0)
        {
            float calculatedTime = (float)frameNumber / framesPerSecond;
            EditorGUILayout.LabelField("Calculated Time (s):", calculatedTime.ToString("F4"));
        }
        
        EditorGUILayout.Space(10);

        // --- Save Settings ---
        EditorGUILayout.LabelField("Save Settings", EditorStyles.boldLabel);
        meshName = EditorGUILayout.TextField("Baked Mesh Name", meshName);
        meshSavePath = EditorGUILayout.TextField("Save Path (Assets/...) ", meshSavePath);
        
        EditorGUILayout.Space(20);

        // --- Action Buttons ---
        
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Bake Combined Mesh", GUILayout.Height(40)))
        {
            BakeCombinedMesh();
        }
        GUI.backgroundColor = Color.white;
        
        if (GUILayout.Button("Auto-Fill from Selection"))
        {
            AutoFillFromSelection();
        }

        if (targetRoot == null)
        {
            EditorGUILayout.HelpBox("The Parent Object must be assigned.", MessageType.Error);
        }
    }
    
    // === 3. Helper Functions ===

    private void AutoFillFromSelection()
    {
        if (Selection.activeGameObject != null)
        {
            targetRoot = Selection.activeGameObject;
            
            if (targetRoot != null)
            {
                meshName = targetRoot.name + "_Combined_F" + frameNumber;
            }
            
            Repaint();
            Debug.Log($"Auto-filled references based on selection: {Selection.activeGameObject.name}");
        }
        else
        {
            Debug.LogWarning("Select the root GameObject with the Animator to auto-fill.");
        }
    }

    // === 4. Core Baking and Combining Logic ===
    private void BakeCombinedMesh()
    {
        if (targetRoot == null)
        {
            Debug.LogError("Baking failed: Target Parent Object is not assigned!");
            return;
        }

        SkinnedMeshRenderer[] renderers = targetRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        if (renderers.Length == 0)
        {
            Debug.LogError("Baking failed: No SkinnedMeshRenderers found on children of the target root!");
            return;
        }

        if (framesPerSecond <= 0)
        {
            Debug.LogError("Baking failed: Frame Rate (FPS) must be greater than zero.");
            return;
        }

        // --- A. Setup Animation Pose ---
        float finalBakeTime = (float)frameNumber / framesPerSecond;
        Animator targetAnimator = targetRoot.GetComponent<Animator>(); 

        if (targetAnimator != null)
        {
            AnimatorStateInfo stateInfo = targetAnimator.GetCurrentAnimatorStateInfo(0);
            float length = stateInfo.length > 0 ? stateInfo.length : 1f;
            
            targetAnimator.Play(stateInfo.fullPathHash, 0, finalBakeTime / length);
            targetAnimator.Update(0f);
        }
        else
        {
            Debug.LogWarning("Animator not found on root. Baking current pose without animation sampling.");
        }

        // --- B. Bake and Combine Meshes ---
        List<Vector3> combinedVertices = new List<Vector3>();
        List<Vector3> combinedNormals = new List<Vector3>();
        List<Vector2> combinedUVs = new List<Vector2>();
        List<int> combinedTriangles = new List<int>();
        int vertexOffset = 0;
        bool shouldRecalculateNormals = false;

        foreach (SkinnedMeshRenderer smr in renderers)
        {
            Mesh bakedSubMesh = new Mesh();
            smr.BakeMesh(bakedSubMesh);

            if (bakedSubMesh.vertexCount == 0)
            {
                DestroyImmediate(bakedSubMesh);
                continue;
            }

            Vector3[] subVertices = bakedSubMesh.vertices;
            Vector3[] subNormals = bakedSubMesh.normals;
            Vector2[] subUVs = bakedSubMesh.uv;

            bool hasNormals = subNormals != null && subNormals.Length > 0;
            bool hasUVs = subUVs != null && subUVs.Length > 0;

            // Prepare placeholder arrays if data is missing, guaranteeing correct array size
            if (!hasNormals)
            {
                 subNormals = new Vector3[subVertices.Length];
                 shouldRecalculateNormals = true;
            }
            if (!hasUVs)
            {
                 subUVs = new Vector2[subVertices.Length];
            }

            // Calculate the matrix to transform the sub-mesh vertices from their local space 
            // into the targetRoot's local space.
            Matrix4x4 rootInverse = targetRoot.transform.worldToLocalMatrix;
            Matrix4x4 finalMatrix = rootInverse * smr.transform.localToWorldMatrix;

            for (int i = 0; i < subVertices.Length; i++)
            {
                combinedVertices.Add(finalMatrix.MultiplyPoint(subVertices[i]));
                
                // Normals: Transform if present, otherwise add placeholder (Vector3.zero)
                if (hasNormals)
                {
                    combinedNormals.Add(finalMatrix.MultiplyVector(subNormals[i]).normalized);
                }
                else
                {
                    combinedNormals.Add(Vector3.zero); 
                }

                // UVs: Safe to add now since subUVs is guaranteed to be the correct size (even if empty values)
                combinedUVs.Add(subUVs[i]); 
            }
            
            // Triangles: Iterate through all submeshes/materials of the current component
            for (int sub = 0; sub < bakedSubMesh.subMeshCount; sub++)
            {
                int[] subTriangles = bakedSubMesh.GetTriangles(sub);
                for (int i = 0; i < subTriangles.Length; i++)
                {
                    // Offset the triangle indices by the total vertices added so far
                    combinedTriangles.Add(subTriangles[i] + vertexOffset);
                }
            }
            
            vertexOffset += subVertices.Length;

            DestroyImmediate(bakedSubMesh);
        }

        // --- C. Create Final Combined Mesh Asset ---
        
        Mesh finalMesh = new Mesh();
        finalMesh.name = meshName;
        finalMesh.vertices = combinedVertices.ToArray();
        finalMesh.normals = combinedNormals.ToArray();
        finalMesh.uv = combinedUVs.ToArray();
        
        // Since we combined all triangles into one list, we create one submesh.
        finalMesh.SetTriangles(combinedTriangles.ToArray(), 0); 
        
        // Recalculate normals if any of the source meshes were missing them
        if (shouldRecalculateNormals)
        {
            finalMesh.RecalculateNormals();
        }
        
        finalMesh.RecalculateBounds();
        finalMesh.Optimize(); 
        
        // --- D. Save to Asset Database ---
        string fullPath = Path.Combine(meshSavePath, finalMesh.name + ".asset");
        fullPath = AssetDatabase.GenerateUniqueAssetPath(fullPath); 
        
        if (!AssetDatabase.IsValidFolder(meshSavePath))
        {
             Directory.CreateDirectory(Application.dataPath + "/" + meshSavePath.Replace("Assets/", ""));
             AssetDatabase.Refresh();
        }
        
        AssetDatabase.CreateAsset(finalMesh, fullPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=green>SUCCESS:</color> Baked **{renderers.Length}** meshes into one at frame <b>{frameNumber}</b> saved to: <b>{fullPath}</b>");
        EditorGUIUtility.PingObject(finalMesh);
    }
}
