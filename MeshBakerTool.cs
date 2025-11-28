using UnityEngine;
using UnityEditor;
using System.IO;

public class MeshBakerTool : EditorWindow
{
    // === Variables for the tool window ===
    private SkinnedMeshRenderer targetRenderer;
    private Animator targetAnimator;
    private AnimationClip targetAnimationClip;
    
    private string meshName = "BakedMesh";
    private string meshSavePath = "Assets/BakedMeshes/";

    // Variables for frame-based input only
    private int frameNumber = 0;
    private int framesPerSecond = 30; // Default FPS

    // === 1. Menu Item Setup ===
    [MenuItem("Tools/Mesh Baker/Bake Animated Mesh")]
    public static void ShowWindow()
    {
        MeshBakerTool window = GetWindow<MeshBakerTool>("Mesh Baker");
        window.minSize = new Vector2(300, 380); // Adjusted size
    }

    // === 2. Drawing the UI ===
    private void OnGUI()
    {
        // Title style
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        EditorGUILayout.LabelField("Animated Mesh Baker (Frame-Based)", titleStyle);
        EditorGUILayout.Space(10);
        
        // --- Input Fields ---
        
        targetRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(
            "Target Skinned Mesh", targetRenderer, typeof(SkinnedMeshRenderer), true);

        targetAnimator = (Animator)EditorGUILayout.ObjectField(
            "Target Animator (Optional)", targetAnimator, typeof(Animator), true);
            
        targetAnimationClip = (AnimationClip)EditorGUILayout.ObjectField(
            "Target Animation Clip", targetAnimationClip, typeof(AnimationClip), false);

        EditorGUILayout.Space(10);

        // --- Frame Input Only ---
        EditorGUILayout.LabelField("Frame Input", EditorStyles.boldLabel);
        
        framesPerSecond = EditorGUILayout.IntField("Frame Rate (FPS)", framesPerSecond);
        frameNumber = EditorGUILayout.IntField("Frame Number", frameNumber);
        
        // Calculate and display the corresponding time
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
        if (GUILayout.Button("Bake Animated Mesh", GUILayout.Height(40)))
        {
            BakeMesh();
        }
        GUI.backgroundColor = Color.white;
        
        if (GUILayout.Button("Auto-Fill from Selection"))
        {
            AutoFillFromSelection();
        }

        // Display current target status
        if (targetRenderer == null)
        {
            EditorGUILayout.HelpBox("A SkinnedMeshRenderer must be assigned to bake.", MessageType.Error);
        }
    }
    
    // === 3. Helper Functions ===

    private void AutoFillFromSelection()
    {
        if (Selection.activeGameObject != null)
        {
            targetRenderer = Selection.activeGameObject.GetComponentInChildren<SkinnedMeshRenderer>(true);
            targetAnimator = Selection.activeGameObject.GetComponentInParent<Animator>(true);
            
            if (targetRenderer != null)
            {
                meshName = targetRenderer.gameObject.name + "_Baked_F" + frameNumber;
            }
            
            Repaint();
            Debug.Log($"Auto-filled references based on selection: {Selection.activeGameObject.name}");
        }
        else
        {
            Debug.LogWarning("Select a GameObject with an Animator/SkinnedMeshRenderer to auto-fill.");
        }
    }


    // === 4. Core Baking Logic ===
    private void BakeMesh()
    {
        if (targetRenderer == null)
        {
            Debug.LogError("Baking failed: SkinnedMeshRenderer is not assigned!");
            return;
        }
        
        if (framesPerSecond <= 0)
        {
            Debug.LogError("Baking failed: Frame Rate (FPS) must be greater than zero.");
            return;
        }

        // Calculate the final time from the frame number
        float finalBakeTime = (float)frameNumber / framesPerSecond;

        // 1. Sample the Animation Pose
        if (targetAnimator != null)
        {
            var stateInfo = targetAnimator.GetCurrentAnimatorStateInfo(0);
            float length = stateInfo.length > 0 ? stateInfo.length : 1f;
            
            // Use the calculated final time to set the normalized time
            targetAnimator.Play(stateInfo.fullPathHash, 0, finalBakeTime / length);
            targetAnimator.Update(0f);
        }
        else if (targetAnimationClip != null && targetRenderer.GetComponent<Animation>() != null)
        {
            // Logic for Legacy Animation Component
            Animation animation = targetRenderer.GetComponent<Animation>();
            if (animation.GetClip(targetAnimationClip.name) == null)
                animation.AddClip(targetAnimationClip, targetAnimationClip.name);
            
            animation.clip = targetAnimationClip;
            animation[targetAnimationClip.name].time = finalBakeTime;
            animation.Sample();
        }
        else
        {
            Debug.LogWarning("No valid Animator or Animation setup found. Baking current pose.");
        }

        // 2. Create and Bake the Mesh
        Mesh bakedMesh = new Mesh();
        targetRenderer.BakeMesh(bakedMesh);
        bakedMesh.name = meshName;

        // 3. Save to Asset Database
        string fullPath = Path.Combine(meshSavePath, meshName + ".asset");
        fullPath = AssetDatabase.GenerateUniqueAssetPath(fullPath); 
        
        if (!AssetDatabase.IsValidFolder(meshSavePath))
        {
             Directory.CreateDirectory(Application.dataPath + "/" + meshSavePath.Replace("Assets/", ""));
             AssetDatabase.Refresh();
        }
        
        AssetDatabase.CreateAsset(bakedMesh, fullPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=green>SUCCESS:</color> Baked frame <b>{frameNumber}</b> (Time: {finalBakeTime:F4}s) saved to: <b>{fullPath}</b>");
        EditorGUIUtility.PingObject(bakedMesh);
    }
}