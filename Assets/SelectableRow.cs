using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // EventTrigger のために必要
using TMPro;
using System.Collections; // ★ コルーチン (IEnumerator) のために復活

/// <summary>
/// UIの「行」を管理するコンポーネント。
/// 子のSelectable (Sliderなど) が選択/ハイライトされたら処理を行う。
/// </summary>
public class SelectableRow : MonoBehaviour
{
    // ... (SerializeField などの変数は変更なし) ...
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

    private void OnDisable()
    {
        // (この関数は変更なし)
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
        // ( ... 1. uiSelectableのnullチェック ... )
        if (uiSelectable == null)
        {
            Debug.LogWarning("uiSelectable が " + gameObject.name + " に設定されていません。", this);
            if (labelText != null)
            {
                 labelText.color = normalColor;
            }
            return;
        }

        // ( ... Dropdownのキャッシュ ... )
        childDropdown = uiSelectable.GetComponent<TMP_Dropdown>();

        // ( ... 2. EventTriggerの取得/追加 ... )
        EventTrigger trigger = uiSelectable.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = uiSelectable.gameObject.AddComponent<EventTrigger>();
        }

        // ( ... 3. 「選択 (Select)」イベント ... )
        EventTrigger.Entry selectEntry = new EventTrigger.Entry();
        selectEntry.eventID = EventTriggerType.Select;
        selectEntry.callback.AddListener((eventData) => { HandleChildSelect(); });
        
        // ( ... 4. 「選択解除 (Deselect)」イベント ... )
        EventTrigger.Entry deselectEntry = new EventTrigger.Entry();
        deselectEntry.eventID = EventTriggerType.Deselect;
        deselectEntry.callback.AddListener((eventData) => { HandleChildDeselect(); });

        // ▼▼▼ ★★★ 5. 「ハイライト (PointerEnter)」イベントのエントリーを作成 ★★★ ▼▼▼
        EventTrigger.Entry pointerEnterEntry = new EventTrigger.Entry();
        pointerEnterEntry.eventID = EventTriggerType.PointerEnter;
        // ハイライト時に HandleChildPointerEnter メソッドを呼ぶように登録
        pointerEnterEntry.callback.AddListener((eventData) => { HandleChildPointerEnter(); });
        // ▲▲▲ ★★★ 追加ここまで ★★★ ▲▲▲


        // 6. 作成したエントリーを EventTrigger に追加
        trigger.triggers.Add(selectEntry);
        trigger.triggers.Add(deselectEntry);
        trigger.triggers.Add(pointerEnterEntry); // ★追加

        // ( ... 7. 初期色の設定 ... )
        if (labelText != null)
        {
            labelText.color = normalColor;
        }
    }

    // ▼▼▼ ★★★ この関数を新しく追加 ★★★ ▼▼▼
    /// <summary>
    /// 子のUI (uiSelectable) がハイライトされた時 (マウスが乗った時) に呼ばれる
    /// </summary>
    public void HandleChildPointerEnter()
    {
        // ★ここに「ハイライト時の音」を鳴らす処理を書く★
        // 例: SoundManager.Instance.PlayUISound(SoundType.UI_Highlight);
        
        Debug.Log(gameObject.name + " がハイライトされました。");
    }
    // ▲▲▲ ★★★ 追加ここまで ★★★ ▲▲▲


    /// <summary>
    /// 子のUI (uiSelectable) が選択された時に EventTrigger から呼ばれる
    /// </summary>
    public void HandleChildSelect()
    {
        // (変更なし)
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
        // (変更なし)
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
        // (変更なし)
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
        // (変更なし)
        HandleChildSelect(); 

        if (uiSelectable != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(uiSelectable.gameObject);
        }
    }
}