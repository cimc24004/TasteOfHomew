// ControlChangeLogger.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class ControlChangeLogger : MonoBehaviour
{
    Dictionary<InputControl, float> lastValues = new Dictionary<InputControl, float>();
    void Start()
    {
        Debug.Log("=== ControlChangeLogger Start ===");
        foreach (var d in InputSystem.devices)
        {
            Debug.Log($"Device: {d.displayName} (layout={d.layout}) path={d.path}");
            foreach (var c in d.allControls)
            {
                lastValues[c] = GetControlValue(c);
            }
        }
    }

    void Update()
    {
        foreach (var kv in new List<InputControl>(lastValues.Keys))
        {
            var c = kv;
            float v = GetControlValue(c);
            float prev = lastValues[c];
            if (Mathf.Abs(v - prev) > 0.02f) // 変化閾値（調整可）
            {
                Debug.Log($"ControlChanged: device='{c.device.displayName}' layout='{c.device.layout}' controlPath='{c.path}' name='{c.name}' value={v}");
                lastValues[c] = v;
            }
        }
    }

    float GetControlValue(InputControl c)
    {
        if (c is AxisControl ac) return ac.ReadValue();
        if (c is ButtonControl bc) return bc.isPressed ? 1f : 0f;
        return 0f;
    }
}
