using UnityEngine;
using UnityEngine.EventSystems; // EventSystem を使うために必要
using UnityEngine.UI;         // Selectable を使うために必要
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement; // InputSystem のために追加

/// <summary>
/// EventSystem を監視し、UIの「選択 (Select)」が変更されたら
/// 汎用の選択SEを再生するシングルトン。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class UISelectSoundManager : Singleton<UISelectSoundManager>
{

    [Header("Audio Settings")]
    [Tooltip("UI要素を選択した時（ハイライト時）に鳴らすSE")]
    [SerializeField]
    private AudioClip selectSound;

    [Tooltip("UI要素を決定（クリック/Submit）した時に鳴らすSE")]
    [SerializeField]
    private AudioClip submitSound; 

    private AudioSource audioSource;

    // 現在（最後）に選択されていたGameObject
    private GameObject lastSelectedObject;

    // --- InputSystem用 (Submit音のため) ---
    private PlayerInput playerInput;
    private InputAction submitAction;
    // ---
    private InputAction clickAction;
    void Start()
    {
        // ★ 起動時は lastSelectedObject を必ず null にしておく
        lastSelectedObject = null;
        // DontDestroyOnLoad(gameObject);
        audioSource = GetComponent<AudioSource>();

        // PlayerInput から "Submit" アクションを取得
        playerInput = PlayerDataHub.instance.CurrentplayerInput;
        
        submitAction = playerInput.actions.FindAction("Submit"); 
        clickAction = playerInput.actions.FindAction("Click");


        submitAction.performed += OnSubmitPressed;
        clickAction.performed += OnSubmitPressed;
    }
    void OnDestroy()
    {
        Debug.Log("削除されます UISelectSoundManager" + SceneManager.GetActiveScene().name + "");

    }
    void Update()
    {
        if (EventSystem.current == null)
        {
            return;
        }

        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
        // 選択されているオブジェクトが、前のフレームと変わったか？
        if (currentSelected != lastSelectedObject)
        {
            // Debug.Log("現在選択されているオブジェクト: " + (currentSelected != null ? currentSelected.name : "null"));
            // 選択対象が null ではなく (何かが選択され)、
            // *かつ*、前のフレームの選択対象も null ではない (null からの選択ではない)
            // かつ、それが Selectable (Button, Toggle, Sliderなど) である場合
            if (currentSelected != null && lastSelectedObject != null && currentSelected.GetComponent<Selectable>() != null)
            {
                // ★選択音を鳴らす
                PlaySound(selectSound);
            }

            // 最後に選択したオブジェクトを更新
            lastSelectedObject = currentSelected;
        }
    }

    /// <summary>
    /// InputSystem の "Submit" が押されたときに呼ばれる
    /// </summary>
    public void OnSubmitPressed(InputAction.CallbackContext context)
    {
        // 現在選択されているUIが「ある」場合のみ
        if (lastSelectedObject != null)
        {
            // ★決定音を鳴らす
            PlaySound(submitSound);
        }
    }

    /// <summary>
    /// 汎用のSE再生メソッド
    /// </summary>
    public void PlaySound(AudioClip clip)
    {
        audioSource.PlayOneShot(clip);
    }
    public void SelectPlaySound()
    {
        audioSource.PlayOneShot(selectSound);
    }
    public void SubmitPlaySound()
    {
        audioSource.PlayOneShot(submitSound);
    }
}