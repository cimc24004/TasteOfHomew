using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using TMPro;
using System.Threading.Tasks; // Task (非同期) のために必要

// --- エディタ専用のAPIを使うために必要 ---
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Localization.Tables; // GetTable, GetEntry のために必要
#endif
// ------------------------------------

/// <summary>
/// エディタ非再生時と再生時の両方で、
/// TMP_Dropdownのオプションをローカライズするスクリプト。
/// </summary>
[ExecuteAlways] // ★エディタ非再生時でも動作するように変更
[RequireComponent(typeof(TMP_Dropdown))]
public class LocalizedDropdown : MonoBehaviour
{
    // Inspectorで設定する、翻訳キーのリスト
    [SerializeField]
    private List<LocalizedString> m_LocalizedOptions;

    private TMP_Dropdown m_Dropdown;

    private void Awake()
    {
        // コンポーネントをキャッシュする
        m_Dropdown = GetComponent<TMP_Dropdown>();
    }

    private void OnEnable()
    {
        // ★[ExecuteAlways] により、エディタ非再生時でもイベントが購読される
        LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
        
        // 有効になった時点でも、リストを更新する
        PopulateOptions();
    }

    private void OnDisable()
    {
        // ★[ExecuteAlways] により、エディタ非再生時でもイベントが解除される
        LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
    }

    // イベントハンドラ（言語が変更されたら呼ばれる）
    private void HandleLocaleChanged(Locale locale)
    {
        PopulateOptions();
    }

    /// <summary>
    /// 再生時かエディタ非再生時かを判断し、
    /// 適切な方法でオプションリストを更新する。
    /// </summary>
    private void PopulateOptions()
    {
        if (m_Dropdown == null)
        {
            m_Dropdown = GetComponent<TMP_Dropdown>();
        }

        // m_Dropdownが何らかの理由で取得できない場合は処理を中断
        if (m_Dropdown == null)
        {
            Debug.LogError("LocalizedDropdown: TMP_Dropdown コンポーネントが見つかりません。", this);
            return;
        }

        if (Application.isPlaying)
        {
            // 【再生時】
            // 従来通りの非同期メソッドを呼び出す
            _ = PopulateOptionsAsync_Runtime();
        }
        else
        {
            // 【エディタ非再生時】
            // エディタ専用の同期メソッドを呼び出す
            #if UNITY_EDITOR
            PopulateOptionsSync_Editor();
            #endif
        }
    }

    /// <summary>
    /// 【再生時用】
    /// オプションのリストを非同期で再構築するメソッド
    /// </summary>
    private async Task PopulateOptionsAsync_Runtime()
    {
        // m_Dropdown が破棄されている可能性をチェック
        if (m_Dropdown == null) return; 

        m_Dropdown.interactable = false;
        m_Dropdown.ClearOptions();

        var optionDataList = new List<TMP_Dropdown.OptionData>();
        
        foreach (var localizedString in m_LocalizedOptions)
        {
            // 非同期で翻訳済みの文字列を取得する
            string translatedText = await localizedString.GetLocalizedStringAsync().Task;
            
            // 翻訳済みの文字列で OptionData を作成
            optionDataList.Add(new TMP_Dropdown.OptionData(translatedText));
        }
        
        // m_Dropdown が待機中に破棄されている可能性をチェック
        if (m_Dropdown == null) return;

        m_Dropdown.AddOptions(optionDataList);
        m_Dropdown.interactable = true;
    }


#if UNITY_EDITOR
    /// <summary>
    /// 【エディタ非再生時用】
    /// Localization Scene Controls の言語設定を使い、
    /// 同期的にオプションリストを再構築するメソッド。
    /// </summary>
    private void PopulateOptionsSync_Editor()
    {
        if (m_Dropdown == null) return;

        m_Dropdown.interactable = false;
        m_Dropdown.ClearOptions();

        var optionDataList = new List<TMP_Dropdown.OptionData>();

        // ▼▼▼ ★★★ (ここが正しい実装) ★★★ ▼▼▼

        // (1) エディタ上で現在選択されているロケール（言語）を取得
        // (Localization Scene Controls ウィンドウがこの値を変更する)
        var selectedLocale = LocalizationSettings.SelectedLocale;

        // (2) もしロケールが取得できなければ（初期化中など）、プロジェクトのデフォルトを試す
        if (selectedLocale == null)
        {
            selectedLocale = LocalizationSettings.ProjectLocale;
        }
        
        if (selectedLocale == null)
        {
            // これでもNullなら、もうどうしようもない
            Debug.LogWarning("LocalizedDropdown: Editor could not find a selected or project locale.", this);
            // Fallback to just keys
             foreach (var locString in m_LocalizedOptions)
             {
                 optionDataList.Add(new TMP_Dropdown.OptionData($"[{locString.TableEntryReference.Key}]"));
             }
        }
        else
        {
            // (3) 高レベルAPI (GetLocalizedStringAsync) を同期的に実行する
            foreach (var localizedString in m_LocalizedOptions)
            {
                string translatedText;
                
                // 特定のロケールを強制する
                var handle = localizedString.GetLocalizedStringAsync(selectedLocale);
                
                // In the editor, this should be fast.
                // WaitForCompletion() は同期的に完了を待つ
                translatedText = handle.WaitForCompletion();

                optionDataList.Add(new TMP_Dropdown.OptionData(translatedText));
            }
        }

        // ▲▲▲ ★★★ (ここまで) ★★★ ▲▲▲

        m_Dropdown.AddOptions(optionDataList);
        m_Dropdown.interactable = true;

        // (4) エディタのUIに変更を通知し、シーンビューを更新する
        EditorUtility.SetDirty(m_Dropdown);
    }
#endif
}