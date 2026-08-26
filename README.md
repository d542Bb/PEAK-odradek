# TerrainScanner

PEAK 的地形扫描器 mod，基于 shader，由 GPU 渲染，采用异步逐帧采样，因此快速且不卡顿。

> 本项目最初从 [FengLvv/Death-stranding-scan](https://github.com/FengLvv/Death-stranding-scan) 移植而来。

## 功能

- 坡面分类（阈值可在 `ScanConfig` 中调整）：
  - 平地：0°–30°
  - 缓坡：30°–50°
  - 陡坡：50°–90°

## 快速开始

1. 将 `src/TerrainScanner/` 下的脚本（或编译好的 DLL）放入你的 Unity 工程 `Assets/` 目录。
2. 在场景中把 `ActiveScan` 挂到某个 GameObject（例如玩家或相机）上。默认用 `Q` 键触发（见 `ActiveScan.cs`）。
3. 运行时确保 `ScanConfig` 正确填充所需材质与粒子预制体：`scanMaterial`、`markMaterial`，以及可选的 `markParticle1/2/3`。

如果使用 BepInEx mod 加载器，插件可在启动时自动完成这些填充。

## 配置要点

大部分运行时选项位于 `ScanConfig`（`src/TerrainScanner/Config.cs`）：

- `horizontalCount` / `verticalCount` — 采样网格大小。调大增加覆盖与精度，但消耗更多 CPU。
- `gridStep` — 采样间距（米）。
- `sampling_originHeightOffset` — 射线起点高于相机/玩家的高度，用于扫描更高地形。
- `sampling_maxDistanceShort` / `sampling_maxDistanceLong` — 地面/边缘与远距离检测的射线长度。
- `steepSpawnProb` / `midSpawnProb` / `flatSpawnProb` — 各类坡面的粒子生成概率。

## 性能建议

- 采样分片执行（使用 `UniTask.Yield()` 避免阻塞主线程）。若提高分辨率，请考虑调小 `horizontalCount/verticalCount` 或降低采样频率。
- 可限制每帧上传到 GPU 的标记数量（例如只保留最近的 N 个标记或按坡面类别优先）。

## 项目出处

### 引用来源

- **移植基础**：本 mod 的地形扫描方案（基于 shader、GPU 渲染、异步逐帧采样）移植自 [FengLvv/Death-stranding-scan](https://github.com/FengLvv/Death-stranding-scan)（**Tzebruh**，MIT）。
- **代码来源仓库**：代码进一步整理自 [haruyuki/TerrainScanner](https://github.com/haruyuki/TerrainScanner)（原为 PeakMods 的 fork，内含聊天 mod 与地形扫描器 mod），仅取其中地形扫描器部分；原作者 **LLightJunction / LIghtJUNction**（MIT）。
- **目标游戏生态**：一个用于 **PEAK** 游戏的 mod，通过 **BepInEx** 加载；构建依赖请见 [src/TerrainScanner/TerrainScanner.csproj](src/TerrainScanner/TerrainScanner.csproj)。
- **贡献者/维护者**：**d542Bb**。

### 许可证说明（重要）

本仓库源码派生自上述 **MIT** 授权的代码（Tzebruh、LLightJunction），本仓库按 **GPL-3.0** 发布（见根目录 `LICENSE`），遵循最严格的许可证要求。发布/分发时请注意：

- 保留原作者版权声明与署名（根 `LICENSE` 为 GPL-3.0，`src/TerrainScanner/LICENSE` 保留原始 MIT 版权行）。
- 修改版需显著标注改动或日期，整理与修复记录见 `src/TerrainScanner/CHANGELOG.md`。
- 本仓库插件标识统一为 `d542Bb.TerrainScanner`（`AssemblyName`），发布命名空间 `d542Bb`。

### 为什么重新开这个仓库

复制代码并单开仓库，主要有几个原因：

1. **历史太杂**：原仓库 `haruyuki/TerrainScanner` 的历史里塞入了大量与地形扫描器无关的内容——聊天 mod 及其本地化 / UI 相关文档、约 83 MB 的 shader 书籍参考资料（`building-quality-shaders-unity-main`）、移植来源的完整 Unity 工程（DOTween、UniTask 等第三方库）、无实际用途的工具脚本。
2. **原仓库存在需要修复的 bug**：为了能在独立的干净仓库中继续维护和修复问题，不受历史包袱拖累，并继续整理地形扫描器功能。
3. **原作者不再经常维护**：原作者已留言「抱歉，没人陪我玩 PEAK，这个 mod 可能不再频繁更新」提交更新可能难以得到响应。并且由于项目变动较大，也难以提交更新合并。

因此我们新建了一个空仓库，只推入裁剪后的代码（单次提交、不含大文件历史）。原仓库作为 `upstream` 保留，便于对照与回看。
