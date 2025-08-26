using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

[DisallowMultipleComponent]
public class CameraFocusController : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("CinemachineBrainが付いたMainCamera。未設定ならCamera.mainから自動取得")]
    public Camera mainCamera;

    [Header("ブレンド秒数(デフォルト)")]
    public float defaultBlendSeconds = 0.35f;
    public float defaultClearSeconds = 0.35f;

    // ── UnityEvent用のデフォルト引数（秒を渡したくない時に使う） ──
    [Header("UnityEvent用デフォルト")]
    [Tooltip("引数なしで注視させる時に使うデフォルトターゲット(Transform)")]
    public Transform defaultFocusTarget;
    [Tooltip("↑が無い時のフォールバック。指定があればこちらを使う")]
    public GameObject defaultFocusTargetGO;
    [Tooltip("UnityEvent用のブレンド秒。<0 なら defaultBlendSeconds / defaultClearSeconds を使う")]
    public float eventBlendSeconds = -1f;

    private CinemachineBrain _brain;

    // 状態
    private Transform _currentTarget;
    private float _currentWeight = 0f; // 0〜1
    private ForceLookAtExtension _activeExt;
    private ICinemachineCamera _lastActive;
    private Coroutine _blendRoutine;

    void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null) _brain = mainCamera.GetComponent<CinemachineBrain>();

        if (_brain == null)
            Debug.LogWarning("[CameraFocusController] CinemachineBrainが見つからない。MainCameraに付いてるか確認して。");
    }

    void LateUpdate()
    {
        if (_brain == null) return;

        var active = _brain.ActiveVirtualCamera;
        if (!ReferenceEquals(active, _lastActive))
        {
            _lastActive = active;
            if (_currentTarget != null && _currentWeight > 0f)
            {
                AttachExtensionToActiveVcam();
                ApplyStateToExtension();
            }
        }
    }

    //==============================================================
    //  通常API（コードから呼ぶ想定） ・・・ 第2引数は省略可（デフォルト値）
    //==============================================================
    public void FocusOn(Transform target, float blendSeconds = -1f)
    {
        if (target == null)
        {
            Debug.LogWarning("[CameraFocusController] FocusOn: targetがnull");
            return;
        }
        if (_brain == null) return;

        _currentTarget = target;
        AttachExtensionToActiveVcam();
        if (_activeExt == null)
        {
            Debug.LogWarning("[CameraFocusController] ForceLookAtExtensionの付与に失敗");
            return;
        }

        float dur = (blendSeconds >= 0f) ? blendSeconds : defaultBlendSeconds;
        StartBlend(toWeight: 1f, seconds: dur);
    }

    public void ClearFocus(float blendSeconds = -1f)
    {
        if (_brain == null) return;
        if (_currentTarget == null && _currentWeight <= 0f) return;

        float dur = (blendSeconds >= 0f) ? blendSeconds : defaultClearSeconds;
        StartBlend(toWeight: 0f, seconds: dur, onComplete: () =>
        {
            if (_activeExt != null)
            {
                _activeExt.Target = null;
                _activeExt.Weight = 0f;
                _activeExt.enabled = false;
            }
            _currentTarget = null;
        });
    }

    public void FocusOn(GameObject go, float blendSeconds = -1f)
        => FocusOn(go != null ? go.transform : null, blendSeconds);

    //==============================================================
    //  UnityEvent用ラッパー（第2引数 = いらない/渡さない想定）
    //  インスペクタでそのまま割り当てられるよう、形を分けてる
    //==============================================================

    // 1) 引数なし：デフォルトに注視（defaultFocusTarget または defaultFocusTargetGO）
    public void FocusOnDefault()
    {
        Transform t = defaultFocusTarget != null
            ? defaultFocusTarget
            : (defaultFocusTargetGO != null ? defaultFocusTargetGO.transform : null);

        float sec = (eventBlendSeconds >= 0f) ? eventBlendSeconds : -1f;
        FocusOn(t, sec);
    }

    // 2) 引数なし：解除（秒数は eventBlendSeconds を使う。負ならデフォルト）
    public void ClearFocusDefault()
    {
        float sec = (eventBlendSeconds >= 0f) ? eventBlendSeconds : -1f;
        ClearFocus(sec);
    }

    // 3) 1引数（Transform）版：秒数いらない
    public void FocusOnTransform(Transform target)
    {
        float sec = (eventBlendSeconds >= 0f) ? eventBlendSeconds : -1f;
        FocusOn(target, sec);
    }

    // 4) 1引数（GameObject）版：秒数いらない
    public void FocusOnGameObject(GameObject go)
    {
        float sec = (eventBlendSeconds >= 0f) ? eventBlendSeconds : -1f;
        FocusOn(go, sec);
    }

    // 5) 1引数（float）版：クリアだけ秒指定したい時用（任意）
    public void ClearFocusSeconds(float seconds)
    {
        ClearFocus(seconds);
    }

    //==============================================================

    private void AttachExtensionToActiveVcam()
    {
        if (_brain == null) return;

        var icam = _brain.ActiveVirtualCamera;
        if (icam == null) return;

        // CM3対応：ICinemachineCamera → CinemachineVirtualCameraBase にキャストして GameObject 取得
        var vcamBase = icam as CinemachineVirtualCameraBase;
        if (vcamBase == null) return;

        var vcamGO = vcamBase.gameObject;

        // すでに別VCamに付いてれば無効化
        if (_activeExt != null && _activeExt.gameObject != vcamGO)
        {
            _activeExt.Target = null;
            _activeExt.Weight = 0f;
            _activeExt.enabled = false;
        }

        _activeExt = vcamGO.GetComponent<ForceLookAtExtension>();
        if (_activeExt == null) _activeExt = vcamGO.AddComponent<ForceLookAtExtension>();
        _activeExt.enabled = true;
    }

    private void ApplyStateToExtension()
    {
        if (_activeExt == null) return;
        _activeExt.Target = _currentTarget;
        _activeExt.Weight = _currentWeight;
    }

    private void StartBlend(float toWeight, float seconds, System.Action onComplete = null)
    {
        if (_blendRoutine != null) StopCoroutine(_blendRoutine);
        _blendRoutine = StartCoroutine(BlendRoutine(toWeight, seconds, onComplete));
    }

    private IEnumerator BlendRoutine(float toWeight, float seconds, System.Action onComplete)
    {
        AttachExtensionToActiveVcam();
        ApplyStateToExtension();

        float from = _currentWeight;
        float t = 0f;
        seconds = Mathf.Max(0f, seconds);

        if (seconds <= 0f)
        {
            _currentWeight = toWeight;
            ApplyStateToExtension();
            onComplete?.Invoke();
            yield break;
        }

        while (t < seconds)
        {
            t += Time.deltaTime;
            _currentWeight = Mathf.Lerp(from, toWeight, Mathf.SmoothStep(0f, 1f, t / seconds));
            ApplyStateToExtension();
            yield return null;
        }

        _currentWeight = toWeight;
        ApplyStateToExtension();
        onComplete?.Invoke();
        _blendRoutine = null;
    }
}
