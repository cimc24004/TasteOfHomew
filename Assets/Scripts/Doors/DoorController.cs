using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks; // ← 追加

public class DoorController : SerializedMonoBehaviour, IInteractable
{
    public enum DoorStatus { Closed, Opened }
    public enum MoveXYZ { X, Y, Z }
    public enum OpenType { SideOpen, Rotate }

    [Header("設定")]
    public OpenType open_type;
    public DoorStatus nowDoorStatus;

    [ShowIf("open_type", OpenType.SideOpen)][Required] public Transform ToOpenTransform;
    [ShowIf("open_type", OpenType.Rotate)] public Vector3 TargetRotateVector;

    private Vector3 DefaultLocalPosition;
    private Vector3 OpenLocalPosition;
    private Vector3 DefaultRotateVector;

    public float Duration = 0.5f;
    public bool TriggerChangeModeOpenClose = true;

    public UnityEvent OpenStartEvent;
    public UnityEvent OpenCompleted;
    public UnityEvent CloseCompleted;

    public AudioClip OpenClip;
    public AudioClip CloseClip;
    public AudioClip CloseCompletedClip;

    private AudioSource audio_source;
    private Collider collider;
    private bool OpenedClosedCompletedAnimation = true;

    [ShowIf("open_type", OpenType.SideOpen)] public MoveXYZ move_x_y_z;

    // ★ 鍵連携
    [Title("鍵")]
    public DoorKeyController keyController;          // ドアに同居させる DoorKeyController を参照
    public AudioClip LockedClip;                     // 施錠時に再生（任意）
    public UnityEvent OnOpenBlockedByLock;           // 施錠で開けられなかった時

    // ★ 鍵確認ダイアログ
    [Title("鍵確認ダイアログ")]
    [Tooltip("DialogueSequenceGroup の groupKey")]
    public string useKeyGroupKey = "Door";
    [Tooltip("Group内の sequenceId")]
    public string useKeySequenceId = "UseKeyPrompt";
    [Tooltip("選択結果を返す一時フラグ（true=使う）")]
    public string useKeyFlagKey = "__door_use_key_tmp";

    private bool _busy; // 連打/多重起動防止

    void Start()
    {
        DefaultLocalPosition = transform.localPosition;
        DefaultRotateVector = transform.localEulerAngles;
        audio_source = GetComponent<AudioSource>();
        collider = GetComponent<Collider>();

        if (open_type == OpenType.SideOpen && ToOpenTransform)
        {
            Vector3 localOffset = ToOpenTransform.localPosition - DefaultLocalPosition;
            switch (move_x_y_z)
            {
                case MoveXYZ.X: localOffset.y = localOffset.z = 0; break;
                case MoveXYZ.Y: localOffset.x = localOffset.z = 0; break;
                case MoveXYZ.Z: localOffset.x = localOffset.y = 0; break;
            }
            OpenLocalPosition = DefaultLocalPosition + localOffset;
        }
    }

    // IInteractable 実装
    public void Interact() => OpenCloseAutoSelect();

    // 既存API互換：同期メソッドだが中で非同期を叩く
    public void OpenCloseAutoSelect()
    {
        OpenCloseAutoSelectAsync(null).Forget();
    }

    /// <summary>
    /// 施錠中 & 鍵所持なら「鍵を使う？」ダイアログを出して選ばせる。
    /// </summary>
    private async UniTaskVoid OpenCloseAutoSelectAsync(GameObject interactor)
    {
        if (_busy || !OpenedClosedCompletedAnimation) return;

        // 施錠中？
        if (keyController != null && keyController.IsLocked)
        {
            // 鍵を持ってる？
            if (keyController.CanUseKey(interactor))
            {
                // 「鍵を使う？」ダイアログ
                bool? use = await PromptUseKeyAsync();
                if (use == true)
                {
                    // 解錠して続行（万一失敗したらブロック）
                    if (!keyController.UnlockWithHeldKey(interactor))
                    {
                        if (LockedClip && audio_source) audio_source.PlayOneShot(LockedClip);
                        keyController.LockedDialogueCallerOnHaveKey.Call();
                        OnOpenBlockedByLock?.Invoke();
                        return;
                    }
                }
                else
                {
                    // 使わない or キャンセル
                    if (LockedClip && audio_source) audio_source.PlayOneShot(LockedClip);
                    OnOpenBlockedByLock?.Invoke();
                    return;
                }
            }
            else
            {
                // 鍵を持っていない
                if (LockedClip && audio_source) audio_source.PlayOneShot(LockedClip);
                OnOpenBlockedByLock?.Invoke();
                keyController.LockedDialogueCallerOnNotHaveKey.Call();
                return;
            }
        }

        // ここまで来たらロックなし。開閉実行
        if (nowDoorStatus == DoorStatus.Closed) Open();
        else Close();
    }

    /// <summary>
    /// 「鍵を使う/使わない」のダイアログを流して、true/false/null を返す。
    /// </summary>
    private async UniTask<bool?> PromptUseKeyAsync()
    {
        if (DialogueController.instance == null)
        {
            Debug.LogWarning("[DoorController] DialogueController.instance が見つからないよ");
            return null;
        }

        if (_busy) return null;
        _busy = true;

        try
        {
            // 一時フラグ初期化
            DialogueController.instance.SetFlag(useKeyFlagKey, false);

            // ダイアログ実行（完了まで待つ）
            await DialogueController.instance.StartDialogueAsync(useKeyGroupKey, useKeySequenceId);

            // 結果取得
            bool use = DialogueController.instance.GetFlag(useKeyFlagKey, false);

            // 任意でクリア
            DialogueController.instance.SetFlag(useKeyFlagKey, false);

            return use;
        }
        catch (System.OperationCanceledException)
        {
            return null;
        }
        finally
        {
            _busy = false;
        }
    }

    // ★ 表示テキスト：施錠中は「鍵がかかっている……。」などに差し替えたい場合
    public string GetDisplayText()
    {
        // if (keyController != null && keyController.IsLocked)
        // {
        //     return "";
        // }
        return "";
    }

    public bool ShouldHighlight() => true;

    [Button]
    public void Open()
    {
        // 直呼び対策：Open() が外部から直で叩かれてもロックを尊重
        if (keyController != null && keyController.IsLocked)
        {
            if (LockedClip && audio_source) audio_source.PlayOneShot(LockedClip);
            OnOpenBlockedByLock?.Invoke();
            return;
        }

        OpenedClosedCompletedAnimation = false;
        nowDoorStatus = DoorStatus.Opened;
        if (OpenClip && audio_source) audio_source.PlayOneShot(OpenClip);
        OpenStartEvent?.Invoke();

        if (open_type == OpenType.SideOpen)
        {
            transform.DOLocalMove(OpenLocalPosition, Duration).SetEase(Ease.Linear).SetLink(gameObject)
                .OnComplete(() =>
                {
                    OpenedClosedCompletedAnimation = true;
                    OpenCompleted?.Invoke();
                });
        }
        else if (open_type == OpenType.Rotate)
        {
            transform.DOLocalRotate(TargetRotateVector, Duration, RotateMode.Fast).SetEase(Ease.Linear).SetLink(gameObject)
                .OnUpdate(() => { if (collider) collider.isTrigger = true; })
                .OnComplete(() =>
                {
                    OpenedClosedCompletedAnimation = true;
                    if (TriggerChangeModeOpenClose && collider) collider.isTrigger = false;
                    OpenCompleted?.Invoke();
                });
        }
    }

    [Button]
    public void Close()
    {
        OpenedClosedCompletedAnimation = false;
        nowDoorStatus = DoorStatus.Closed;
        if (CloseClip && audio_source) audio_source.PlayOneShot(CloseClip);

        Tween onDoneTween = null;

        if (open_type == OpenType.SideOpen)
        {
            onDoneTween = transform.DOLocalMove(DefaultLocalPosition, Duration).SetEase(Ease.Linear).SetLink(gameObject);
        }
        else if (open_type == OpenType.Rotate)
        {
            onDoneTween = transform.DOLocalRotate(DefaultRotateVector, Duration, RotateMode.Fast).SetEase(Ease.Linear).SetLink(gameObject)
                .OnUpdate(() => { if (TriggerChangeModeOpenClose && collider) collider.isTrigger = true; });
        }

        if (onDoneTween != null)
        {
            onDoneTween.OnComplete(() =>
            {
                if (CloseCompletedClip && audio_source) audio_source.PlayOneShot(CloseCompletedClip);
                OpenedClosedCompletedAnimation = true;
                if (collider) collider.isTrigger = false;
                CloseCompleted?.Invoke();

                // 閉じ切り後：自動再ロック通知
                if (keyController != null) keyController.NotifyDoorClosed(this);
            });
        }
    }

    [Button("即座に開ける (音・アニメなし)")]
    public void ForceOpen()
    {
        // ロック状態を無視して強制的に開きます
        nowDoorStatus = DoorStatus.Opened;
        OpenedClosedCompletedAnimation = false; // 念のため処理中はフラグを更新

        if (open_type == OpenType.SideOpen)
        {
            transform.localPosition = OpenLocalPosition;
        }
        else if (open_type == OpenType.Rotate)
        {
            transform.localEulerAngles = TargetRotateVector;
            // 開いた状態でのコライダー設定を反映
            if (TriggerChangeModeOpenClose && collider) collider.isTrigger = false; 
        }

        OpenedClosedCompletedAnimation = true; // 処理完了
        OpenCompleted?.Invoke(); // イベントを呼び出す
    }

    [Button("即座に閉める (音・アニメなし)")]
    public void ForceClose()
    {
        nowDoorStatus = DoorStatus.Closed;
        OpenedClosedCompletedAnimation = false; // 念のため処理中はフラグを更新

        if (open_type == OpenType.SideOpen)
        {
            transform.localPosition = DefaultLocalPosition;
        }
        else if (open_type == OpenType.Rotate)
        {
            transform.localEulerAngles = DefaultRotateVector;
        }

        // 閉じた状態ではコライダーを通常状態に戻す
        if (collider) collider.isTrigger = false; 
        OpenedClosedCompletedAnimation = true; // 処理完了
        CloseCompleted?.Invoke(); // イベントを呼び出す

        // 自動再ロックの通知
        if (keyController != null) keyController.NotifyDoorClosed(this);
    }
}
