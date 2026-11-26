using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization;
using Cysharp.Threading.Tasks;
using Steamworks;

public class LanguageSelector : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;

async UniTaskVoid Start()
    {
        // 1. Localization システムの初期化待機
        await LocalizationSettings.InitializationOperation;

        // 2. Steamの言語設定に合わせて Localization を切り替える
        if (SteamManager.Initialized)
        {
            string steamLang = SteamApps.GetCurrentGameLanguage();
            Locale targetLocale = null;

            // 言語判定ロジック
            if (steamLang == "japanese")
            {
                targetLocale = LocalizationSettings.AvailableLocales.GetLocale("ja");
            }
            else if (steamLang == "koreana")
            {
                targetLocale = LocalizationSettings.AvailableLocales.GetLocale("ko");
            }
            else if (steamLang == "schinese") // 簡体字
            {
                targetLocale = LocalizationSettings.AvailableLocales.GetLocale("zh-Hans");
            }
            else if (steamLang == "tchinese") // 繁体字
            {
                targetLocale = LocalizationSettings.AvailableLocales.GetLocale("zh-Hant");
            }

            // 上記の条件に当てはまらなかった場合（英語、ドイツ語、スペイン語など全て）、
            // または取得に失敗した場合は、デフォルトとして「英語 (en)」を設定する
            if (targetLocale == null)
            {
                targetLocale = LocalizationSettings.AvailableLocales.GetLocale("en");
            }

            // 適用（英語Localeの取得に成功していれば適用される）
            if (targetLocale != null)
            {
                LocalizationSettings.SelectedLocale = targetLocale;
            }
        }

        // 3. ドロップダウンの選択肢を作成し、現在の言語を選択状態にする
        PopulateDropdown();

        // 4. イベントリスナー登録
        dropdown.onValueChanged.AddListener(OnLanguageChanged);
    }

    private void PopulateDropdown()
    {
        dropdown.ClearOptions();

        var locales = LocalizationSettings.AvailableLocales.Locales;
        var options = new List<string>();

        int currentIndex = 0;

        for (int i = 0; i < locales.Count; i++)
        {
            var locale = locales[i];

            // UI表示用の名前を追加
            options.Add(locale.Identifier.CultureInfo.NativeName);

            // 現在設定されているLocaleと、リスト内のLocaleが一致するかチェック
            // 確実に一致させるため、Identifier（言語コード）同士で比較しても良いですが、
            // 上記でGetLocaleを使っていればオブジェクト比較でも概ね動作します
            if (locale == LocalizationSettings.SelectedLocale)
            {
                currentIndex = i;
            }
        }

        dropdown.AddOptions(options);

        // イベントを発火させずに、現在の言語の位置を選択状態にする
        dropdown.SetValueWithoutNotify(currentIndex);

        // 見た目も即座に更新する
        dropdown.RefreshShownValue();
    }

    private void OnLanguageChanged(int index)
    {
        // 範囲チェック
        if (index < 0 || index >= LocalizationSettings.AvailableLocales.Locales.Count) return;

        var selectedLocale = LocalizationSettings.AvailableLocales.Locales[index];
        LocalizationSettings.SelectedLocale = selectedLocale;
    }
}
