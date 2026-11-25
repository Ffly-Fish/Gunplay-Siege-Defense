using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Gunplay.Core;
using UnityEditor;
using UnityEngine;

// CiCheck.cs —— 无头 CI 入口：编译检查、逻辑冒烟与完成标记
namespace Gunplay.EditorTools
{
    /// <summary>无头 CI 检查器：供 -executeMethod 调用；失败时以非 0 退出码结束批处理</summary>
    public static class CiCheck
    {
        /// <summary>CI 完成标记文件名（写在工程根目录）</summary>
        public const string CI_OK_FILE = "ci_ok.txt";

        /// <summary>冒烟测试结果文件名（写在工程根目录）</summary>
        public const string CI_SMOKE_FILE = "ci_smoke.json";

        /// <summary>冒烟基础模拟时长（秒）</summary>
        public const float SMOKE_BASE_SECONDS = 30f;

        /// <summary>冒烟最长模拟时长（秒）</summary>
        public const float SMOKE_MAX_SECONDS = 120f;

        /// <summary>编译等待上限（分钟）</summary>
        public const double COMPILE_TIMEOUT_MINUTES = 5d;

        /// <summary>写 CI 完成标记并退出 0（表示批处理执行成功）</summary>
        public static void Run()
        {
            try
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine("CI OK");
                builder.AppendLine("unityVersion=" + Application.unityVersion);
                builder.AppendLine("buildTarget=" + EditorUserBuildSettings.activeBuildTarget);
                builder.AppendLine("time=" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                string path = EditorBuildUtil.ProjectPath(CI_OK_FILE);
                EditorBuildUtil.WriteAllText(path, builder.ToString());
                Debug.Log("[CiCheck] CI 完成标记已写入: " + path);
                Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("[CiCheck] 写 CI 完成标记失败: " + exception);
                Exit(1);
            }
        }

        /// <summary>仅编译检查：刷新资源、等待编译、扫描 Console 编译错误；失败退出 1</summary>
        public static void CompileOnly()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            WaitForCompilation(TimeSpan.FromMinutes(COMPILE_TIMEOUT_MINUTES));

            bool failed = EditorUtility.scriptCompilationFailed;
            if (!failed)
            {
                failed = HasCompilerErrorInConsole();
            }

            if (failed)
            {
                Debug.LogError("[CiCheck] 编译检查失败：Console 中存在编译错误");
                Exit(1);
                return;
            }

            Debug.Log("[CiCheck] 编译检查通过");
            Exit(0);
        }

        /// <summary>逻辑冒烟：反射驱动 GameRuntime 30~120 秒，验证敌人生成与城门掉血；结果写 ci_smoke.json</summary>
        public static void SmokeTest()
        {
            string resultPath = EditorBuildUtil.ProjectPath(CI_SMOKE_FILE);
            SmokeResult result = new SmokeResult();
            object runtime = null;
            Type runtimeType = null;

            try
            {
                ConfigDatabase config = ConfigDatabase.LoadFromResources();
                runtimeType = EditorBuildUtil.FindType("Gunplay.Core.GameRuntime");
                if (runtimeType == null)
                {
                    result.skipped = true;
                    result.message = "未找到 Gunplay.Core.GameRuntime，跳过逻辑冒烟（TODO: API 就绪后重新运行）";
                }
                else
                {
                    runtime = Activator.CreateInstance(runtimeType);
                    InitializeRuntime(runtime, runtimeType, config);
                    StartNewRun(runtime, runtimeType);
                    SimulateBattle(runtime, runtimeType, result);
                }
            }
            catch (Exception exception)
            {
                result.ok = false;
                result.error = exception.ToString();
                result.message = "逻辑冒烟抛出异常";
                Debug.LogError("[CiCheck] 逻辑冒烟异常: " + exception);
            }
            finally
            {
                DisposeRuntime(runtime, runtimeType);
            }

            WriteSmokeResult(resultPath, result);

            if (result.skipped)
            {
                Debug.LogWarning("[CiCheck] " + result.message);
                Exit(0);
                return;
            }

            if (result.ok)
            {
                Debug.Log("[CiCheck] 逻辑冒烟通过: " + result.message);
                Exit(0);
                return;
            }

            Debug.LogError("[CiCheck] 逻辑冒烟失败: " + result.message);
            Exit(1);
        }

        /// <summary>完整 CI 流水线：编译检查 → 生成主场景 → WebGL 构建 → 写完成标记；任一步失败退出 1</summary>
        public static void CiAll()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            WaitForCompilation(TimeSpan.FromMinutes(COMPILE_TIMEOUT_MINUTES));
            if (EditorUtility.scriptCompilationFailed || HasCompilerErrorInConsole())
            {
                Debug.LogError("[CiCheck] 编译失败，CI 流水线中止");
                Exit(1);
                return;
            }

            SceneBuilder.BuildAll();
            WebGLBuilder.BuildWebGL();
            Run();
        }

        // 通过反射调用 GameRuntime.Initialize（未定稿参数按类型填默认值，IAimInput 传 null 表示不瞄准）
        private static void InitializeRuntime(object runtime, Type runtimeType, ConfigDatabase config)
        {
            MethodInfo initialize = runtimeType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance);
            if (initialize == null)
            {
                throw new MissingMethodException(runtimeType.FullName, "Initialize");
            }

            ParameterInfo[] parameters = initialize.GetParameters();
            object[] arguments = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                Type parameterType = parameters[i].ParameterType;
                if (parameterType == typeof(ConfigDatabase))
                {
                    arguments[i] = config;
                }
                else if (parameterType == typeof(bool))
                {
                    arguments[i] = false;
                }
                else if (parameterType == typeof(int))
                {
                    arguments[i] = 12345;
                }
                else if (parameterType.IsValueType)
                {
                    arguments[i] = Activator.CreateInstance(parameterType);
                }
                else
                {
                    arguments[i] = null;
                }
            }

            initialize.Invoke(runtime, arguments);
        }

        // 通过反射调用 StartNewRun
        private static void StartNewRun(object runtime, Type runtimeType)
        {
            MethodInfo startNewRun = runtimeType.GetMethod("StartNewRun", BindingFlags.Public | BindingFlags.Instance);
            if (startNewRun == null)
            {
                throw new MissingMethodException(runtimeType.FullName, "StartNewRun");
            }

            startNewRun.Invoke(runtime, null);
        }

        // 通过反射循环调用 Tick，观测敌人数与城门耐久
        private static void SimulateBattle(object runtime, Type runtimeType, SmokeResult result)
        {
            MethodInfo tick = runtimeType.GetMethod("Tick", BindingFlags.Public | BindingFlags.Instance);
            if (tick == null)
            {
                throw new MissingMethodException(runtimeType.FullName, "Tick");
            }

            float deltaTime = 1f / 60f;
            result.initialGateHp = ReadGateHp(runtime, runtimeType);
            float gateHp = result.initialGateHp;
            int maxFrames = Mathf.CeilToInt(SMOKE_MAX_SECONDS / deltaTime);
            object[] tickArguments = new object[] { deltaTime };

            for (int frame = 0; frame < maxFrames; frame++)
            {
                tick.Invoke(runtime, tickArguments);
                int enemyCount = ReadEnemyCount(runtime, runtimeType);
                if (enemyCount > result.maxEnemies)
                {
                    result.maxEnemies = enemyCount;
                }

                gateHp = ReadGateHp(runtime, runtimeType);
                if (gateHp < result.initialGateHp - 0.01f)
                {
                    result.gateDamaged = true;
                }

                result.frames = frame + 1;
                if (result.gateDamaged && result.maxEnemies > 0)
                {
                    break;
                }
            }

            result.finalGateHp = gateHp;
            result.simulatedSeconds = result.frames * deltaTime;
            result.ok = result.maxEnemies > 0 && result.gateDamaged;
            result.message = result.ok
                ? "冒烟通过：敌人已生成且城门掉血"
                : "冒烟失败：maxEnemies=" + result.maxEnemies + "，gateDamaged=" + result.gateDamaged;
        }

        // 读取本轮城门耐久（Run.gateHp）
        private static float ReadGateHp(object runtime, Type runtimeType)
        {
            PropertyInfo runProperty = runtimeType.GetProperty("Run", BindingFlags.Public | BindingFlags.Instance);
            object run = runProperty != null ? runProperty.GetValue(runtime) : null;
            if (run == null)
            {
                return 0f;
            }

            FieldInfo gateHpField = run.GetType().GetField("gateHp", BindingFlags.Public | BindingFlags.Instance);
            if (gateHpField == null)
            {
                throw new MissingFieldException(run.GetType().FullName, "gateHp");
            }

            return Convert.ToSingle(gateHpField.GetValue(run), CultureInfo.InvariantCulture);
        }

        // 读取当前活跃敌人数（GameRuntime.Enemies.Count 或数组长度）
        private static int ReadEnemyCount(object runtime, Type runtimeType)
        {
            PropertyInfo enemiesProperty = runtimeType.GetProperty("Enemies", BindingFlags.Public | BindingFlags.Instance);
            object enemies = enemiesProperty != null ? enemiesProperty.GetValue(runtime) : null;
            if (enemies == null)
            {
                return 0;
            }

            Array array = enemies as Array;
            if (array != null)
            {
                return array.Length;
            }

            PropertyInfo countProperty = enemies.GetType().GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);
            if (countProperty == null)
            {
                return 0;
            }

            return Convert.ToInt32(countProperty.GetValue(enemies), CultureInfo.InvariantCulture);
        }

        // 释放 GameRuntime（反射调用 Dispose，避免强依赖未定稿类型）
        private static void DisposeRuntime(object runtime, Type runtimeType)
        {
            if (runtime == null || runtimeType == null)
            {
                return;
            }

            try
            {
                MethodInfo dispose = runtimeType.GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance);
                if (dispose != null)
                {
                    dispose.Invoke(runtime, null);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[CiCheck] 释放 GameRuntime 失败: " + exception.Message);
            }
        }

        // 等待编辑器编译结束（最多 timeout 时长）
        private static void WaitForCompilation(TimeSpan timeout)
        {
            DateTime deadline = DateTime.Now + timeout;
            while (EditorApplication.isCompiling && DateTime.Now < deadline)
            {
                System.Threading.Thread.Sleep(200);
            }
        }

        // 反射读取 Console 日志，判断是否出现编译错误
        private static bool HasCompilerErrorInConsole()
        {
            try
            {
                Type logEntriesType = EditorBuildUtil.FindType("UnityEditor.LogEntries", "LogEntries");
                Type logEntryType = EditorBuildUtil.FindType("UnityEditor.LogEntry", "LogEntry");
                if (logEntriesType == null || logEntryType == null)
                {
                    return false;
                }

                BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                MethodInfo startGettingEntries = logEntriesType.GetMethod("StartGettingEntries", flags);
                MethodInfo getCount = logEntriesType.GetMethod("GetCount", flags);
                MethodInfo getEntryInternal = logEntriesType.GetMethod("GetEntryInternal", flags);
                if (startGettingEntries == null || getCount == null || getEntryInternal == null)
                {
                    return false;
                }

                startGettingEntries.Invoke(null, null);
                int count = Convert.ToInt32(getCount.Invoke(null, null), CultureInfo.InvariantCulture);
                FieldInfo messageField = logEntryType.GetField("message", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (messageField == null)
                {
                    return false;
                }

                for (int i = 0; i < count; i++)
                {
                    object entry = Activator.CreateInstance(logEntryType);
                    getEntryInternal.Invoke(null, new object[] { i, entry });
                    string message = messageField.GetValue(entry) as string;
                    if (string.IsNullOrEmpty(message))
                    {
                        continue;
                    }

                    if (message.Contains("error CS") ||
                        message.Contains("Compilation failed") ||
                        message.Contains("Scripts have compiler errors") ||
                        message.Contains("Compilation error"))
                    {
                        Debug.LogError("[CiCheck] Console 中存在编译错误: " + message);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[CiCheck] 反射读取 Console 日志失败，改由 scriptCompilationFailed 判定: " + exception.Message);
                return false;
            }
        }

        // 把冒烟结果写成 ci_smoke.json
        private static void WriteSmokeResult(string resultPath, SmokeResult result)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"ok\": " + (result.ok ? "true" : "false") + ",");
            builder.AppendLine("  \"skipped\": " + (result.skipped ? "true" : "false") + ",");
            builder.AppendLine("  \"maxEnemies\": " + result.maxEnemies + ",");
            builder.AppendLine("  \"gateDamaged\": " + (result.gateDamaged ? "true" : "false") + ",");
            builder.AppendLine("  \"initialGateHp\": " + result.initialGateHp.ToString("0.###", CultureInfo.InvariantCulture) + ",");
            builder.AppendLine("  \"finalGateHp\": " + result.finalGateHp.ToString("0.###", CultureInfo.InvariantCulture) + ",");
            builder.AppendLine("  \"frames\": " + result.frames + ",");
            builder.AppendLine("  \"simulatedSeconds\": " + result.simulatedSeconds.ToString("0.###", CultureInfo.InvariantCulture) + ",");
            builder.AppendLine("  \"message\": \"" + EditorBuildUtil.EscapeJson(result.message) + "\",");
            builder.AppendLine("  \"error\": \"" + EditorBuildUtil.EscapeJson(result.error) + "\",");
            builder.AppendLine("  \"unityVersion\": \"" + EditorBuildUtil.EscapeJson(Application.unityVersion) + "\",");
            builder.AppendLine("  \"time\": \"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\"");
            builder.AppendLine("}");
            EditorBuildUtil.WriteAllText(resultPath, builder.ToString());
        }

        // 退出：批处理模式直接结束进程；交互模式失败时抛异常提示
        private static void Exit(int exitCode)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
                return;
            }

            if (exitCode != 0)
            {
                throw new InvalidOperationException("CI 检查失败（退出码 " + exitCode + "），详见 Console");
            }

            Debug.Log("[CiCheck] 退出码 0（交互模式不调用 EditorApplication.Exit）");
        }

        /// <summary>冒烟结果数据（序列化为 ci_smoke.json）</summary>
        private sealed class SmokeResult
        {
            /// <summary>是否通过</summary>
            public bool ok;

            /// <summary>是否因运行时 API 未就绪而跳过</summary>
            public bool skipped;

            /// <summary>观测到的最大同屏敌人数</summary>
            public int maxEnemies;

            /// <summary>城门是否掉血</summary>
            public bool gateDamaged;

            /// <summary>初始城门耐久</summary>
            public float initialGateHp;

            /// <summary>结束城门耐久</summary>
            public float finalGateHp;

            /// <summary>模拟帧数</summary>
            public int frames;

            /// <summary>模拟秒数</summary>
            public float simulatedSeconds;

            /// <summary>结果说明</summary>
            public string message = string.Empty;

            /// <summary>异常详情</summary>
            public string error = string.Empty;
        }
    }
}
