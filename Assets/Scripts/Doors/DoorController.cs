using UnityEngine;
using UnityEngine.Events;
using DG.Tweening;
using Sirenix.OdinInspector;

public class DoorController : SerializedMonoBehaviour, IInteractable
{
    public enum DoorStatus { Closed, Opened }
    public enum MoveXYZ { X, Y, Z }
    public enum OpenType { SideOpen, Rotate }

    [Header("設定")]
    public OpenType open_type;
    public DoorStatus nowDoorStatus;

    [ShowIf("open_type", OpenType.SideOpen)] [Required] public Transform ToOpenTransform;
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
    public string GetDisplayText() => "ドアを開ける";
    public bool ShouldHighlight() => true;

    [ShowIf("open_type", OpenType.SideOpen)] public MoveXYZ move_x_y_z;

    public void OpenCloseAutoSelect()
    {
        if (!OpenedClosedCompletedAnimation) return;
        if (nowDoorStatus == DoorStatus.Closed) Open();
        else Close();
    }

    [Button]
    public void Open()
    {
        OpenedClosedCompletedAnimation = false;
        nowDoorStatus = DoorStatus.Opened;
        if (OpenClip && audio_source) audio_source.PlayOneShot(OpenClip);
        OpenStartEvent?.Invoke();

        if (open_type == OpenType.SideOpen)
        {
            transform.DOLocalMove(OpenLocalPosition, Duration).SetEase(Ease.Linear).SetLink(gameObject)
                .OnComplete(() => { OpenedClosedCompletedAnimation = true; OpenCompleted?.Invoke(); });
        }
        else if (open_type == OpenType.Rotate)
        {
            transform.DOLocalRotate(TargetRotateVector, Duration, RotateMode.Fast).SetEase(Ease.Linear).SetLink(gameObject)
                .OnUpdate(() => collider.isTrigger = true)
                .OnComplete(() =>
                {
                    OpenedClosedCompletedAnimation = true;
                    if (TriggerChangeModeOpenClose) collider.isTrigger = false;
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

        if (open_type == OpenType.SideOpen)
        {
            transform.DOLocalMove(DefaultLocalPosition, Duration).SetEase(Ease.Linear).SetLink(gameObject)
                .OnComplete(() => { OpenedClosedCompletedAnimation = true; CloseCompleted?.Invoke(); });
        }
        else if (open_type == OpenType.Rotate)
        {
            transform.DOLocalRotate(DefaultRotateVector, Duration, RotateMode.Fast).SetEase(Ease.Linear).SetLink(gameObject)
                .OnUpdate(() => { if (TriggerChangeModeOpenClose) collider.isTrigger = true; })
                .OnComplete(() =>
                {
                    if (CloseCompletedClip && audio_source) audio_source.PlayOneShot(CloseCompletedClip);
                    OpenedClosedCompletedAnimation = true;
                    collider.isTrigger = false;
                    CloseCompleted?.Invoke();
                });
        }
    }
}
