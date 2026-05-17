using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AnimatorStatePathScanner
{
    public readonly struct StatePathInfo
    {
        public readonly int LayerIndex;
        public readonly string LayerName;
        public readonly string StateName;
        public readonly string FullPath;

        public StatePathInfo(int layerIndex, string layerName, string stateName, string fullPath)
        {
            LayerIndex = layerIndex;
            LayerName = layerName;
            StateName = stateName;
            FullPath = fullPath;
        }
    }

    public static List<StatePathInfo> GetStatePaths(AnimatorController controller)
    {
        var result = new List<StatePathInfo>();

        if (controller == null)
            return result;

        for (int i = 0; i < controller.layers.Length; i++)
        {
            AnimatorControllerLayer layer = controller.layers[i];
            ScanStateMachine(layer.stateMachine, i, layer.name, layer.name, result);
        }

        return result;
    }

    private static void ScanStateMachine(
        AnimatorStateMachine stateMachine,
        int layerIndex,
        string layerName,
        string currentPath,
        List<StatePathInfo> result)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            AnimatorState state = childState.state;
            string fullPath = currentPath + "." + state.name;
            result.Add(new StatePathInfo(layerIndex, layerName, state.name, fullPath));
        }

        foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
        {
            AnimatorStateMachine nestedStateMachine = childMachine.stateMachine;
            string nestedPath = currentPath + "." + nestedStateMachine.name;
            ScanStateMachine(nestedStateMachine, layerIndex, layerName, nestedPath, result);
        }
    }
}

public class AnimatorStatePathScannerWindow : EditorWindow
{
    private AnimatorController controller;
    private ActionConfig actionConfig;
    private Vector2 scroll;
    private List<AnimatorStatePathScanner.StatePathInfo> statePaths = new();

    [MenuItem("Tools/Animation/Animator State Path Scanner")]
    private static void Open()
    {
        GetWindow<AnimatorStatePathScannerWindow>("State Path Scanner");
    }

    private void OnEnable()
    {
        if (Selection.activeObject is AnimatorController selectedController)
        {
            controller = selectedController;
            Scan();
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Animator Controller State Paths", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        controller = (AnimatorController)EditorGUILayout.ObjectField(
            "Controller",
            controller,
            typeof(AnimatorController),
            false);

        actionConfig = (ActionConfig)EditorGUILayout.ObjectField(
            "Action Config",
            actionConfig,
            typeof(ActionConfig),
            false);

        if (EditorGUI.EndChangeCheck())
            Scan();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Scan"))
                Scan();

            if (GUILayout.Button("Log All"))
                LogAll();

            if (GUILayout.Button("Auto Fill StatePath"))
                AutoFillActionConfigStatePaths();
        }

        EditorGUILayout.Space();

        if (controller == null)
        {
            EditorGUILayout.HelpBox("Assign an AnimatorController first.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Found States: " + statePaths.Count);

        scroll = EditorGUILayout.BeginScrollView(scroll);

        foreach (AnimatorStatePathScanner.StatePathInfo info in statePaths)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(info.FullPath);

                if (GUILayout.Button("Copy", GUILayout.Width(55f)))
                    EditorGUIUtility.systemCopyBuffer = info.FullPath;
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void Scan()
    {
        statePaths = AnimatorStatePathScanner.GetStatePaths(controller);
    }

    private void LogAll()
    {
        Scan();

        foreach (AnimatorStatePathScanner.StatePathInfo info in statePaths)
        {
            Debug.Log($"Layer {info.LayerIndex}: {info.FullPath}", controller);
        }
    }

    private void AutoFillActionConfigStatePaths()
    {
        if (controller == null)
        {
            Debug.LogWarning("AnimatorController is null.");
            return;
        }

        if (actionConfig == null)
        {
            Debug.LogWarning("ActionConfig is null.");
            return;
        }

        Scan();

        SerializedObject serializedConfig = new SerializedObject(actionConfig);
        SerializedProperty actionsProperty = serializedConfig.FindProperty("actions");

        if (actionsProperty == null || !actionsProperty.isArray)
        {
            Debug.LogWarning("Cannot find serialized list field named 'actions'. Make it public or add [SerializeField].", actionConfig);
            return;
        }

        int filledCount = 0;
        int skippedCount = 0;
        int preservedCount = 0;

        serializedConfig.Update();

        for (int i = 0; i < actionsProperty.arraySize; i++)
        {
            SerializedProperty actionProperty = actionsProperty.GetArrayElementAtIndex(i);
            SerializedProperty actionIdProperty = actionProperty.FindPropertyRelative("actionId");
            SerializedProperty statePathProperty = FindStatePathProperty(actionProperty);

            if (actionIdProperty == null || statePathProperty == null)
            {
                skippedCount++;
                continue;
            }

            string actionIdName = actionIdProperty.stringValue;
            if (string.IsNullOrWhiteSpace(actionIdName))
            {
                skippedCount++;
                continue;
            }

            if (TryResolveStatePath(actionIdName, out string resolvedPath))
            {
                if (statePathProperty.stringValue == resolvedPath)
                {
                    preservedCount++;
                }
                else
                {
                    statePathProperty.stringValue = resolvedPath;
                    filledCount++;
                }

                continue;
            }

            Debug.LogWarning(
                $"ActionId '{actionIdName}' could not be resolved to a unique Animator state path. Fill it manually or rename the Animator state.",
                actionConfig);
            skippedCount++;
        }

        serializedConfig.ApplyModifiedProperties();
        EditorUtility.SetDirty(actionConfig);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"Auto fill completed. Filled: {filledCount}, unchanged: {preservedCount}, skipped: {skippedCount}.",
            actionConfig);
    }

    private bool TryResolveStatePath(string actionId, out string resolvedPath)
    {
        resolvedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(actionId))
            return false;

        List<AnimatorStatePathScanner.StatePathInfo> exactMatches = statePaths
            .Where(path => path.StateName == actionId)
            .ToList();

        if (exactMatches.Count == 1)
        {
            resolvedPath = exactMatches[0].FullPath;
            return true;
        }

        string normalizedActionId = NormalizePathToken(actionId);
        List<AnimatorStatePathScanner.StatePathInfo> normalizedMatches = statePaths
            .Where(path => NormalizePathToken(path.StateName) == normalizedActionId)
            .ToList();

        if (normalizedMatches.Count == 1)
        {
            resolvedPath = normalizedMatches[0].FullPath;
            return true;
        }

        return false;
    }

    private static SerializedProperty FindStatePathProperty(SerializedProperty actionProperty)
    {
        return actionProperty.FindPropertyRelative("StatePath")
            ?? actionProperty.FindPropertyRelative("statePath")
            ?? actionProperty.FindPropertyRelative("animatorStatePath");
    }

    private static string NormalizePathToken(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }
}
