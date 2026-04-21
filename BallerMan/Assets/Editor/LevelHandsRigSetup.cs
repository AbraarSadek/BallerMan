using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

public static class LevelHandsRigSetup
{
    private const string HandsRigPrefabPath =
        "Assets/Samples/XR Interaction Toolkit/3.2.2/Hands Interaction Demo/Prefabs/XR Origin Hands (XR Rig).prefab";
    private const string XriDefaultInputActionsPath =
        "Assets/Samples/XR Interaction Toolkit/3.2.2/Starter Assets/XRI Default Input Actions.inputactions";

    [MenuItem("Tools/BallerMan/Install Hands Rig In Active Scene")]
    public static void InstallHandsRigInActiveScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("Open the scene you want to fix before running the hands rig installer.");
            return;
        }

        var handsRigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HandsRigPrefabPath);
        if (handsRigPrefab == null)
        {
            Debug.LogError($"Could not load hands rig prefab at '{HandsRigPrefabPath}'.");
            return;
        }

        XROrigin existingHandsRig = null;
        XROrigin existingControllerRig = null;

        foreach (var xrOrigin in Object.FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (xrOrigin == null || xrOrigin.gameObject.scene != scene)
                continue;

            if (xrOrigin.name.Contains("Hands"))
                existingHandsRig = xrOrigin;
            else if (xrOrigin.name == "XR Origin (XR Rig)")
                existingControllerRig = xrOrigin;
        }

        var spawnPosition = existingControllerRig != null ? existingControllerRig.transform.position : Vector3.zero;
        var spawnRotation = existingControllerRig != null ? existingControllerRig.transform.rotation : Quaternion.identity;

        if (existingHandsRig == null)
        {
            var rigObject = (GameObject)PrefabUtility.InstantiatePrefab(handsRigPrefab, scene);
            rigObject.name = "XR Origin Hands (XR Rig)";
            rigObject.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            existingHandsRig = rigObject.GetComponent<XROrigin>();
        }
        else
        {
            existingHandsRig.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            existingHandsRig.gameObject.SetActive(true);
        }

        if (existingControllerRig != null)
            existingControllerRig.gameObject.SetActive(false);

        var interactionManager = Object.FindFirstObjectByType<XRInteractionManager>(FindObjectsInactive.Exclude);
        if (interactionManager == null)
        {
            var managerObject = new GameObject("XR Interaction Manager");
            SceneManager.MoveGameObjectToScene(managerObject, scene);
            interactionManager = managerObject.AddComponent<XRInteractionManager>();
        }

        AssignInteractionManager(scene, interactionManager);
        EnsureInputActionManager(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Installed XR Hands rig, disabled the controller rig, assigned the XR Interaction Manager, and ensured XRI input actions are enabled.");
    }

    private static void AssignInteractionManager(Scene scene, XRInteractionManager interactionManager)
    {
        foreach (var interactor in Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (interactor == null || interactor.gameObject.scene != scene)
                continue;

            SetObjectReference(interactor, "m_InteractionManager", interactionManager);
        }

        foreach (var interactable in Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (interactable == null || interactable.gameObject.scene != scene)
                continue;

            SetObjectReference(interactable, "m_InteractionManager", interactionManager);
        }
    }

    private static void SetObjectReference(Object target, string propertyName, Object value)
    {
        var serializedObject = new SerializedObject(target);
        var property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void EnsureInputActionManager(Scene scene)
    {
        InputActionManager actionManager = null;

        foreach (var candidate in Object.FindObjectsByType<InputActionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate != null && candidate.gameObject.scene == scene)
            {
                actionManager = candidate;
                break;
            }
        }

        if (actionManager == null)
        {
            var actionManagerObject = new GameObject("XRI Input Action Manager");
            SceneManager.MoveGameObjectToScene(actionManagerObject, scene);
            actionManager = actionManagerObject.AddComponent<InputActionManager>();
        }

        var inputActions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(XriDefaultInputActionsPath);
        if (inputActions == null)
        {
            Debug.LogWarning($"Could not load XRI input actions at '{XriDefaultInputActionsPath}'.");
            return;
        }

        var serializedObject = new SerializedObject(actionManager);
        var actionAssets = serializedObject.FindProperty("m_ActionAssets");
        if (actionAssets == null)
            return;

        actionAssets.arraySize = 1;
        actionAssets.GetArrayElementAtIndex(0).objectReferenceValue = inputActions;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        actionManager.gameObject.SetActive(true);
        actionManager.enabled = true;
        EditorUtility.SetDirty(actionManager);
    }
}
