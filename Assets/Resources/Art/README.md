本目录 = 游戏外部美术资源（见 docs/素材清单.md）。

Sprites/<key>.png            单帧精灵
Sprites/<key>_0.png ...      帧序列（连号）
Audio/<key>.wav | .ogg       音效

放入后无需改代码：SpriteFactory / AudioBus 会优先读取这里的文件，缺失则自动回退程序化美术。
导入设置由 Assets/Editor/ArtImportPostprocess.cs 自动处理（像素风：Point 采样、PPU 100、无压缩）。
