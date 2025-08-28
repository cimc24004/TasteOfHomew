using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Homework/Quiz Question", fileName = "QQA_")]
public class QuizQuestionAsset : ScriptableObject
{
    [Header("見た目")]
    public Sprite sheetSprite;            // 紙の画像
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
}
