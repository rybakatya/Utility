// UserData.cs  (runtime, NOT Editor-only)
using System;
using System.Collections.Generic;
using UnityEngine;

public interface ICustomData { } // marker interface

[Serializable]
public class CustomDataContainer
{
    [Serializable]
    public class Entry
    {
        public string key;
        [SerializeReference] public ICustomData data; // concrete type chosen in Inspector
    }

    public List<Entry> entries = new();

    // --- Runtime helpers (same as the SO version) ---
    public bool TryGet<T>(string key, out T value) where T : class, ICustomData
    {
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.key == key && e.data is T t) { value = t; return true; }
        }
        value = null;
        return false;
    }

    public T GetOrCreate<T>(string key) where T : class, ICustomData, new()
    {
        if (TryGet<T>(key, out var existing)) return existing;
        var created = new T();
        entries.Add(new Entry { key = key, data = created });
        return created;
    }

    public void Set<T>(string key, T value) where T : class, ICustomData
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].key == key) { entries[i].data = value; return; }
        }
        entries.Add(new Entry { key = key, data = value });
    }
}

// Example payloads (make your own types like these)
[Serializable] public class IntData : ICustomData { public int value; }
[Serializable] public class FloatData : ICustomData { public float value; }
[Serializable] public class StringData : ICustomData { public string value; }
[Serializable] public class Vec3Data : ICustomData { public Vector3 value; }
[Serializable] public class CurveData : ICustomData { public AnimationCurve curve = AnimationCurve.Linear(0, 0, 1, 1); }
[Serializable] public class GameObjectCollection : ICustomData { public GameObject[] collection; }



