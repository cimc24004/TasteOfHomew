using UnityEngine;

/// <summary>
/// "FadeInOutCanvas" オブジェクトにアタッチしてください。
/// SceneManagerController がこのCanvasGroupを見つけるための「目印」として機能します。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class TimelineFadeRegistrar : MonoBehaviour
{
    // SceneManagerController がこの CanvasGroup を直接参照します。
    private CanvasGroup _cg;
    public CanvasGroup CanvasGroup
    {
        get
        {
            if (_cg == null) _cg = GetComponent<CanvasGroup>();
            return _cg;
        }
    }

    // このスクリプトは、Awake() や Start() での登録処理は不要です。
    // アタッチされているだけで、SMCが FindObjectOfType で見つけ出します。
}
