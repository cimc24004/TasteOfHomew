// TranslationProcessorWindow.cs
// (Assets/Editor フォルダに配置してください)
// ★ 翻訳済みJSONをインポートしてアセットに書き戻す機能を追加

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
    [JsonProperty("items")]
    public List<TranslationItem> items = new List<TranslationItem>();
}

[System.Serializable]
public class TranslationItem
{
    [JsonProperty("collectionGUID")]
    public string collectionGUID;
    
    [JsonProperty("key")]
    public string key;
    
    [JsonProperty("japaneseText")]
    public string japaneseText;

    // --- ▼▼▼ 修正箇所 (インポート時に読み取るキー) ▼▼▼ ---
    [JsonProperty("englishText")]
    public string englishText;

    [JsonProperty("chineseText")]
    public string chineseText;

    [JsonProperty("koreanText")]
    public string koreanText;
    // --- ▲▲▲ 修正完了 ▲▲▲ ---
}

/// <summary>
/// Python経由で「翻訳用の日本語表 (JSON)」をエクスポート/インポートするウィンドウ
/// </summary>
public class TranslationProcessorWindow : EditorWindow
{
    // --- UI用変数 ---
    private string searchPath = "Assets/Localization";
    private string pythonExePath = "python"; 
    private string pythonScriptPath = "Assets/Python/translate.py";
    
    private string exportSavePath = "Assets/Translation_Export.json"; // エクスポートJSONの保存先
    
    // --- ▼▼▼ 修正箇所 (インポートUI用) ▼▼▼ ---
    private string importJsonPath = "Assets/Translation_Export.json"; // ★ インポートするJSONパス
    // --- ▲▲▲ 修正完了 ▲▲▲ ---
    
    private LocaleIdentifier sourceLocale = new LocaleIdentifier("ja");

    private bool isProcessing = false;

    [MenuItem("Window/Localization/Python Translator")]
    public static void ShowWindow()
    {
        GetWindow<TranslationProcessorWindow>("Python Translator");
    }

    // --- UIの描画 ---
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Python Translation Exporter / Importer", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("1. [Export] でJSONを生成し、Geminiに渡して翻訳してもらいます。\n2. [Import] で翻訳済みのJSONを読み込み、アセットに適用します。", MessageType.Info);
        
        EditorGUILayout.Space(10);

        // --- 設定エリア ---
        EditorGUILayout.LabelField("Settings (Common)", EditorStyles.miniBoldLabel);
        searchPath = EditorGUILayout.TextField("Collection Search Path", searchPath);
        pythonExePath = EditorGUILayout.TextField("Python Executable Path", pythonExePath);
        pythonScriptPath = EditorGUILayout.TextField("Python Script Path", pythonScriptPath);
        
        EditorGUILayout.Space(10);
        
        // --- 1. エクスポートエリア ---
        EditorGUILayout.LabelField("1. Export for Gemini", EditorStyles.miniBoldLabel);
        exportSavePath = EditorGUILayout.TextField("Export JSON Save Path", exportSavePath);

        GUI.color = Color.green;
        EditorGUI.BeginDisabledGroup(isProcessing);
        if (GUILayout.Button(isProcessing ? "Processing..." : "Export Japanese Table to .json File", GUILayout.Height(40)))
        {
            EditorApplication.delayCall += RunExportProcess;
        }
        EditorGUI.EndDisabledGroup();
        GUI.color = Color.white;
        
        
        // --- ▼▼▼ 修正箇所 (2. インポートエリア) ▼▼▼ ---
        EditorGUILayout.Space(20); // エクスポートとインポートを区切る
        EditorGUILayout.LabelField("2. Import from Gemini", EditorStyles.boldLabel);
        importJsonPath = EditorGUILayout.TextField("Import JSON File Path", importJsonPath);

        GUI.color = Color.yellow; // 書き込み処理なので黄色
        EditorGUI.BeginDisabledGroup(isProcessing);
        if (GUILayout.Button(isProcessing ? "Processing..." : "Import Translations from .json File", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("Confirm Import", 
                $"本当に {importJsonPath} の内容をインポートしますか？\n\n" +
                "既存の翻訳（en, zh-Hans, ko）が上書きされます。\n" +
                "（この操作は Ctrl+Z で元に戻せます）", 
                "Import (実行)", "Cancel (中止)"))
            {
                EditorApplication.delayCall += RunImportProcess; // ★ インポート用の関数を呼ぶ
            }
        }
        EditorGUI.EndDisabledGroup();
        GUI.color = Color.white;
        // --- ▲▲▲ 修正完了 ▲▲▲ ---
    }

    /// <summary>
    /// (エクスポート) プロセス全体を実行
    /// </summary>
    private async void RunExportProcess()
    {
        isProcessing = true;
        Repaint(); 

        try
        {
            TranslationRequest request = BuildTranslationRequest();
            if (request.items.Count == 0)
            {
                Debug.Log("エクスポート対象のエントリーが見つかりませんでした。");
                return;
            }

            string jsonRequest = JsonConvert.SerializeObject(request); 
            string scriptFullPath = Path.GetFullPath(pythonScriptPath);
            Debug.Log($"Sending {request.items.Count} items to Python for formatting...");

            string jsonResponse = await Task.Run(() => 
                RunPythonScript(jsonRequest, scriptFullPath) 
            );

            if (string.IsNullOrEmpty(jsonResponse))
            {
                Debug.LogError("Pythonからの応答がありませんでした。");
                return;
            }

            if (string.IsNullOrEmpty(exportSavePath))
            {
                Debug.LogError("Export Save Path が空です。");
                return;
            }
            if (!exportSavePath.StartsWith("Assets/"))
            {
                Debug.LogError($"Export Save Path は 'Assets/' から始まるパスにしてください。");
                return;
            }
            
            try
            {
                string directory = Path.GetDirectoryName(exportSavePath);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(exportSavePath, jsonResponse, Encoding.UTF8);
                AssetDatabase.Refresh();
                
                TextAsset jsonFile = AssetDatabase.LoadAssetAtPath<TextAsset>(exportSavePath);
                if (jsonFile != null) EditorGUIUtility.PingObject(jsonFile);

                EditorUtility.DisplayDialog("Success", 
                    $"「日本語の表」(JSON) を保存しました:\n{exportSavePath}\n\n" +
                    "その .json ファイルを Gemini にアップロードしてください。", "OK");
            }
            catch (System.Exception e) { Debug.LogError($"JSONファイルの保存に失敗しました: {e.Message}"); }
        }
        catch (System.Exception e) { Debug.LogError($"エクスポートプロセスでエラーが発生しました: {e}"); }
        finally
        {
            isProcessing = false;
            Repaint();
        }
    }

    // --- ▼▼▼ 修正箇所 (インポート用関数) ▼▼▼ ---
    /// <summary>
    /// (インポート) プロセス全体を実行
    /// </summary>
    private void RunImportProcess()
    {
        isProcessing = true;
        Repaint();

        try
        {
            if (string.IsNullOrEmpty(importJsonPath) || !File.Exists(importJsonPath))
            {
                Debug.LogError($"Import Error: ファイルが見つかりません: {importJsonPath}");
                EditorUtility.DisplayDialog("Error", $"ファイルが見つかりません:\n{importJsonPath}", "OK");
                return;
            }

            // 1. JSONファイルを読み込む
            string jsonContent = File.ReadAllText(importJsonPath, Encoding.UTF8);

            // 2. Json.NET でデシリアライズ (形式はエクスポート時と同じ TranslationRequest)
            TranslationRequest importedData = JsonConvert.DeserializeObject<TranslationRequest>(jsonContent);

            if (importedData == null || importedData.items == null)
            {
                Debug.LogError("Import Error: JSONの解析に失敗しました。形式が間違っている可能性があります。");
                EditorUtility.DisplayDialog("Error", "JSONの解析に失敗しました。", "OK");
                return;
            }

            // 3. 翻訳を適用
            Debug.Log($"Importing {importedData.items.Count} entries...");
            ApplyTranslations(importedData.items); // ★ ApplyTranslations を呼ぶ

            EditorUtility.DisplayDialog("Success", $"インポートが完了しました。\n{importedData.items.Count} 件のエントリーが処理されました。", "OK");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"インポートプロセスでエラーが発生しました: {e}");
            EditorUtility.DisplayDialog("Error", $"インポートプロセスでエラーが発生しました:\n{e.Message}", "OK");
        }
        finally
        {
            isProcessing = false;
            Repaint();
        }
    }
    // --- ▲▲▲ 修正完了 ▲▲▲ ---

    /// <summary>
    /// (エクスポート用) 翻訳対象のデータを収集
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

                request.items.Add(new TranslationItem
                {
                    collectionGUID = guid,
                    key = key,
                    japaneseText = jaText
                    // (空のキーはPython側で追加)
                });
            }
        }
        return request;
    }

    /// <summary>
    /// (エクスポート用) Pythonスクリプトを実行
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

        startInfo.EnvironmentVariables["PYTHONIOENCODING"] = "UTF-8";

        string jsonBase64 = System.Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonRequest));

        try
        {
            using (Process process = Process.Start(startInfo))
            {
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
    
    // --- ▼▼▼ 修正箇所 (インポート用関数) ▼▼▼ ---
    /// <summary>
    /// (インポート用) 翻訳結果を StringTable アセットに書き込む
    /// </summary>
    private void ApplyTranslations(List<TranslationItem> items) // ★ 引数を List<TranslationItem> に変更
    {
        HashSet<Object> modifiedAssets = new HashSet<Object>();

        foreach (var result in items) // ★
        {
            string path = AssetDatabase.GUIDToAssetPath(result.collectionGUID);
            StringTableCollection collection = AssetDatabase.LoadAssetAtPath<StringTableCollection>(path);
            if (collection == null)
            {
                Debug.LogWarning($"Collection が見つかりません (GUID: {result.collectionGUID}, Key: {result.key})。スキップします。");
                continue;
            }
            
            // ★ 英語 (en) の処理を復活
            SetTableValue(collection, new LocaleIdentifier("en"), result.key, result.englishText, modifiedAssets);
            SetTableValue(collection, new LocaleIdentifier("zh-Hans"), result.key, result.chineseText, modifiedAssets);
            SetTableValue(collection, new LocaleIdentifier("ko"), result.key, result.koreanText, modifiedAssets);
        }

        if (modifiedAssets.Count > 0)
        {
            Debug.Log($"ApplyTranslations: {modifiedAssets.Count} 個のアセットが更新されました。");
        }
    }
    // --- ▲▲▲ 修正完了 ▲▲▲ ---

    /// <summary>
    /// (インポート用) 実際の書き込み処理 (Undo対応)
    /// </summary>
    private void SetTableValue(StringTableCollection collection, LocaleIdentifier localeId, string key, string value, HashSet<Object> modifiedAssets)
    {
        // ★ 翻訳結果が null (JSONにキーが存在しない) または "" (空文字) の場合は、
        // スキップせず、「空として上書き」するのが正しい
        if (value == null)
        {
            // (JSONに en: "" がなく、en: null だった場合の対策)
            value = ""; 
        }

        StringTable table = collection.GetTable(localeId) as StringTable;
        if (table == null)
        {
            // ターゲットのテーブル (例: en) が存在しない場合、
            // 空の翻訳を書き込むためだけにテーブルを自動作成する
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
            Debug.Log($"[Translator] Import: テーブル {fullAssetPath} を新規作成しました。");
        }

        var entry = table.GetEntry(key);
        
        if (entry == null && string.IsNullOrEmpty(value))
        {
            // (例: 翻訳前も空、翻訳後も空 の場合)
            // 何もする必要がないのでスキップ
            return;
        }

        if (entry != null && entry.Value == value)
        {
            // (例: 既に "TEST" が入っていて、翻訳結果も "TEST" の場合)
            // 変更がないのでスキップ
            return;
        }
        
        Undo.RecordObject(table, "Apply Translation (Import)");

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