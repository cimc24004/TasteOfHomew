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
    [SerializeField] private string myMotherKilledKey = "EscapeMyHouseMotherKilled"; // ← これを“使う”ようにした

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

    // --- Start 系（既存） ---
    public void OnFriendSelected()
    {
        Debug.Log("[EscapeController] Friend selected");
        onFriendStart?.Invoke();
    }

    public void OnFriendEnded()
    {
        Debug.Log("[EscapeController] Friend end");
        onFriendEnd?.Invoke();
        dataShareController.SetFlagToTrue("EscapeFriendHouseEnd");
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

    // ★ 追加：母殺しエンド（専用フラグを立てて、基本は onMyEnd を流用）
    public void OnMyMotherKilledEnded()
    {
        dataShareController.SetFlagToTrue(myMotherKilledKey);
        Debug.Log("[EscapeController] My Mother Killed end");
        onMyEnd?.Invoke(); // 別イベントが欲しければ onMyMotherKilledEnd を新設して差し替え可
    }

    // ---------- ここからフラグ駆動 ----------
    /// <summary>
    /// DialogueController の flags から判断して End を実行。
    /// 優先度：Friend > MyMotherKilled > My
    /// </summary>
    public void ExecuteEndFromFlags()
    {
        if (dialogueController == null) TryFindDialogueControllerSingleton();

        bool friendVal, myVal, mkVal;
        bool friendFound = TryGetFlagValue(friendHouseKey, out friendVal);
        bool myFound     = TryGetFlagValue(myHouseKey, out myVal);
        bool mkFound     = TryGetFlagValue(myMotherKilledKey, out mkVal);

        Debug.Log($"[EscapeController] ExecuteEndFromFlags: friend({friendFound}:{friendVal}) / motherKill({mkFound}:{mkVal}) / my({myFound}:{myVal})");

        // 優先度：Friend > MotherKilled > My
        if (friendFound && friendVal) { OnFriendEnded(); return; }
        if (mkFound && mkVal)         { OnMyMotherKilledEnded(); return; }
        if (myFound && myVal)         { OnMyEnded(); return; }

        Debug.LogWarning("[EscapeController] No matching escape flag was true. ExecuteEndFromFlags did nothing.");
    }

    /// <summary>
    /// flags をチェックして該当する Start を呼ぶ。
    /// 優先度：Friend > MyMotherKilled > My（Start は MotherKilled でも My 側で開始する想定）
    /// </summary>
    public void ExecuteStartFromFlags()
    {
        if (dialogueController == null) TryFindDialogueControllerSingleton();

        bool friendVal, myVal, mkVal;
        bool friendFound = TryGetFlagValue(friendHouseKey, out friendVal);
        bool myFound     = TryGetFlagValue(myHouseKey, out myVal);
        bool mkFound     = TryGetFlagValue(myMotherKilledKey, out mkVal);

        Debug.Log($"[EscapeController] ExecuteStartFromFlags: friend({friendFound}:{friendVal}) / motherKill({mkFound}:{mkVal}) / my({myFound}:{myVal})");

        if (friendFound && friendVal) { OnFriendSelected(); return; }

        // 母殺しルートは基本「自宅ルートの派生」なので Start は My 側に寄せる
        if ((mkFound && mkVal) || (myFound && myVal)) { OnMySelected(); return; }

        Debug.LogWarning("[EscapeController] ExecuteStartFromFlags: no true flag found, nothing executed.");
    }

    // ---------- フラグ取得（既存の柔軟ロジック） ----------
    private bool TryGetFlagValue(string key, out bool value)
    {
        value = false;
        if (string.IsNullOrEmpty(key)) return false;

        if (dialogueController != null)
        {
            Type t = dialogueController.GetType();

            string[] methodNames = { "HasFlag", "Has", "HasGameFlag", "GetFlag", "GetBool", "IsFlagSet" };
            foreach (var mn in methodNames)
            {
                MethodInfo mi = t.GetMethod(mn, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (mi != null && mi.ReturnType == typeof(bool))
                {
                    try
                    {
                        object ret = mi.Invoke(dialogueController, new object[] { key });
                        if (ret is bool b) { value = b; return true; }
                    }
                    catch { }
                }
            }

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
                    if (ret is bool b) { value = b; return true; }
                }
                catch { }
            }
        }

        return false;
    }

    private bool TryExtractFromFlagsContainer(object container, string key, out bool value)
    {
        value = false;
        if (container == null) return false;

        if (container is System.Collections.IDictionary dict)
        {
            try
            {
                if (dict.Contains(key))
                {
                    object v = dict[key];
                    if (v is bool b) { value = b; return true; }
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
                    if (ret is bool b) { value = b; return true; }
                }
                catch { }
            }
        }

        try
        {
            PropertyInfo indexer = ct.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance);
            if (indexer != null)
            {
                object ret = indexer.GetValue(container, new object[] { key });
                if (ret is bool b) { value = b; return true; }
            }
        }
        catch { }

        return false;
    }

    // シーンから DialogueController を探す簡易ヘルパ
    private void TryFindDialogueControllerSingleton()
    {
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
