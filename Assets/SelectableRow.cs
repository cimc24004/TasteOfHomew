using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // EventTrigger のために必要
using TMPro;
using System.Collections; // ★ コルーチン (IEnumerator) のために復活

/// <summary>
/// UIの「行」を管理するコンポーネント。
/// 子のSelectable (Sliderなど) が選択されたらラベルの色を変更する。
/// Dropdownを開いている間も、選択色を維持する。
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

    private TMP_Dropdown childDropdown; // Dropdownかどうかを判定するためにキャッシュ
    private Coroutine checkDeselectionCoroutine; // OnDeselect 用のコルーチン

    // ▼▼▼ ★★★ この関数を新しく追加 ★★★ ▼▼▼
    /// <summary>
    /// このGameObjectが非アクティブになる（パネルが閉じられる）ときに呼ばれる
    /// </summary>
    private void OnDisable()
    {
        // 1. 実行中のコルーチンがあれば停止する
        if (checkDeselectionCoroutine != null)
        {
            StopCoroutine(checkDeselectionCoroutine);
            checkDeselectionCoroutine = null;
        }

        // 2. パネルが非表示になるときに、強制的に色を「非選択色」に戻す
        // これにより、タブを切り替えて戻ってきたときに色が残るのを防ぐ
        if (labelText != null)
        {
            labelText.color = normalColor;
        }
    }
    // ▲▲▲ ★★★ 追加ここまで ★★★ ▲▲▲

    void Start()
    {
        // 1. uiSelectableがインスペクターで設定されていなければ、警告を出して終了
        if (uiSelectable == null)
        {
            Debug.LogWarning("uiSelectable が " + gameObject.name + " に設定されていません。", this);
            if (labelText != null)
            {
                 labelText.color = normalColor;
            }
            return;
        }

        // Dropdownコンポーネントをキャッシュしておく
        childDropdown = uiSelectable.GetComponent<TMP_Dropdown>();


        // 2. uiSelectable の GameObject から EventTrigger を取得（なければ追加）
        EventTrigger trigger = uiSelectable.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = uiSelectable.gameObject.AddComponent<EventTrigger>();
        }

        // 3. 「選択 (Select)」イベントのエントリーを作成
        EventTrigger.Entry selectEntry = new EventTrigger.Entry();
        selectEntry.eventID = EventTriggerType.Select;
        selectEntry.callback.AddListener((eventData) => { HandleChildSelect(); });
        
        // 4. 「選択解除 (Deselect)」イベントのエントリーを作成
        EventTrigger.Entry deselectEntry = new EventTrigger.Entry();
        deselectEntry.eventID = EventTriggerType.Deselect;
        deselectEntry.callback.AddListener((eventData) => { HandleChildDeselect(); });

        // 5. 作成したエントリーを EventTrigger に追加
        trigger.triggers.Add(selectEntry);
        trigger.triggers.Add(deselectEntry);

        // 6. 初期色を「選択されていない色」に設定
        if (labelText != null) // Startで呼ぶので、HandleChildDeselectのフル機能は不要
        {
            labelText.color = normalColor;
        }
    }

    /// <summary>
    /// 子のUI (uiSelectable) が選択された時に EventTrigger から呼ばれる
    /// </summary>
    public void HandleChildSelect()
    {
        // もし「選択解除チェック中」のコルーチンが動いていたら、停止する
        if (checkDeselectionCoroutine != null)
        {
            StopCoroutine(checkDeselectionCoroutine);
            checkDeselectionCoroutine = null;
        }

        if (labelText != null)
        {
            labelText.color = selectedColor;
        }
    }

    /// <summary>
    /// 子のUI (uiSelectable) が選択解除された時に EventTrigger から呼ばれる
    /// </summary>
    public void HandleChildDeselect()
    {
        // すぐに色を戻さず、1フレーム待つコルーチンを開始する
        
        // 既にチェック中なら（前回のチェックを）停止する
        if (checkDeselectionCoroutine != null)
        {
            StopCoroutine(checkDeselectionCoroutine);
        }
        // 新しいチェックを開始
        checkDeselectionCoroutine = StartCoroutine(CheckDeselectionRoutine());
    }


    /// <summary>
    /// 1フレーム待ってから、本当に選択解除されたかチェックする
    /// </summary>
    private IEnumerator CheckDeselectionRoutine()
    {
        // 1フレーム待機し、EventSystemが新しい選択を確定するのを待つ
        yield return new WaitForEndOfFrame(); 

        if (EventSystem.current == null) 
        {
             checkDeselectionCoroutine = null;
             yield break;
        }

        // ケース1: 子がDropdownで、かつ、リストが開いている(IsExpanded)
        if (childDropdown != null && childDropdown.IsExpanded)
        {
            // Dropdownのリストが選択されている状態なので、色は戻さない
        }
        // ケース2: EventSystemの新しい選択が「自分自身の子」である
        else if (EventSystem.current.currentSelectedGameObject == uiSelectable.gameObject)
        {
            // 色は戻さない
        }
        // ケース3: 上記以外 (＝本当に別の行に移動したか、Dropdownが閉じた)
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
        // 1. まず、自分自身の色を「選択色」に強制的に変更する
        HandleChildSelect(); 

        // 2. 次に、EventSystemに子の選択を要求する
        if (uiSelectable != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(uiSelectable.gameObject);
        }
    }
}