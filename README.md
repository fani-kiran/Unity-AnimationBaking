# Unity-AnimationBaking
Create a mesh from a frame of Animation 
Best used for creating a pose from an animated mesh for 3D printing. 

Configure the Script in the Inspector:
1. Assign the SkinnedMeshRenderer of your animated object to the skinnedMeshRenderer field in the Inspector.
2. If using the Animator component, assign the Animator to the animator field.
3. If using the legacy Animation component, assign the AnimationClip you want to bake to the animationClip field.
4. Set the animationTime to the specific point in the animation you want to capture (in seconds).
5. Define the meshSavePath and meshName.

Bake and Save the Mesh:
1. Right-click on the MeshBaker component in the Inspector and select "Bake Animated Mesh".
2. A new .asset file containing the baked mesh will be created in the specified meshSavePath.

Use the Baked Mesh:
You can now create a new GameObject, add a MeshFilter and MeshRenderer component to it, and assign the newly baked mesh to the MeshFilter. This will display the static snapshot of your animated object.
