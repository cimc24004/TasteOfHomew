using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;

[DisallowMultipleComponent]
public class DoorKeyController : SerializedMonoBehaviour
{
    public enum LockMode { Manual, RequireKeyId }

    [Title("基本設定")]
    public LockMode lockMode = LockMode.RequireKeyId;

    [LabelText("初期状態は施錠")] public bool initiallyLocked = true;

    [LabelText("閉じたら自動で再ロック")] 
    public bool relockOnClose = false;

    [ShowIf(nameof(relockOnClose))]
    [LabelText("全ての連結ドアが閉じたら再ロック")]
    [InfoBox("linkedDoors に登録がある場合のみ有効。未設定なら単一ドアとして扱う。")]
    public bool relockRequireAllLinkedDoorsClosed = true;

    [ShowIf(nameof(lockMode), LockMode.RequireKeyId)]
    [Title("鍵ID設定")]
    [LabelText("必要な鍵ID")] public string requiredKeyId = "Key_A";
    [ShowIf(nameof(lockMode), LockMode.RequireKeyId)]
    [LabelText("開錠時に鍵を消費する")] public bool consumeKeyOnUnlock = false;

    [Title("UI/サウンド")]
    [LabelText("施錠表示テキスト")] public string lockedDisplayText = "鍵がかかっている";
    public AudioClip lockedSfx;
    public AudioClip unlockSfx;
    public AudioClip lockSfx;

    [Title("イベント")]
    public UnityEvent OnLocked;
    public UnityEvent OnUnlocked;
    public UnityEvent OnUnlockFailed;

    [Title("連結ドア（任意・複数可）")]
    [InfoBox("同じ鍵で管理したい DoorController を登録しておくと、全閉判定などに使える。")]
    public List<DoorController> linkedDoors = new();

    [SerializeField] private bool _locked;
    [SerializeField] public DialogueCaller LockedDialogueCallerOnNotHaveKey;
    [SerializeField] public DialogueCaller LockedDialogueCallerOnHaveKey;

    private AudioSource _audio;

    public bool IsLocked => _locked;

    void Awake()
    {
        _locked = initiallyLocked;
        _audio = GetComponent<AudioSource>();
    }

    /// <summary>
    /// これまで通り：即判定して、鍵があれば（必要なら消費して）解錠までやる。
    /// </summary>
    public bool TryAuthorizeOpen(GameObject interactor = null)
    {
        if (!_locked) return true;

        if (lockMode == LockMode.Manual)
        {
            Play(lockedSfx); OnUnlockFailed?.Invoke(); 
            return false;
        }

        if (string.IsNullOrEmpty(requiredKeyId))
        {
            Play(lockedSfx); OnUnlockFailed?.Invoke();
            return false;
        }

        var ring = ResolveKeyring(interactor);
        if (ring == null)
        {
            Play(lockedSfx); OnUnlockFailed?.Invoke();
            return false;
        }

        if (ring.HasKey(requiredKeyId))
        {
            if (consumeKeyOnUnlock) ring.ConsumeKey(requiredKeyId);
            _locked = false;
            Play(unlockSfx); OnUnlocked?.Invoke();
            return true;
        }

        Play(lockedSfx); OnUnlockFailed?.Invoke();
        return false;
    }

    /// <summary>
    /// ドアが閉じた通知（引数あり版）：どのドアが閉じたかを受け取る。
    /// </summary>
    public void NotifyDoorClosed(DoorController who)
    {
        if (!relockOnClose) return;

        if (relockRequireAllLinkedDoorsClosed && linkedDoors != null && linkedDoors.Count > 0)
        {
            // 連結ドアが全部 Closed ならロック
            if (AreAllLinkedDoorsClosed())
            {
                _locked = true;
                Play(lockSfx);
                OnLocked?.Invoke();
            }
        }
        else
        {
            // 単一運用 or 先着再ロック方式
            _locked = true;
            Play(lockSfx);
            OnLocked?.Invoke();
        }
    }

    /// <summary>
    /// 互換用：旧API。引数なしでも従来通り動く。
    /// </summary>
    public void NotifyDoorClosed()
    {
        NotifyDoorClosed(null);
    }

    [Button("施錠")]
    public void Lock()
    {
        _locked = true;
        Play(lockSfx);
        OnLocked?.Invoke();
    }

    [Button("解錠")]
    public void Unlock()
    {
        _locked = false;
        Play(unlockSfx);
        OnUnlocked?.Invoke();
    }

    private bool AreAllLinkedDoorsClosed()
    {
        if (linkedDoors == null || linkedDoors.Count == 0) return true;
        foreach (var d in linkedDoors)
        {
            if (!d) continue; // nullは無視（壊れてても全閉判定を阻害しない）
            if (d.nowDoorStatus != DoorController.DoorStatus.Closed) return false;
        }
        return true;
    }

    private void Play(AudioClip clip)
    {
        if (clip && _audio) _audio.PlayOneShot(clip);
    }

    private IKeyring ResolveKeyring(GameObject interactor)
    {
        if (interactor)
        {
            var ring = interactor.GetComponentInParent<IKeyring>();
            if (ring != null) return ring;
        }
        if (KeyringService.Current != null) return KeyringService.Current;
        return null;
    }

    // ==== ここから追記分 ====

    /// <summary>
    /// 施錠中かつ、必要鍵を「所持しているか」だけを判定（解錠はしない）。
    /// </summary>
    public bool CanUseKey(GameObject interactor = null)
    {
        if (!_locked) return false;
        if (lockMode != LockMode.RequireKeyId) return false;
        if (string.IsNullOrEmpty(requiredKeyId)) return false;

        var ring = ResolveKeyring(interactor);
        return ring != null && ring.HasKey(requiredKeyId);
    }

    /// <summary>
    /// 所持している鍵を使って解錠（必要なら消費）。成功で true。
    /// </summary>
    public bool UnlockWithHeldKey(GameObject interactor = null)
    {
        if (!_locked) return true; // すでに解錠済み
        if (lockMode == LockMode.Manual) return false;
        if (string.IsNullOrEmpty(requiredKeyId)) return false;

        var ring = ResolveKeyring(interactor);
        if (ring == null || !ring.HasKey(requiredKeyId)) return false;

        if (consumeKeyOnUnlock) ring.ConsumeKey(requiredKeyId);
        _locked = false;
        Play(unlockSfx);
        OnUnlocked?.Invoke();
        return true;
    }
}

// ==== 既出の Keyring まわり（無改変） ====
public interface IKeyring
{
    bool HasKey(string keyId);
    bool ConsumeKey(string keyId);
}

public static class KeyringService
{
    public static IKeyring Current;
}

