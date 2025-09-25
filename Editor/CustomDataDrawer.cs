// UserDataContainerDrawer.cs  (Editor)
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(CustomDataContainer))]
public class CustomDataContainerDrawer : PropertyDrawer
{
    static List<Type> _payloadTypes;
    static string[] _payloadNames;

    const float PADDING = 6f;
    const float SPACING = 4f;
    const float BTN_W = 22f;

    static void EnsureTypes()
    {
        if (_payloadTypes != null) return;

        _payloadTypes = TypeCache.GetTypesDerivedFrom<ICustomData>()
            .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition &&
                        t.GetCustomAttributes(typeof(SerializableAttribute), true).Any())
            .OrderBy(t => t.Name)
            .ToList();

        _payloadNames = _payloadTypes.Select(t => t.Name).ToArray();
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EnsureTypes();
        var entries = property.FindPropertyRelative("entries");
        float lineH = EditorGUIUtility.singleLineHeight;

        // Header row
        var headerRect = new Rect(position.x, position.y, position.width, lineH);
        var buttonsRect = new Rect(headerRect.xMax - BTN_W, headerRect.y, BTN_W, lineH);
        var foldoutRect = new Rect(headerRect.x, headerRect.y, headerRect.width - (buttonsRect.width + 4f), lineH);

        EditorGUI.BeginProperty(position, label, property);

        // Foldout draws only on the left area (no overlap with +)
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        // Right-aligned + button (icon style)
        var plusContent = EditorGUIUtility.IconContent("Toolbar Plus");
        if (plusContent == null || plusContent.image == null) plusContent = new GUIContent("+");
        if (GUI.Button(buttonsRect, plusContent, EditorStyles.iconButton))
        {
            Undo.RecordObject(property.serializedObject.targetObject, "Add Entry");
            entries.arraySize++;
            var e = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            e.FindPropertyRelative("key").stringValue = "NewKey";

            var dataProp = e.FindPropertyRelative("data");
            if (_payloadTypes.Count > 0)
                dataProp.managedReferenceValue = Activator.CreateInstance(_payloadTypes[0]);

            property.serializedObject.ApplyModifiedProperties();
            GUIUtility.ExitGUI(); // ensure immediate relayout; single click works
            return;
        }

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        float y = headerRect.yMax + SPACING;

        for (int i = 0; i < entries.arraySize; i++)
        {
            var e = entries.GetArrayElementAtIndex(i);
            var keyProp = e.FindPropertyRelative("key");
            var dataProp = e.FindPropertyRelative("data");

            float payloadH = EditorGUI.GetPropertyHeight(dataProp, includeChildren: true);
            float boxH = PADDING + lineH + SPACING + lineH + SPACING + payloadH + PADDING;

            var boxRect = new Rect(position.x, y, position.width, boxH);
            GUI.Box(boxRect, GUIContent.none);

            float x = boxRect.x + PADDING;
            float w = boxRect.width - PADDING * 2f;
            float rowY = boxRect.y + PADDING;

            // Row 1: Key + Remove
            var keyRect = new Rect(x, rowY, w - 28f, lineH);
            keyProp.stringValue = EditorGUI.TextField(keyRect, "Key", keyProp.stringValue);

            var delRect = new Rect(x + w - 24f, rowY, 24f, lineH);
            if (GUI.Button(delRect, "X"))
            {
                Undo.RecordObject(property.serializedObject.targetObject, "Remove Entry");
                entries.DeleteArrayElementAtIndex(i);
                property.serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
                return;
            }

            // Row 2: Type popup
            rowY += lineH + SPACING;
            int idx = IndexOfManagedType(dataProp);
            int nextIdx = EditorGUI.Popup(new Rect(x, rowY, w, lineH), "Type", Mathf.Max(0, idx), _payloadNames);
            if (nextIdx != idx && nextIdx >= 0 && nextIdx < _payloadTypes.Count)
            {
                Undo.RecordObject(property.serializedObject.targetObject, "Change Payload Type");
                dataProp.managedReferenceValue = Activator.CreateInstance(_payloadTypes[nextIdx]);
                property.serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
                return;
            }

            // Row 3+: Inline payload
            rowY += lineH + SPACING;
            EditorGUI.PropertyField(new Rect(x, rowY, w, payloadH), dataProp, includeChildren: true);

            y += boxH + SPACING;
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float h = EditorGUIUtility.singleLineHeight + SPACING; // header
        if (!property.isExpanded) return h;

        var entries = property.FindPropertyRelative("entries");
        for (int i = 0; i < entries.arraySize; i++)
        {
            var dataProp = entries.GetArrayElementAtIndex(i).FindPropertyRelative("data");
            float payloadH = EditorGUI.GetPropertyHeight(dataProp, true);
            h += PADDING + EditorGUIUtility.singleLineHeight + SPACING +
                 EditorGUIUtility.singleLineHeight + SPACING + payloadH +
                 PADDING + SPACING;
        }
        return h;
    }

    static int IndexOfManagedType(SerializedProperty dataProp)
    {
        var obj = dataProp.managedReferenceValue;
        if (obj == null || _payloadTypes == null) return -1;
        var t = obj.GetType();
        for (int i = 0; i < _payloadTypes.Count; i++)
            if (_payloadTypes[i] == t) return i;
        return -1;
    }
}
#endif
