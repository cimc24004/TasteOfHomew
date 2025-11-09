using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using UnityEngine.Localization; // ← 【追加】

[CreateAssetMenu(fileName = "Readable_", menuName = "Game/Readable Asset")]
public class ReadableAsset : ScriptableObject
{
    [HorizontalGroup("h"), LabelText("ID"), Required]
    public string id;                  // セーブ/フラグ用の一意キー

    // ▼▼▼ 【(1) 古いデータ（移行用）】 ▼▼▼
    [Title("--- OLD DATA (for Migration) ---")]
    [HorizontalGroup("h_old"), LabelText("タイトル (Old)")]
    [TextArea(1, 2)]
    public string title;

    [LabelText("ページ本文(複数) (Old)")]
    [TextArea(3, 5)]
    public List<string> pages = new();
    // ▲▲▲ 【(1) ここまで】 ▲▲▲


    // ▼▼▼ 【(2) 新しいローカライズ用データ】 ▼▼▼
    [Title("--- LOCALIZATION (NEW) ---")]
    [HorizontalGroup("h_loc"), LabelText("タイトル (Localized)")]
    public LocalizedString localizedTitle;

    [LabelText("ページ本文(複数) (Localized)")]
    public List<LocalizedString> localizedPages = new();
    // ▲▲▲ 【(2) ここまで】 ▲▲▲


    [Title("UI/挙動")]
    [LabelText("背景画像(任意)")]
    public List<Sprite> backgroundImages = new(); // pagesと同インデックス対応（足りない分はnullでOK）

    [LabelText("前面画像(任意)")]
    public List<Sprite> foregroundImages = new(); // pagesと同インデックス対応（足りない分はnullでOK）

    [LabelText("開いたらゲームを一時停止")]
    public bool pauseWhileOpen = true;

    [LabelText("最初に開くページ(1始まり)")]
    [MinValue(1)] public int defaultStartPage = 1;

    [LabelText("各ページで閉じるボタンを表示")]
    [Tooltip("本文ページのインデックスに対応します。チェックすると表示、しないと非表示。数が足りないページは表示されます。")]
    public List<bool> showCloseButtonOnPages = new();

    [Title("効果音(任意)")]
    public AudioClip openSE;
    public AudioClip closeSE;
    public AudioClip turnSE;
}