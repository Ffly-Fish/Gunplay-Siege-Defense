using Gunplay.Core;
using UnityEngine;

// AimInput.cs —— 单指瞄准输入：抖音 / 移动端用第一根手指，编辑器用鼠标左键模拟
namespace Gunplay.View
{
    /// <summary>
    /// 单指瞄准输入实现（实现 Gunplay.View.IAimInput）。
    /// 规则：第一根按下的手指锁定为瞄准手指；世界坐标由正交相机反算；
    /// 手指落在屏幕底部「炮台区」时视为按在炮台上，只保持开火、不改变瞄准方向；手指抬起立即停止瞄准。
    /// </summary>
    public sealed class AimInput : MonoBehaviour, IAimInput
    {
        /// <summary>屏幕高度比例的下限（避免配置为负）</summary>
        private const float MinCannonZoneRatio = 0f;
        /// <summary>屏幕高度比例的上限（避免整块屏幕都算炮台区）</summary>
        private const float MaxCannonZoneRatio = 0.5f;
        /// <summary>相机深度兜底值（相机不在原点时的默认投影距离）</summary>
        private const float FallbackCameraDepth = 10f;

        /// <summary>进行屏幕坐标转换的相机（留空则用 Camera.main）</summary>
        [Header("相机")]
        [Tooltip("把屏幕坐标换算成世界坐标的相机；留空时自动使用 Camera.main")]
        [SerializeField]
        private Camera targetCamera;

        /// <summary>屏幕底部多大比例算「炮台区」</summary>
        [Header("触摸规则")]
        [Tooltip("屏幕底部该比例区域视为「按在炮台」：保持开火但不改变瞄准方向（0~0.5）")]
        [Range(MinCannonZoneRatio, MaxCannonZoneRatio)]
        [SerializeField]
        private float cannonZoneHeightRatio = 0.2f;

        /// <summary>触点离炮口太近时视为无效瞄准的最小距离（世界单位）</summary>
        [Tooltip("触点离炮口小于该距离（世界单位）时视为无效瞄准，沿用上一次方向")]
        [SerializeField]
        private float minAimDistance = 0.25f;

        /// <summary>没有触摸设备时是否允许用鼠标左键模拟</summary>
        [Header("编辑器模拟")]
        [Tooltip("没有触摸设备时用鼠标左键按住模拟单指瞄准（编辑器 / PC 调试用）")]
        [SerializeField]
        private bool enableMouseSimulation = true;

        /// <summary>未输入时的默认瞄准方向</summary>
        [Tooltip("初始瞄准方向（未输入时的默认朝向，默认正上方）")]
        [SerializeField]
        private Vector2 initialDirection = Vector2.up;

        /// <summary>当前锁定的手指 id（-1 表示未锁定）</summary>
        private int activeFingerId = -1;
        /// <summary>当前是否有指针（手指或鼠标）按住</summary>
        private bool pointerActive;
        /// <summary>当前是否处于瞄准（= 按住即为 true，抬起即 false）</summary>
        private bool isAiming;
        /// <summary>当前瞄准点的世界坐标</summary>
        private Vector2 aimWorldPos;
        /// <summary>当前瞄准方向（单位向量，从炮口指向瞄准点）</summary>
        private Vector2 aimDirection;
        /// <summary>缓存后的可用相机</summary>
        private Camera resolvedCamera;

        /// <summary>是否处于瞄准状态（IAimInput）</summary>
        public bool IsAiming
        {
            get { return isAiming; }
        }

        /// <summary>瞄准点世界坐标（IAimInput）</summary>
        public Vector2 AimWorldPos
        {
            get { return aimWorldPos; }
        }

        /// <summary>瞄准方向单位向量（炮口 → 瞄准点），供表现层画准星线用</summary>
        public Vector2 AimDirection
        {
            get { return aimDirection; }
        }

        /// <summary>当前是否真有指针按住（区分「保持开火」与「无输入」）</summary>
        public bool HasPointer
        {
            get { return pointerActive; }
        }

        /// <summary>初始化默认方向与瞄准点</summary>
        private void Awake()
        {
            aimDirection = initialDirection.sqrMagnitude > 0.0001f ? initialDirection.normalized : Vector2.up;
            aimWorldPos = BattleField.Muzzle + aimDirection * 4f;
            ResolveCamera();
        }

        /// <summary>启用时恢复默认方向</summary>
        private void OnEnable()
        {
            aimDirection = initialDirection.sqrMagnitude > 0.0001f ? initialDirection.normalized : Vector2.up;
        }

        /// <summary>禁用时立即停止瞄准，避免切场景后仍处于开火状态</summary>
        private void OnDisable()
        {
            Release();
        }

        /// <summary>每帧读取输入（触摸优先，其次鼠标模拟）</summary>
        private void Update()
        {
            ResolveCamera();
            if (resolvedCamera == null)
            {
                return;
            }

            if (TryHandleTouch())
            {
                return;
            }

            if (enableMouseSimulation && TryHandleMouse())
            {
                return;
            }

            // 完全没有输入：停止瞄准（齐射停止）
            if (!pointerActive && isAiming)
            {
                Release();
            }
        }

        /// <summary>处理触摸输入（锁定第一根手指）；返回 false 表示本帧没有触摸</summary>
        private bool TryHandleTouch()
        {
            if (Input.touchCount <= 0)
            {
                if (activeFingerId >= 0 && !enableMouseSimulation)
                {
                    activeFingerId = -1;
                }

                return false;
            }

            bool found = false;
            Touch tracked = default(Touch);

            if (activeFingerId < 0)
            {
                // 首次按下：取第一根手指并锁定
                tracked = Input.GetTouch(0);
                activeFingerId = tracked.fingerId;
                found = true;
            }
            else
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.fingerId == activeFingerId)
                    {
                        tracked = touch;
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                // 锁定的手指已抬起：停止瞄准
                activeFingerId = -1;
                Release();
                return false;
            }

            if (tracked.phase == TouchPhase.Ended || tracked.phase == TouchPhase.Canceled)
            {
                Release();
                activeFingerId = -1;
                return true;
            }

            ProcessPointer(tracked.position);
            return true;
        }

        /// <summary>处理鼠标模拟输入；返回 false 表示本帧没有鼠标输入</summary>
        private bool TryHandleMouse()
        {
            if (Input.GetMouseButtonDown(0))
            {
                ProcessPointer(Input.mousePosition);
                return true;
            }

            if (Input.GetMouseButton(0) && pointerActive)
            {
                ProcessPointer(Input.mousePosition);
                return true;
            }

            if (Input.GetMouseButtonUp(0) && pointerActive)
            {
                Release();
                return true;
            }

            return false;
        }

        /// <summary>把屏幕坐标处理成瞄准状态：炮台区不改变方向，其余区域更新方向</summary>
        /// <param name="screenPosition">屏幕坐标（像素）</param>
        private void ProcessPointer(Vector2 screenPosition)
        {
            pointerActive = true;
            isAiming = true;

            float zoneHeight = Mathf.Clamp(cannonZoneHeightRatio, MinCannonZoneRatio, MaxCannonZoneRatio) * Screen.height;
            if (screenPosition.y <= zoneHeight)
            {
                // 按在炮台：保持上一次的瞄准方向，只维持开火
                return;
            }

            Vector3 world = ScreenToWorld(screenPosition);
            aimWorldPos = new Vector2(world.x, world.y);

            Vector2 toPoint = aimWorldPos - BattleField.Muzzle;
            if (toPoint.sqrMagnitude >= minAimDistance * minAimDistance)
            {
                aimDirection = toPoint.normalized;
            }
            // 距离炮口过近时沿用上一次方向，避免指向抖动导致齐射乱转
        }

        /// <summary>屏幕坐标 → 世界坐标（含 0.5 像素取整，降低抖动）</summary>
        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            float depth = Mathf.Abs(resolvedCamera.transform.position.z);
            if (depth <= 0.01f)
            {
                depth = FallbackCameraDepth;
            }

            Vector3 world = resolvedCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
            world.z = 0f;
            return world;
        }

        /// <summary>结束瞄准</summary>
        private void Release()
        {
            pointerActive = false;
            isAiming = false;
            activeFingerId = -1;
        }

        /// <summary>解析可用相机（targetCamera 未配置时回退 Camera.main）</summary>
        private void ResolveCamera()
        {
            if (targetCamera != null)
            {
                resolvedCamera = targetCamera;
                return;
            }

            if (resolvedCamera != null && resolvedCamera.isActiveAndEnabled)
            {
                return;
            }

            resolvedCamera = Camera.main;
        }
    }
}
