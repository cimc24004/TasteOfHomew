// QuizQuestionAsset.cs

using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(menuName = "Homework/Quiz Question", fileName = "QQA_")]
public class QuizQuestionAsset : SerializedScriptableObject
{
    [Header("見た目")]
    public Sprite sheetSprite;             // 紙の画像
    [Header("問題文(Dialogue)")]
    public string questionSequenceId; // 例: "Q1", "Q2"…（groupは questionGroupKey で共通）

    [Header("選択肢（2〜4個程度）")]
    public List<string> choices = new List<string>();
    [Tooltip("正解のインデックス（0始まり）")]
    public int correctIndex = 0;

    [Header("SFX設定")]
    [Tooltip("この問題だけSFXを鳴らさない")]
    public bool muteSfxForThisQuestion = false;
    public AudioClip correctSfxOverride;
    public AudioClip wrongSfxOverride;

    [Header("演出")]
    [Tooltip("チェックを入れるとホラー演出になり、〇×画像が表示されず、専用BGMが流れます")]
    public bool horrorDirection = false;
    [ShowIf("horrorDirection")]
    [Tooltip("ホラー演出時に流すBGM")]
    public AudioClip horrorSound;
    
    // ★ ここから追加
    [ShowIf("horrorDirection")]
    [Range(0f, 1f)]
    [Tooltip("ホラー演出時のBGMの目標音量")]
    public float horrorVolume = 1.0f;
    // ★ ここまで追加
}