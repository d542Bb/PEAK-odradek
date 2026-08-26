v1.1.2
- 采样改为以相机为坐标轴的规整矩形点阵（跟随镜头 Yaw、不受俯仰影响），并做视锥过滤。
- 立足/坡度分级：`<40°` 白点、`40°~50°` 黄色警告、`≥50°` 红叉（参考 Foothold）。
- 扫描音效：支持音量/冷却配置，音效先于扫描效果约 0.5s。
- cfg 增强：移除 `GridStep`，新增 `MarkIconSize` / Mark 三色可调，修复 `HorizontalCount/VerticalCount` 变更不生效。
- Style 静态项（颜色/宽度）真正接入材质；扫描动画目标参数化（`Anim*` 项标注"不建议修改"）。
- 修复：`ScanFeature` 初始化（`Create` 未调用、AssetBundle 同步加载导致原地扫描无反应）；扫描点穿透表面（绑定深度实现遮挡）。
- 新增引用来源：Tzebruh/Foothold（立足判定）。

v1.0.0
- Initial release of TerrainScanner mod.