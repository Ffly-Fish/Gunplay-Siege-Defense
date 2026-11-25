using System.Collections.Generic;
using UnityEngine;

// TrackSystem.cs —— 埋点（抖音小游戏数据上报的统一入口，当前先本地记录）
namespace Gunplay.Run
{
    /// <summary>埋点系统：统一事件名并交给平台适配层上报</summary>
    public sealed class TrackSystem
    {
        // 是否输出调试日志
        private readonly bool verbose;
        // 本地记录最近的事件（调试 / 无平台时用）
        private readonly List<string> recent = new List<string>(64);

        /// <summary>构造：verbose 为 true 时输出 Debug.Log</summary>
        public TrackSystem(bool verboseLogging)
        {
            verbose = verboseLogging;
        }

        /// <summary>上报一个事件（payload 为 JSON 字符串，可为空）</summary>
        public void Track(string eventName, string payload = null)
        {
            string line = string.IsNullOrEmpty(payload) ? eventName : $"{eventName}:{payload}";
            recent.Add(line);
            if (recent.Count > 64)
            {
                recent.RemoveAt(0);
            }

            if (verbose)
            {
                Debug.Log($"[Track] {line}");
            }

            // TODO: 接入抖音小游戏 SDK 的 tt.reportAnalytics（需在 Assets/Plugins/WebGL 放 .jslib 桥接）
        }

        /// <summary>最近上报的事件（调试用）</summary>
        public IReadOnlyList<string> Recent => recent;
    }
}
