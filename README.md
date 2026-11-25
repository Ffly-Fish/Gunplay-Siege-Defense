# 枪法守城闯大关（Gunplay Siege Defense）

竖屏定点守城射击小游戏：单指按住瞄准、自动齐射、连杀倍率、关间随机商店、无限闯关。
Unity 2022.3.62f3c1 + 2D URP，目标平台为抖音小游戏（WebGL），同时提供浏览器网页版。

## 网页版（GitHub Pages）

游戏已构建为纯静态站点，位于 `docs/`：

- `docs/index.html` —— 入口页面
- `docs/Build/` —— Unity WebGL 产物（gzip + 内置解压兜底，任何静态托管都能跑）
- `docs/.nojekyll` —— 关闭 Jekyll，避免下划线开头的文件被忽略

### 开启步骤

1. 把本仓库推到 GitHub（建议 public）。
2. 仓库 **Settings → Pages** → Source 选「Deploy from a branch」→ Branch 选 `main`、目录选 `/docs` → Save。
3. 等 1–2 分钟，访问 `https://<你的用户名>.github.io/<仓库名>/`。

### 重新构建站点

在 Unity 编辑器里执行菜单 **Gunplay → 6. 导出 GitHub Pages 站点**（`Assets/Editor/PagesExporter.cs`），
它会重新构建 WebGL 并把产物复制到 `docs/`。

## 目录

- `Assets/Scripts/` 运行时代码（Core / Combat / Waves / Run / View / UI / Bootstrap / Platform）
- `Assets/Editor/` 编辑器工具（场景生成、构建、CI 校验、Pages 导出）
- `Assets/Resources/` 配置 JSON、字体、像素美术、音效
- `docs/` 网页版站点 + 设计与交付文档
- `Tools/` 工程级脚本（字体子集化等）

## 文档

- `AGENTS.md` —— 项目约定与构建/验证流水线
- `docs/交付说明.md` —— 玩法、构建流程、抖音接入
- `docs/素材清单.md` —— 美术素材来源与取舍

## 授权说明

美术素材来自 Fantasy Dreamland 系列（个人授权包），仅供本项目管理使用；如需公开发布请确认素材授权范围。
