using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Linq;

public class SetupPlayerAnimations : EditorWindow
{
    [MenuItem("Tools/Setup Player Animations")]
    public static void ExecuteSetup()
    {
        Debug.Log("Starting Player Animations Setup...");

        // 1. Locate and Load Sprite Sheets
        string runPath = "Assets/player/Run.png";
        string kickPath = "Assets/player/Kick.png";
        string jumpStrikePath = "Assets/player/Jump_Strike.png";

        if (!File.Exists(runPath))
        {
            Debug.LogError($"Could not find Run spritesheet at: {runPath}");
            return;
        }

        if (!File.Exists(kickPath))
        {
            Debug.LogError($"Could not find Kick spritesheet at: {kickPath}");
            return;
        }

        if (!File.Exists(jumpStrikePath))
        {
            Debug.LogError($"Could not find Jump_Strike spritesheet at: {jumpStrikePath}");
            return;
        }

        // Helper to load and sort sprites numerically (Run_0, Run_1, etc.)
        Sprite[] runSprites = LoadAndSortSprites(runPath);
        Sprite[] kickSprites = LoadAndSortSprites(kickPath);
        Sprite[] jumpStrikeSprites = LoadAndSortSprites(jumpStrikePath);

        if (runSprites.Length == 0)
        {
            Debug.LogError($"No sliced sprites found in {runPath}. Make sure it is sliced in the Sprite Editor.");
            return;
        }

        if (kickSprites.Length == 0)
        {
            Debug.LogError($"No sliced sprites found in {kickPath}. Make sure it is sliced in the Sprite Editor.");
            return;
        }

        if (jumpStrikeSprites.Length == 0)
        {
            Debug.LogError($"No sliced sprites found in {jumpStrikePath}. Make sure it is sliced in the Sprite Editor.");
            return;
        }

        Debug.Log($"Loaded {runSprites.Length} Run sprites, {kickSprites.Length} Kick sprites, and {jumpStrikeSprites.Length} Jump_Strike sprites.");

        // 2. Create/Overwrite Animation Clips
        // Idle Animation (loops, uses Run_0)
        Sprite[] idleSprites = new Sprite[] { runSprites[0] };
        AnimationClip idleClip = CreateAnimationClip(idleSprites, "Assets/player/Idle.anim", 12f, true);

        // Run Animation (loops, uses Run_0 to Run_11)
        AnimationClip runClip = CreateAnimationClip(runSprites, "Assets/player/Run.anim", 12f, true);

        // Kick Animation (does NOT loop, uses Kick_0 to Kick_4)
        AnimationClip kickClip = CreateAnimationClip(kickSprites, "Assets/player/Kick.anim", 12f, false);

        // Jump Strike Animation (does NOT loop)
        AnimationClip jumpStrikeClip = CreateAnimationClip(jumpStrikeSprites, "Assets/player/Jump_Strike.anim", 12f, false);

        Debug.Log("Animation Clips (Idle, Run, Kick, Jump_Strike) successfully created/updated.");

        // 3. Configure the Animator Controller
        string controllerPath = "Assets/player/player_run.controller";
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        if (controller == null)
        {
            Debug.LogError($"Failed to create or overwrite Animator Controller at: {controllerPath}");
            return;
        }

        // Add Parameters
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Kick", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);

        // Get State Machine
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

        // Add States
        AnimatorState idleState = stateMachine.AddState("Idle");
        idleState.motion = idleClip;

        AnimatorState runState = stateMachine.AddState("Run");
        runState.motion = runClip;

        AnimatorState kickState = stateMachine.AddState("Kick");
        kickState.motion = kickClip;

        AnimatorState jumpState = stateMachine.AddState("Jump_Strike");
        jumpState.motion = jumpStrikeClip;

        // Establish Transitions
        // Idle -> Run (Speed > 0.01)
        AnimatorStateTransition idleToRun = idleState.AddTransition(runState);
        idleToRun.AddCondition(AnimatorConditionMode.Greater, 0.01f, "Speed");
        idleToRun.hasExitTime = false;
        idleToRun.duration = 0.05f;

        // Run -> Idle (Speed < 0.01)
        AnimatorStateTransition runToIdle = runState.AddTransition(idleState);
        runToIdle.AddCondition(AnimatorConditionMode.Less, 0.01f, "Speed");
        runToIdle.hasExitTime = false;
        runToIdle.duration = 0.05f;

        // Any State -> Kick (Kick trigger active)
        AnimatorStateTransition anyToKick = stateMachine.AddAnyStateTransition(kickState);
        anyToKick.AddCondition(AnimatorConditionMode.If, 0, "Kick");
        anyToKick.hasExitTime = false;
        anyToKick.duration = 0.05f;

        // Kick -> Idle (Automatic transition once animation finishes)
        AnimatorStateTransition kickToIdle = kickState.AddTransition(idleState);
        kickToIdle.hasExitTime = true;
        kickToIdle.exitTime = 1f; // 100% of the clip duration
        kickToIdle.duration = 0.1f;

        // Any State -> Jump_Strike (Jump trigger active)
        AnimatorStateTransition anyToJump = stateMachine.AddAnyStateTransition(jumpState);
        anyToJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
        anyToJump.hasExitTime = false;
        anyToJump.duration = 0.05f;
        anyToJump.canTransitionToSelf = false;

        // Jump_Strike -> Idle (when IsGrounded becomes true and animation finishes)
        AnimatorStateTransition jumpToIdle = jumpState.AddTransition(idleState);
        jumpToIdle.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
        jumpToIdle.hasExitTime = true;
        jumpToIdle.exitTime = 0.9f;
        jumpToIdle.duration = 0.1f;

        // Save Assets
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Animator Controller successfully set up with Idle, Run, Kick, and Jump_Strike states!");

        // ─────────────────────────────────────────────────────────────────────
        //  4. Create "Ground" layer if it doesn't exist
        // ─────────────────────────────────────────────────────────────────────
        int groundLayerIndex = CreateLayerIfNeeded("Ground");
        Debug.Log($"Ground layer index: {groundLayerIndex}");

        // ─────────────────────────────────────────────────────────────────────
        //  5. Assign Ground layer to all ground objects (Squares) in the scene
        // ─────────────────────────────────────────────────────────────────────
        AssignGroundLayer(groundLayerIndex);

        // ─────────────────────────────────────────────────────────────────────
        //  6. Find or Create Player GameObject in Scene
        // ─────────────────────────────────────────────────────────────────────
        GameObject playerGo = GameObject.Find("Player");
        if (playerGo == null)
        {
            playerGo = new GameObject("Player");
            Undo.RegisterCreatedObjectUndo(playerGo, "Create Player");
            Debug.Log("Created 'Player' GameObject in the active scene.");
        }
        playerGo.tag = "Player";

        // Setup SpriteRenderer
        SpriteRenderer sr = playerGo.GetComponent<SpriteRenderer>();
        if (sr == null) sr = playerGo.AddComponent<SpriteRenderer>();
        sr.sprite = runSprites[0];
        sr.sortingOrder = 10; // Ensure player renders in front of backgrounds

        // Setup Animator
        Animator anim = playerGo.GetComponent<Animator>();
        if (anim == null) anim = playerGo.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;

        // Setup Rigidbody2D — gravity MUST be enabled for jumping to work
        Rigidbody2D rb = playerGo.GetComponent<Rigidbody2D>();
        if (rb == null) rb = playerGo.AddComponent<Rigidbody2D>();
        rb.gravityScale = 3f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // Setup BoxCollider2D
        BoxCollider2D col = playerGo.GetComponent<BoxCollider2D>();
        if (col == null) col = playerGo.AddComponent<BoxCollider2D>();
        
        // Reset and resize collider based on sprite bounds
        col.size = sr.sprite.bounds.size;
        col.offset = sr.sprite.bounds.center;

        // Setup PlayerController and auto-configure jump settings via SerializedObject
        PlayerController pc = playerGo.GetComponent<PlayerController>();
        if (pc == null) pc = playerGo.AddComponent<PlayerController>();

        // Use SerializedObject to set private serialized fields
        SerializedObject so = new SerializedObject(pc);
        so.Update();

        // Set groundLayer mask to the Ground layer
        SerializedProperty groundLayerProp = so.FindProperty("groundLayer");
        if (groundLayerProp != null)
        {
            groundLayerProp.intValue = 1 << groundLayerIndex;
            Debug.Log($"Set PlayerController.groundLayer mask to layer {groundLayerIndex} (Ground)");
        }

        // Set jump force
        SerializedProperty jumpForceProp = so.FindProperty("jumpForce");
        if (jumpForceProp != null)
        {
            jumpForceProp.floatValue = 10f;
        }

        // Set ground check offset (slightly below player feet)
        SerializedProperty groundCheckOffsetProp = so.FindProperty("groundCheckOffset");
        if (groundCheckOffsetProp != null)
        {
            groundCheckOffsetProp.vector2Value = new Vector2(0f, -0.35f);
        }

        // Set ground check radius
        SerializedProperty groundCheckRadiusProp = so.FindProperty("groundCheckRadius");
        if (groundCheckRadiusProp != null)
        {
            groundCheckRadiusProp.floatValue = 0.15f;
        }

        so.ApplyModifiedProperties();

        // ─────────────────────────────────────────────────────────────────────
        //  7. Find Camera and configure CameraController
        // ─────────────────────────────────────────────────────────────────────
        GameObject cameraGo = GameObject.FindWithTag("MainCamera");
        if (cameraGo == null)
        {
            cameraGo = GameObject.Find("Camera");
        }
        if (cameraGo == null)
        {
            cameraGo = GameObject.Find("Main Camera");
        }

        if (cameraGo != null)
        {
            CameraController cameraCtrl = cameraGo.GetComponent<CameraController>();
            if (cameraCtrl == null)
            {
                cameraCtrl = cameraGo.AddComponent<CameraController>();
                Debug.Log("Attached CameraController to Camera GameObject.");
            }

            SerializedObject camSo = new SerializedObject(cameraCtrl);
            camSo.Update();
            SerializedProperty targetProp = camSo.FindProperty("target");
            if (targetProp != null)
            {
                targetProp.objectReferenceValue = playerGo.transform;
                Debug.Log("Configured CameraController target to Player.");
            }
            camSo.ApplyModifiedProperties();
        }
        else
        {
            Debug.LogWarning("Could not find a Camera GameObject in the scene to attach CameraController to.");
        }

        // Mark Scene as Dirty to ensure Unity prompts to save the scene changes
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("Player GameObject successfully configured and set up in the scene!");

        // Show a confirmation dialog in Unity so the user knows it worked
        EditorUtility.DisplayDialog("Setup Complete", 
            "Player animations generated and 'Player' GameObject has been created/configured in your active scene!\n\n" +
            "Jump: Up Arrow or W key\n" +
            "Jump Forward: Up Arrow + Right Arrow\n" +
            "Gravity, Ground layer, and ground detection have been auto-configured.\n\n" +
            "Camera smooth follow has been attached and configured to follow the Player!", 
            "OK");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Creates a layer by name if it doesn't already exist. Returns layer index.
    // ─────────────────────────────────────────────────────────────────────────
    private static int CreateLayerIfNeeded(string layerName)
    {
        // Check if layer already exists
        int existingLayer = LayerMask.NameToLayer(layerName);
        if (existingLayer >= 0)
        {
            Debug.Log($"Layer '{layerName}' already exists at index {existingLayer}.");
            return existingLayer;
        }

        // Open TagManager asset to add the layer
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
        SerializedProperty layersProp = tagManager.FindProperty("layers");

        // User layers start at index 6 (0-5 are built-in)
        for (int i = 6; i < layersProp.arraySize; i++)
        {
            SerializedProperty layerEntry = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(layerEntry.stringValue))
            {
                layerEntry.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"Created layer '{layerName}' at index {i}.");
                return i;
            }
        }

        Debug.LogError($"Could not create layer '{layerName}': all layer slots are full!");
        return -1;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Assigns the Ground layer to all Square objects and ensures they have colliders
    // ─────────────────────────────────────────────────────────────────────────
    private static void AssignGroundLayer(int layerIndex)
    {
        if (layerIndex < 0) return;

        // Find all GameObjects in the scene whose name starts with "Square"
        GameObject[] allObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject go in allObjects)
        {
            if (go.name.StartsWith("Square"))
            {
                go.layer = layerIndex;

                // Ensure the ground object has a BoxCollider2D for collision
                BoxCollider2D groundCol = go.GetComponent<BoxCollider2D>();
                if (groundCol == null)
                {
                    groundCol = go.AddComponent<BoxCollider2D>();
                    Debug.Log($"Added BoxCollider2D to '{go.name}'.");
                }

                Debug.Log($"Assigned '{go.name}' to Ground layer ({layerIndex}).");
            }
        }
    }

    private static Sprite[] LoadAndSortSprites(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .OrderBy(s =>
            {
                string[] parts = s.name.Split('_');
                if (parts.Length > 1 && int.TryParse(parts[parts.Length - 1], out int index))
                {
                    return index;
                }
                return 0;
            })
            .ToArray();
    }

    private static AnimationClip CreateAnimationClip(Sprite[] sprites, string savePath, float frameRate, bool loop)
    {
        AnimationClip clip = new AnimationClip();
        clip.frameRate = frameRate;

        // Set looping settings
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        // Bind to SpriteRenderer's m_Sprite
        EditorCurveBinding binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = "",
            propertyName = "m_Sprite"
        };

        // Populate Keyframes
        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / frameRate,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        // Save as Asset
        AssetDatabase.CreateAsset(clip, savePath);
        return clip;
    }
}
