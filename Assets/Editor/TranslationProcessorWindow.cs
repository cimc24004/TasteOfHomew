// TranslationProcessorWindow.cs
// (Assets/Editor フォルダに配置してください)
// ★ Json.NET を使用
// ★ Pythonへの受け渡しで StandardInput + Base64 を使用

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.Localization.Tables;
using UnityEditor.Localization;
using System.IO;
using UnityEngine.Localization;
using System.Linq;
using System.Diagnostics; // Process を使うため
using System.Text; // Encoding を使うため
using Debug = UnityEngine.Debug; // UnityのDebugクラスを明示
using Newtonsoft.Json; // ★ Json.NET を
using System.Threading.Tasks; // Taskを使うため

// --- JSON送受信用データ構造 ---
[System.Serializable]
public class TranslationRequest
{
    public List<TranslationItem> items = new List<TranslationItem>();
}

[System.Serializable]
public class TranslationItem
{
    public string collectionGUID;
    public string key;
    public string japaneseText;
}

[System.Serializable]
public class TranslationResponse
{
    public List<TranslationResult> results = new List<TranslationResult>();
}

[System.Serializable]
public class TranslationResult
{
    public string collectionGUID;
    public string key;
    // public string englishText; // 英語は除外
    public string chineseText;
    public string koreanText;
}

/// <summary>
/// Python(PowerShell)経由で翻訳を実行するエディタウィンドウ
/// </summary>
public class TranslationProcessorWindow : EditorWindow
{
    // --- UI用変数 ---
    private string searchPath = "Assets/Localization";
    private string pythonExePath = "python"; 
    private string pythonScriptPath = "Assets/Python/translate.py";
    private Vector2 scrollPos;
    
    // 翻訳対象のロケール
    private LocaleIdentifier sourceLocale = new LocaleIdentifier("ja");
    
    private List<LocaleIdentifier> targetLocales = new List<LocaleIdentifier>
    {
        // new LocaleIdentifier("en"), // 英語は後で
        new LocaleIdentifier("zh-Hans"),
        new LocaleIdentifier("ko")
    };
    
    private bool isProcessing = false;

    // --- メニューに項目を追加 ---
    [MenuItem("Window/Localization/Python Translator")]
    public static void ShowWindow()
    {
        GetWindow<TranslationProcessorWindow>("Python Translator");
    }

    // --- UIの描画 ---
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Python Translation Processor", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("指定フォルダ内のCollectionを検索し、ソース言語(ja)が入力済みで、ターゲット言語(zh-Hans, ko)が空のエントリーをPythonに送り、翻訳結果を書き戻します。", MessageType.Info);
        EditorGUILayout.Space(10);

        // --- 設定エリア ---
        EditorGUILayout.LabelField("Settings", EditorStyles.miniBoldLabel);
        searchPath = EditorGUILayout.TextField("Collection Search Path", searchPath);
        pythonExePath = EditorGUILayout.TextField("Python Executable Path", pythonExePath);
        pythonScriptPath = EditorGUILayout.TextField("Python Script Path", pythonScriptPath);
        
        EditorGUILayout.Space(10);

        // --- 実行エリア ---
        GUI.color = Color.cyan;
        
        EditorGUI.BeginDisabledGroup(isProcessing);
        if (GUILayout.Button(isProcessing ? "Translating..." : "Find Empty Fields and Translate via Python (No English)", GUILayout.Height(40)))
        {
            EditorApplication.delayCall += RunTranslationProcess;
        }
        EditorGUI.EndDisabledGroup();
        
        GUI.color = Color.white;
    }

    /// <summary>
    /// 翻訳プロセス全体を実行
    /// </summary>
    private async void RunTranslationProcess()
    {
        isProcessing = true;
        Repaint(); 

        try
        {
            // 1. 翻訳リクエストを作成
            TranslationRequest request = BuildTranslationRequest();
            if (request.items.Count == 0)
            {
                Debug.Log("翻訳対象（日本語が入力済みで、他言語が空）のエントリーが見つかりませんでした。");
                EditorUtility.DisplayDialog("Result", "翻訳対象のエントリーが見つかりませんでした。", "OK");
                return;
            }

            // 2. Pythonスクリプトを実行
            // ★ Json.NET でシリアル化
            string jsonRequest = JsonConvert.SerializeObject(request); 
            string scriptFullPath = Path.GetFullPath(pythonScriptPath);
            
            Debug.Log($"Sending {request.items.Count} items to Python...");

            // Python実行 (Task.Runでワーカースレッドに)
            string jsonResponse = await Task.Run(() => 
                RunPythonScript(jsonRequest, scriptFullPath) // ★ Base64を使う修正版
            );

            if (string.IsNullOrEmpty(jsonResponse))
            {
                Debug.LogError("Pythonからの応答がありませんでした。");
                EditorUtility.DisplayDialog("Error", "Pythonからの応答がありませんでした。", "OK");
                return;
            }

            // ★ Json.NET でデシリアライズ (エスケープ文字の問題なし)
            TranslationResponse response = JsonConvert.DeserializeObject<TranslationResponse>(jsonResponse);

            // 3. 応答をパースしてUnityに書き戻す
            ApplyTranslations(response); 
            
            Debug.Log($"翻訳が完了しました。 {response.results.Count} 件のキーが更新されました。");
            EditorUtility.DisplayDialog("Success", $"翻訳が完了しました。\n{response.results.Count} 件のキーが更新されました。", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"翻訳プロセスでエラーが発生しました: {e}");
            EditorUtility.DisplayDialog("Error", $"翻訳プロセスでエラーが発生しました:\n{e.Message}", "OK");
        }
        finally
        {
            isProcessing = false;
            Repaint();
        }
    }

    /// <summary>
    /// 翻訳対象のデータを収集
    /// </summary>
    private TranslationRequest BuildTranslationRequest()
    {
        string[] guids = AssetDatabase.FindAssets("t:StringTableCollection", new[] { searchPath });
        TranslationRequest request = new TranslationRequest();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            StringTableCollection collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(path);
            if (collection == null || collection.SharedData == null) continue;

            StringTable sourceTable = collection.GetTable(sourceLocale) as StringTable;
            if (sourceTable == null) continue; 

            foreach (var kvp in sourceTable) 
            {
                StringTableEntry entry = kvp.Value; 
                string key = entry.Key;
                string jaText = entry.Value;

                if (string.IsNullOrEmpty(jaText)) continue;

                bool needsTranslation = false;
                foreach (var targetLocale in targetLocales)
                {
                    StringTable targetTable = collection.GetTable(targetLocale) as StringTable;
                    if (targetTable != null)
                    {
                        var targetEntry = targetTable.GetEntry(key);
                        if (targetEntry == null || string.IsNullOrEmpty(targetEntry.Value))
                        {
                            needsTranslation = true;
                            break;
                        }
                    }
                    else
                    {
                        needsTranslation = true;
                        break;
                    }
                }

                if (needsTranslation)
                {
                    request.items.Add(new TranslationItem
                    {
                        collectionGUID = guid,
                        key = key,
                        japaneseText = jaText
                    });
                }
            }
        }
        return request;
    }

    /// <summary>
    /// Pythonスクリプトを直接実行し、StandardInputでJSONを渡し、StandardOutputから結果を受け取る
    /// </summary>
    private string RunPythonScript(string jsonRequest, string scriptFullPath)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = pythonExePath; // "python"
        startInfo.Arguments = $"\"{scriptFullPath}\""; 
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardInput = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.CreateNoWindow = true;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;

        // --- ▼▼▼ Base64 (ASCIIエラー回避) ▼▼▼ ---
        string jsonBase64 = System.Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonRequest));
        // --- ▲▲▲ ---

        try
        {
            using (Process process = Process.Start(startInfo))
            {
                // ★ StandardInput に「Base64エンコードされた文字列」を書き込む
                using (StreamWriter writer = process.StandardInput)
                {
                    writer.Write(jsonBase64);
                }

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                
                process.WaitForExit();

                if (process.ExitCode != 0 || !string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"Python Error (ExitCode {process.ExitCode}): {error}");
                    return null;
                }
                
                return output; // Pythonのprint()結果
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Process Start Error: {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// 翻訳結果を StringTable アセットに書き込む
    /// </summary>
    private void ApplyTranslations(TranslationResponse response)
    {
        HashSet<Object> modifiedAssets = new HashSet<Object>();

        foreach (var result in response.results)
        {
            string path = AssetDatabase.GUIDToAssetPath(result.collectionGUID);
            StringTableCollection collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(path);
            if (collection == null) continue;
            
            SetTableValue(collection, new LocaleIdentifier("zh-Hans"), result.key, result.chineseText, modifiedAssets);
            SetTableValue(collection, new LocaleIdentifier("ko"), result.key, result.koreanText, modifiedAssets);
        }

        if (modifiedAssets.Count > 0)
        {
            Debug.Log($"ApplyTranslations: {modifiedAssets.Count} 個のアセットが更新されました。");
        }
    }

    /// <summary>
    /// 実際の書き込み処理 (Undo対応)
    /// </summary>
    private void SetTableValue(StringTableCollection collection, LocaleIdentifier localeId, string key, string value, HashSet<Object> modifiedAssets)
    {
        if (value == null) return; 

        StringTable table = collection.GetTable(localeId) as StringTable;
        if (table == null)
        {
            table = ScriptableObject.CreateInstance<StringTable>();
            table.LocaleIdentifier = localeId;
            table.SharedData = collection.SharedData;

            string tablePath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(collection));
            string tableName = $"{collection.TableCollectionName}_{localeId.Code}.asset";
            string fullAssetPath = Path.Combine(tablePath, tableName).Replace("\\", "/");
                            
            AssetDatabase.CreateAsset(table, fullAssetPath);
            collection.AddTable(table);
            modifiedAssets.Add(table);
            modifiedAssets.Add(collection);
            Debug.Log($"[Translator] Created new table: {fullAssetPath}");
        }

        var entry = table.GetEntry(key);
        if (entry != null && entry.Value == value)
        {
            return;
        }
        
        Undo.RecordObject(table, "Apply Translation");

        if (entry != null)
        {
            entry.Value = value;
        }
        else
        {
            entry = table.AddEntry(key, value);
        }
        
        if (entry.IsSmart) entry.IsSmart = true;
        
        EditorUtility.SetDirty(table);
        modifiedAssets.Add(table);
    }
}