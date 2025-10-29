using UnityEngine;
using Sirenix.OdinInspector;

public class KeyPickup : SerializedMonoBehaviour, IInteractable
{
    [LabelText("この鍵のID")]
    public string keyId = "Key_A";

    [Tooltip("拾ったときに破棄するかどうか")]
    public bool destroyOnPickup = true;

    public AudioClip pickupClip;

    private AudioSource _audio;

    void Awake()
    {
        _audio = GetComponent<AudioSource>();
    }

    public void Interact()
    {
        var interactor = PlayerStatus.instance ? PlayerStatus.instance.gameObject : null;

        // interactor から Keyring を解決
        IKeyring ring = null;
        if (interactor) ring = interactor.GetComponentInParent<IKeyring>();
        if (ring == null && KeyringService.Current != null) ring = KeyringService.Current;

        if (ring != null)
        {
            (ring as SimpleKeyring)?.AddKey(keyId); // SimpleKeyring向け
            if (pickupClip && _audio) _audio.PlayOneShot(pickupClip);
            if (destroyOnPickup) Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning($"[KeyPickup] interactorに Keyring が見つからなかった: {keyId}");
        }
    }

    public string GetDisplayText()
    {
        return $"鍵を拾う";
    }

    public bool ShouldHighlight() => true;
}
