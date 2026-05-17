using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

[CustomEditor(typeof(ActionId))]
public class ActionIdEditor : Editor
{
    private SerializedProperty actionIdsProperty;
    private ActionId targetTable;
    private ReorderableList actionIdsList;

    private void OnEnable()
    {
        actionIdsProperty = serializedObject.FindProperty("ActionIds");
        targetTable = target as ActionId;
        BuildActionIdsList();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Action Id Table", EditorStyles.boldLabel);

        if (actionIdsProperty == null || !actionIdsProperty.isArray)
        {
            EditorGUILayout.HelpBox("Cannot find serialized list field 'ActionIds'.", MessageType.Error);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        DrawLegacyImportSection();

        if (actionIdsList != null)
            actionIdsList.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawLegacyImportSection()
    {
        if (targetTable == null || HasNamedEntries(actionIdsProperty))
            return;

        var legacyNames = GetLegacyNames(targetTable);
        if (legacyNames.Count == 0)
            return;

        EditorGUILayout.HelpBox(
            "This ActionId asset still contains the old string list format. Import those names into the new Name/Clip structure to edit clips and use dropdowns reliably.",
            MessageType.Warning);

        if (GUILayout.Button("Import Legacy Names"))
        {
            ImportLegacyNames(actionIdsProperty, legacyNames);
        }

        EditorGUILayout.Space(4f);
    }

    private void BuildActionIdsList()
    {
        if (actionIdsProperty == null)
            return;

        actionIdsList = new ReorderableList(serializedObject, actionIdsProperty, true, true, true, true);

        actionIdsList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "Actions");
        };

        actionIdsList.onAddCallback = list =>
        {
            list.serializedProperty.arraySize++;
            SerializedProperty element = list.serializedProperty.GetArrayElementAtIndex(list.serializedProperty.arraySize - 1);
            element.isExpanded = true;
        };

        actionIdsList.elementHeightCallback = index =>
        {
            if (index < 0 || index >= actionIdsProperty.arraySize)
                return EditorGUIUtility.singleLineHeight + 6f;

            SerializedProperty element = actionIdsProperty.GetArrayElementAtIndex(index);
            return element.isExpanded
                ? EditorGUIUtility.singleLineHeight * 3f + 16f
                : EditorGUIUtility.singleLineHeight + 8f;
        };

        actionIdsList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            if (index < 0 || index >= actionIdsProperty.arraySize)
                return;

            SerializedProperty element = actionIdsProperty.GetArrayElementAtIndex(index);
            DrawAnimationInfoElement(rect, element, index);
        };
    }

    private void DrawAnimationInfoElement(Rect rect, SerializedProperty element, int index)
    {
        SerializedProperty nameProperty = element.FindPropertyRelative("name");
        SerializedProperty clipProperty = element.FindPropertyRelative("clip");
        string title = string.IsNullOrEmpty(nameProperty?.stringValue)
            ? $"Action {index}"
            : nameProperty.stringValue;

        rect.y += 2f;
        Rect foldoutRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
        element.isExpanded = EditorGUI.Foldout(foldoutRect, element.isExpanded, $"{index}: {title}", true);

        if (!element.isExpanded)
            return;

        EditorGUI.indentLevel++;

        Rect nameRect = new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight + 2f, rect.width, EditorGUIUtility.singleLineHeight);
        Rect clipRect = new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight * 2f + 4f, rect.width, EditorGUIUtility.singleLineHeight);

        if (nameProperty != null)
            EditorGUI.PropertyField(nameRect, nameProperty, new GUIContent("Name"));

        if (clipProperty != null)
            EditorGUI.PropertyField(clipRect, clipProperty, new GUIContent("Clip"));

        EditorGUI.indentLevel--;
    }

    public static List<string> GetOptionNames(ActionId table)
    {
        if (table == null)
            return new List<string>();

        List<string> names = table.ActionIds?
            .Where(info => info != null && !string.IsNullOrEmpty(info.name))
            .Select(info => info.name)
            .Distinct()
            .ToList();

        if (names != null && names.Count > 0)
            return names;

        return GetLegacyNames(table);
    }

    public static bool UsesLegacyData(ActionId table)
    {
        if (table == null)
            return false;

        bool hasStructuredEntries = table.ActionIds != null &&
            table.ActionIds.Any(info => info != null && !string.IsNullOrEmpty(info.name));

        return !hasStructuredEntries && GetLegacyNames(table).Count > 0;
    }

    public static AnimationClip FindClipByName(ActionId table, string name)
    {
        if (table == null || string.IsNullOrEmpty(name) || table.ActionIds == null)
            return null;

        AnimationInfo info = table.ActionIds.FirstOrDefault(item => item != null && item.name == name);
        return info != null ? info.clip : null;
    }

    private static bool HasNamedEntries(SerializedProperty actionIdsProperty)
    {
        if (actionIdsProperty == null || !actionIdsProperty.isArray)
            return false;

        for (int i = 0; i < actionIdsProperty.arraySize; i++)
        {
            SerializedProperty element = actionIdsProperty.GetArrayElementAtIndex(i);
            SerializedProperty nameProperty = element.FindPropertyRelative("name");

            if (nameProperty != null && !string.IsNullOrEmpty(nameProperty.stringValue))
                return true;
        }

        return false;
    }

    private static List<string> GetLegacyNames(ActionId table)
    {
        string assetPath = AssetDatabase.GetAssetPath(table);
        if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
            return new List<string>();

        List<string> names = new List<string>();
        bool inActionIds = false;

        foreach (string rawLine in File.ReadLines(assetPath))
        {
            string line = rawLine ?? string.Empty;

            if (!inActionIds)
            {
                if (line.Trim() == "ActionIds:")
                    inActionIds = true;

                continue;
            }

            if (line.StartsWith("  - "))
            {
                string value = line.Substring(4).Trim();
                if (!string.IsNullOrEmpty(value) && !value.Contains(":"))
                    names.Add(value);

                continue;
            }

            if (!line.StartsWith("    "))
                break;
        }

        return names
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct()
            .ToList();
    }

    private static void ImportLegacyNames(SerializedProperty actionIdsProperty, IReadOnlyList<string> legacyNames)
    {
        if (actionIdsProperty == null || !actionIdsProperty.isArray || legacyNames == null)
            return;

        actionIdsProperty.arraySize = legacyNames.Count;

        for (int i = 0; i < legacyNames.Count; i++)
        {
            SerializedProperty element = actionIdsProperty.GetArrayElementAtIndex(i);
            SerializedProperty nameProperty = element.FindPropertyRelative("name");
            SerializedProperty clipProperty = element.FindPropertyRelative("clip");

            if (nameProperty != null)
                nameProperty.stringValue = legacyNames[i];

            if (clipProperty != null)
                clipProperty.objectReferenceValue = null;

            element.isExpanded = true;
        }
    }
}
