using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// UIの「行」を管理するコンポーネント。
/// 子のSelectable (Sliderなど) が選択/ハイライトされたら処理を行う。
/// </summary>
public class SelectableRow : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("選択時に色を変えるテキスト (必須)")]
    [SerializeField]
    private TextMeshProUGUI labelText;

    [Header("Highlight Colors")]
    [Tooltip("選択されていない時のテキスト色")]
    [SerializeField]
    private Color normalColor = new Color(0.8f, 0.8f, 0.8f, 1f); // 例: グレー

    [Tooltip("選択されている時のテキスト色")]
    [SerializeField]
    private Color selectedColor = Color.white; // 例: 白
    
    [Tooltip("この行が管理するSlider, Dropdown, Button, Toggleなど")]
    [SerializeField]
    private Selectable uiSelectable; //Slider Dropdown Button Toggle

    private TMP_Dropdown childDropdown; 
    private Coroutine checkDeselectionCoroutine; 

    // ▼▼▼ ★★★ 追加: 有効化された瞬間に色をチェックする ★★★ ▼▼▼
    private void OnEnable()
    {
        // パネルが表示された瞬間、もし自分が選択済みなら色を白にする
        UpdateColorState();
    }
    // ▲▲▲ ★★★ 追加ここまで ★★★ ▲▲▲

    private void OnDisable()
    {
        if (checkDeselectionCoroutine != null)
        {
            StopCoroutine(checkDeselectionCoroutine);
            checkDeselectionCoroutine = null;
        }
        if (labelText != null)
        {
            labelText.color = normalColor;
        }
    }

    void Start()
    {
        if (uiSelectable == null)
        {
            Debug.LogWarning("uiSelectable が " + gameObject.name + " に設定されていません。", this);
            if (labelText != null)
            {
                 labelText.color = normalColor;
            }
            return;
        }

        childDropdown = uiSelectable.GetComponent<TMP_Dropdown>();

        // EventTriggerの取得/追加
        EventTrigger trigger = uiSelectable.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = uiSelectable.gameObject.AddComponent<EventTrigger>();
        }

        // 「選択 (Select)」イベント
        EventTrigger.Entry selectEntry = new EventTrigger.Entry();
        selectEntry.eventID = EventTriggerType.Select;
        selectEntry.callback.AddListener((eventData) => { HandleChildSelect(); });
        
        // 「選択解除 (Deselect)」イベント
        EventTrigger.Entry deselectEntry = new EventTrigger.Entry();
        deselectEntry.eventID = EventTriggerType.Deselect;
        deselectEntry.callback.AddListener((eventData) => { HandleChildDeselect(); });

        // 「ハイライト (PointerEnter)」イベント
        EventTrigger.Entry pointerEnterEntry = new EventTrigger.Entry();
        pointerEnterEntry.eventID = EventTriggerType.PointerEnter;
        pointerEnterEntry.callback.AddListener((eventData) => { HandleChildPointerEnter(); });

        // エントリーを追加
        trigger.triggers.Add(selectEntry);
        trigger.triggers.Add(deselectEntry);
        trigger.triggers.Add(pointerEnterEntry);

        // ▼▼▼ ★★★ 変更: 初期色の設定を修正 ★★★ ▼▼▼
        // 単純に normalColor を入れるのではなく、今の状態を見て決める
        UpdateColorState();
        // ▲▲▲ ★★★ 変更ここまで ★★★ ▲▲▲
    }

    // ▼▼▼ ★★★ 追加: 現在の状態に合わせて色を適用するヘルパー関数 ★★★ ▼▼▼
    /// <summary>
    /// 現在のEventSystemの選択状態を確認して、テキストの色を初期化する
    /// (StartやOnEnableのタイミングで、コントローラーによって既に選択されている可能性があるため)
    /// </summary>
    private void UpdateColorState()
    {
        if (labelText == null) return;

        bool isSelected = false;

        // EventSystem上で、このオブジェクトが現在選択されているかチェック
        if (EventSystem.current != null && uiSelectable != null)
        {
            if (EventSystem.current.currentSelectedGameObject == uiSelectable.gameObject)
            {
                isSelected = true;
            }
        }

        // 選択されていれば白、そうでなければグレー
        labelText.color = isSelected ? selectedColor : normalColor;
    }
    // ▲▲▲ ★★★ 追加ここまで ★★★ ▲▲▲

    /// <summary>
    /// 子のUI (uiSelectable) がハイライトされた時 (マウスが乗った時) に呼ばれる
    /// </summary>
    public void HandleChildPointerEnter()
    {
        // ★ここに「ハイライト時の音」を鳴らす処理を書く★
        // 例: SoundManager.Instance.PlayUISound(SoundType.UI_Highlight);
        
        // Debug.Log(gameObject.name + " がハイライトされました。");
    }

    /// <summary>
    /// 子のUI (uiSelectable) が選択された時に EventTrigger から呼ばれる
    /// </summary>
    public void HandleChildSelect()
    {
        if (checkDeselectionCoroutine != null)
        {
            StopCoroutine(checkDeselectionCoroutine);
            checkDeselectionCoroutine = null;
        }

        if (labelText != null)
        {
            labelText.color = selectedColor;
        }
        
        // ★ここに「選択時の音」を鳴らす処理を書く★
        // 例: SoundManager.Instance.PlayUISound(SoundType.UI_Select);
    }

    /// <summary>
    /// 子のUI (uiSelectable) が選択解除された時に EventTrigger から呼ばれる
    /// </summary>
    public void HandleChildDeselect()
    {
        if (checkDeselectionCoroutine != null)
        {
            StopCoroutine(checkDeselectionCoroutine);
        }
        checkDeselectionCoroutine = StartCoroutine(CheckDeselectionRoutine());
    }

    /// <summary>
    /// 1フレーム待ってから、本当に選択解除されたかチェックする
    /// </summary>
    private IEnumerator CheckDeselectionRoutine()
    {
        yield return new WaitForEndOfFrame(); 

        if (EventSystem.current == null) 
        {
             checkDeselectionCoroutine = null;
             yield break;
        }

        if (childDropdown != null && childDropdown.IsExpanded)
        {
            // Dropdownのリストが選択されている状態なので、色は戻さない
        }
        else if (EventSystem.current.currentSelectedGameObject == uiSelectable.gameObject)
        {
            // 色は戻さない
        }
        else
        {
            if (labelText != null)
            {
                labelText.color = normalColor; // 色を通常色に戻す
            }
        }
        
        checkDeselectionCoroutine = null;
    }

    /// <summary>
    /// この行が管理する uiSelectable を EventSystem に選択させる
    /// (ConfigMenuUIController から呼ばれる)
    /// </summary>
    public void SelectChildElement()
    {
        HandleChildSelect(); // 視覚的に選択状態にする

        if (uiSelectable != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(uiSelectable.gameObject);
        }
    }
}