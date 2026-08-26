# TerrainScanner

PEAK 的地形扫描器 mod：基于 shader 的 `Death Stranding` 风格透视地形扫描，由 GPU 渲染、异步采样，快速且不卡顿。

[![TerrainScanner 效果预览](https://raw.githubusercontent.com/d542Bb/PEAK-odradek/main/Assets/ScreenShot.png)](https://raw.githubusercontent.com/d542Bb/PEAK-odradek/main/Assets/ScreenShot.png)

## 这是什么

- **立足提示**：按地形坡度提示可站立位置：
  - `< 40°`：可站立（白点）
  - `40°–50°`：中等坡度，可站但有风险（黄色警告）
  - `≥ 50°`：陡坡，不可站立（红叉）
- **扫描效果**：从镜头出发的扫描波 + 轮廓描边 + 跟随扫描的标记点。
- **可自定义**：扫描按键、颜色、标记大小、描边、音效音量等都可在配置文件里调整。

## 安装方法

1. 需要 **PEAK** 游戏与 **BepInEx**（或使用支持 Thunderstore 的 Mod Manager）。
2. 将本插件 DLL 放入游戏的 `BepInEx/plugins/` 目录（或直接用 Mod Manager 一键安装）。
3. 启动游戏，按下扫描键（默认 `F`）即可触发扫描。

> 依赖：`BepInEx`、`PEAKModding/PEAKLib.Core`、`Cysharp/UniTask`（Mod Manager 会自动安装）。

## 常见问题

- 没有反应？确认插件已正确放到 `BepInEx/plugins/`，并且游戏中使用 Mod Manager 安装。
- 想改按键/颜色/音量？编辑游戏目录下 `BepInEx/config/d542Bb.TerrainScanner.cfg`（改后重启游戏生效）。

## 致谢

派生自 [FengLvv/Death-stranding-scan](https://github.com/FengLvv/Death-stranding-scan) 与 [Tzebruh/Foothold](https://github.com/Tzebruh/Foothold)（均为 MIT），整理自 [haruyuki/TerrainScanner](https://github.com/haruyuki/TerrainScanner)。维护：d542Bb。