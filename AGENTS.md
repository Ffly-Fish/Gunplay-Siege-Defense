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
- 主场景：`Assets/Scenes/Main.unity`（由菜单 `Gunplay/1. 重建主场景` 生成，勿手改）；场景必须挂 5 个根物体：MainCamera、UiRoot、GameBootstrap、AimInput、BattleView。
- 代码结构：`Assets/Scripts/{Core,Combat,Waves,Run,View,UI,Bootstrap}`；编辑器工具在 `Assets/Editor`（namespace `Gunplay.EditorTools`，批处理 `-executeMethod` 必须写全限定名）。
- 数值配置：`Assets/Resources/Config/{weapons,enemies,upgrades,tuning}.json`，改数值不必改代码。
- 枪口与弹道：`BattleField.GetBarrelPivot()`（枪塔旋转原点）/ `GetMuzzleTip(direction)`（旋转后的枪口尖端），子弹、激光、瞄准线、枪口火光都必须用它，否则斜射会穿帮。
- 字体：`Assets/Resources/Fonts/MainFont.ttf`（HarmonyOS Sans SC 子集化，全量备份 `Tools/MainFont_full.ttf`）。**新增中文文案后必须重跑 `Tools/subset_font.py`**，否则缺字会渲染成空白（`CiArtCheck` 有逐字校验会拦住）。
- **本机正在打开该工程的 Unity 编辑器会独占工程**：此时不要对真实工程跑 `-batchmode`，一律在克隆沙箱里验证。

## 三、玩法与美术（已确定）

- 竖屏定点守城射击：单指按住瞄准、自动齐射、连杀倍率、关间随机商店、无限闯关、每 5 关一个三阶段 Boss。
- 已实现：7 种武器、6 种敌人（含盾兵环绕护盾与 Explosive/Piercing 克制）、城墙/词条/商店/模拟广告/统计存档；战斗内可暂停（`GameState.Paused`）与一键通关（测试用），每关通过后城墙自动回满。
- 美术风格：**像素奇幻（Fantasy Dreamland）**，统一 **50 像素 = 1 世界单位**。
  - 外部素材放在 `Assets/Resources/Art/Sprites/<key>.png`、音效放在 `Assets/Resources/Art/Audio/<key>.wav`；导入规则由 `Assets/Editor/ArtImportPostprocess.cs` 统一接管（Point、无 mipmap、PPU 50）。
  - 缺失时不报错，自动回退程序化美术（`SpriteFactory` / `AudioBus`），所以加/换素材只放同名文件即可。
  - 护盾、血条、瞄准线、连斩提示等仍为程序化图形（护盾必须用弧片表达「正面才挡子弹」）。
  - 界面底图不落 PNG：由 `Assets/Scripts/UI/UiPixelArt.cs` 在运行时生成九宫格像素皮肤（32×32、Point 采样、`Image.type = Sliced`），五个面板全部使用；布局仍由 uGUI 代码搭。
  - 炮台贴图 `cannon` 是**枪塔**（96×120，枪口朝 +Y），`BattleView.CannonDisplayHeight = 1.9`，图片顶边自动对齐 `BattleField.Muzzle`。
  - 素材清单与取舍理由见 `docs/素材清单.md`（新版在 `docs/素材清单_更新版.md`）。

## 四、目录约定

- `Assets/Scripts/`：运行时代码；`Assets/Editor/`：编辑器工具；`Assets/Scenes/`：场景；`Assets/Resources/`：配置、字体、美术、音效。
- `Assets/外部素材/`：用户下载的原始素材包（101MB，已在 `.gitignore` 中排除，不进版本库、不在 Resources 下、不进包体）。
- `Tools/`：工程级脚本（字体子集化等），不在 `Assets` 下、不进包体。
- `docs/`：手册抽取稿、交付说明、素材清单、验收截图。
- 不使用中文文件名（`Assets/外部素材` 例外，属用户目录），避免 Unity 导入与 Git 的编码问题。

## 五、构建与验证流水线（重要）

真实工程被编辑器占用，验证在克隆 `E:/UnityProject/_ci/GunplayCI` 里做（`E:/UnityProject/_ci/ci_compile.sh <全限定入口>` 先镜像代码再跑批处理）：

1. **离线编译门（最快）**：`gen_rsp.py` 生成响应文件后 `dotnet exec <Roslyn>/csc.dll @offline/compile.rsp`，要求 0 error / 0 warning。
2. **逻辑自检**：`Gunplay.EditorTools.CiSelfTest.Run` → `E:/UnityProject/_ci/selftest.log`（SELF_TEST_OK，当前 passed=58）。
3. **场景校验**：`Gunplay.EditorTools.CiSceneCheck.Run` → `E:/UnityProject/_ci/scenecheck.log`（SCENE_CHECK_OK）。
4. **素材校验**：`Gunplay.EditorTools.CiArtCheck.Run` → `E:/UnityProject/_ci/artcheck.log`（ART_CHECK_OK：必需精灵 16/16、音效 18/18、字体缺字 0）。
5. **抖音包校验**：`Gunplay.EditorTools.DouyinToolkitCheck.Run` → `E:/UnityProject/_ci/douyincheck.log`（DOUYIN_CHECK_OK；`appid` 占位会记 WARN）。
6. **出包**：`Gunplay.EditorTools.WebGLBuilder.BuildWebGL` → `Build/WebGL` + `Build/Douyin/gunplay-siege-defense`（后处理自动写 `game.json`/`project.config.json` 并注入全屏样式）。
7. **浏览器验收**：本地静态服务器（`Build/WebGL` 下 `python -m http.server 8765`）+ Edge（可见窗口 + `--remote-debugging-port`）；`document.hidden` 必须为 false，否则 Unity 主循环暂停、输入无效；改包后要强制刷新（否则 `.data` 走 304 缓存）。
8. **回传**：`Build/`、`Assets/Scenes/Main.unity`、新增脚本的 `.cs.meta` 一起同步到真实工程（缺 `.meta` 会让场景引用变 Missing Script）。

## 六、编码注意事项（踩过的坑）

- `Gunplay.View.SpriteActor` 构造时默认 `SetActive(false)`（为对象池设计）：**静态元素创建后必须显式 `SetActive(true)`**，否则永远不渲染（首版背景/城墙/炮台因此漏渲染）。
- WebGL 下 `Screen.orientation` 会抛 `NotSupportedError`（无害警告）；`document.hidden` 时主循环暂停。
- 新增 `.cs` 文件后必须把 `.meta` 一并回传真实工程，场景/资源引用才稳。
- **色彩空间必须是 Gamma**：抖音 TTSDK 出包把 WebGL 图形 API 固定为 WebGL 1.0（`OpenGLES2`），WebGL 1.0 不支持 Linear，构建会报 `Incompatible color space with graphics API`。`Assets/Editor/BuildWebGL.cs` 已强制 `PlayerSettings.colorSpace = ColorSpace.Gamma`；检查点 `ProjectSettings/ProjectSettings.asset`: `m_ActiveColorSpace: 0` 且 `m_BuildTargetGraphicsAPIs` 里 WebGLSupport 的 `m_APIs: 08000000`（TTSDK 安装时写入）。
- TTSDK 安装会改写 ProjectSettings（`scriptingDefineSymbols` 的 `WebGL: TTSDK_MIX_ENGINE`、WebGL 内存 512MB、调试符号开、数据缓存关、Brotli 压缩）——这些是它出包需要的样子，**不要用我们的 `WebGLBuilder` 覆盖后再用 TTSDK 出包**，两条路径择一。
- **新增中文文案 → 重跑 `Tools/subset_font.py`**（子集字体缺字是静默失败：字形直接不显示，例如「暂停」显示成「停」）。
- 暂停实现靠 `GameRuntime.Tick` 在 `state != Battle` 时直接返回，所以新增系统必须挂在这个 Tick 之下才会被一并冻结。
- `ci_compile.sh` 必须镜像 `Scripts/Resources/Editor/Plugins`：漏掉 `Assets/Plugins` 时 WebGL 链接会报 `undefined symbol: GunplayDouyin_*`。
- 用脚本生成 C# 时小心模板字符串把 `\"` 写进源码（`CS1056 意外的字符"`），插值字符串直接写 `$"{...}"`。
- 验收用的临时脚本（如伪造结算/商店的 `TempUiDemoShot`）**必须在正式出包前删除**，不要留在 `Assets/Scripts` 里。
- 商店报价要按 id/档位去重，且城墙奖励分小修/加固/大修三档，否则一批三张会完全重复。

## 六之九、浏览器里「看不到怪物但能打中」= TTSDK 混引擎污染（务必记住）

- 症状：普通浏览器打开 WebGL 包，能进战斗、能开枪、能打死敌人，但世界层（背景/城墙/枪塔/敌人）**整屏黑**，只剩 uGUI 界面。
- 根因：工程装了抖音 TTSDK（`Assets/Plugins/ByteGame/com.bytedance.starksdk`）。TTSDK 安装/出包时会把
  `ProjectSettings.asset` 改成 `scriptingDefineSymbols: WebGL: TTSDK_MIX_ENGINE`，并把 WebGL 图形 API
  固定成 `m_APIs: 08000000`（OpenGL ES 2.0 = WebGL 1.0）。编译出的包会启用 TTSDK 的「混合渲染引擎」，
  这套运行时假设自己跑在抖音容器里；普通浏览器里 `UNBridge is Not Available`，相机渲染的精灵整层不显示。
- **在 Unity 编辑器里构建网页版的正确做法**：点菜单 `Gunplay/8. 切到浏览器可玩模式（关闭 TTSDK 混引擎）` → 等重编译 → 直接 Build；
  要回抖音出包前点 `Gunplay/9. 切回抖音出包模式（开启 TTSDK 混引擎）`。实现见 `Assets/Editor/WebGraphicsMode.cs`
  （菜单 8 = 移除宏 `TTSDK_MIX_ENGINE` + `PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, true)`；菜单 9 = 反向：写回宏并固定 OpenGL ES 2.0）。
- 结论：**同一个工程不能既出「抖音包」又出「浏览器包」，必须分两条出包路径**：
  1. 抖音小游戏包：TTSDK Build Tool（或克隆里反射调 `TTSDK.Tool.StarkBuilder.Build`）——保留 TTSDK。
  2. 网页版 / GitHub Pages：`bash E:/UnityProject/_ci/ci_web_build.sh` —— 在克隆里镜像代码时**排除 ByteGame**
     并还原 `TTSDK_MIX_ENGINE` 与图形 API，出干净的 WebGL 包，再导出到 `docs/`。
- 排查关键词：浏览器控制台出现 `TTSDK Init`、`UNBridge is Not Available`、`AndroidUIManager` 就说明污染了。
- 附带事实：带了 TTSDK 的包 wasm 8.19MB，干净包 7.48MB。

## 七、当前待办

- [x] `tt` 轻量桥（激励视频 / 分享 / 启动参数）：`Assets/Plugins/WebGL/GunplayDouyin.jslib` + `Assets/Scripts/Platform/DouyinBridge.cs`。
- [ ] 抖音官方 WebGL 小游戏适配层（`tt` 的 canvas 渲染 / 触摸 / 音频）仍需接入，否则 WebGL 画布在抖音容器里无法正常显示。
- [ ] `DouyinBridge` 里的 `adunit-gold-double` / `adunit-free-reroll` 换成抖音开放平台的真实广告位 id。
- [x] `project.config.json` 的 `appid` 已填真实值 `tt8d3dfb80134f55ff07`（常量 `DouyinBuildPostprocess.MINI_GAME_APPID`）。
- [ ] `Assets/外部素材/` 中未使用的 Kenney / Ninja 包可按需清理。
