# Gunplay Siege Defense — 项目约定

> 本文件是本项目的长期约定，任何人都必须遵守。修改规则前先与用户确认。

## 一、用户强制要求（2025-11-25 下达）

1. **命名规范**：变量名、方法名、对象名、类名等命名，必须符合 C# / Unity 命名规范（字段 camelCase、属性/方法/类 PascalCase、常量 UPPER_SNAKE 或 PascalCase 保持一致），同时要简洁、易懂。
2. **Inspector 暴露变量**：所有暴露到 Unity 面板的变量，都必须拥有 `[Header("")]`、`[Tooltip("")]`，且用中文表明其名称和作用。
3. **注释**：所有变量、方法、对象、类，都必须有对应的 `///` 或 `//` 注释，写清楚作用。
4. **待办标记**：如有需要留空的地方，请用 `// TODO:` 标记，并说明内容。
5. **及时沟通**：如果有需要用户帮忙、操作的地方，随时停下来向用户说明。

## 二、项目技术事实

- Unity 版本：2022.3.62f3c1（中国版），2D (URP) 模板；URP 14.0.12 + Renderer2D，已装 TextMeshPro、Timeline、uGUI、Test Framework、WebGL 支持。
- 目标平台：抖音小游戏（Unity WebGL，竖屏 9:16）；玩法与数值按 `docs/manual_extract.md`（开发手册 v1.2 抽取稿）。
- 主场景：`Assets/Scenes/Main.unity`（由菜单 `Gunplay/1. 重建主场景` 生成，勿手改）。
- 代码结构：`Assets/Scripts/{Core,Combat,Waves,Run,View,UI,Bootstrap}`；编辑器工具在 `Assets/Editor`（namespace `Gunplay.EditorTools`，批处理 `-executeMethod` 必须写全限定名）。
- 数值配置：`Assets/Resources/Config/{weapons,enemies,upgrades,tuning}.json`，改数值不必改代码。
- 字体：`Assets/Resources/Fonts/MainFont.ttf`（HarmonyOS Sans SC 子集化，全量备份 `Tools/MainFont_full.ttf`，重新子集用 `Tools/subset_font.py`）。
- **本机正在打开该工程的 Unity 编辑器会独占工程**：此时不要对真实工程跑 `-batchmode`，一律在克隆沙箱里验证。

## 三、玩法方向（已确定）

- 竖屏定点守城射击：单指按住瞄准、自动齐射、连杀倍率、关间随机商店、无限闯关、每 5 关一个三阶段 Boss。
- 已实现：7 种武器、6 种敌人（含盾兵环绕护盾与 Explosive/Piercing 克制）、城墙/词条/商店/模拟广告/统计存档。
- 美术与音效为程序化生成（`SpriteFactory` / `AudioBus`），可替换为正式素材而不动逻辑层。

## 四、目录约定

- `Assets/Scripts/`：运行时代码；`Assets/Editor/`：编辑器工具；`Assets/Scenes/`：场景；`Assets/Resources/`：配置与字体。
- `Tools/`：工程级脚本（字体子集化等），不在 `Assets` 下、不进包体。
- `docs/`：手册抽取稿、交付说明、验收截图。
- 不使用中文文件名，避免 Unity 导入与 Git 的编码问题。

## 五、构建与验证流水线（重要）

真实工程被编辑器占用，验证在克隆 `E:/UnityProject/_ci/GunplayCI` 里做（`E:/UnityProject/_ci/ci_compile.sh <全限定入口>` 先镜像代码再跑批处理）：

1. **离线编译门（最快）**：`gen_rsp.py` 生成响应文件后 `dotnet exec <Roslyn>/csc.dll @offline/compile.rsp`，要求 0 error / 0 warning。
2. **逻辑自检**：`Gunplay.EditorTools.CiSelfTest.Run` → `E:/UnityProject/_ci/selftest.log`（SELF_TEST_OK）。
3. **场景校验**：`Gunplay.EditorTools.CiSceneCheck.Run` → `E:/UnityProject/_ci/scenecheck.log`（SCENE_CHECK_OK）。
4. **出包**：`Gunplay.EditorTools.WebGLBuilder.BuildWebGL` → `Build/WebGL` + `Build/Douyin/gunplay-siege-defense`（后处理自动写 `game.json`/`project.config.json` 并注入全屏样式）。
5. **浏览器验收**：本地静态服务器 + Edge（可见窗口 + `--remote-debugging-port`）；`document.hidden` 必须为 false，否则 Unity 主循环暂停、输入无效。
6. **回传**：`Build/`、`Assets/Scenes/Main.unity`、新增脚本的 `.cs.meta` 一起同步到真实工程（缺 `.meta` 会让场景引用变 Missing Script）。

## 六、当前待办

- [ ] 抖音平台适配层（`tt` 的 canvas / 输入 / 音频桥接）待接，目前是标准 WebGL 包 + 抖音小游戏工程外壳。
- [ ] `project.config.json` 里的 `appid` 需填真实小游戏 appid。
- [ ] 正式美术 / 音效素材替换程序化占位；真实广告 SDK 接入。
