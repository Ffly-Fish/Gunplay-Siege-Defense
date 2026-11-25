using System;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

// DouyinBridge.cs —— 抖音小游戏平台桥接（编辑器/普通浏览器自动降级，不抛异常）
namespace Gunplay.Platform
{
    /// <summary>抖音（tt）桥接：SDK 检测、激励视频、分享、启动参数（WebGL 下调用 Assets/Plugins/WebGL/GunplayDouyin.jslib）</summary>
    public static class DouyinBridge
    {
        /// <summary>金币翻倍激励视频广告位 Id</summary>
        // TODO: 在抖音开发者后台创建激励视频广告位后，把占位值替换成真实的 adUnitId（否则 load/show 失败，按「广告未看完」处理）
        public const string PlacementGoldDouble = "adunit-gold-double";

        /// <summary>免费重刷激励视频广告位 Id</summary>
        // TODO: 同上，换成免费重刷的 adUnitId
        public const string PlacementFreeReroll = "adunit-free-reroll";

        /// <summary>接收 JS SendMessage 回传的隐藏物体名（与 jslib 约定，勿改）</summary>
        private const string ReceiverObjectName = "GunplayDouyinReceiver";

        /// <summary>接收 JS SendMessage 回传的方法名（与 jslib 约定，勿改）</summary>
        private const string ReceiverMethodName = "OnRewardedVideoClosed";

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>JS：环境中是否存在 tt SDK（1/0）</summary>
        [DllImport("__Internal")]
        private static extern int GunplayDouyin_HasSdk();

        /// <summary>JS：播放激励视频（结束后 SendMessage 回传 isEnded）</summary>
        [DllImport("__Internal")]
        private static extern void GunplayDouyin_ShowRewardedVideo(string placementId, string objectName, string callbackMethod);

        /// <summary>JS：发起分享（1 成功发起 / 0 失败）</summary>
        [DllImport("__Internal")]
        private static extern int GunplayDouyin_Share(string title);

        /// <summary>JS：读取启动参数 JSON（返回 malloc 指针，需用 GunplayDouyin_Free 释放）</summary>
        [DllImport("__Internal")]
        private static extern IntPtr GunplayDouyin_GetLaunchQuery();

        /// <summary>JS：释放启动参数字符串内存</summary>
        [DllImport("__Internal")]
        private static extern void GunplayDouyin_Free(IntPtr pointer);

        /// <summary>隐藏的广告结果接收器（跨场景保留）</summary>
        private static RewardedAdReceiver receiver;

        /// <summary>当前等待结果的激励视频回调（同一时刻只允许一个广告）</summary>
        private static Action<bool> pendingCallback;
#endif

        /// <summary>当前是否运行在抖音小游戏环境（存在 tt SDK）</summary>
        public static bool HasDouyinSdk
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                try
                {
                    return GunplayDouyin_HasSdk() != 0;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[Douyin] 检测 tt SDK 失败: " + exception.Message);
                    return false;
                }
#else
                return false;
#endif
            }
        }

        /// <summary>激励视频是否可播放（SDK 就绪即可尝试；真实广告库存由 JS 在播放时加载）</summary>
        public static bool IsRewardedAdReady => HasDouyinSdk;

        /// <summary>播放激励视频：看完回调 true；中途关闭、加载失败、SDK 未就绪均回调 false</summary>
        public static void ShowRewardedVideo(string placementId, Action<bool> onResult)
        {
            if (onResult == null)
            {
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            if (!HasDouyinSdk)
            {
                InvokeSafely(onResult, false);
                return;
            }

            if (pendingCallback != null)
            {
                Debug.LogWarning("[Douyin] 上一个激励视频尚未结束，本次直接按未看完处理");
                InvokeSafely(onResult, false);
                return;
            }

            EnsureReceiver();
            if (receiver == null)
            {
                InvokeSafely(onResult, false);
                return;
            }

            pendingCallback = onResult;
            try
            {
                GunplayDouyin_ShowRewardedVideo(placementId ?? string.Empty, ReceiverObjectName, ReceiverMethodName);
            }
            catch (Exception exception)
            {
                pendingCallback = null;
                Debug.LogWarning("[Douyin] 调用激励视频失败: " + exception.Message);
                InvokeSafely(onResult, false);
            }
#else
            // 编辑器 / CI / 普通浏览器没有 tt：直接按未看完处理，绝不影响主流程
            InvokeSafely(onResult, false);
#endif
        }

        /// <summary>分享（标题由调用方给出）：发起成功回调 true，未接入 SDK 或调用失败回调 false</summary>
        public static void Share(string title, Action<bool> onResult)
        {
            if (onResult == null)
            {
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            bool shared = false;
            try
            {
                shared = GunplayDouyin_Share(title ?? string.Empty) != 0;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Douyin] 调用分享失败: " + exception.Message);
            }

            InvokeSafely(onResult, shared);
#else
            InvokeSafely(onResult, false);
#endif
        }

        /// <summary>读取抖音启动参数（query 的 JSON；非抖音环境返回空字符串）</summary>
        public static string GetLaunchQuery()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            IntPtr pointer = IntPtr.Zero;
            try
            {
                pointer = GunplayDouyin_GetLaunchQuery();
                return pointer == IntPtr.Zero ? string.Empty : (Marshal.PtrToStringAnsi(pointer) ?? string.Empty);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Douyin] 读取启动参数失败: " + exception.Message);
                return string.Empty;
            }
            finally
            {
                if (pointer != IntPtr.Zero)
                {
                    try
                    {
                        GunplayDouyin_Free(pointer);
                    }
                    catch (Exception)
                    {
                        // 释放失败无需处理：进程结束时内存一并回收
                    }
                }
            }
#else
            return string.Empty;
#endif
        }

        /// <summary>把广告回调安全地投递给业务层（业务异常不影响 Unity 主循环）</summary>
        private static void InvokeSafely(Action<bool> callback, bool result)
        {
            if (callback == null)
            {
                return;
            }

            try
            {
                callback(result);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Douyin] 广告回调处理异常: " + exception);
            }
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>确保隐藏的广告结果接收器存在（DontDestroyOnLoad）</summary>
        private static void EnsureReceiver()
        {
            if (receiver != null)
            {
                return;
            }

            GameObject host = new GameObject(ReceiverObjectName);
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideInHierarchy;
            receiver = host.AddComponent<RewardedAdReceiver>();
        }

        /// <summary>接收 JS 侧 SendMessage 回传的隐藏组件（无需暴露到 Inspector）</summary>
        private sealed class RewardedAdReceiver : MonoBehaviour
        {
            /// <summary>JS 侧 onClose / onError 回传入口（"1" 看完，"0" 未看完）</summary>
            public void OnRewardedVideoClosed(string result)
            {
                Action<bool> callback = pendingCallback;
                pendingCallback = null;
                InvokeSafely(callback, result == "1");
            }
        }
#endif
    }
}
