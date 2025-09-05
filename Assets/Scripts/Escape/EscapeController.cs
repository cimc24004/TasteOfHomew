// EscapeControllerNoPoll_WithFlagEnd.cs
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

public class EscapeController : MonoBehaviour
{
    [Header("DialogueController（任意：Inspectorでセット）")]
    [SerializeField] private MonoBehaviour dialogueController;

    [Header("フラグキー")]
    [SerializeField] private string friendHouseKey = "EscapeFriendHouse";
    [SerializeField] private string myHouseKey = "EscapeMyHouse";

    [Header("イベント（Start / End を外部からアサイン）")]
    public UnityEvent onFriendStart;
    public UnityEvent onFriendEnd;

    public UnityEvent onMyStart;
    public UnityEvent onMyEnd;
    private DataShareController dataShareController;
    private void Awake()
    {
        if (dialogueController == null) TryFindDialogueControllerSingleton();
        
    }
    void Start()
    {
        dataShareController = DataShareController.Instance;
    }
    // --- 既存の Start 呼び出し用メソッド（そのまま） ---
    public void OnFriendSelected()
    {
        Debug.Log("[EscapeController] Friend selected");
        onFriendStart?.Invoke();
    }

    public void OnFriendEnded()
    {
        Debug.Log("[EscapeController] Friend end");
        onFriendEnd?.Invoke();
        dataShareController.SetFlagToTrue("FriendHouseEnd");
    }

    public void OnMySelected()
    {
        Debug.Log("[EscapeController] My selected");
        onMyStart?.Invoke();
    }

    public void OnMyEnded()
    {
        dataShareController.SetFlagToTrue("EscapeMyHouseEnd");
        Debug.Log("[EscapeController] My end");
        onMyEnd?.Invoke();
    }

    // ---------- ここから追加メソッド ----------
    /// <summary>
    /// DialogueController の flags から判断して、
    /// friend が立っていれば OnFriendEnded() を、そうでなければ my が立っていれば OnMyEnded() を呼ぶ。
    /// 優先度：Friend > My
    /// </summary>
    public void ExecuteEndFromFlags()
    {
        // try to ensure dialogueController reference
        if (dialogueController == null) TryFindDialogueControllerSingleton();

        bool friendVal = false;
        bool myVal = false;
        bool friendFound = TryGetFlagValue(friendHouseKey, out friendVal);
        bool myFound = TryGetFlagValue(myHouseKey, out myVal);

        // ログ（デバッグ用）
        Debug.Log($"[EscapeController] ExecuteEndFromFlags: friendFound={friendFound}, friendVal={friendVal}, myFound={myFound}, myVal={myVal}");

        // 優先度：友達フラグが true ならそちらの End を実行
        if (friendFound && friendVal)
        {
            OnFriendEnded();
            return;
        }

        if (myFound && myVal)
        {
            OnMyEnded();
            return;
        }

        // どちらも見つからない or false のときは何もしない（必要ならログを追加）
        Debug.LogWarning("[EscapeController] No matching escape flag was true. ExecuteEndFromFlags did nothing.");
    }

    // --- フラグ取得の柔軟処理（DialogueController 実装に依存しない） ---
    private bool TryGetFlagValue(string key, out bool value)
    {
        value = false;

        if (string.IsNullOrEmpty(key)) return false;

        if (dialogueController != null)
        {
            Type t = dialogueController.GetType();

            // よくあるメソッド名を試す（Has/Get系）
            string[] methodNames = { "HasFlag", "Has", "HasGameFlag", "GetFlag", "GetBool", "IsFlagSet" };
            foreach (var mn in methodNames)
            {
                MethodInfo mi = t.GetMethod(mn, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (mi != null && mi.ReturnType == typeof(bool))
                {
                    try
                    {
                        object ret = mi.Invoke(dialogueController, new object[] { key });
                        if (ret is bool b)
                        {
                            value = b;
                            return true;
                        }
                    }
                    catch { /* ignore */ }
                }
            }

            // 'flags' 等のフィールド/プロパティを探す
            string[] fieldNames = { "flags", "gameFlags", "GameFlags" };
            foreach (var fn in fieldNames)
            {
                FieldInfo fi = t.GetField(fn, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (fi != null)
                {
                    object fieldObj = fi.GetValue(dialogueController);
                    if (TryExtractFromFlagsContainer(fieldObj, key, out value)) return true;
                }

                PropertyInfo pi = t.GetProperty(fn, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (pi != null)
                {
                    object propObj = pi.GetValue(dialogueController);
                    if (TryExtractFromFlagsContainer(propObj, key, out value)) return true;
                }
            }
        }

        // 静的 GameFlags クラスがあればそれを試す（任意）
        Type gfType = Type.GetType("GameFlags") ?? Type.GetType("GameFlags,Assembly-CSharp");
        if (gfType != null)
        {
            MethodInfo mi = gfType.GetMethod("Has", BindingFlags.Public | BindingFlags.Static)
                            ?? gfType.GetMethod("HasFlag", BindingFlags.Public | BindingFlags.Static);
            if (mi != null)
            {
                try
                {
                    object ret = mi.Invoke(null, new object[] { key });
                    if (ret is bool b)
                    {
                        value = b;
                        return true;
                    }
                }
                catch { /* ignore */ }
            }
        }

        // 見つからなかった
        return false;
    }

    private bool TryExtractFromFlagsContainer(object container, string key, out bool value)
    {
        value = false;
        if (container == null) return false;

        // IDictionary 風の取り扱い
        if (container is System.Collections.IDictionary dict)
        {
            try
            {
                if (dict.Contains(key))
                {
                    object v = dict[key];
                    if (v is bool b)
                    {
                        value = b;
                        return true;
                    }
                }
            }
            catch { }
        }

        Type ct = container.GetType();
        string[] methodNames = { "Has", "HasFlag", "Get", "GetFlag", "ContainsKey" };
        foreach (var mn in methodNames)
        {
            MethodInfo mi = ct.GetMethod(mn, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (mi != null)
            {
                try
                {
                    object ret = mi.Invoke(container, new object[] { key });
                    if (ret is bool b)
                    {
                        value = b;
                        return true;
                    }
                }
                catch { }
            }
        }

        // インデクサ試行
        try
        {
            PropertyInfo indexer = ct.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance);
            if (indexer != null)
            {
                object ret = indexer.GetValue(container, new object[] { key });
                if (ret is bool b)
                {
                    value = b;
                    return true;
                }
            }
        }
        catch { }

        return false;
    }
    /// <summary>
    /// flags をチェックして該当する Start メソッドを呼ぶ。
    /// 優先度：Friend > My
    /// DialogueController の flags を参照する実装（dialogueController フィールドに依存）。
    /// </summary>
    public void ExecuteStartFromFlags()
    {
        // dialogueController が未設定なら探す（既存のヘルパを利用）
        if (dialogueController == null) TryFindDialogueControllerSingleton();

        bool friendVal = false;
        bool myVal = false;
        bool friendFound = TryGetFlagValue(friendHouseKey, out friendVal);
        bool myFound = TryGetFlagValue(myHouseKey, out myVal);

        Debug.Log($"[EscapeController] ExecuteStartFromFlags: friendFound={friendFound}, friendVal={friendVal}, myFound={myFound}, myVal={myVal}");

        // friend優先
        if (friendFound && friendVal)
        {
            OnFriendSelected(); // Start イベントを呼ぶ
            return;
        }

        if (myFound && myVal)
        {
            OnMySelected(); // Start イベントを呼ぶ
            return;
        }

        Debug.LogWarning("[EscapeController] ExecuteStartFromFlags: no true flag found, nothing executed.");
    }

    // シーンから DialogueController を探す簡易ヘルパ
    private void TryFindDialogueControllerSingleton()
    {
        // try static Instance property first
        Type dcType = Type.GetType("DialogueController") ?? Type.GetType("DialogueController,Assembly-CSharp");
        if (dcType != null)
        {
            PropertyInfo pi = dcType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (pi != null)
            {
                object inst = pi.GetValue(null);
                if (inst is MonoBehaviour mb)
                {
                    dialogueController = mb;
                    return;
                }
            }

            FieldInfo fi = dcType.GetField("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
                           ?? dcType.GetField("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
            if (fi != null)
            {
                object inst = fi.GetValue(null);
                if (inst is MonoBehaviour mb2)
                {
                    dialogueController = mb2;
                    return;
                }
            }
        }

        // fallback: find by name in scene
        foreach (var mb in FindObjectsOfType<MonoBehaviour>())
        {
            if (mb.GetType().Name == "DialogueController")
            {
                dialogueController = mb;
                return;
            }
        }
    }
}
