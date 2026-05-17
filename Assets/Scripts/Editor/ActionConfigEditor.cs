using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ActionConfig))]
public class ActionConfigEditor : Editor
{
    private SerializedProperty actionsProperty;
    private SerializedProperty actionIdTableProperty;
    private SerializedProperty animatorControllerProperty;
    private SerializedProperty defaultActionProperty;
    private Actor previewActor;
    private string sceneEditCollisionPropertyPath;
    private HashSet<string> duplicateActionNames = new HashSet<string>();

    private void OnEnable()
    {
        actionsProperty = serializedObject.FindProperty("actions");
        actionIdTableProperty = serializedObject.FindProperty("actionId");
        animatorControllerProperty = serializedObject.FindProperty("animatorController");
        defaultActionProperty = serializedObject.FindProperty("DefaultAction");
        SceneView.duringSceneGui += HandleSceneGUI;
        RefreshEditorState(false);
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= HandleSceneGUI;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (actionsProperty == null)
        {
            EditorGUILayout.HelpBox("Cannot find serialized field 'actions'.", MessageType.Error);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        EditorGUILayout.LabelField("Action Config", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        if (animatorControllerProperty != null)
            EditorGUILayout.PropertyField(animatorControllerProperty, new GUIContent("Animator Controller"));

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(actionIdTableProperty, new GUIContent("Action Id Table"));
        EditorGUI.EndChangeCheck();

        DrawAnimationInfoDropdown(defaultActionProperty, new GUIContent("Default Animation"));

        EditorGUI.BeginChangeCheck();
        previewActor = (Actor)EditorGUILayout.ObjectField(
            "Scene Preview Actor",
            previewActor,
            typeof(Actor),
            true);

        if (EditorGUI.EndChangeCheck())
            SceneView.RepaintAll();

        if (actionIdTableProperty != null && actionIdTableProperty.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Assign an ActionId table first, then the Action entries can be generated and selected from dropdowns.", MessageType.Warning);
        }
        else
        {
            ActionId table = actionIdTableProperty.objectReferenceValue as ActionId;

            if (ActionIdEditor.UsesLegacyData(table))
            {
                EditorGUILayout.HelpBox(
                    "The assigned ActionId asset is still using the old string list format. Dropdowns can read names, but clip data will stay empty until you import legacy names in the ActionId inspector.",
                    MessageType.Warning);
            }

            DrawDuplicateWarnings();
        }

        EditorGUILayout.HelpBox(
            "Attack collision path dragging and scene handles use this scene Actor as the root. The Actor name is not included in saved paths.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh"))
                RefreshEditorState(true);
        }

        EditorGUILayout.Space(4f);

        DrawActions();

        serializedObject.ApplyModifiedProperties();
    }

    private void RefreshEditorState(bool syncActions)
    {
        serializedObject.ApplyModifiedProperties();

        if (syncActions)
        {
            foreach (UnityEngine.Object selectedTarget in targets)
            {
                ActionConfig config = selectedTarget as ActionConfig;
                if (config == null)
                    continue;

                Undo.RecordObject(config, "Refresh Actions");
                config.RefreshActions();
                config.BuildMap();
                EditorUtility.SetDirty(config);
            }
        }

        serializedObject.Update();

        RefreshDuplicateCache();
        Repaint();
    }

    private void DrawActions()
    {
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        EditorGUI.indentLevel++;

        for (int i = 0; i < actionsProperty.arraySize; i++)
        {
            SerializedProperty actionProperty = actionsProperty.GetArrayElementAtIndex(i);
            DrawAction(actionProperty, i);
        }

        EditorGUI.indentLevel--;
    }

    private void DrawAction(SerializedProperty actionProperty, int index)
    {
        if (actionsProperty == null)
            return;

        SerializedProperty animationInfoProperty = actionProperty.FindPropertyRelative("animationInfo");
        SerializedProperty transitionsProperty = actionProperty.FindPropertyRelative("transitions");
        SerializedProperty modulesProperty = actionProperty.FindPropertyRelative("modules");
        string actionName = string.IsNullOrEmpty(GetAnimationInfoName(animationInfoProperty))
            ? "Action " + index
            : GetAnimationInfoName(animationInfoProperty);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool isDuplicate = IsDuplicateActionName(actionProperty);
                actionProperty.isExpanded = EditorGUILayout.Foldout(
                    actionProperty.isExpanded,
                    $"{index}: {actionName}",
                    true);

                Color previousColor = GUI.backgroundColor;
                GUI.backgroundColor = isDuplicate ? new Color(1f, 0.45f, 0.45f) : previousColor;

                string removeLabel = isDuplicate ? "Remove!" : "Remove";
                if (GUILayout.Button(removeLabel, GUILayout.Width(70f)))
                {
                    GUI.backgroundColor = previousColor;
                    actionsProperty.DeleteArrayElementAtIndex(index);
                    return;
                }

                GUI.backgroundColor = previousColor;
            }

            if (!actionProperty.isExpanded)
                return;

            EditorGUI.indentLevel++;

            DrawAnimationInfoDropdown(animationInfoProperty, new GUIContent("Action"));
            DrawSerializedChildrenExcept(actionProperty, animationInfoProperty?.propertyPath, transitionsProperty?.propertyPath, modulesProperty?.propertyPath);
            DrawTransitions(transitionsProperty);
            DrawModules(modulesProperty);

            EditorGUI.indentLevel--;
        }
    }

    private void DrawTransitions(SerializedProperty transitionsProperty)
    {
        EditorGUILayout.Space(3f);

        if (transitionsProperty == null || !transitionsProperty.isArray)
        {
            EditorGUILayout.HelpBox("Transitions field is not a serialized list.", MessageType.Warning);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Transitions", EditorStyles.boldLabel);

            if (GUILayout.Button("Add Transition", GUILayout.Width(110f)))
            {
                transitionsProperty.arraySize++;
                SerializedProperty transitionProperty = transitionsProperty.GetArrayElementAtIndex(transitionsProperty.arraySize - 1);
                transitionProperty.isExpanded = true;
            }
        }

        EditorGUI.indentLevel++;

        for (int i = 0; i < transitionsProperty.arraySize; i++)
        {
            SerializedProperty transitionProperty = transitionsProperty.GetArrayElementAtIndex(i);
            DrawTransition(transitionsProperty, transitionProperty, i);
        }

        EditorGUI.indentLevel--;
    }

    private void DrawTransition(SerializedProperty transitionsProperty, SerializedProperty transitionProperty, int index)
    {
        SerializedProperty targetActionIdProperty = transitionProperty.FindPropertyRelative("actionId");
        SerializedProperty typeProperty = transitionProperty.FindPropertyRelative("type");

        string targetName = string.IsNullOrEmpty(targetActionIdProperty?.stringValue)
            ? "<None>"
            : targetActionIdProperty.stringValue;

        string typeName = typeProperty != null && typeProperty.propertyType == SerializedPropertyType.Enum
            ? typeProperty.enumDisplayNames[typeProperty.enumValueIndex]
            : "Transition";

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                transitionProperty.isExpanded = EditorGUILayout.Foldout(
                    transitionProperty.isExpanded,
                    $"{index}: {typeName} -> {targetName}",
                    true);

                if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                {
                    transitionsProperty.DeleteArrayElementAtIndex(index);
                    return;
                }
            }

            if (!transitionProperty.isExpanded)
                return;

            EditorGUI.indentLevel++;
            DrawActionIdDropdown(targetActionIdProperty);
            DrawSerializedChildrenExcept(transitionProperty, targetActionIdProperty?.propertyPath);
            EditorGUI.indentLevel--;
        }
    }

    private void DrawModules(SerializedProperty modulesProperty)
    {
        EditorGUILayout.Space(3f);

        if (modulesProperty == null || !modulesProperty.isArray)
        {
            EditorGUILayout.HelpBox("Modules field is not a serialized list.", MessageType.Warning);
            return;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Modules", EditorStyles.boldLabel);

            if (GUILayout.Button("Add Module", GUILayout.Width(100f)))
                ShowAddModuleMenu(modulesProperty.propertyPath);
        }

        EditorGUI.indentLevel++;

        for (int i = 0; i < modulesProperty.arraySize; i++)
        {
            SerializedProperty moduleProperty = modulesProperty.GetArrayElementAtIndex(i);
            DrawModuleElement(modulesProperty, moduleProperty, i);
        }

        EditorGUI.indentLevel--;
    }

    private void DrawModuleElement(SerializedProperty modulesProperty, SerializedProperty moduleProperty, int index)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                string typeName = GetManagedReferenceTypeName(moduleProperty);

                moduleProperty.isExpanded = EditorGUILayout.Foldout(
                    moduleProperty.isExpanded,
                    $"{index}: {typeName}",
                    true);

                if (GUILayout.Button("Change", GUILayout.Width(70f)))
                    ShowChangeModuleMenu(moduleProperty.propertyPath);

                if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                {
                    modulesProperty.DeleteArrayElementAtIndex(index);
                    return;
                }
            }

            if (moduleProperty.managedReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Null module. Use Change to select a module type.", MessageType.Info);
                return;
            }

            if (!moduleProperty.isExpanded)
                return;

            EditorGUI.indentLevel++;
            if (moduleProperty.managedReferenceValue is AttackCollisionModule)
                DrawAttackCollisionModule(moduleProperty);
            else
                DrawManagedReferenceChildren(moduleProperty);
            EditorGUI.indentLevel--;
        }
    }

    private void DrawAttackCollisionModule(SerializedProperty moduleProperty)
    {
        SerializedProperty collisionsProperty = moduleProperty.FindPropertyRelative("collisions");

        if (collisionsProperty == null || !collisionsProperty.isArray)
        {
            EditorGUILayout.HelpBox("Cannot find serialized list field 'collisions'.", MessageType.Warning);
            return;
        }

        DrawManagedReferenceChildrenExcept(moduleProperty, collisionsProperty.propertyPath);

        EditorGUILayout.Space(3f);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Collisions", EditorStyles.boldLabel);

            if (GUILayout.Button("Add Collision", GUILayout.Width(110f)))
            {
                collisionsProperty.arraySize++;
                SerializedProperty collisionProperty = collisionsProperty.GetArrayElementAtIndex(collisionsProperty.arraySize - 1);
                collisionProperty.isExpanded = true;
                InitializeCollisionDefaults(collisionProperty);
            }
        }

        EditorGUI.indentLevel++;

        for (int i = 0; i < collisionsProperty.arraySize; i++)
        {
            SerializedProperty collisionProperty = collisionsProperty.GetArrayElementAtIndex(i);
            DrawCollision(collisionsProperty, collisionProperty, i);
        }

        EditorGUI.indentLevel--;
    }

    private static void InitializeCollisionDefaults(SerializedProperty collisionProperty)
    {
        SerializedProperty radiusProperty = collisionProperty.FindPropertyRelative("Radius");
        SerializedProperty heightProperty = collisionProperty.FindPropertyRelative("Height");

        if (radiusProperty != null && radiusProperty.floatValue <= 0f)
            radiusProperty.floatValue = 0.15f;

        if (heightProperty != null && heightProperty.floatValue <= 0f)
            heightProperty.floatValue = 0.5f;
    }

    private void DrawCollision(SerializedProperty collisionsProperty, SerializedProperty collisionProperty, int index)
    {
        SerializedProperty pathProperty = collisionProperty.FindPropertyRelative("path");
        string title = string.IsNullOrEmpty(pathProperty.stringValue)
            ? $"Collision {index}"
            : $"Collision {index}: {pathProperty.stringValue}";

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                collisionProperty.isExpanded = EditorGUILayout.Foldout(
                    collisionProperty.isExpanded,
                    title,
                    true);

                bool isSceneEditing = sceneEditCollisionPropertyPath == collisionProperty.propertyPath;
                string editButtonText = isSceneEditing ? "Editing" : "Edit Scene";

                if (GUILayout.Button(editButtonText, GUILayout.Width(85f)))
                {
                    sceneEditCollisionPropertyPath = isSceneEditing
                        ? null
                        : collisionProperty.propertyPath;
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                {
                    if (sceneEditCollisionPropertyPath == collisionProperty.propertyPath)
                        sceneEditCollisionPropertyPath = null;

                    collisionsProperty.DeleteArrayElementAtIndex(index);
                    SceneView.RepaintAll();
                    return;
                }
            }

            if (!collisionProperty.isExpanded)
                return;

            EditorGUI.indentLevel++;

            DrawCollisionSocketField(collisionProperty);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(pathProperty);
                bool pathChanged = EditorGUI.EndChangeCheck();
                bool findClicked = GUILayout.Button("Find", GUILayout.Width(50f));

                if (pathChanged || findClicked)
                {
                    if (ResolveSocketByPath(pathProperty.stringValue, out Actor resolvedActor, out _))
                        previewActor = resolvedActor;

                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.PropertyField(collisionProperty.FindPropertyRelative("position"));
            EditorGUILayout.PropertyField(collisionProperty.FindPropertyRelative("rotation"));
            EditorGUILayout.PropertyField(collisionProperty.FindPropertyRelative("Radius"));
            EditorGUILayout.PropertyField(collisionProperty.FindPropertyRelative("Height"));

            EditorGUI.indentLevel--;
        }
    }

    private void DrawCollisionSocketField(SerializedProperty collisionProperty)
    {
        SerializedProperty pathProperty = collisionProperty.FindPropertyRelative("path");
        ResolveSocketByPath(pathProperty.stringValue, out Actor resolvedActor, out Transform currentSocket);

        if (resolvedActor != null)
            previewActor = resolvedActor;

        EditorGUI.BeginChangeCheck();
        Transform selectedSocket = (Transform)EditorGUILayout.ObjectField(
            "Socket",
            currentSocket,
            typeof(Transform),
            true);

        if (!string.IsNullOrEmpty(pathProperty.stringValue) && currentSocket == null)
            EditorGUILayout.HelpBox("Socket path is saved, but no matching scene Transform was found. Set Scene Preview Actor or click Find after editing Path.", MessageType.Warning);

        if (!EditorGUI.EndChangeCheck())
            return;

        if (selectedSocket == null)
        {
            pathProperty.stringValue = string.Empty;
            SceneView.RepaintAll();
            return;
        }

        Actor socketActor = selectedSocket.GetComponentInParent<Actor>();

        if (socketActor == null)
        {
            Debug.LogWarning("Dragged Transform is not under an Actor.", selectedSocket);
            return;
        }

        previewActor = socketActor;
        string path = GetPathUntilActor(selectedSocket);

        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("Failed to build socket path.", selectedSocket);
            return;
        }

        pathProperty.stringValue = path;
        SceneView.RepaintAll();
    }

    private void DrawManagedReferenceChildren(SerializedProperty moduleProperty)
    {
        DrawManagedReferenceChildrenExcept(moduleProperty, null);
    }

    private void DrawManagedReferenceChildrenExcept(SerializedProperty moduleProperty, string excludedPropertyPath)
    {
        DrawSerializedChildrenExcept(moduleProperty, excludedPropertyPath);
    }

    private void DrawActionIdDropdown(SerializedProperty actionIdProperty)
    {
        if (actionIdProperty == null)
            return;

        ActionId table = actionIdTableProperty != null
            ? actionIdTableProperty.objectReferenceValue as ActionId
            : null;

        List<string> options = GetActionIdOptions(table);

        if (options.Count == 0)
        {
            EditorGUILayout.PropertyField(actionIdProperty, new GUIContent("Action Id"));
            return;
        }

        string current = actionIdProperty.stringValue ?? string.Empty;
        List<string> values = new();
        List<string> labels = new();

        if (string.IsNullOrEmpty(current))
        {
            values.Add(string.Empty);
            labels.Add("<None>");
        }
        else if (!options.Contains(current))
        {
            values.Add(current);
            labels.Add(current + " (Not in table)");
        }

        foreach (string option in options)
        {
            if (values.Contains(option))
                continue;

            values.Add(option);
            labels.Add(option);
        }

        int currentIndex = values.IndexOf(current);
        if (currentIndex < 0)
            currentIndex = 0;

        int newIndex = EditorGUILayout.Popup("Action Id", currentIndex, labels.ToArray());
        if (newIndex >= 0 && newIndex < values.Count)
        {
            actionIdProperty.stringValue = values[newIndex];
        }
    }

    private void DrawAnimationInfoDropdown(SerializedProperty animationInfoProperty, GUIContent label)
    {
        if (animationInfoProperty == null)
            return;

        ActionId table = actionIdTableProperty != null
            ? actionIdTableProperty.objectReferenceValue as ActionId
            : null;

        List<string> options = GetAnimationInfoOptions(table);

        if (options.Count == 0)
        {
            EditorGUILayout.PropertyField(animationInfoProperty, label, true);
            return;
        }

        string currentName = GetAnimationInfoName(animationInfoProperty);
        List<string> values = new();
        List<string> labels = new();

        if (string.IsNullOrEmpty(currentName))
        {
            values.Add(string.Empty);
            labels.Add("<None>");
        }
        else if (!options.Contains(currentName))
        {
            values.Add(currentName);
            labels.Add(currentName + " (Not in table)");
        }

        foreach (string option in options)
        {
            if (string.IsNullOrEmpty(option) || values.Contains(option))
                continue;

            values.Add(option);
            labels.Add(option);
        }

        int currentIndex = values.IndexOf(currentName);
        if (currentIndex < 0)
            currentIndex = 0;

        int newIndex = EditorGUILayout.Popup(label, currentIndex, labels.ToArray());
        if (newIndex >= 0 && newIndex < values.Count && newIndex != currentIndex)
        {
            SetAnimationInfo(animationInfoProperty, table, values[newIndex]);
        }

        DrawAnimationInfoClipPreview(animationInfoProperty);
    }

    private static void DrawAnimationInfoClipPreview(SerializedProperty animationInfoProperty)
    {
        SerializedProperty clipProperty = animationInfoProperty.FindPropertyRelative("clip");

        if (clipProperty == null)
            return;

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(clipProperty, new GUIContent("Clip"));
        }
    }

    private static string GetAnimationInfoName(SerializedProperty animationInfoProperty)
    {
        SerializedProperty nameProperty = animationInfoProperty?.FindPropertyRelative("name");
        return nameProperty != null ? nameProperty.stringValue : string.Empty;
    }

    private static void SetAnimationInfo(SerializedProperty animationInfoProperty, ActionId table, string name)
    {
        SerializedProperty nameProperty = animationInfoProperty.FindPropertyRelative("name");
        SerializedProperty clipProperty = animationInfoProperty.FindPropertyRelative("clip");

        if (nameProperty != null)
            nameProperty.stringValue = name ?? string.Empty;

        if (clipProperty != null)
            clipProperty.objectReferenceValue = ActionIdEditor.FindClipByName(table, name);
    }

    private static List<string> GetAnimationInfoOptions(ActionId table)
    {
        return ActionIdEditor.GetOptionNames(table);
    }

    private static List<string> GetActionIdOptions(ActionId table)
    {
        return ActionIdEditor.GetOptionNames(table);
    }

    private void DrawDuplicateWarnings()
    {
        if (duplicateActionNames == null || duplicateActionNames.Count == 0)
            return;

        EditorGUILayout.HelpBox(
            "Duplicate actions detected: " + string.Join(", ", duplicateActionNames.OrderBy(name => name)) + ". Remove extra entries manually to avoid losing configuration.",
            MessageType.Warning);
    }

    private bool IsDuplicateActionName(SerializedProperty actionProperty)
    {
        if (duplicateActionNames == null || duplicateActionNames.Count == 0 || actionProperty == null)
            return false;

        SerializedProperty animationInfoProperty = actionProperty.FindPropertyRelative("animationInfo");
        string currentName = GetAnimationInfoName(animationInfoProperty);
        return !string.IsNullOrEmpty(currentName) && duplicateActionNames.Contains(currentName);
    }

    private void RefreshDuplicateCache()
    {
        duplicateActionNames.Clear();

        if (actionsProperty == null || actionsProperty.arraySize == 0)
            return;

        Dictionary<string, int> counts = new Dictionary<string, int>();

        for (int i = 0; i < actionsProperty.arraySize; i++)
        {
            SerializedProperty actionProperty = actionsProperty.GetArrayElementAtIndex(i);
            SerializedProperty animationInfoProperty = actionProperty.FindPropertyRelative("animationInfo");
            string name = GetAnimationInfoName(animationInfoProperty);

            if (string.IsNullOrEmpty(name))
                continue;

            counts[name] = counts.TryGetValue(name, out int count) ? count + 1 : 1;
        }

        foreach (var pair in counts)
        {
            if (pair.Value > 1)
                duplicateActionNames.Add(pair.Key);
        }
    }

    private void DrawSerializedChildrenExcept(SerializedProperty parentProperty, params string[] excludedPropertyPaths)
    {
        SerializedProperty iterator = parentProperty.Copy();
        SerializedProperty endProperty = iterator.GetEndProperty();
        bool enterChildren = true;
        HashSet<string> excluded = new HashSet<string>(
            excludedPropertyPaths?.Where(path => !string.IsNullOrEmpty(path)) ?? Enumerable.Empty<string>());

        while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
        {
            if (excluded.Contains(iterator.propertyPath))
            {
                enterChildren = false;
                continue;
            }

            EditorGUILayout.PropertyField(iterator, true);
            enterChildren = false;
        }
    }

    private void ShowAddModuleMenu(string modulesPropertyPath)
    {
        GenericMenu menu = new GenericMenu();
        List<Type> moduleTypes = GetModuleTypes();

        if (moduleTypes.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("No ActionModule subclasses found"));
        }

        foreach (Type type in moduleTypes)
        {
            menu.AddItem(GetModuleMenuName(type), false, () =>
            {
                Undo.RecordObject(target, "Add Action Module");

                SerializedObject so = new SerializedObject(target);
                SerializedProperty modules = so.FindProperty(modulesPropertyPath);

                if (modules == null || !modules.isArray)
                    return;

                modules.arraySize++;
                SerializedProperty element = modules.GetArrayElementAtIndex(modules.arraySize - 1);
                element.managedReferenceValue = Activator.CreateInstance(type);
                element.isExpanded = true;

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                Repaint();
            });
        }

        menu.ShowAsContext();
    }

    private void ShowChangeModuleMenu(string modulePropertyPath)
    {
        GenericMenu menu = new GenericMenu();
        List<Type> moduleTypes = GetModuleTypes();

        if (moduleTypes.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("No ActionModule subclasses found"));
        }

        foreach (Type type in moduleTypes)
        {
            menu.AddItem(GetModuleMenuName(type), false, () =>
            {
                Undo.RecordObject(target, "Change Action Module");

                SerializedObject so = new SerializedObject(target);
                SerializedProperty module = so.FindProperty(modulePropertyPath);

                if (module == null)
                    return;

                module.managedReferenceValue = Activator.CreateInstance(type);
                module.isExpanded = true;

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                Repaint();
            });
        }

        menu.ShowAsContext();
    }

    private static List<Type> GetModuleTypes()
    {
        return TypeCache.GetTypesDerivedFrom<ActionModule>()
            .Where(type => !type.IsAbstract)
            .Where(type => !type.IsGenericType)
            .Where(type => type.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(type => type.Name)
            .ToList();
    }

    private static GUIContent GetModuleMenuName(Type type)
    {
        string typeName = ObjectNames.NicifyVariableName(type.Name);
        return new GUIContent(typeName);
    }

    private static string GetManagedReferenceTypeName(SerializedProperty property)
    {
        object value = property.managedReferenceValue;

        if (value == null)
            return "Null Module";

        return ObjectNames.NicifyVariableName(value.GetType().Name);
    }

    private void HandleSceneGUI(SceneView sceneView)
    {
        if (string.IsNullOrEmpty(sceneEditCollisionPropertyPath))
            return;

        SerializedObject so = new SerializedObject(target);
        so.Update();

        SerializedProperty collisionProperty = so.FindProperty(sceneEditCollisionPropertyPath);

        if (collisionProperty == null)
        {
            sceneEditCollisionPropertyPath = null;
            return;
        }

        SerializedProperty pathProperty = collisionProperty.FindPropertyRelative("path");
        SerializedProperty positionProperty = collisionProperty.FindPropertyRelative("position");
        SerializedProperty rotationProperty = collisionProperty.FindPropertyRelative("rotation");
        SerializedProperty radiusProperty = collisionProperty.FindPropertyRelative("Radius");
        SerializedProperty heightProperty = collisionProperty.FindPropertyRelative("Height");

        if (pathProperty == null
            || positionProperty == null
            || rotationProperty == null
            || radiusProperty == null
            || heightProperty == null)
        {
            return;
        }

        ResolveSocketByPath(pathProperty.stringValue, out Actor resolvedActor, out Transform socket);

        if (resolvedActor != null)
            previewActor = resolvedActor;

        if (previewActor == null)
        {
            Handles.Label(Vector3.zero, "Assign Scene Preview Actor or drag a socket first.");
            return;
        }

        if (socket == null)
        {
            Handles.Label(
                previewActor.transform.position,
                "Collision path not found: " + pathProperty.stringValue);
            return;
        }

        Vector3 localPosition = positionProperty.vector3Value;
        Quaternion localRotation = Quaternion.Euler(rotationProperty.vector3Value);
        float radius = Mathf.Max(0.001f, radiusProperty.floatValue);
        float height = Mathf.Max(radius * 2f, heightProperty.floatValue);

        Vector3 worldPosition = TransformPointNoScale(socket, localPosition);
        Quaternion worldRotation = socket.rotation * localRotation;

        DrawCapsule(worldPosition, worldRotation, radius, height);

        EditorGUI.BeginChangeCheck();

        float handleSize = HandleUtility.GetHandleSize(worldPosition);
        Vector3 newWorldPosition = Handles.PositionHandle(worldPosition, worldRotation);
        Quaternion newWorldRotation = Handles.RotationHandle(worldRotation, newWorldPosition);

        float newRadius = Handles.ScaleValueHandle(
            radius,
            newWorldPosition + newWorldRotation * Vector3.right * radius,
            newWorldRotation,
            handleSize * 0.15f,
            Handles.SphereHandleCap,
            0.01f);

        float newHeight = Handles.ScaleValueHandle(
            height,
            newWorldPosition + newWorldRotation * Vector3.up * (height * 0.5f),
            newWorldRotation,
            handleSize * 0.15f,
            Handles.CubeHandleCap,
            0.01f);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(target, "Edit Attack Collision");

            newRadius = Mathf.Max(0.001f, newRadius);
            newHeight = Mathf.Max(newRadius * 2f, newHeight);

            positionProperty.vector3Value = InverseTransformPointNoScale(socket, newWorldPosition);
            rotationProperty.vector3Value = (Quaternion.Inverse(socket.rotation) * newWorldRotation).eulerAngles;
            radiusProperty.floatValue = newRadius;
            heightProperty.floatValue = newHeight;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            Repaint();
        }

        Handles.Label(
            newWorldPosition + Vector3.up * handleSize * 0.25f,
            $"Editing Collision\nPath: {pathProperty.stringValue}\nRadius: {radiusProperty.floatValue:F2}, Height: {heightProperty.floatValue:F2}");
    }

    private static void DrawCapsule(Vector3 worldPosition, Quaternion worldRotation, float radius, float height)
    {
        Matrix4x4 matrix = Matrix4x4.TRS(worldPosition, worldRotation, Vector3.one);
        Color color = new Color(1f, 0.2f, 0.05f, 0.9f);

        using (new Handles.DrawingScope(color, matrix))
        {
            float cylinderHalfHeight = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 top = Vector3.up * cylinderHalfHeight;
            Vector3 bottom = Vector3.down * cylinderHalfHeight;

            Handles.DrawWireDisc(top, Vector3.up, radius);
            Handles.DrawWireDisc(bottom, Vector3.up, radius);

            Handles.DrawWireDisc(top, Vector3.forward, radius);
            Handles.DrawWireDisc(bottom, Vector3.forward, radius);
            Handles.DrawWireDisc(top, Vector3.right, radius);
            Handles.DrawWireDisc(bottom, Vector3.right, radius);

            Handles.DrawLine(top + Vector3.right * radius, bottom + Vector3.right * radius);
            Handles.DrawLine(top - Vector3.right * radius, bottom - Vector3.right * radius);
            Handles.DrawLine(top + Vector3.forward * radius, bottom + Vector3.forward * radius);
            Handles.DrawLine(top - Vector3.forward * radius, bottom - Vector3.forward * radius);
        }
    }

    private static Vector3 TransformPointNoScale(Transform transform, Vector3 localPoint)
    {
        return transform.position + transform.rotation * localPoint;
    }

    private static Vector3 InverseTransformPointNoScale(Transform transform, Vector3 worldPoint)
    {
        return Quaternion.Inverse(transform.rotation) * (worldPoint - transform.position);
    }

    private bool ResolveSocketByPath(string path, out Actor actor, out Transform socket)
    {
        actor = null;
        socket = null;

        if (string.IsNullOrEmpty(path))
            return false;

        if (previewActor != null && !EditorUtility.IsPersistent(previewActor))
        {
            socket = previewActor.transform.Find(path);

            if (socket != null)
            {
                actor = previewActor;
                return true;
            }
        }

        actor = FindActorByPath(path);

        if (actor == null)
            return false;

        socket = actor.transform.Find(path);
        return socket != null;
    }

    private static Actor FindActorByPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        Actor[] actors = UnityEngine.Object.FindObjectsByType<Actor>(FindObjectsInactive.Include);

        foreach (Actor actor in actors)
        {
            if (actor == null || EditorUtility.IsPersistent(actor))
                continue;

            if (actor.transform.Find(path) != null)
                return actor;
        }

        return null;
    }

    private static string GetPathUntilActor(Transform targetTransform)
    {
        if (targetTransform == null)
            return string.Empty;

        Actor actor = targetTransform.GetComponentInParent<Actor>();

        if (actor == null)
            return string.Empty;

        List<string> names = new List<string>();
        Transform current = targetTransform;

        while (current != null && current != actor.transform)
        {
            names.Add(current.name);
            current = current.parent;
        }

        if (current != actor.transform)
            return string.Empty;

        names.Reverse();
        return string.Join("/", names);
    }
}
