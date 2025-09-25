using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class EditorUtilis
{
    /// <summary>
    /// Creates a new empty ScriptableObject asset of type T at the given path/name.
    /// </summary>
    public static T CreateScriptable<T>(string path, string name) where T : ScriptableObject
        => CreateScriptable(path, name, default(T));

    /// <summary>
    /// Creates a new ScriptableObject asset of type T at the given path/name,
    /// cloning the provided instance (values are copied). If instance is null,
    /// an empty asset is created.
    /// </summary>
    public static T CreateScriptable<T>(string path, string name, T instance) where T : ScriptableObject
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must not be null or empty. Expected a folder like 'Assets/MyFolder'.", nameof(path));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name must not be null or empty.", nameof(name));

        // Ensure path starts at Assets
        if (!path.StartsWith("Assets/", StringComparison.Ordinal) && !path.Equals("Assets", StringComparison.Ordinal))
            throw new ArgumentException("Path must start with 'Assets/'.", nameof(path));

        CreateFolderIfNotExist(path);

        string targetPath = AssetDatabase.GenerateUniqueAssetPath($"{path.TrimEnd('/')}/{name}.asset");

        // Create or clone instance
        T toSave = instance == null ? ScriptableObject.CreateInstance<T>() : UnityEngine.Object.Instantiate(instance);
        toSave.name = name;

        AssetDatabase.CreateAsset(toSave, targetPath);
        Undo.RegisterCreatedObjectUndo(toSave, $"Create {typeof(T).Name}");

        EditorUtility.SetDirty(toSave);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = toSave;
        EditorGUIUtility.PingObject(toSave);
        return toSave;
    }

    
    public static void CreateFolderIfNotExist(string assetFolderPath)
    {
        // assetFolderPath like "Assets/Sub/Folder"
        var parts = assetFolderPath.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets")
            throw new ArgumentException("Folder must be under 'Assets/'.", nameof(assetFolderPath));

        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    /// <summary>
    /// Draw an inline inspector for a ScriptableObject.
    /// Cache the Editor you pass in to avoid GC churn.
    /// </summary>
    /// <param name="target">ScriptableObject instance (asset or runtime instance)</param>
    /// <param name="expanded">Foldout state you manage outside</param>
    /// <param name="cachedEditor">Cache field you keep outside (Editor)</param>
    /// <param name="boxed">Draw inside a "box" style</param>
    public static void DrawInspector(UnityEngine.Object target, ref bool expanded, ref Editor cachedEditor, bool boxed = true)
    {
        if (!target) return;

        if (boxed) EditorGUILayout.BeginVertical("box");
        expanded = EditorGUILayout.InspectorTitlebar(expanded, target);
        if (expanded)
        {
            // Reuse the editor for performance
            Editor.CreateCachedEditor(target, null, ref cachedEditor);

            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.IndentLevelScope())
            {
                cachedEditor.OnInspectorGUI();
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, $"Edit {target.name}");
                EditorUtility.SetDirty(target);
                // If it's an asset on disk, you may also want to save immediately:
                // AssetDatabase.SaveAssets();
            }
        }
        if (boxed) EditorGUILayout.EndVertical();
    }
    public static bool GetWorldPositionUnderMouse(Event e, out Vector3 position)
    {
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (Physics.Raycast(ray, out var hit))
        {
            position = hit.point;
            return true;
        }
        position = Vector3.zero;
        return false;
    }

    public static void DrawGroundRect(Vector3 center, float width, float height,
                                  Color fill, Color outline, float yawDeg = 0f)
    {
        Vector3 right = Quaternion.Euler(0f, yawDeg, 0f) * Vector3.right;
        Vector3 forward = Quaternion.Euler(0f, yawDeg, 0f) * Vector3.forward;

        Vector3 rx = right * (width * 0.5f);
        Vector3 fz = forward * (height * 0.5f);

        // Corners lie on y = center.y
        Vector3 a = center - rx - fz; // bottom-left
        Vector3 b = center - rx + fz; // top-left
        Vector3 c = center + rx + fz; // top-right
        Vector3 d = center + rx - fz; // bottom-right

        Handles.DrawSolidRectangleWithOutline(new[] { a, b, c, d }, fill, outline);
    }

    /// <summary>
    /// Finds and returns all assets of type T in the project (under "Assets/").
    /// Works for ScriptableObjects and most asset types (Materials, Textures, Animations, etc.).
    /// For abstract/interface T, it searches ScriptableObjects and filters by assignability.
    /// </summary>
    public static List<T> FindAllAssetsOfTypeInProject<T>() where T : UnityEngine.Object
    {
        var results = new List<T>();
        var seen = new HashSet<int>(); // instanceID de-dup (covers subassets)

        string[] guids;

        var t = typeof(T);
        bool useTypeFilter = !(t.IsAbstract || t.IsInterface);

        if (useTypeFilter)
        {
            // Fast path: Unity's type filter (includes subclasses)
            // e.g., "t:MySOType", "t:Material", "t:AnimationClip"
            guids = AssetDatabase.FindAssets($"t:{t.Name}");
            LoadFromGuids(guids, results, seen);
        }
        else
        {
            // Fallback for abstract/interface: search all ScriptableObjects,
            // then filter by assignability. (Avoids scanning every asset.)
            guids = AssetDatabase.FindAssets("t:ScriptableObject");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                // Load main asset + subassets, then filter
                LoadPathFilteringAssignable<T>(path, results, seen);
            }
        }

        return results;
    }

    // --- Helpers ---

    private static void LoadFromGuids<T>(IEnumerable<string> guids, List<T> outList, HashSet<int> seen)
        where T : UnityEngine.Object
    {
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);

            // Load main asset (if it matches T)
            var main = AssetDatabase.LoadAssetAtPath<T>(path);
            if (main && seen.Add(main.GetInstanceID()))
                outList.Add(main);

            // Also check subassets at the same path (e.g., nested SOs)
            var subs = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
            for (int i = 0; i < subs.Length; i++)
            {
                if (subs[i] is T tObj && seen.Add(tObj.GetInstanceID()))
                    outList.Add(tObj);
            }
        }
    }

    private static void LoadPathFilteringAssignable<T>(string path, List<T> outList, HashSet<int> seen)
        where T : UnityEngine.Object
    {
        // Main asset
        var mainObj = AssetDatabase.LoadMainAssetAtPath(path);
        TryAddIfAssignable(mainObj, outList, seen);

        // Subassets
        var subs = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
        for (int i = 0; i < subs.Length; i++)
            TryAddIfAssignable(subs[i], outList, seen);
    }

    private static void TryAddIfAssignable<T>(UnityEngine.Object obj, List<T> outList, HashSet<int> seen)
        where T : UnityEngine.Object
    {
        if (!obj) return;

        // Unity’s generic cast checks inheritance; also handle components being subassets (rare).
        if (obj is T tObj && seen.Add(tObj.GetInstanceID()))
        {
            outList.Add(tObj);
            return;
        }

        // If T is an abstract/interface implemented by a ScriptableObject, handle via reflection
        // (e.g., T is IMyConfig, obj is SomeConfigSO : ScriptableObject, IMyConfig).
        var wanted = typeof(T);
        if (wanted.IsAssignableFrom(obj.GetType()))
        {
            // Safe downcast via UnityEngine.Object
            var cast = obj as T;
            if (cast && seen.Add(cast.GetInstanceID()))
                outList.Add(cast);
        }
    }
}
