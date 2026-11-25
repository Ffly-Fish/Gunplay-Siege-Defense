using UnityEngine;

// IAimInput.cs —— 瞄准输入抽象（模拟层 GameRuntime 只依赖本接口，表现层 AimInput 提供实现）
namespace Gunplay.View
{
    /// <summary>
    /// 瞄准输入抽象：模拟层（GameRuntime）只依赖此接口，不直接读 Input / 触摸。
    /// 实现方（AimInput）负责把手指 / 鼠标位置转换为世界坐标并记录是否处于瞄准状态。
    /// </summary>
    public interface IAimInput
    {
        /// <summary>当前是否有手指（或鼠标）按住处于瞄准状态；false 时停止开火</summary>
        bool IsAiming { get; }

        /// <summary>当前瞄准点的世界坐标（最近一次有效输入的世界位置）</summary>
        Vector2 AimWorldPos { get; }
    }
}
