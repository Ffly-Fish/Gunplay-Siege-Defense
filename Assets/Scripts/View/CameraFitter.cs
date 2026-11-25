using UnityEngine;

using Gunplay.Core;

// CameraFitter.cs —— 相机视野适配：保证竖屏设备（尤其超长屏）左右战场边界始终完整可见
namespace Gunplay.View
{
    /// <summary>相机适配器：按屏幕宽高比调整正交相机尺寸，避免长屏手机裁切战场左右边界</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFitter : MonoBehaviour
    {
        [Header("设计视野半高")]
        [Tooltip("9:16 设计分辨率下相机可见半高（世界单位）")]
        [SerializeField] private float designHalfHeight = BattleField.HalfHeight;

        [Header("必须完整可见的半宽")]
        [Tooltip("战场左右边界半宽（世界单位）；长屏时相机自动放大以保证它可见")]
        [SerializeField] private float requiredHalfWidth = BattleField.HalfWidth;

        // 相机组件缓存
        private Camera targetCamera;
        // 上次适配时的屏幕尺寸（变化才重新计算）
        private int lastScreenWidth;
        // 上次适配时的屏幕高度
        private int lastScreenHeight;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            Fit();
        }

        private void OnEnable()
        {
            Fit();
        }

        private void Update()
        {
            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
            {
                Fit();
            }
        }

        /// <summary>按当前屏幕宽高比重算正交尺寸：半高 = max(设计半高, 必须半宽 / 宽高比)</summary>
        public void Fit()
        {
            if (targetCamera == null)
            {
                targetCamera = GetComponent<Camera>();
            }

            if (targetCamera == null || !targetCamera.orthographic)
            {
                return;
            }

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;

            float aspect = lastScreenHeight > 0 ? (float)lastScreenWidth / lastScreenHeight : (float)BattleField.HalfWidth / BattleField.HalfHeight;
            float requiredHalfHeight = requiredHalfWidth / Mathf.Max(0.0001f, aspect);
            targetCamera.orthographicSize = Mathf.Max(designHalfHeight, requiredHalfHeight);
        }
    }
}
