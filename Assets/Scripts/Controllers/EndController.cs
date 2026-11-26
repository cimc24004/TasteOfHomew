using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Playables;
using UnityEngine.UI;

/// <summary>
/// 「このフラグが立っていたら、このエンディング」という対応 1 件分。
/// </summary>
[System.Serializable]
public class KeyUnityEvent
{
    [Tooltip("チェックするキー名")]
    public string key;
    [Tooltip("エンディング名")]
    public string EndingName = "Ending1";
    [Tooltip("キーが true のときに呼ばれるイベント")]
    public UnityEvent onTrue;

    [Tooltip("一度実行したら以降は実行しない (必要なければオフに)")]
    public bool invokeOnlyOnce = false;

    [Tooltip("優先度（大きいほど優先）。同点はリスト順で早い方が勝ち")]
    public int priority = 0;

    [HideInInspector] public bool invoked = false;
}

/// <summary>
/// エンディングシーンの進行役。
///
/// ・DataShareController のフラグを見て、どのエンディングを再生するかを決める
/// ・エンディング名の表示と、Steam 実績の解除
/// ・タイムラインが一時停止している間、プレイヤーのクリックで再開させる
/// </summary>
public class EndController : MonoBehaviour
{
    public enum ResolvePolicy
    {
        HighestPriorityOnly, // 最も高い priority の1件だけ実行（同点はリスト順）
        InvokeAllTrue        // true のものは全部実行
    }

    // ===== Inspector =====

    [SerializeField] private Button ClickedJudgeButton;
    [SerializeField] private bool WaitForPlayerInputClick;
    [SerializeField] private TextMeshProUGUI EndingNameTextUI;
    [SerializeField] private ResolvePolicy resolvePolicy = ResolvePolicy.HighestPriorityOnly;

    [SerializeField] private List<KeyUnityEvent> keyEvents = new List<KeyUnityEvent>();

    [SerializeField] private TimelinePlayback EndingTimelinePlayback;
    [SerializeField] private PlayerInput playerInput;

    // ===== 実行時の状態 =====

    private PauseMenuController pauseMenuController;
    private DataShareController dataShareController;

    // ===== Unity ライフサイクル =====

    private async void Start()
    {
        // エンディング中はポーズメニューを開けないようにする
        pauseMenuController = PauseMenuController.instance;
        pauseMenuController.PauseMenuOpenPermit = false;

        // 他のコントローラーの初期化を少し待ってから、エンディングを決める
        await UniTask.WaitForSeconds(0.1f);
        CheckKeys();

        playerInput.actions.FindAction("Click").performed += OnClickPerformed;
        playerInput.actions.FindAction("Submit").performed += OnClickPerformed;
    }

    private void OnDestroy()
    {
        // シーン移動時は playerInput が先に破棄されていることがある
        if (playerInput == null) return;

        var clickAction = playerInput.actions?.FindAction("Click");
        var submitAction = playerInput.actions?.FindAction("Submit");
        if (clickAction != null) clickAction.performed -= OnClickPerformed;
        if (submitAction != null) submitAction.performed -= OnClickPerformed;
    }

    // ===== 公開 API: エンディングの決定 =====

    /// <summary>
    /// フラグが true のエンディングを探して、そのイベントを実行する。
    /// resolvePolicy に応じて、優先度が最も高い 1 件だけ、または該当するものすべてを実行する。
    /// </summary>
    [ContextMenu("Check Keys (Run)")]
    public void CheckKeys()
    {
        if (dataShareController == null)
        {
            dataShareController = DataShareController.instance;
            if (dataShareController == null)
            {
                Debug.LogWarning("[EndController] DataShareController.instance が見つかりません。");
                return;
            }
        }

        var candidates = CollectTrueEvents(skipAlreadyInvoked: true);
        if (candidates.Count == 0) return;

        if (resolvePolicy == ResolvePolicy.InvokeAllTrue)
        {
            foreach (var keyEvent in candidates)
            {
                InvokeEvent(keyEvent);
            }
        }
        else
        {
            var winner = SelectHighestPriority(candidates);
            if (winner != null)
            {
                InvokeEvent(winner);
            }
        }
    }

    /// <summary>成立したエンディングの名前を画面に表示し、対応する Steam 実績を解除する。</summary>
    public void EndingNameSet()
    {
        EventSystem.current.SetSelectedGameObject(null);

        // 名前を表示するだけなので、CheckKeys と違って「実行済みかどうか」は見ない
        var candidates = CollectTrueEvents(skipAlreadyInvoked: false);
        if (candidates.Count == 0)
        {
            Debug.LogWarning("[EndController] EndingNameSet: 表示すべき True のエンディングフラグが見つかりません。");
            return;
        }

        var winner = SelectHighestPriority(candidates);
        if (winner != null)
        {
            if (EndingNameTextUI != null)
            {
                EndingNameTextUI.SetText(winner.EndingName);
            }
            else
            {
                Debug.LogWarning("[EndController] EndingNameTextUI がアタッチされていません。");
            }
        }

        if (SteamManager.Initialized)
        {
            UnlockSteamAchievement(GetAchievementKey(winner.EndingName));
        }
    }

    // ===== 公開 API: クリック待ち =====

    /// <summary>タイムラインを止めて、プレイヤーのクリックを待つ状態にする（タイムラインのシグナルから呼ばれる）。</summary>
    public void WaitForPlayerClick()
    {
        EventSystem.current.SetSelectedGameObject(ClickedJudgeButton.gameObject);
        WaitForPlayerInputClick = true;
    }

    /// <summary>クリック待ちの状態でクリックされたら、タイムラインを再開する。</summary>
    public void OnClickClickedJudgeButton()
    {
        if (WaitForPlayerInputClick && !EndingTimelinePlayback.IsPlayingFromSignal)
        {
            EndingTimelinePlayback.Resume();
            WaitForPlayerInputClick = false;
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void OnClickPerformed(InputAction.CallbackContext context)
    {
        OnClickClickedJudgeButton();
    }

    // ===== エンディング判定の内部処理 =====

    /// <summary>フラグが true になっているエンディングを、Inspector の並び順のまま集める。</summary>
    private List<KeyUnityEvent> CollectTrueEvents(bool skipAlreadyInvoked)
    {
        var result = new List<KeyUnityEvent>();
        foreach (var keyEvent in keyEvents)
        {
            if (string.IsNullOrEmpty(keyEvent.key)) continue;
            if (!dataShareController.GetFlag(keyEvent.key)) continue;
            if (skipAlreadyInvoked && keyEvent.invokeOnlyOnce && keyEvent.invoked) continue;

            result.Add(keyEvent);
        }
        return result;
    }

    /// <summary>priority が最も高いものを返す。同点なら先に並んでいる方。</summary>
    private static KeyUnityEvent SelectHighestPriority(List<KeyUnityEvent> candidates)
    {
        KeyUnityEvent winner = null;
        int bestPriority = int.MinValue;

        foreach (var candidate in candidates)
        {
            if (candidate.priority > bestPriority)
            {
                bestPriority = candidate.priority;
                winner = candidate;
            }
        }
        return winner;
    }

    private static void InvokeEvent(KeyUnityEvent keyEvent)
    {
        keyEvent.onTrue?.Invoke();
        keyEvent.invoked = true;
    }

    // ===== Steam 実績 =====

    /// <summary>エンディング名に含まれる番号から、Steam 実績の API 名を決める。</summary>
    private static string GetAchievementKey(string endingName)
    {
        if (endingName.Contains("3")) return "ENDING3";
        if (endingName.Contains("2")) return "ENDING2";
        return "ENDING1";
    }

    private static void UnlockSteamAchievement(string achievementKey)
    {
        SteamUserStats.GetAchievement(achievementKey, out bool isUnlocked);
        if (!isUnlocked)
        {
            SteamUserStats.SetAchievement(achievementKey);
        }
        SteamUserStats.StoreStats();
    }
}
