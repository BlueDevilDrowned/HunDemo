using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ActionEditorWindow : EditorWindow
{
    private const float LeftPanelWidth = 220f;
    private const float PreviewHeight = 320f;
    private const float TimelineLabelWidth = 190f;
    private const float TimelineRowHeight = 28f;
    private const float TimelineHandleWidth = 6f;
    private const float MinModuleDuration = 0.02f;

    private ActionConfig actionConfig;
    private SerializedObject configSerializedObject;
    private SerializedProperty actionsProperty;

    private Actor previewSourceActor;
    private Actor previewActorInstance;
    private GameObject previewVisualRoot;
    private Animator previewAnimator;
    private GameObject previewCameraObject;
    private Camera previewCamera;
    private GameObject previewLightObject;
    private Light previewLight;
    private GameObject previewFloorObject;
    private Scene previewScene;
    private RenderTexture previewTexture;
    private string previewStatusText = "Preview not built.";
    private string previewResolvedStatePath = string.Empty;
    private int previewResolvedLayerIndex = -1;

    private int selectedActionIndex;
    private Vector2 actionScroll;
    private Vector2 hierarchyScroll;
    private Vector2 previewOrbit = new Vector2(20f, -35f);
    private Vector3 previewPan;
    private float previewDistance = 4f;
    private float previewTime;
    private bool previewPlaying;
    private double lastEditorTime;
    private bool isDraggingOrbit;
    private bool isDraggingPan;

    private Vector2 timelineScroll;
    private int draggingModuleIndex = -1;
    private DragMode dragMode = DragMode.None;
    private float dragStartMouseX;
    private float dragOriginalStart;
    private float dragOriginalEnd;
    private float dragClipLength;
    private bool dragWasFullRange;

    private enum DragMode
    {
        None,
        Move,
        ResizeStart,
        ResizeEnd
    }

    [MenuItem("Tools/Action Editor")]
    private static void Open()
    {
        GetWindow<ActionEditorWindow>("Action Editor");
    }

    private void OnEnable()
    {
        EditorApplication.update += Tick;
        EnsurePreviewScene();
        RebuildPreviewActor();
    }

    private void OnDisable()
    {
        EditorApplication.update -= Tick;
        DestroyPreviewResources();
    }

    private void OnGUI()
    {
        DrawHeader();

        if (actionConfig == null)
        {
            EditorGUILayout.HelpBox("Drag an ActionConfig asset first.", MessageType.Info);
            return;
        }

        if (configSerializedObject == null)
            BindConfig(actionConfig);

        configSerializedObject.Update();
        ClampSelection();

        EditorGUILayout.Space(4f);

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawActionList();

            using (new EditorGUILayout.VerticalScope())
            {
                DrawPreviewToolbar();
                DrawPreviewViewport();
                DrawTimeline();
            }
        }

        configSerializedObject.ApplyModifiedProperties();
        RepaintIfPreviewPlaying();
    }

    private void DrawHeader()
    {
        EditorGUI.BeginChangeCheck();
        ActionConfig newConfig = (ActionConfig)EditorGUILayout.ObjectField(
            "Action Config",
            actionConfig,
            typeof(ActionConfig),
            false);

        if (EditorGUI.EndChangeCheck())
            BindConfig(newConfig);

        EditorGUI.BeginChangeCheck();
        previewSourceActor = (Actor)EditorGUILayout.ObjectField(
            "Preview Actor",
            previewSourceActor,
            typeof(Actor),
            true);

        if (EditorGUI.EndChangeCheck())
            RebuildPreviewActor();
    }

    private void DrawActionList()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(LeftPanelWidth)))
        {
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            actionScroll = EditorGUILayout.BeginScrollView(actionScroll);

            if (actionsProperty == null || actionsProperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No actions found on this config.", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < actionsProperty.arraySize; i++)
                {
                    SerializedProperty actionProperty = actionsProperty.GetArrayElementAtIndex(i);
                    string label = GetActionLabel(actionProperty, i);
                    bool selected = i == selectedActionIndex;

                    GUIStyle style = selected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                    if (GUILayout.Button(label, style, GUILayout.ExpandWidth(true)))
                    {
                        SelectAction(i);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawPreviewToolbar()
    {
        SerializedProperty selectedAction = GetSelectedActionProperty();
        float clipLength = GetSelectedActionLength(selectedAction);

        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button(previewPlaying ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                previewPlaying = !previewPlaying;
                lastEditorTime = EditorApplication.timeSinceStartup;
            }

            if (GUILayout.Button("Reset", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                previewPlaying = false;
                previewTime = 0f;
                ApplyPreviewPose();
            }

            if (GUILayout.Button("Frame", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                FramePreviewCamera();

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(selectedAction == null);
            EditorGUI.BeginChangeCheck();
            float newTime = EditorGUILayout.Slider(previewTime, 0f, Mathf.Max(clipLength, 0.0001f));
            if (EditorGUI.EndChangeCheck())
            {
                previewPlaying = false;
                previewTime = newTime;
                ApplyPreviewPose();
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.Label(string.Format("{0:0.00} / {1:0.00}", previewTime, clipLength), EditorStyles.miniLabel, GUILayout.Width(90f));
        }
    }

    private void DrawPreviewViewport()
    {
        Rect rect = GUILayoutUtility.GetRect(10f, PreviewHeight, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f));

        HandlePreviewInput(rect);

        if (Event.current.type == EventType.Repaint)
        {
            RenderPreview(rect);
        }

        if (previewTexture != null)
            GUI.DrawTexture(rect, previewTexture, ScaleMode.StretchToFill, false);

        DrawPreviewOverlay(rect);
    }

    private void DrawPreviewOverlay(Rect rect)
    {
        SerializedProperty selectedAction = GetSelectedActionProperty();
        string actionName = selectedAction != null ? GetActionLabel(selectedAction, selectedActionIndex) : "<None>";

        GUI.Label(
            new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 20f),
            "Action: " + actionName,
            EditorStyles.whiteBoldLabel);

        GUI.Label(
            new Rect(rect.x + 8f, rect.y + 30f, rect.width - 16f, 18f),
            previewStatusText,
            EditorStyles.whiteMiniLabel);

        if (previewVisualRoot == null || previewAnimator == null)
        {
            GUI.Label(
                new Rect(rect.x + 8f, rect.y + 50f, rect.width - 16f, 40f),
                "Assign a Preview Actor to see animation playback.",
                EditorStyles.whiteMiniLabel);
        }
        else if (!string.IsNullOrEmpty(previewResolvedStatePath))
        {
            GUI.Label(
                new Rect(rect.x + 8f, rect.y + 50f, rect.width - 16f, 40f),
                $"State: {previewResolvedStatePath}\nLayer: {previewResolvedLayerIndex}",
                EditorStyles.whiteMiniLabel);
        }

    }

    private void DrawTimeline()
    {
        SerializedProperty selectedAction = GetSelectedActionProperty();
        if (selectedAction == null)
        {
            EditorGUILayout.HelpBox("Select an action to edit its module track.", MessageType.Info);
            return;
        }

        SerializedProperty modulesProperty = selectedAction.FindPropertyRelative("modules");
        if (modulesProperty == null || !modulesProperty.isArray)
        {
            EditorGUILayout.HelpBox("This action has no module list.", MessageType.Warning);
            return;
        }

        float clipLength = Mathf.Max(GetSelectedActionLength(selectedAction), 0.0001f);
        int moduleCount = modulesProperty.arraySize;
        float totalHeight = 44f + Mathf.Max(1, moduleCount) * (TimelineRowHeight + 6f);

        Rect outer = GUILayoutUtility.GetRect(10f, totalHeight, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(outer, new Color(0.12f, 0.12f, 0.12f));

        Rect rulerRect = new Rect(outer.x + TimelineLabelWidth, outer.y + 6f, outer.width - TimelineLabelWidth - 10f, 20f);
        DrawRuler(rulerRect, clipLength);

        if (moduleCount == 0)
        {
            GUI.Label(
                new Rect(outer.x + 8f, outer.y + 34f, outer.width - 16f, 20f),
                "No modules on this action.",
                EditorStyles.miniLabel);
            return;
        }

        float pixelsPerSecond = rulerRect.width / clipLength;
        float y = outer.y + 32f;

        for (int i = 0; i < moduleCount; i++)
        {
            SerializedProperty moduleProperty = modulesProperty.GetArrayElementAtIndex(i);
            Rect rowRect = new Rect(outer.x + 6f, y, outer.width - 12f, TimelineRowHeight);
            DrawModuleRow(moduleProperty, rowRect, clipLength, pixelsPerSecond, i);
            y += TimelineRowHeight + 6f;
        }

        DrawCurrentTimeMarker(rulerRect, clipLength);
        HandleTimelineInput(outer, modulesProperty, clipLength, pixelsPerSecond);
    }

    private void DrawRuler(Rect rect, float clipLength)
    {
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f));

        float step = GetNiceStep(clipLength);
        float pixelsPerSecond = rect.width / clipLength;

        for (float t = 0f; t <= clipLength + 0.0001f; t += step)
        {
            float x = rect.x + t * pixelsPerSecond;
            bool major = Mathf.Approximately(t % (step * 2f), 0f);
            float lineHeight = major ? 18f : 10f;
            EditorGUI.DrawRect(new Rect(x, rect.y + rect.height - lineHeight, 1f, lineHeight), major ? Color.white : new Color(0.65f, 0.65f, 0.65f));

            if (major)
            {
                GUI.Label(
                    new Rect(x + 2f, rect.y - 1f, 60f, 18f),
                    t.ToString("0.00"),
                    EditorStyles.miniLabel);
            }
        }
    }

    private void DrawCurrentTimeMarker(Rect rulerRect, float clipLength)
    {
        if (clipLength <= 0f)
            return;

        float pixelsPerSecond = rulerRect.width / clipLength;
        float x = rulerRect.x + Mathf.Clamp(previewTime, 0f, clipLength) * pixelsPerSecond;
        EditorGUI.DrawRect(new Rect(x, rulerRect.y - 4f, 2f, rulerRect.height + 8f), new Color(1f, 0.45f, 0.15f, 0.9f));
    }

    private void DrawModuleRow(SerializedProperty moduleProperty, Rect rowRect, float clipLength, float pixelsPerSecond, int index)
    {
        SerializedProperty startProperty = moduleProperty.FindPropertyRelative("start");
        SerializedProperty endProperty = moduleProperty.FindPropertyRelative("end");
        string typeName = GetManagedReferenceTypeName(moduleProperty);

        if (startProperty == null || endProperty == null)
        {
            EditorGUI.DrawRect(rowRect, new Color(0.2f, 0.2f, 0.2f));
            GUI.Label(new Rect(rowRect.x + 6f, rowRect.y + 5f, rowRect.width, rowRect.height), typeName, EditorStyles.miniLabel);
            return;
        }

        bool isFullRange = Mathf.Approximately(startProperty.floatValue, 0f) && Mathf.Approximately(endProperty.floatValue, 0f);
        float visualStart = isFullRange ? 0f : Mathf.Clamp(startProperty.floatValue, 0f, clipLength);
        float visualEnd = isFullRange ? clipLength : Mathf.Clamp(endProperty.floatValue, visualStart, clipLength);

        Rect labelRect = new Rect(rowRect.x, rowRect.y, TimelineLabelWidth - 8f, rowRect.height);
        Rect trackRect = new Rect(rowRect.x + TimelineLabelWidth, rowRect.y + 3f, rowRect.width - TimelineLabelWidth, rowRect.height - 6f);

        EditorGUI.DrawRect(trackRect, new Color(0.18f, 0.18f, 0.18f));
        GUI.Label(labelRect, typeName, EditorStyles.miniLabel);

        float startX = trackRect.x + visualStart * pixelsPerSecond;
        float endX = trackRect.x + visualEnd * pixelsPerSecond;
        float width = Mathf.Max(8f, endX - startX);
        Rect barRect = new Rect(startX, trackRect.y, width, trackRect.height);
        Color color = GetModuleColor(moduleProperty);
        EditorGUI.DrawRect(barRect, color);

        Rect leftHandle = new Rect(barRect.xMin - TimelineHandleWidth * 0.5f, barRect.y, TimelineHandleWidth, barRect.height);
        Rect rightHandle = new Rect(barRect.xMax - TimelineHandleWidth * 0.5f, barRect.y, TimelineHandleWidth, barRect.height);

        EditorGUI.DrawRect(leftHandle, new Color(1f, 1f, 1f, 0.2f));
        EditorGUI.DrawRect(rightHandle, new Color(1f, 1f, 1f, 0.2f));
        GUI.Label(new Rect(barRect.x + 6f, barRect.y + 4f, barRect.width - 12f, barRect.height - 8f), typeName, EditorStyles.whiteMiniLabel);

        if (isFullRange)
        {
            GUI.Label(new Rect(barRect.xMax - 60f, barRect.y + 4f, 56f, 16f), "Full", EditorStyles.whiteMiniLabel);
        }
    }

    private void HandleTimelineInput(Rect outerRect, SerializedProperty modulesProperty, float clipLength, float pixelsPerSecond)
    {
        if (Event.current == null)
            return;

        Event evt = Event.current;
        Vector2 mouse = evt.mousePosition;
        Rect rulerRect = new Rect(outerRect.x + TimelineLabelWidth, outerRect.y + 6f, outerRect.width - TimelineLabelWidth - 10f, 20f);

        if (!outerRect.Contains(mouse) && evt.type != EventType.MouseDrag && evt.type != EventType.MouseUp)
            return;

        if (draggingModuleIndex >= 0)
        {
            if (evt.type == EventType.MouseDrag)
            {
                float deltaTime = (mouse.x - dragStartMouseX) / pixelsPerSecond;
                ApplyModuleDrag(modulesProperty, draggingModuleIndex, dragMode, deltaTime, clipLength);
                evt.Use();
                Repaint();
                return;
            }

            if (evt.type == EventType.MouseUp)
            {
                FinalizeModuleDrag(modulesProperty, draggingModuleIndex, clipLength);
                draggingModuleIndex = -1;
                dragMode = DragMode.None;
                evt.Use();
                Repaint();
                return;
            }
        }

        if (evt.type != EventType.MouseDown || evt.button != 0)
            return;

        SerializedProperty selectedAction = GetSelectedActionProperty();
        if (selectedAction == null)
            return;

        SerializedProperty selectedModules = selectedAction.FindPropertyRelative("modules");
        if (selectedModules == null || !selectedModules.isArray)
            return;

        float y = rulerRect.y + 28f;
        for (int i = 0; i < selectedModules.arraySize; i++)
        {
            SerializedProperty moduleProperty = selectedModules.GetArrayElementAtIndex(i);
            SerializedProperty startProperty = moduleProperty.FindPropertyRelative("start");
            SerializedProperty endProperty = moduleProperty.FindPropertyRelative("end");
            if (startProperty == null || endProperty == null)
                continue;

            bool isFullRange = Mathf.Approximately(startProperty.floatValue, 0f) && Mathf.Approximately(endProperty.floatValue, 0f);
            float visualStart = isFullRange ? 0f : Mathf.Clamp(startProperty.floatValue, 0f, clipLength);
            float visualEnd = isFullRange ? clipLength : Mathf.Clamp(endProperty.floatValue, visualStart, clipLength);

            Rect rowRect = new Rect(rulerRect.x - TimelineLabelWidth, y, rulerRect.width + TimelineLabelWidth, TimelineRowHeight);
            Rect trackRect = new Rect(rulerRect.x, rowRect.y + 3f, rulerRect.width, rowRect.height - 6f);
            float startX = trackRect.x + visualStart * pixelsPerSecond;
            float endX = trackRect.x + visualEnd * pixelsPerSecond;
            Rect barRect = new Rect(startX, trackRect.y, Mathf.Max(8f, endX - startX), trackRect.height);
            Rect leftHandle = new Rect(barRect.xMin - TimelineHandleWidth * 0.5f, barRect.y, TimelineHandleWidth, barRect.height);
            Rect rightHandle = new Rect(barRect.xMax - TimelineHandleWidth * 0.5f, barRect.y, TimelineHandleWidth, barRect.height);

            if (leftHandle.Contains(mouse))
            {
                BeginModuleDrag(i, DragMode.ResizeStart, mouse.x, startProperty.floatValue, endProperty.floatValue, clipLength, isFullRange);
                evt.Use();
                return;
            }

            if (rightHandle.Contains(mouse))
            {
                BeginModuleDrag(i, DragMode.ResizeEnd, mouse.x, startProperty.floatValue, endProperty.floatValue, clipLength, isFullRange);
                evt.Use();
                return;
            }

            if (barRect.Contains(mouse))
            {
                BeginModuleDrag(i, DragMode.Move, mouse.x, startProperty.floatValue, endProperty.floatValue, clipLength, isFullRange);
                evt.Use();
                return;
            }

            y += TimelineRowHeight + 6f;
        }
    }

    private void BeginModuleDrag(int index, DragMode mode, float mouseX, float start, float end, float clipLength, bool isFullRange)
    {
        draggingModuleIndex = index;
        dragMode = mode;
        dragStartMouseX = mouseX;
        dragOriginalStart = isFullRange ? 0f : start;
        dragOriginalEnd = isFullRange ? clipLength : end;
        dragClipLength = clipLength;
        dragWasFullRange = isFullRange;
        previewPlaying = false;
        Undo.RecordObject(actionConfig, "Edit Action Module Time");
    }

    private void ApplyModuleDrag(SerializedProperty modulesProperty, int index, DragMode mode, float deltaTime, float clipLength)
    {
        if (modulesProperty == null || index < 0 || index >= modulesProperty.arraySize)
            return;

        SerializedProperty moduleProperty = modulesProperty.GetArrayElementAtIndex(index);
        SerializedProperty startProperty = moduleProperty.FindPropertyRelative("start");
        SerializedProperty endProperty = moduleProperty.FindPropertyRelative("end");

        if (startProperty == null || endProperty == null)
            return;

        float originalStart = dragOriginalStart;
        float originalEnd = dragOriginalEnd;
        float duration = Mathf.Max(MinModuleDuration, originalEnd - originalStart);

        float newStart = originalStart;
        float newEnd = originalEnd;

        switch (mode)
        {
            case DragMode.Move:
            {
                newStart = Mathf.Clamp(originalStart + deltaTime, 0f, Mathf.Max(0f, clipLength - duration));
                newEnd = newStart + duration;
                break;
            }
            case DragMode.ResizeStart:
            {
                newStart = Mathf.Clamp(originalStart + deltaTime, 0f, originalEnd - MinModuleDuration);
                break;
            }
            case DragMode.ResizeEnd:
            {
                newEnd = Mathf.Clamp(originalEnd + deltaTime, originalStart + MinModuleDuration, clipLength);
                break;
            }
        }

        SetModuleRange(startProperty, endProperty, newStart, newEnd, clipLength);
        configSerializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(actionConfig);
    }

    private void FinalizeModuleDrag(SerializedProperty modulesProperty, int index, float clipLength)
    {
        if (modulesProperty == null || index < 0 || index >= modulesProperty.arraySize)
            return;

        SerializedProperty moduleProperty = modulesProperty.GetArrayElementAtIndex(index);
        SerializedProperty startProperty = moduleProperty.FindPropertyRelative("start");
        SerializedProperty endProperty = moduleProperty.FindPropertyRelative("end");

        if (startProperty == null || endProperty == null)
            return;

        float start = Mathf.Max(0f, startProperty.floatValue);
        float end = Mathf.Max(start, endProperty.floatValue);
        if (Mathf.Approximately(start, 0f) && Mathf.Approximately(end, clipLength))
        {
            startProperty.floatValue = 0f;
            endProperty.floatValue = 0f;
        }

        configSerializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(actionConfig);
    }

    private static void SetModuleRange(SerializedProperty startProperty, SerializedProperty endProperty, float start, float end, float clipLength)
    {
        start = Mathf.Clamp(start, 0f, clipLength);
        end = Mathf.Clamp(end, start + MinModuleDuration, clipLength);

        if (Mathf.Approximately(start, 0f) && Mathf.Approximately(end, clipLength))
        {
            startProperty.floatValue = 0f;
            endProperty.floatValue = 0f;
        }
        else
        {
            startProperty.floatValue = start;
            endProperty.floatValue = end;
        }
    }

    private void HandlePreviewInput(Rect rect)
    {
        Event evt = Event.current;
        if (!rect.Contains(evt.mousePosition))
            return;

        if (evt.type == EventType.ScrollWheel)
        {
            previewDistance = Mathf.Clamp(previewDistance * (1f + evt.delta.y * 0.05f), 0.5f, 50f);
            evt.Use();
            Repaint();
            return;
        }

        if (evt.type == EventType.MouseDown && evt.button == 1)
        {
            isDraggingOrbit = true;
            dragStartMouseX = evt.mousePosition.x;
            evt.Use();
            return;
        }

        if (evt.type == EventType.MouseDown && evt.button == 2)
        {
            isDraggingPan = true;
            dragStartMouseX = evt.mousePosition.x;
            evt.Use();
            return;
        }

        if (evt.type == EventType.MouseUp && evt.button == 1)
        {
            isDraggingOrbit = false;
            evt.Use();
            return;
        }

        if (evt.type == EventType.MouseUp && evt.button == 2)
        {
            isDraggingPan = false;
            evt.Use();
            return;
        }

        if (evt.type == EventType.MouseDrag && isDraggingOrbit)
        {
            previewOrbit.x -= evt.delta.y * 0.3f;
            previewOrbit.y += evt.delta.x * 0.3f;
            evt.Use();
            Repaint();
            return;
        }

        if (evt.type == EventType.MouseDrag && isDraggingPan)
        {
            Vector3 right = previewCamera != null ? previewCamera.transform.right : Vector3.right;
            Vector3 up = previewCamera != null ? previewCamera.transform.up : Vector3.up;
            previewPan += (-right * evt.delta.x + up * evt.delta.y) * 0.01f * previewDistance;
            evt.Use();
            Repaint();
            return;
        }

        if (evt.type == EventType.MouseDown && evt.button == 0 && evt.clickCount == 2)
        {
            FramePreviewCamera();
            evt.Use();
        }
    }

    private void RenderPreview(Rect rect)
    {
        if (previewCamera == null || previewScene.IsValid() == false)
            return;

        EnsurePreviewTexture(rect);
        if (previewTexture == null)
            return;

        if (!previewPlaying)
            ApplyPreviewPose();

        UpdatePreviewCameraTransform();

        previewCamera.targetTexture = previewTexture;
        previewCamera.Render();
    }

    private void UpdatePreviewCameraTransform()
    {
        if (previewCamera == null)
            return;

        Vector3 pivot = previewPan + GetPreviewBoundsCenter();
        Quaternion rotation = Quaternion.Euler(previewOrbit.x, previewOrbit.y, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -previewDistance);

        previewCamera.transform.position = pivot + offset;
        previewCamera.transform.rotation = Quaternion.LookRotation(pivot - previewCamera.transform.position, Vector3.up);
    }

    private void FramePreviewCamera()
    {
        Bounds bounds = GetPreviewBounds();
        previewPan = Vector3.zero;
        previewDistance = Mathf.Max(2f, bounds.extents.magnitude * 2.2f);
        previewOrbit = new Vector2(20f, -35f);
        UpdatePreviewCameraTransform();
        Repaint();
    }

    private void ApplyPreviewPose()
    {
        if (previewVisualRoot == null || actionConfig == null)
        {
            previewStatusText = "Preview actor missing.";
            return;
        }

        SerializedProperty selectedAction = GetSelectedActionProperty();
        if (selectedAction == null)
        {
            previewStatusText = "No action selected.";
            return;
        }

        SerializedProperty statePathProperty = selectedAction.FindPropertyRelative("StatePath");
        if (statePathProperty == null || string.IsNullOrEmpty(statePathProperty.stringValue))
        {
            previewStatusText = "StatePath is empty.";
            return;
        }

        if (previewAnimator == null || actionConfig.animatorController == null)
        {
            previewStatusText = "Animator controller missing.";
            return;
        }

        previewAnimator.runtimeAnimatorController = actionConfig.animatorController;
        previewAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        previewAnimator.applyRootMotion = true;
        previewAnimator.enabled = true;

        float clipLength = Mathf.Max(GetSelectedActionLength(selectedAction), 0.0001f);
        float normalizedTime = Mathf.Clamp01(previewTime / clipLength);
        previewResolvedLayerIndex = GetStateLayerIndex(statePathProperty.stringValue);
        previewResolvedStatePath = statePathProperty.stringValue;

        if (!TryFindStateByPath(actionConfig.animatorController as AnimatorController, statePathProperty.stringValue, out AnimatorState state) || state == null)
        {
            previewStatusText = "State not found in AnimatorController: " + statePathProperty.stringValue;
            return;
        }

        previewAnimator.speed = 1f;
        previewAnimator.Play(statePathProperty.stringValue, previewResolvedLayerIndex, normalizedTime);
        previewAnimator.Update(0f);
        previewStatusText = $"Playing at {previewTime:0.00}s / {clipLength:0.00}s";
    }

    private void Tick()
    {
        if (!previewPlaying || actionConfig == null)
            return;

        SerializedProperty selectedAction = GetSelectedActionProperty();
        float clipLength = GetSelectedActionLength(selectedAction);
        if (clipLength <= 0f)
            return;

        double now = EditorApplication.timeSinceStartup;
        float delta = (float)(now - lastEditorTime);
        lastEditorTime = now;

        previewTime += delta;
        bool loop = GetSelectedActionLoop(selectedAction);

        if (loop)
        {
            previewTime = Mathf.Repeat(previewTime, clipLength);
        }
        else if (previewTime >= clipLength)
        {
            previewTime = clipLength;
            previewPlaying = false;
        }

        if (previewAnimator != null)
        {
            previewAnimator.Update(delta);
            previewStatusText = $"Running at {previewTime:0.00}s / {clipLength:0.00}s";
        }
        else
        {
            ApplyPreviewPose();
        }
        Repaint();
    }

    private void RepaintIfPreviewPlaying()
    {
        if (previewPlaying)
            Repaint();
    }

    private void BindConfig(ActionConfig newConfig)
    {
        actionConfig = newConfig;
        configSerializedObject = actionConfig != null ? new SerializedObject(actionConfig) : null;
        actionsProperty = configSerializedObject != null ? configSerializedObject.FindProperty("actions") : null;
        selectedActionIndex = 0;
        previewTime = 0f;
        previewPlaying = false;
        RebuildPreviewActor();
        Repaint();
    }

    private void SelectAction(int index)
    {
        selectedActionIndex = index;
        previewTime = 0f;
        previewPlaying = false;
        ApplyPreviewPose();
        FramePreviewCamera();
    }

    private void ClampSelection()
    {
        if (actionsProperty == null || actionsProperty.arraySize == 0)
        {
            selectedActionIndex = -1;
            return;
        }

        selectedActionIndex = Mathf.Clamp(selectedActionIndex, 0, actionsProperty.arraySize - 1);
    }

    private SerializedProperty GetSelectedActionProperty()
    {
        if (actionsProperty == null || selectedActionIndex < 0 || selectedActionIndex >= actionsProperty.arraySize)
            return null;

        return actionsProperty.GetArrayElementAtIndex(selectedActionIndex);
    }

    private void RebuildPreviewActor()
    {
        DestroyPreviewActor();

        Actor sourceActor = previewSourceActor;

        if (actionConfig == null || sourceActor == null)
            return;

        EnsurePreviewScene();

        previewVisualRoot = Instantiate(sourceActor.gameObject);
        previewVisualRoot.hideFlags = HideFlags.HideAndDontSave;
        SceneManager.MoveGameObjectToScene(previewVisualRoot, previewScene);

        previewActorInstance = previewVisualRoot.GetComponent<Actor>();
        previewAnimator = previewVisualRoot.GetComponentInChildren<Animator>(true);
        if (previewAnimator == null)
        {
            previewStatusText = "Preview actor has no Animator.";
            return;
        }

        DisablePreviewBehaviours(previewVisualRoot);
        FramePreviewCamera();
        ApplyPreviewPose();
    }

    private void DestroyPreviewActor()
    {
        if (previewVisualRoot != null)
        {
            DestroyImmediate(previewVisualRoot);
            previewVisualRoot = null;
        }

        previewAnimator = null;
        previewActorInstance = null;
        previewResolvedStatePath = string.Empty;
        previewResolvedLayerIndex = -1;
        previewStatusText = "Preview not built.";
    }

    private void EnsurePreviewScene()
    {
        if (previewScene.IsValid())
            return;

        previewScene = EditorSceneManager.NewPreviewScene();
        CreatePreviewEnvironment();
    }

    private void CreatePreviewEnvironment()
    {
        previewCameraObject = new GameObject("Action Preview Camera");
        previewCameraObject.hideFlags = HideFlags.HideAndDontSave;
        SceneManager.MoveGameObjectToScene(previewCameraObject, previewScene);
        previewCamera = previewCameraObject.AddComponent<Camera>();
        previewCamera.cameraType = CameraType.Preview;
        previewCamera.clearFlags = CameraClearFlags.Color;
        previewCamera.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        previewCamera.nearClipPlane = 0.01f;
        previewCamera.farClipPlane = 100f;
        previewCamera.fieldOfView = 35f;

        previewLightObject = new GameObject("Action Preview Light");
        previewLightObject.hideFlags = HideFlags.HideAndDontSave;
        SceneManager.MoveGameObjectToScene(previewLightObject, previewScene);
        previewLight = previewLightObject.AddComponent<Light>();
        previewLight.type = LightType.Directional;
        previewLight.intensity = 1.2f;
        previewLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        previewFloorObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
        previewFloorObject.name = "Action Preview Floor";
        previewFloorObject.hideFlags = HideFlags.HideAndDontSave;
        SceneManager.MoveGameObjectToScene(previewFloorObject, previewScene);
        previewFloorObject.transform.position = Vector3.zero;
        previewFloorObject.transform.localScale = Vector3.one * 4f;
        Collider floorCollider = previewFloorObject.GetComponent<Collider>();
        if (floorCollider != null)
            DestroyImmediate(floorCollider);
    }

    private void EnsurePreviewTexture(Rect rect)
    {
        int width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
        int height = Mathf.Max(1, Mathf.RoundToInt(rect.height));

        if (previewTexture != null && previewTexture.width == width && previewTexture.height == height)
            return;

        if (previewTexture != null)
        {
            previewTexture.Release();
            DestroyImmediate(previewTexture);
        }

        previewTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private void DestroyPreviewResources()
    {
        DestroyPreviewActor();

        if (previewTexture != null)
        {
            previewTexture.Release();
            DestroyImmediate(previewTexture);
            previewTexture = null;
        }

        if (previewCameraObject != null)
        {
            DestroyImmediate(previewCameraObject);
            previewCameraObject = null;
            previewCamera = null;
        }

        if (previewLightObject != null)
        {
            DestroyImmediate(previewLightObject);
            previewLightObject = null;
            previewLight = null;
        }

        if (previewFloorObject != null)
        {
            DestroyImmediate(previewFloorObject);
            previewFloorObject = null;
        }

        if (previewScene.IsValid())
        {
            EditorSceneManager.ClosePreviewScene(previewScene);
            previewScene = default;
        }
    }

    private Bounds GetPreviewBounds()
    {
        if (previewVisualRoot == null)
            return new Bounds(Vector3.zero, Vector3.one * 2f);

        Renderer[] renderers = previewVisualRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
            return new Bounds(previewVisualRoot.transform.position, Vector3.one * 2f);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    private Vector3 GetPreviewBoundsCenter()
    {
        return GetPreviewBounds().center;
    }

    private float GetSelectedActionLength(SerializedProperty actionProperty)
    {
        if (actionProperty == null || actionConfig == null)
            return 1f;

        SerializedProperty statePathProperty = actionProperty.FindPropertyRelative("StatePath");
        if (statePathProperty == null || string.IsNullOrEmpty(statePathProperty.stringValue))
            return 1f;

        AnimatorController controller = actionConfig.animatorController as AnimatorController;
        if (controller == null)
            return 1f;

        if (!TryFindStateByPath(controller, statePathProperty.stringValue, out AnimatorState state) || state == null)
            return 1f;

        return GetMotionLength(state.motion);
    }

    private bool GetSelectedActionLoop(SerializedProperty actionProperty)
    {
        if (actionProperty == null)
            return false;

        SerializedProperty loopProperty = actionProperty.FindPropertyRelative("Loop");
        return loopProperty != null && loopProperty.boolValue;
    }

    private static float GetMotionLength(Motion motion)
    {
        if (motion == null)
            return 1f;

        if (motion is AnimationClip clip)
            return Mathf.Max(0.0001f, clip.length);

        if (motion is BlendTree tree)
        {
            float maxLength = 0f;
            foreach (ChildMotion child in tree.children)
                maxLength = Mathf.Max(maxLength, GetMotionLength(child.motion));

            return Mathf.Max(0.0001f, maxLength);
        }

        return 1f;
    }

    private static void DisablePreviewBehaviours(GameObject root)
    {
        if (root == null)
            return;

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            behaviour.enabled = false;
        }

        CharacterController[] characterControllers = root.GetComponentsInChildren<CharacterController>(true);
        foreach (CharacterController characterController in characterControllers)
            characterController.enabled = false;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
            collider.enabled = false;
    }

    private int GetStateLayerIndex(string fullPath)
    {
        if (actionConfig == null)
            return 0;

        AnimatorController controller = actionConfig.animatorController as AnimatorController;
        if (controller == null || string.IsNullOrEmpty(fullPath))
            return 0;

        string[] parts = fullPath.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return 0;

        for (int i = 0; i < controller.layers.Length; i++)
        {
            if (controller.layers[i].name == parts[0])
                return i;
        }

        return 0;
    }

    private static bool TryFindStateByPath(AnimatorController controller, string fullPath, out AnimatorState state)
    {
        state = null;

        if (controller == null || string.IsNullOrEmpty(fullPath))
            return false;

        string[] parts = fullPath.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return false;

        AnimatorControllerLayer layer = controller.layers.FirstOrDefault(l => l.name == parts[0]);
        if (layer == null)
            return false;

        return TryFindStateRecursive(layer.stateMachine, parts, 1, out state);
    }

    private static bool TryFindStateRecursive(AnimatorStateMachine stateMachine, string[] parts, int index, out AnimatorState state)
    {
        state = null;
        if (stateMachine == null || parts == null || index >= parts.Length)
            return false;

        if (index == parts.Length - 1)
        {
            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                if (childState.state != null && childState.state.name == parts[index])
                {
                    state = childState.state;
                    return true;
                }
            }

            return false;
        }

        foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
        {
            if (childMachine.stateMachine != null && childMachine.stateMachine.name == parts[index])
                return TryFindStateRecursive(childMachine.stateMachine, parts, index + 1, out state);
        }

        return false;
    }

    private static float GetNiceStep(float clipLength)
    {
        if (clipLength <= 0.1f)
            return 0.01f;
        if (clipLength <= 0.5f)
            return 0.05f;
        if (clipLength <= 2f)
            return 0.1f;
        if (clipLength <= 5f)
            return 0.25f;
        if (clipLength <= 10f)
            return 0.5f;
        return 1f;
    }

    private static string GetActionLabel(SerializedProperty actionProperty, int index)
    {
        SerializedProperty actionIdProperty = actionProperty.FindPropertyRelative("actionId");
        string actionName = actionIdProperty != null && !string.IsNullOrEmpty(actionIdProperty.stringValue)
            ? actionIdProperty.stringValue
            : "Action " + index;

        return index + ": " + actionName;
    }

    private static string GetManagedReferenceTypeName(SerializedProperty property)
    {
        if (property == null || property.managedReferenceValue == null)
            return "Null Module";

        return ObjectNames.NicifyVariableName(property.managedReferenceValue.GetType().Name);
    }

    private static Color GetModuleColor(SerializedProperty property)
    {
        string typeName = property != null && property.managedReferenceValue != null
            ? property.managedReferenceValue.GetType().Name
            : "Null";

        int hash = typeName.GetHashCode();
        float hue = Mathf.Abs(hash % 360) / 360f;
        Color color = Color.HSVToRGB(hue, 0.55f, 0.85f);
        color.a = 0.9f;
        return color;
    }
}
