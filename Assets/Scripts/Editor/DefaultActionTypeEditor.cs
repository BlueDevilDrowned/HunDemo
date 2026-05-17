using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DefaultActionType))]
public class DefaultActionTypeEditor : Editor
{
    private SerializedProperty actionIdTableProperty;
    private SerializedProperty defaultActionsProperty;

    private void OnEnable()
    {
        actionIdTableProperty = serializedObject.FindProperty("actionId");
        defaultActionsProperty = serializedObject.FindProperty("DefaultActions");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Default Action Type Map", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(actionIdTableProperty, new GUIContent("Action Id Table"));

        if (actionIdTableProperty.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox("Assign an ActionId table to enable Action Id dropdowns.", MessageType.Warning);
        }

        EditorGUILayout.Space(4f);
        DrawDefaultActions();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawDefaultActions()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Default Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Add", GUILayout.Width(70f)))
            {
                defaultActionsProperty.arraySize++;
            }
        }

        EditorGUI.indentLevel++;

        for (int i = 0; i < defaultActionsProperty.arraySize; i++)
        {
            SerializedProperty element = defaultActionsProperty.GetArrayElementAtIndex(i);
            DrawDefaultAction(element, i);
        }

        EditorGUI.indentLevel--;
    }

    private void DrawDefaultAction(SerializedProperty element, int index)
    {
        SerializedProperty actionTypeProperty = element.FindPropertyRelative("actionType");
        SerializedProperty actionIdProperty = element.FindPropertyRelative("actionId");
        SerializedProperty interruptLevelProperty = element.FindPropertyRelative("interruptLevel");

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Entry {index}", EditorStyles.boldLabel);

                if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                {
                    defaultActionsProperty.DeleteArrayElementAtIndex(index);
                    return;
                }
            }

            EditorGUILayout.PropertyField(actionTypeProperty);
            DrawActionIdDropdown(actionIdProperty);

            if (interruptLevelProperty != null)
                EditorGUILayout.PropertyField(interruptLevelProperty, new GUIContent("Interrupt Level"));
        }
    }

    private void DrawActionIdDropdown(SerializedProperty actionIdProperty)
    {
        if (actionIdProperty == null)
            return;

        ActionId table = actionIdTableProperty.objectReferenceValue as ActionId;
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

    private static List<string> GetActionIdOptions(ActionId table)
    {
        return ActionIdEditor.GetOptionNames(table);
    }
}
