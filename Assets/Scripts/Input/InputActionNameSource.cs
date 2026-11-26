using System;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization.SmartFormat.Core.Extensions;
using UnityEngine.Localization.SmartFormat.Core.Parsing; // これが必要
using UnityEngine.Localization.SmartFormat.PersistentVariables; // こっちの ISource を使う

// [Serializable]
// [DisplayName("Input Action Name Source")]
// public class InputActionNameSource : ISource
// {
//     [SerializeField]
//     private InputActionAsset m_ActionAsset;

//     // このメソッドが "InputActionName" や "list" など、全てのセレクターで呼び出される
//     public bool TryEvaluateSelector(ISelectorInfo selector)
//     {
//         // === ステップ1: "InputActionName" というセレクター以外は無視する ===
//         // お前の #{InputActionName:FriendGameJump} の「InputActionName」の部分
//         string selectorName = selector.SelectorText;
//         if (selectorName != "InputActionName")
//         {
//             // "これは俺の仕事じゃない" と false を返す
//             return false;
//         }

//         //
//         // === ステップ2: ここに来たら、仕事（InputActionName）だと確定 ===
//         //

//         // お前の #{InputActionName:FriendGameJump} の「FriendGameJump」の部分を取得
//         string actionName = "";
//         if (selector.Format != null)
//         {
//             actionName = selector.Format.GetLiteralText();
//         }

//         // 引数がなかった場合 (例: #{InputActionName})
//         if (string.IsNullOrEmpty(actionName))
//         {
//             selector.Result = "ACTION_NAME_IS_EMPTY";
//             return true; // "処理した" (エラーを返した)
//         }

//         // === ステップ3: いつもの処理（ただし、必ず true を返す） ===
//         if (m_ActionAsset == null)
//         {
//             selector.Result = "NO_ASSET_ASSIGNED";
//             return true; // "処理した"
//         }

//         var action = m_ActionAsset.FindAction(actionName);
//         if (action == null)
//         {
//             selector.Result = $"NOT_FOUND:{actionName}";
//             return true; // "処理した"
//         }

//         if (action.controls.Count == 0)
//         {
//             selector.Result = "NO_BINDING";
//             return true; // "処理した"
//         }

//         int bindingIndex = action.GetBindingIndexForControl(action.controls[0]);
//         if (bindingIndex == -1)
//         {
//              selector.Result = "BINDING_ERROR";
//              return true; // "処理した"
//         }

//         // 正常にキー名を返す
//         selector.Result = action.GetBindingDisplayString(bindingIndex);
// return true; // "俺が正常に処理した" と true を返す
//     }
// }
