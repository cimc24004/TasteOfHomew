using Cysharp.Threading.Tasks;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 開閉できるドア（引き戸 = SideOpen / 開き戸 = Rotate）。
///
/// ・インタラクトされると開閉を切り替える
/// ・鍵（DoorKeyController）が付いていて施錠中なら、鍵を持っているときだけ「鍵を使う？」と確認して開ける
/// ・開閉時の音を StealthGameController に知らせる（ステルス中の見張りが聞きつける）
/// </summary>
public class DoorController : SerializedMonoBehaviour, IInteractable
{
    public enum DoorStatus { Closed, Opened }
    public enum MoveXYZ { X, Y, Z }
    public enum OpenType { SideOpen, Rotate }

    // ===== Inspector =====

    [Header("設定")]
    public OpenType open_type;
    public DoorStatus nowDoorStatus;

    [ShowIf("open_type", OpenType.SideOpen)][Required] public Transform ToOpenTransform;
    [ShowIf("open_type", OpenType.Rotate)] public Vector3 TargetRotateVector;

    public float Duration = 0.5f;
    public bool TriggerChangeModeOpenClose = true;

    public UnityEvent OpenStartEvent;
    public UnityEvent OpenCompleted;
    public UnityEvent CloseCompleted;

    public AudioClip OpenClip;
    public AudioClip CloseClip;
    public AudioClip CloseCompletedClip;

    [ShowIf("open_type", OpenType.SideOpen)] public MoveXYZ move_x_y_z;

    [Title("鍵")]
    public DoorKeyController keyController;
    public AudioClip LockedClip;
    public UnityEvent OnOpenBlockedByLock;

    [Title("音 (ステルス)")]
    [Tooltip("ドアを開けた時にステルスAIに通知する音の半径。0以下なら通知しない。")]
    public float openSoundRadius = 10f;

    [Tooltip("ドアを閉めた時にステルスAIに通知する音の半径。0以下なら通知しない。")]
    public float closeSoundRadius = 5f;

    [Title("鍵確認ダイアログ")]
    [Tooltip("DialogueSequenceGroup の groupKey")]
    public string useKeyGroupKey = "Door";
    [Tooltip("Group内の sequenceId")]
    public string useKeySequenceId = "UseKeyPrompt";
    [Tooltip("選択結果を返す一時フラグ（true=使う）")]
    public string useKeyFlagKey = "__door_use_key_tmp";

    // ===== 実行時の状態 =====

    private Vector3 closedLocalPosition;
    private Vector3 openLocalPosition;
    private Vector3 closedLocalEulerAngles;

    private AudioSource audioSource;
    private Collider doorCollider;

    /// <summary>開閉アニメーションが終わっているか（動いている最中は操作を受け付けない）。</summary>
    private bool animationCompleted = true;

    /// <summary>「鍵を使う？」の確認ダイアログを表示中か。</summary>
    private bool isPromptingKey;

    // ===== Unity ライフサイクル =====

    private void Start()
    {
        closedLocalPosition = transform.localPosition;
        closedLocalEulerAngles = transform.localEulerAngles;
        audioSource = GetComponent<AudioSource>();
        doorCollider = GetComponent<Collider>();

        if (open_type == OpenType.SideOpen && ToOpenTransform)
        {
            openLocalPosition = closedLocalPosition + GetSlideOffset();
        }
    }

    /// <summary>引き戸の移動量。ToOpenTransform までの差分のうち、指定された 1 軸の成分だけを使う。</summary>
    private Vector3 GetSlideOffset()
    {
        Vector3 offset = ToOpenTransform.localPosition - closedLocalPosition;
        switch (move_x_y_z)
        {
            case MoveXYZ.X: return new Vector3(offset.x, 0f, 0f);
            case MoveXYZ.Y: return new Vector3(0f, offset.y, 0f);
            case MoveXYZ.Z: return new Vector3(0f, 0f, offset.z);
            default:        return offset;
        }
    }

    // ===== IInteractable =====

    /// <summary>プレイヤーがインタラクトした扱いで開閉する（クロスヘア以外から呼ぶ場合）。</summary>
    public void Interact()
    {
        OpenCloseAutoSelectAsync(PlayerStatus.instance.gameObject).Forget();
    }

    /// <summary>誰が操作したか（interactor）を指定して開閉する。鍵の所持判定と、物音の発生源として使う。</summary>
    public void Interact(GameObject interactor)
    {
        OpenCloseAutoSelectAsync(interactor).Forget();
    }

    public string GetDisplayText()
    {
        return "";
    }

    public bool ShouldHighlight() => true;

    // ===== インタラクト時の流れ =====

    private async UniTaskVoid OpenCloseAutoSelectAsync(GameObject interactor)
    {
        if (isPromptingKey || !animationCompleted) return;

        if (keyController != null && keyController.IsLocked)
        {
            bool unlocked = await TryUnlockAsync(interactor);
            if (!unlocked) return;
        }

        if (nowDoorStatus == DoorStatus.Closed) Open(interactor);
        else Close(interactor);
    }

    /// <summary>
    /// 施錠中のドアを開けようとしたときの処理。解錠できたら true。
    /// 鍵を持っていれば「鍵を使う？」と確認し、「使う」を選んだときだけ解錠する。
    /// </summary>
    private async UniTask<bool> TryUnlockAsync(GameObject interactor)
    {
        if (!keyController.CanUseKey(interactor))
        {
            PlaySound(LockedClip);
            OnOpenBlockedByLock?.Invoke();
            keyController.LockedDialogueCallerOnNotHaveKey.Call();
            return false;
        }

        bool? useKey = await PromptUseKeyAsync();
        if (useKey != true)
        {
            // 「使わない」を選んだ、またはキャンセルされた
            PlaySound(LockedClip);
            OnOpenBlockedByLock?.Invoke();
            return false;
        }

        if (!keyController.UnlockWithHeldKey(interactor))
        {
            PlaySound(LockedClip);
            keyController.LockedDialogueCallerOnHaveKey.Call();
            OnOpenBlockedByLock?.Invoke();
            return false;
        }

        return true;
    }

    /// <summary>
    /// 「鍵を使う？」の選択肢ダイアログを表示する。
    /// 戻り値: true = 使う / false = 使わない / null = 表示できなかった or 中断された。
    /// 選択結果は、ダイアログ側が書き込む一時フラグ（useKeyFlagKey）経由で受け取る。
    /// </summary>
    private async UniTask<bool?> PromptUseKeyAsync()
    {
        if (DialogueController.instance == null)
        {
            Debug.LogWarning("[DoorController] DialogueController.instance が見つからないよ");
            return null;
        }

        if (isPromptingKey) return null;
        isPromptingKey = true;

        try
        {
            DialogueController.instance.SetFlag(useKeyFlagKey, false);
            await DialogueController.instance.StartDialogueAsync(useKeyGroupKey, useKeySequenceId);

            bool useKey = DialogueController.instance.GetFlag(useKeyFlagKey, false);
            DialogueController.instance.SetFlag(useKeyFlagKey, false);
            return useKey;
        }
        catch (System.OperationCanceledException)
        {
            return null;
        }
        finally
        {
            isPromptingKey = false;
        }
    }

    // ===== 開く =====

    [Button]
    public void Open() => Open(null);

    /// <summary>ドアを開ける。施錠中なら開かず、施錠音とイベントだけを出す。</summary>
    public void Open(GameObject interactor = null)
    {
        if (keyController != null && keyController.IsLocked)
        {
            PlaySound(LockedClip);
            OnOpenBlockedByLock?.Invoke();
            return;
        }

        animationCompleted = false;
        nowDoorStatus = DoorStatus.Opened;
        PlaySound(OpenClip);
        OpenStartEvent?.Invoke();

        if (openSoundRadius > 0f)
        {
            StealthGameController.MakeSoundAt(transform.position, openSoundRadius, interactor);
        }

        if (open_type == OpenType.SideOpen)
        {
            transform.DOLocalMove(openLocalPosition, Duration).SetEase(Ease.Linear).SetLink(gameObject)
                .OnComplete(() =>
                {
                    animationCompleted = true;
                    OpenCompleted?.Invoke();
                });
        }
        else if (open_type == OpenType.Rotate)
        {
            // 回転中はプレイヤーを押し出さないよう、コライダーをトリガーにしておく
            transform.DOLocalRotate(TargetRotateVector, Duration, RotateMode.Fast).SetEase(Ease.Linear).SetLink(gameObject)
                .OnUpdate(() =>
                {
                    if (doorCollider) doorCollider.isTrigger = true;
                })
                .OnComplete(() =>
                {
                    animationCompleted = true;
                    if (TriggerChangeModeOpenClose && doorCollider) doorCollider.isTrigger = false;
                    OpenCompleted?.Invoke();
                });
        }
    }

    [Button("即座に開ける (音・アニメなし)")]
    public void ForceOpen()
    {
        nowDoorStatus = DoorStatus.Opened;

        if (open_type == OpenType.SideOpen)
        {
            transform.localPosition = openLocalPosition;
        }
        else if (open_type == OpenType.Rotate)
        {
            transform.localEulerAngles = TargetRotateVector;
            if (TriggerChangeModeOpenClose && doorCollider) doorCollider.isTrigger = false;
        }

        animationCompleted = true;
        OpenCompleted?.Invoke();
    }

    // ===== 閉める =====

    [Button]
    public void Close() => Close(null);

    public void Close(GameObject interactor = null)
    {
        animationCompleted = false;
        nowDoorStatus = DoorStatus.Closed;
        PlaySound(CloseClip);

        if (closeSoundRadius > 0f)
        {
            StealthGameController.MakeSoundAt(transform.position, closeSoundRadius, interactor);
        }

        Tween closeTween = null;
        if (open_type == OpenType.SideOpen)
        {
            closeTween = transform.DOLocalMove(closedLocalPosition, Duration).SetEase(Ease.Linear).SetLink(gameObject);
        }
        else if (open_type == OpenType.Rotate)
        {
            closeTween = transform.DOLocalRotate(closedLocalEulerAngles, Duration, RotateMode.Fast).SetEase(Ease.Linear).SetLink(gameObject)
                .OnUpdate(() =>
                {
                    if (TriggerChangeModeOpenClose && doorCollider) doorCollider.isTrigger = true;
                });
        }

        if (closeTween != null)
        {
            closeTween.OnComplete(() =>
            {
                PlaySound(CloseCompletedClip);
                FinishClosing();
            });
        }
    }

    [Button("即座に閉める (音・アニメなし)")]
    public void ForceClose()
    {
        nowDoorStatus = DoorStatus.Closed;

        if (open_type == OpenType.SideOpen)
        {
            transform.localPosition = closedLocalPosition;
        }
        else if (open_type == OpenType.Rotate)
        {
            transform.localEulerAngles = closedLocalEulerAngles;
        }

        FinishClosing();
    }

    /// <summary>閉まりきったときの共通処理（鍵には「閉まった」ことを知らせる。自動で再施錠する設定のため）。</summary>
    private void FinishClosing()
    {
        animationCompleted = true;
        if (doorCollider) doorCollider.isTrigger = false;
        CloseCompleted?.Invoke();

        if (keyController != null) keyController.NotifyDoorClosed(this);
    }

    // ===== 小さなヘルパー =====

    private void PlaySound(AudioClip clip)
    {
        if (clip && audioSource) audioSource.PlayOneShot(clip);
    }
}
