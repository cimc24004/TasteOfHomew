// DoorController.cs

using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks;

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
    public DoorKeyController keyController;
    public AudioClip LockedClip;
    public UnityEvent OnOpenBlockedByLock;

    // ★ 音 (ステルス)
    [Title("音 (ステルス)")]
    [Tooltip("ドアを開けた時にステルスAIに通知する音の半径。0以下なら通知しない。")]
    public float openSoundRadius = 10f;

    [Tooltip("ドアを閉めた時にステルスAIに通知する音の半径。0以下なら通知しない。")]
    public float closeSoundRadius = 5f;

    // ★ 鍵確認ダイアログ
    [Title("鍵確認ダイアログ")]
    [Tooltip("DialogueSequenceGroup の groupKey")]
    public string useKeyGroupKey = "Door";
    [Tooltip("Group内の sequenceId")]
    public string useKeySequenceId = "UseKeyPrompt";
    [Tooltip("選択結果を返す一時フラグ（true=使う）")]
    public string useKeyFlagKey = "__door_use_key_tmp";

    private bool _busy;

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

    // ▼▼▼ 修正 ▼▼▼
    // ★ 1. IInteractable の実装 (引数なし・既存の定義)
    // (CrossHairController以外から呼ばれた場合)
    public void Interact()
    {

        OpenCloseAutoSelectAsync(PlayerStatus.instance.gameObject).Forget();
    }

    // ★ 2. CrossHairController が呼ぶための、引数ありの Interact
    public void Interact(GameObject interactor)
    {
        OpenCloseAutoSelectAsync(interactor).Forget();
    }
    // ▲▲▲ 修正 ▲▲▲

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
                    // 解錠して続行
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

        // ロックなし。開閉実行 (interactor を渡す)
        if (nowDoorStatus == DoorStatus.Closed) Open(interactor);
        else Close(interactor);
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
            DialogueController.instance.SetFlag(useKeyFlagKey, false);
            await DialogueController.instance.StartDialogueAsync(useKeyGroupKey, useKeySequenceId);
            bool use = DialogueController.instance.GetFlag(useKeyFlagKey, false);
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

    public string GetDisplayText()
    {
        return "";
    }

    public bool ShouldHighlight() => true;

    // ▼▼▼ 修正 (interactor を受け取れるように) ▼▼▼
    [Button]
    public void Open() => Open(null); // ボタン用
    public void Open(GameObject interactor = null)
    {
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

        // ★ 音イベントを「誰が」発生させたかを渡す
        if (openSoundRadius > 0f)
        {
            StealthGameController.MakeSoundAt(transform.position, openSoundRadius, interactor);
        }

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
    public void Close() => Close(null); // ボタン用
    public void Close(GameObject interactor = null)
    {
        OpenedClosedCompletedAnimation = false;
        nowDoorStatus = DoorStatus.Closed;
        if (CloseClip && audio_source) audio_source.PlayOneShot(CloseClip);

        // ★ 音イベントを「誰が」発生させたかを渡す
        if (closeSoundRadius > 0f)
        {
            StealthGameController.MakeSoundAt(transform.position, closeSoundRadius, interactor);
        }

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

                if (keyController != null) keyController.NotifyDoorClosed(this);
            });
        }
    }
    // ▲▲▲ 修正ここまで ▲▲▲

    [Button("即座に開ける (音・アニメなし)")]
    public void ForceOpen()
    {
        nowDoorStatus = DoorStatus.Opened;
        OpenedClosedCompletedAnimation = false; 

        if (open_type == OpenType.SideOpen)
        {
            transform.localPosition = OpenLocalPosition;
        }
        else if (open_type == OpenType.Rotate)
        {
            transform.localEulerAngles = TargetRotateVector;
            if (TriggerChangeModeOpenClose && collider) collider.isTrigger = false; 
        }

        OpenedClosedCompletedAnimation = true; 
        OpenCompleted?.Invoke(); 
    }

    [Button("即座に閉める (音・アニメなし)")]
    public void ForceClose()
    {
        nowDoorStatus = DoorStatus.Closed;
        OpenedClosedCompletedAnimation = false; 

        if (open_type == OpenType.SideOpen)
        {
            transform.localPosition = DefaultLocalPosition;
        }
        else if (open_type == OpenType.Rotate)
        {
            transform.localEulerAngles = DefaultRotateVector;
        }
 
        if (collider) collider.isTrigger = false; 
        OpenedClosedCompletedAnimation = true; 
        CloseCompleted?.Invoke(); 

        if (keyController != null) keyController.NotifyDoorClosed(this);
    }
}