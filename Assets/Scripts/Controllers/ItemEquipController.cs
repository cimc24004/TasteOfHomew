using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 所持アイテムをキー付きで管理するコントローラ。
/// - stringキー → (GameObject, hasItem(bool))
/// - hasItem が true のとき、その GameObject を SetActive(true/false) で切替
/// </summary>
[DisallowMultipleComponent]
public class ItemEquipController : Singleton<ItemEquipController>
{
    [Serializable, InlineProperty, HideReferenceObjectPicker]
    public class ItemData
    {
        [HorizontalGroup("row", 70), LabelText("所持"), LabelWidth(36)]
        [ToggleLeft] 
        public bool hasItem;

        public GameObject obj;

        // 行末に個別適用ボタン
        [HorizontalGroup("row", 84)]
        [LabelText("適用")]
        [GUIColor(0.6f, 0.9f, 1f)]
        [Button(ButtonSizes.Small)]
        public void Apply(ItemEquipController owner)
        {
            Debug.Assert(owner != null, "[ItemEquipController] owner is null.");
            owner?.ApplyActiveState(this);
        }
    }

    [Title("Items")]
    [DictionaryDrawerSettings(
        DisplayMode = DictionaryDisplayOptions.ExpandedFoldout,
        KeyLabel = "Key (string)",
        ValueLabel = "Data")]
    [Searchable]
    [SerializeField] private Dictionary<string, ItemData> items = new();

    [Title("Options")]
    [LabelText("OnEnableで自動反映")]
    public bool applyOnEnable = true;

    [LabelText("キー削除時にオブジェクトを非表示")]
    public bool deactivateOnRemove = true;

    [InfoBox("インスペクタで値を変更したら即座に SetActive まで反映（エディタ中）。" +
             "プレイモード外でも反映されるので、意図しない非表示に注意。", InfoMessageType.Info)]
    [LabelText("インスペクタ変更で自動反映（Editor）")]
    public bool autoApplyInEditor = true;
    [SerializeField] private AudioSource ItemEquipAudioSource;
    private void OnEnable()
    {
        if (applyOnEnable)
            ApplyActiveStates();
    }

#if UNITY_EDITOR
    // インスペクタ変更時（エディタ）に反映
    private void OnValidate()
    {
        if (!autoApplyInEditor) return;

        // Prefabステージや編集中でも反映したいので条件は緩め
        try { ApplyActiveStates(); }
        catch { /* 編集タイミングで例外が出ても握りつぶさない方針だが、OnValidateは黙っておく */ }
    }
#endif

    // ===== Public API =====

    /// <summary>hasItem に基づいて全アイテムの SetActive を反映。</summary>
    [Button(ButtonSizes.Medium), GUIColor(0.7f, 1f, 0.7f)]
    [LabelText("全アイテムの表示状態を反映")]
    public void ApplyActiveStates()
    {
        foreach (var kv in items)
        {
            var key = kv.Key;
            var data = kv.Value;

            if (data == null)
            {
                Debug.LogWarning($"[ItemEquipController] '{key}' のデータが null。", this);
                continue;
            }

            if (data.obj == null)
            {
                Debug.LogWarning($"[ItemEquipController] '{key}' の GameObject が未設定。", this);
                continue;
            }
            data.obj.SetActive(data.hasItem);
        }
    }

    /// <summary>キーを指定して追加/更新。hasItemに応じて即時反映可。</summary>
    [ButtonGroup("crud"), LabelText("追加/更新"), GUIColor(0.6f, 0.9f, 1f)]
    public void AddOrUpdateItem(string key, GameObject go, bool hasItem, bool applyImmediately = true)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("key が空です。");
        Debug.Assert(go != null, "[ItemEquipController] AddOrUpdateItem: go が null。");

        if (!items.TryGetValue(key, out var data) || data == null)
        {
            data = new ItemData();
            items[key] = data;
        }

        data.obj = go;
        data.hasItem = hasItem;

        if (applyImmediately && go != null)
            go.SetActive(hasItem);
    }

    /// <summary>キーを削除。Optionでオブジェクトを非表示に。</summary>
    [ButtonGroup("crud"), LabelText("削除"), GUIColor(1f, 0.6f, 0.6f)]
    public bool RemoveItem(string key)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("key が空です。");

        if (items.TryGetValue(key, out var data))
        {
            if (deactivateOnRemove && data?.obj != null)
                data.obj.SetActive(false);

            return items.Remove(key);
        }
        return false;
    }
    /// <summary>キーの所持フラグだけ更新（即時反映可）。</summary>
    [ButtonGroup("crud"), LabelText("所持切替(適用可)")]
    public void SetHasItem(string key)
    {
        if (!items.TryGetValue(key, out var data) || data == null)
            throw new KeyNotFoundException($"キー '{key}' は未登録。");
        ApplyActiveState(key);
        data.hasItem = true;
        ItemEquipAudioSource.Play();
        data.obj.SetActive(true);
    }
    /// <summary>キーの所持フラグだけ更新（即時反映可）。</summary>
    [ButtonGroup("crud"), LabelText("所持切替(適用可)")]
    public void SetHasItem(string key, bool hasItem, bool applyImmediately = true)
    {
        if (!items.TryGetValue(key, out var data) || data == null)
            throw new KeyNotFoundException($"キー '{key}' は未登録。");
        ApplyActiveState(key);
        data.hasItem = hasItem;

        if (applyImmediately && data.obj != null)
            data.obj.SetActive(hasItem);
    }

    /// <summary>所持中か？</summary>
    public bool HasItem(string key)
    {
        return items.TryGetValue(key, out var data) && data != null && data.hasItem;
    }

    /// <summary>GameObject取得（未登録なら null）。</summary>
    public GameObject GetItemObject(string key)
    {
        return items.TryGetValue(key, out var data) ? data?.obj : null;
    }

    /// <summary>特定キーだけ反映。</summary>
    public void ApplyActiveState(string key)
    {
        if (!items.TryGetValue(key, out var data) || data == null)
            throw new KeyNotFoundException($"キー '{key}' は未登録。");

        ApplyActiveState(data);
    }

    /// <summary>辞書データを直接渡して反映（内部用）。</summary>
    private void ApplyActiveState(ItemData data)
    {
        if (data.obj == null)
        {
            Debug.LogWarning("[ItemEquipController] GameObject が未設定。", this);
            return;
        }
        data.obj.SetActive(data.hasItem);
    }

    /// <summary>全クリア（必要なら全て非表示）。</summary>
    [Button(ButtonSizes.Small), GUIColor(1f, 0.8f, 0.5f)]
    [LabelText("全クリア（任意で非表示）")]
    public void ClearAll(bool deactivateObjects = true)
    {
        if (deactivateObjects)
        {
            foreach (var kv in items)
            {
                var data = kv.Value;
                if (data?.obj != null)
                    data.obj.SetActive(false);
            }
        }
        items.Clear();
    }
}
