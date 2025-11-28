    using UnityEngine;
    using UnityEditor; // Required for AssetDatabase
    using System.IO;   // Required for Path

    public class MeshBaker : MonoBehaviour
    {
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public AnimationClip animationClip; // If using the legacy Animation component
        public Animator animator; // If using the Animator component
        public string meshSavePath = "Assets/BakedMeshes/";
        public string meshName = "BakedMesh";
        public float animationTime = 0f; // The time in seconds to bake the mesh

        [ContextMenu("Bake Animated Mesh")]
        void BakeAnimatedMesh()
        {
            if (skinnedMeshRenderer == null)
            {
                Debug.LogError("SkinnedMeshRenderer not assigned!");
                return;
            }

            // Sample the animation at the desired time
            if (animator != null)
            {
                // For Animator, set the normalized time and force an update
                animator.Play(animator.GetCurrentAnimatorStateInfo(0).fullPathHash, 0, animationTime / animator.GetCurrentAnimatorStateInfo(0).length);
                animator.Update(0f); // Force update to apply animation state
            }
            else if (animationClip != null && skinnedMeshRenderer.gameObject.GetComponent<Animation>() != null)
            {
                // For legacy Animation component
                Animation animation = skinnedMeshRenderer.gameObject.GetComponent<Animation>();
                animation.clip = animationClip;
                animation[animationClip.name].time = animationTime;
                animation.Sample();
            }
            else
            {
                Debug.LogWarning("No Animator or Animation component found, or AnimationClip not assigned. Baking current pose.");
            }

            Mesh bakedMesh = new Mesh();
            skinnedMeshRenderer.BakeMesh(bakedMesh);

            // Ensure the save path exists
            if (!AssetDatabase.IsValidFolder(meshSavePath))
            {
                Directory.CreateDirectory(Application.dataPath + "/" + meshSavePath.Replace("Assets/", ""));
                AssetDatabase.Refresh();
            }

            string fullPath = Path.Combine(meshSavePath, meshName + ".asset");
            AssetDatabase.CreateAsset(bakedMesh, AssetDatabase.GenerateUniqueAssetPath(fullPath));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Baked mesh saved to: " + fullPath);
        }
    }