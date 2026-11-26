using System;
// using System.Reflection; // リフレクションは不要になりました
using UnityEngine;
using UnityEngine.Events;

// DialogueController が Singleton<DialogueController> を継承していることを前提とします。
// DialogueController.Instance にアクセスするために、DialogueController.cs が必要です。

public class EscapeController : MonoBehaviour
{
    [Header("DialogueController（任意：空でもOK）")]
    // 型を MonoBehaviour から DialogueController に変更
    [SerializeField] private DialogueController dialogueController;

    [Header("フラグキー")]
    [SerializeField] private string friendHouseKey = "EscapeFriendHouse";
    [SerializeField] private string myHouseKey = "EscapeMyHouse";
    [SerializeField] private string myMotherKilledKey = "EscapeMyHouseMotherKilled";

    [Header("イベント（Start / End を外部からアサイン）")]
    public UnityEvent onFriendStart;
    public UnityEvent onFriendEnd;

    public UnityEvent onMyStart;
    public UnityEvent onMyEnd;

    private DataShareController dataShareController;

    private void Awake()
    {
        // 取得ロジックをシンプル化
        // Inspectorでセットされていなければ、SingletonのInstanceから取得する
        if (dialogueController == null)
        {
            dialogueController = DialogueController.instance;

            // もしAwake時点でInstanceがなければ警告を出す (Startや実行時にも再試行するためエラーにはしない)
            if (dialogueController == null)
            {
                Debug.LogWarning("[EscapeController] Awake: DialogueController.Instance がまだ利用できません。");
            }
        }
    }

    void Start()
    {
        // (元のコードから変更なし)
        dataShareController = DataShareController.instance;

        // Start時点でもう一度チェック (シーンロード順の問題対策)
        if (dialogueController == null)
        {
            dialogueController = DialogueController.instance;
        }
    }

    // --- Start 系（既存） ---

    // (元のコードから変更なし)
    public void OnFriendSelected()
    {
        Debug.Log("[EscapeController] Friend selected");
        onFriendStart?.Invoke();
    }

    // (元のコードから変更なし)
    public void OnFriendEnded()
    {
        Debug.Log("[EscapeController] Friend end");
        onFriendEnd?.Invoke();
        dataShareController.SetFlagToTrue("EscapeFriendHouseEnd");
    }

    // (元のコードから変更なし)
    public void OnMySelected()
    {
        Debug.Log("[EscapeController] My selected");
        onMyStart?.Invoke();
    }

    // (元のコードから変更なし)
    public void OnMyEnded()
    {
        dataShareController.SetFlagToTrue("EscapeMyHouseEnd");
        Debug.Log("[EscapeController] My end");
        onMyEnd?.Invoke();
    }

    // (元のコードから変更なし)
    public void OnMyMotherKilledEnded()
    {
        dataShareController.SetFlagToTrue(myMotherKilledKey);
        Debug.Log("[EscapeController] My Mother Killed end");
        onMyEnd?.Invoke(); // 別イベントが欲しければ onMyMotherKilledEnd を新設して差し替え可
    }

    // ---------- ここからフラグ駆動（大幅に修正） ----------

    /// <summary>
    /// DialogueController の flags から判断して End を実行。
    /// 優先度：Friend > MyMotherKilled > My
    /// </summary>
    public void ExecuteEndFromFlags()
    {
        // 実行時に参照がなければ、SingletonのInstanceから取得を試みる
        if (dialogueController == null)
        {
            dialogueController = DialogueController.instance;
            if (dialogueController == null)
            {
                // ここで処理が実行される＝DialogueControllerがシーンに存在しないか、Instanceがnull
                Debug.LogError("[EscapeController] ExecuteEndFromFlags: DialogueController.Instance が見つかりません！処理を中断します。");
                return;
            }
        }

        // リフレクションを廃止し、公開メソッド GetFlag を直接呼び出す
        bool friendVal = dialogueController.GetFlag(friendHouseKey);
        bool myVal = dialogueController.GetFlag(myHouseKey);
        bool mkVal = dialogueController.GetFlag(myMotherKilledKey);

        // ログを見やすく変更
        Debug.Log($"[EscapeController] ExecuteEndFromFlags: friend({friendVal}) / motherKill({mkVal}) / my({myVal})");

        // 優先度：Friend > MotherKilled > My
        // (元のコードから変更なし)
        if (friendVal) { OnFriendEnded(); return; }
        if (mkVal) { OnMyMotherKilledEnded(); return; }
        if (myVal) { OnMyEnded(); return; }

        Debug.LogWarning("[EscapeController] No matching escape flag was true. ExecuteEndFromFlags did nothing.");
    }

    /// <summary>
    /// flags をチェックして該当する Start を呼ぶ。
    /// 優先度：Friend > MyMotherKilled > My（Start は MotherKilled でも My 側で開始する想定）
    /// </summary>
    public void ExecuteStartFromFlags()
    {
        // 実行時に参照がなければ、SingletonのInstanceから取得を試みる
        if (dialogueController == null)
        {
            dialogueController = DialogueController.instance;
            if (dialogueController == null)
            {
                Debug.LogError("[EscapeController] ExecuteStartFromFlags: DialogueController.Instance が見つかりません！処理を中断します。");
                return;
            }
        }

        // リフレクションを廃止し、公開メソッド GetFlag を直接呼び出す
        bool friendVal = dialogueController.GetFlag(friendHouseKey);
        bool myVal = dialogueController.GetFlag(myHouseKey);
        bool mkVal = dialogueController.GetFlag(myMotherKilledKey);

        // ログを見やすく変更
        Debug.Log($"[EscapeController] ExecuteStartFromFlags: friend({friendVal}) / motherKill({mkVal}) / my({myVal})");

        // (元のコードから変更なし)
        if (friendVal) { OnFriendSelected(); return; }

        // 母殺しルートは基本「自宅ルートの派生」なので Start は My 側に寄せる
        // (元のコードから変更なし)
        if (mkVal || myVal) { OnMySelected(); return; }

        Debug.LogWarning("[EscapeController] ExecuteStartFromFlags: no true flag found, nothing executed.");
    }

    // ---------- フラグ取得（既存の柔軟ロジック） ----------

    // 以下のメソッドはすべて不要になったため削除しました
    // private bool TryGetFlagValue(string key, out bool value) { ... }
    // private bool TryExtractFromFlagsContainer(object container, string key, out bool value) { ... }
    // private void TryFindDialogueControllerSingleton() { ... }
}
