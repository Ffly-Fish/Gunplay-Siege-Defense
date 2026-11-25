# Gunplay Siege Defense — 项目约定

> 本文件是本项目的长期约定，任何人都必须遵守。修改规则前先与用户确认。

## 一、用户强制要求（2025-11-25 下达）

1. **命名规范**：变量名、方法名、对象名、类名等命名，必须符合 C# / Unity 命名规范（字段 camelCase、属性/方法/类 PascalCase、常量 UPPER_SNAKE 或 PascalCase 保持一致），同时要简洁、易懂。
2. **Inspector 暴露变量**：所有暴露到 Unity 面板的变量，都必须拥有 `[Header("")]`、`[Tooltip("")]`，且用中文表明其名称和作用。
3. **注释**：所有变量、方法、对象、类，都必须有对应的 `///` 或 `//` 注释，写清楚作用。
4. **待办标记**：如有需要留空的地方，请用 `// TODO:` 标记，并说明内容。
5. **及时沟通**：如果有需要用户帮忙、操作的地方，随时停下来向用户说明。

## 二、项目技术事实

- Unity 版本：2022.3.62f3c1（中国版），项目由 2D (URP) 模板创建。
- 渲染：Universal Render Pipeline 14.0.12 + Renderer2D；已装 TextMeshPro、Timeline、uGUI、Visual Scripting、Test Framework。
- 当前场景：`Assets/Scenes/SampleScene.unity`（空模板场景），尚无任何游戏代码。
- 本机存在已打开的 Unity 实例时会占用该项目，此时不要用 `-batchmode` 打开同一个工程。

## 三、待定事项

- [ ] 玩法方向（俯视角 / 横版 / 3D 视角）、首版范围、美术方案 —— 等待用户讨论后确定。

## 四、目录约定（待玩法确定后细化）

- `Assets/Scripts/`：运行时代码；`Assets/Editor/`：编辑器工具脚本；`Assets/Scenes/`：场景。
- 不使用中文文件名，避免 Unity 导入与 Git 的编码问题。
