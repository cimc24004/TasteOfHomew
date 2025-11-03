using UnityEngine;
using UnityEngine.EventSystems; // EventSystem を使うために必要
using UnityEngine.UI;         // Selectable を使うために必要
using UnityEngine.InputSystem; // InputSystem のために追加

/// <summary>
/// EventSystem を監視し、UIの「選択 (Select)」が変更されたら
/// 汎用の選択SEを再生するシングルトン。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class UISelectSoundManager : MonoBehaviour
{
    public static UISelectSoundManager Instance { get; private set; }

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
    void Awake()
    {
        // シングルトン設定
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // AudioSource を取得
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    void Start()
    {
        // ★ 起動時は lastSelectedObject を必ず null にしておく
        lastSelectedObject = null;

        // PlayerInput から "Submit" アクションを取得
        if (PlayerDataHub.instance != null && PlayerDataHub.instance.CurrentplayerInput != null)
        {
            playerInput = PlayerDataHub.instance.CurrentplayerInput;
            
            submitAction = playerInput.actions.FindAction("Submit"); 
            clickAction = playerInput.actions.FindAction("Click");

            if (submitAction != null)
            {
                submitAction.performed += OnSubmitPressed;
            }
            if (clickAction != null)
            {
                clickAction.performed += OnSubmitPressed;
            }
        }
    }

    void OnDestroy()
    {
        // 購読解除
        if (submitAction != null)
        {
            submitAction.performed -= OnSubmitPressed;
        }
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
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(selectSound);
        }
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