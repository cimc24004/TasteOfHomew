using System.Collections.Generic;
using UnityEngine;
using TMPro; // TextMeshPro を使うために必要
using UnityEngine.Localization.Settings; // Localization パッケージの設定を操作するために必要
using Cysharp.Threading.Tasks; // UniTask (非同期処理) のために必要

/// <summary>
/// TMP_Dropdown を使って、Unity Localization の言語を切り替えるスクリプト。
/// このスクリプトを、言語切り替えに使いたい TMP_Dropdown と同じ GameObject にアタッチしてください。
/// </summary>
public class LanguageSelector : MonoBehaviour
{
    [SerializeField]private TMP_Dropdown dropdown;

    // Start() を非同期（UniTaskVoid）で実行
    async UniTaskVoid Start()
    {
        // 1. Localization システムの初期化が完了するまで待機
        // (これが完了しないと、利用可能な言語リストが取得できないため)
        await LocalizationSettings.InitializationOperation;

        // 2. ドロップダウンのオプションを初期化
        PopulateDropdown();
        
        // 3. ドロップダウンの値が変更されたときに OnLanguageChanged メソッドを呼ぶよう設定
        dropdown.onValueChanged.AddListener(OnLanguageChanged);
    }

    /// <summary>
    /// 利用可能な言語リストでドロップダウンの選択肢を埋めます
    /// </summary>
    private void PopulateDropdown()
    {
        // 既存の選択肢をクリア
        dropdown.ClearOptions();

        // 利用可能な言語（ロケール）のリストを取得
        var locales = LocalizationSettings.AvailableLocales.Locales;
        
        var options = new List<string>();
        int currentIndex = 0; // 現在選択されている言語のインデックス

        for (int i = 0; i < locales.Count; i++)
        {
            var locale = locales[i];
            
            // ▼▼▼ 【修正】 "English (en)" ではなく "English" や "日本語" を表示 ▼▼▼
            options.Add(locale.Identifier.CultureInfo.NativeName); 
            // ▲▲▲ 【修正ここまで】 ▲▲▲
            
            // 現在ゲームで選択されている言語かどうかをチェック
            if (locale == LocalizationSettings.SelectedLocale)
            {
                currentIndex = i;
            }
        }

        // ドロップダウンに選択肢リストを追加
        dropdown.AddOptions(options);

        // ★重要★
        // SetValueWithoutNotify を使い、「イベントを発火させずに」
        // 現在の言語をデフォルトとしてドロップダウンに表示します。
        dropdown.SetValueWithoutNotify(currentIndex);
    }

    /// <summary>
    /// ドロップダウンで新しい言語が選択されたときに呼び出されます
    /// </summary>
    /// <param name="index">新しく選択された選択肢のインデックス</param>
    private void OnLanguageChanged(int index)
    {
        // インデックスを使って、選択されたロケール（言語）を取得
        var selectedLocale = LocalizationSettings.AvailableLocales.Locales[index];
        
        // Localization パッケージの「現在の言語」を、選択されたものに切り替える
        LocalizationSettings.SelectedLocale = selectedLocale;
    }
}