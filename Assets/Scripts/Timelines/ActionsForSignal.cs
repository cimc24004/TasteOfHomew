using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; // 新Input System前提（使わないなら外してOK）

/// <summary>
/// TimelineのSignalから呼ぶ“おまとめアクション”
/// - プレイヤー操作を止める/戻す
/// - 指定したオブジェクトを一括で非表示/再表示
/// - 任意でカーソル制御
/// </summary>
public class ActionsForSignal : MonoBehaviour
{
    [Header("=== プレイヤー操作を止める対象 ===")]
    [SerializeField] private PlayerInput playerInput;            // 無ければ空でOK
    [SerializeField] private List<Behaviour> componentsToDisable = new(); // 例: 自作Mover, CinemachineInputProvider など

    [Header("=== タイムライン中に隠すオブジェクト ===")]
    [Tooltip("タイムライン再生中だけ非表示にしたいオブジェクトをここに突っ込む")]
    [SerializeField] private List<GameObject> hideTargets = new();

    [Header("=== カーソル制御（必要なら） ===")]
    [SerializeField] private bool manageCursor = true;
    [SerializeField] private CursorLockMode lockModeInGameplay = CursorLockMode.Locked;
    [SerializeField] private CursorLockMode lockModeInTimeline = CursorLockMode.None;
    [SerializeField] private bool cursorVisibleInGameplay = false;
    [SerializeField] private bool cursorVisibleInTimeline = true;

    // 内部退避
    private bool _statesCaptured = false;
    private bool _prevPlayerInputEnabled;
    private readonly List<bool> _prevCompEnabled = new();
    private readonly List<(CanvasGroup cg, float a, bool br, bool it)> _prevCanvasGroupStates = new();
    private readonly List<(GameObject go, bool active)> _prevActiveStates = new();

    private CursorLockMode _prevCursorLock;
    private bool _prevCursorVisible;

    private int _lockDepth = 0; // ネスト対応

    // ===== まとめ関数（Signalからこれを使うのが楽） =====

    // タイムライン開始
    public void LockForTimeline()
    {
        FreezePlayer(true);
        SetTargetsVisible(false);
        if (manageCursor) ApplyCursorForTimeline(true);
    }

    // タイムライン終了
    public void UnlockAfterTimeline()
    {
        if (manageCursor) ApplyCursorForTimeline(false);
        SetTargetsVisible(true);
        FreezePlayer(false);
    }

    // ===== 個別でも呼べる =====
    public void DisablePlayerControl() => FreezePlayer(true);
    public void EnablePlayerControl()  => FreezePlayer(false);
    public void HideTargets()          => SetTargetsVisible(false);
    public void ShowTargets()          => SetTargetsVisible(true);

    // ===== 実装 =====
    private void FreezePlayer(bool freeze)
    {
        if (freeze)
        {
            _lockDepth++;
            if (_lockDepth > 1) return; // 既にロック中

            if (!_statesCaptured)
            {
                _prevCompEnabled.Clear();
                if (playerInput) _prevPlayerInputEnabled = playerInput.enabled;

                foreach (var comp in componentsToDisable)
                    _prevCompEnabled.Add(comp ? comp.enabled : false);

                if (manageCursor)
                {
                    _prevCursorLock = Cursor.lockState;
                    _prevCursorVisible = Cursor.visible;
                }

                _statesCaptured = true;
            }

            if (playerInput) playerInput.enabled = false;
            for (int i = 0; i < componentsToDisable.Count; i++)
                if (componentsToDisable[i]) componentsToDisable[i].enabled = false;
        }
        else
        {
            if (_lockDepth == 0) return;
            _lockDepth--;
            if (_lockDepth > 0) return;

            if (_statesCaptured)
            {
                if (playerInput) playerInput.enabled = _prevPlayerInputEnabled;
                for (int i = 0; i < componentsToDisable.Count; i++)
                    if (componentsToDisable[i]) componentsToDisable[i].enabled = (i < _prevCompEnabled.Count) && _prevCompEnabled[i];

                _statesCaptured = false;
            }
        }
    }

    private void SetTargetsVisible(bool visible)
    {
        if (!visible)
        {
            // 初回非表示時に状態退避
            _prevCanvasGroupStates.Clear();
            _prevActiveStates.Clear();

            foreach (var go in hideTargets)
            {
                if (!go) continue;

                // CanvasGroupがあればαで隠す（UIのレイアウト崩したくない場合）
                var cg = go.GetComponent<CanvasGroup>();
                if (cg)
                {
                    _prevCanvasGroupStates.Add((cg, cg.alpha, cg.blocksRaycasts, cg.interactable));
                    cg.alpha = 0f;
                    cg.blocksRaycasts = false;
                    cg.interactable = false;
                }
                else
                {
                    // 無ければSetActiveで丸ごと隠す
                    _prevActiveStates.Add((go, go.activeSelf));
                    go.SetActive(false);
                }
            }
        }
        else
        {
            // 退避した状態に戻す
            foreach (var s in _prevCanvasGroupStates)
            {
                if (!s.cg) continue;
                s.cg.alpha = s.a;
                s.cg.blocksRaycasts = s.br;
                s.cg.interactable = s.it;
            }
            foreach (var s in _prevActiveStates)
            {
                if (!s.go) continue;
                s.go.SetActive(s.active);
            }
            _prevCanvasGroupStates.Clear();
            _prevActiveStates.Clear();
        }
    }

    private void ApplyCursorForTimeline(bool timelineMode)
    {
        if (timelineMode)
        {
            Cursor.lockState = lockModeInTimeline;
            Cursor.visible = cursorVisibleInTimeline;
        }
        else
        {
            // “通常時の望ましい状態”に戻す（元の値に戻したいなら_prevで戻す実装でもOK）
            Cursor.lockState = lockModeInGameplay;
            Cursor.visible = cursorVisibleInGameplay;
        }
    }
}
