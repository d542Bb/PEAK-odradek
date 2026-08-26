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

## 关于

抱歉，没人陪我玩 PEAK，这个 mod 可能不再频繁更新。