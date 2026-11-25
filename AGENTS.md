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
- 字体：`Assets/Resources/Fonts/MainFont.ttf`（HarmonyOS Sans SC 子集化，全量备份 `Tools/MainFont_full.ttf`，重新子集用 `Tools/subset_font.py`）。
- **本机正在打开该工程的 Unity 编辑器会独占工程**：此时不要对真实工程跑 `-batchmode`，一律在克隆沙箱里验证。

## 三、玩法与美术（已确定）

- 竖屏定点守城射击：单指按住瞄准、自动齐射、连杀倍率、关间随机商店、无限闯关、每 5 关一个三阶段 Boss。
- 已实现：7 种武器、6 种敌人（含盾兵环绕护盾与 Explosive/Piercing 克制）、城墙/词条/商店/模拟广告/统计存档。
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
2. **逻辑自检**：`Gunplay.EditorTools.CiSelfTest.Run` → `E:/UnityProject/_ci/selftest.log`（SELF_TEST_OK）。
3. **场景校验**：`Gunplay.EditorTools.CiSceneCheck.Run` → `E:/UnityProject/_ci/scenecheck.log`（SCENE_CHECK_OK）。
4. **素材校验**：`Gunplay.EditorTools.CiArtCheck.Run` → `E:/UnityProject/_ci/artcheck.log`（ART_CHECK_OK）。
5. **抖音包校验**：`Gunplay.EditorTools.DouyinToolkitCheck.Run` → `E:/UnityProject/_ci/douyincheck.log`（DOUYIN_CHECK_OK；`appid` 占位会记 WARN）。
6. **出包**：`Gunplay.EditorTools.WebGLBuilder.BuildWebGL` → `Build/WebGL` + `Build/Douyin/gunplay-siege-defense`（后处理自动写 `game.json`/`project.config.json` 并注入全屏样式）。
7. **浏览器验收**：本地静态服务器（`Build/WebGL` 下 `python -m http.server 8765`）+ Edge（可见窗口 + `--remote-debugging-port`）；`document.hidden` 必须为 false，否则 Unity 主循环暂停、输入无效；改包后要强制刷新（否则 `.data` 走 304 缓存）。
8. **回传**：`Build/`、`Assets/Scenes/Main.unity`、新增脚本的 `.cs.meta` 一起同步到真实工程（缺 `.meta` 会让场景引用变 Missing Script）。

## 六、编码注意事项（踩过的坑）

- `Gunplay.View.SpriteActor` 构造时默认 `SetActive(false)`（为对象池设计）：**静态元素创建后必须显式 `SetActive(true)`**，否则永远不渲染（首版背景/城墙/炮台因此漏渲染）。
- WebGL 下 `Screen.orientation` 会抛 `NotSupportedError`（无害警告）；`document.hidden` 时主循环暂停。
- 新增 `.cs` 文件后必须把 `.meta` 一并回传真实工程，场景/资源引用才稳。
- `ci_compile.sh` 必须镜像 `Scripts/Resources/Editor/Plugins`：漏掉 `Assets/Plugins` 时 WebGL 链接会报 `undefined symbol: GunplayDouyin_*`。
- 用脚本生成 C# 时小心模板字符串把 `\"` 写进源码（`CS1056 意外的字符"`），插值字符串直接写 `$"{...}"`。
- 验收用的临时脚本（如伪造结算/商店的 `TempUiDemoShot`）**必须在正式出包前删除**，不要留在 `Assets/Scripts` 里。
- 商店报价要按 id/档位去重，且城墙奖励分小修/加固/大修三档，否则一批三张会完全重复。

## 七、当前待办

- [x] `tt` 轻量桥（激励视频 / 分享 / 启动参数）：`Assets/Plugins/WebGL/GunplayDouyin.jslib` + `Assets/Scripts/Platform/DouyinBridge.cs`。
- [ ] 抖音官方 WebGL 小游戏适配层（`tt` 的 canvas 渲染 / 触摸 / 音频）仍需接入，否则 WebGL 画布在抖音容器里无法正常显示。
- [ ] `DouyinBridge` 里的 `adunit-gold-double` / `adunit-free-reroll` 换成抖音开放平台的真实广告位 id。
- [ ] `project.config.json` 里的 `appid` 需填真实小游戏 appid。
- [ ] `Assets/外部素材/` 中未使用的 Kenney / Ninja 包可按需清理。
