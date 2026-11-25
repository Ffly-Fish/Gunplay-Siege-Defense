using UnityEngine;

// UiSafeArea.cs —— 刘海屏安全区适配（把节点收缩到 Screen.safeArea 内）
namespace Gunplay.UI
{
    /// <summary>安全区适配器：跟随 Screen.safeArea 调整自身锚点，避免刘海 / 挖孔遮挡界面</summary>
    [DisallowMultipleComponent]
    public sealed class UiSafeArea : MonoBehaviour
    {
        // 上一次应用的安全区（用于检测变化）
        private Rect lastSafeArea;
        // 上一次应用的屏幕尺寸（用于检测分辨率 / 旋转变化）
        private Vector2Int lastScreenSize;
        // 自身 RectTransform 缓存
        private RectTransform rectTransform;

        private void Awake()
        {
            rectTransform = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea != lastSafeArea || screenSize != lastScreenSize)
            {
                Apply();
            }
        }

        // 把安全区换算成归一化锚点并应用到自身
        private void Apply()
        {
            if (rectTransform == null)
            {
                return;
            }

            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            Rect safeArea = Screen.safeArea;
            lastSafeArea = safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
