using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/TextDatabase")]
public class TextDatabase : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string key;
        [TextArea]
        public string text;
    }

    public List<Entry> entries = new List<Entry>();

    public string GetText(string key)
    {
        var entry = entries.Find(e => e.key == key);
        return entry != null ? entry.text : string.Empty;
    }
}
