using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TerrainScanner.DS;

// Lightweight sampling helpers and shared enum used by the scanning pipeline.
public static class ScanRaySampling
{
    public enum RayMarkCategory
    {
        Road = 0,       // 走道 / 触发器（保留原语义）
        Safe = 1,       // 可站立（白点）≈ Foothold 的 standable
        Warning = 2,    // 警戒（保留，暂未启用）
        Danger = 3,     // 不可站立（红叉）≈ Foothold 的 non-standable
        Undefined = 4
    }

    // Perform a grid of downward (and short forward-angled) raycasts centered near the player/camera.
    // - Bias samples toward the camera/player center so climbing-relevant surfaces (in front of the player)
    //   get denser sampling.
    // - Use a short downward ray to find ground/ledges and a forward-angled ray for near-vertical faces.
    // onHit is invoked with the RaycastHit and grid indices (row i, col j). The callback may be invoked
    // multiple times for the same (i,j) as different ray variants are tried; later results overwrite earlier.
    public static async UniTask PerformGridSamples(Transform player, ScanConfig config, int horizCount, int vertCount, float gridStep, int layerMask, int maxRays, Action<RaycastHit, int, int> onHit, int scanId)
    {
        if (player == null) return;

        try
        {
            // 以相机为坐标轴的规整矩形采样：矩形平面跟随相机 Yaw(摇头) + Pitch(俯仰)，
            // 射线沿"相机上向的反方向"(近似世界下方向)投影到地形。这样命中地表后屏幕上呈现
            // 规整的点阵（一排排水平线），而不是视锥透视造成的"近处扇形密、远处稀"。
            var cam = Camera.main;
            if (cam == null)
            {
                TerrainScannerPlugin.Logger?.LogWarning("[ScanRaySampling] Camera.main is null.");
                return;
            }

            var camT = cam.transform;
            // 水平前向：镜头 forward 投影到 y=0 平面，确保矩形在镜头正前方（不会因叉积符号推到后方）
            Vector3 camHorizFwd = new Vector3(camT.forward.x, 0f, camT.forward.z);
            if (camHorizFwd.sqrMagnitude < 0.0001f) camHorizFwd = new Vector3(player.forward.x, 0f, player.forward.z);
            if (camHorizFwd.sqrMagnitude < 0.0001f) camHorizFwd = Vector3.forward;
            camHorizFwd.Normalize();
            // 水平右向 = cross(up, fwdH)，跟随镜头摇头方向
            Vector3 camHorizRight = Vector3.Cross(Vector3.up, camHorizFwd);
            // 射线方向始终沿世界下方向打，命中地表位置只由起点的水平投影决定，点阵不会因 pitch 变形
            Vector3 downDir = Vector3.down;

            // 采样参数
            float originHeightOffset = config?.sampling_originHeightOffset ?? 10f; // 起点抬高量
            float forwardOffset = config?.sampling_forwardOffset ?? 1.0f;          // 矩形离相机的近平面距离（前向沿水平投影前向推）
            float step = (gridStep > 0.001f) ? gridStep : 0.5f;                    // 网格间距
            float jitterScale = config?.sampling_jitterScale ?? 0.06f;
            float maxDistance = config?.sampling_maxDistanceShort ?? 30f;          // 向下射线最大距离

            int raysDone = 0;

            for (int i = 0; i < vertCount; i++)
            {
                // 行：沿镜头水平前向（yaw方向）推进。vertCount 现在就是"矩形有几排水平线"。
                float depth = forwardOffset + i * step;
                Vector3 rowCenter = new Vector3(camT.position.x, camT.position.y + originHeightOffset, camT.position.z)
                                  + camHorizFwd * depth;

                for (int j = 0; j < horizCount; j++)
                {
                    // 列：沿水平右向铺开。把 j 映射到 -0.5..0.5，直接乘总宽度
                    float half = (horizCount - 1) * step * 0.5f;
                    float u = (horizCount == 1) ? 0f : (j / (float)(horizCount - 1)) - 0.5f;
                    float jitterX = UnityEngine.Random.Range(-jitterScale, jitterScale) * step;
                    float jitterZ = UnityEngine.Random.Range(-jitterScale, jitterScale) * step;

                    Vector3 rayOrigin = rowCenter
                                      + camHorizRight * (u * 2f * half + jitterX)
                                      + camHorizFwd * jitterZ;

                    RaycastHit hitDown;
                    if (Physics.Raycast(rayOrigin, downDir, out hitDown, maxDistance, layerMask))
                    {
                        // 视锥过滤（基于命中点）：避免屏幕外也出标记
                        var vp = cam.WorldToViewportPoint(hitDown.point);
                        bool inView = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
                        if (!inView) continue;

                        try { onHit?.Invoke(hitDown, i, j); } catch (Exception ex) { TerrainScannerPlugin.Logger?.LogWarning($"[ScanRaySampling] onHit callback threw: {ex.Message}"); }
                        raysDone++;
                        if (raysDone >= maxRays) return;
                    }
                }

                await UniTask.Yield();
            }
        }
        catch (Exception ex)
        {
            TerrainScannerPlugin.Logger?.LogWarning($"[ScanRaySampling] PerformGridSamples failed: {ex}");
        }
    }

    // 梯度上山法采样 -- 搭配 GenerateTerrainMarksPlus 函数使用！
    // 英文名：Gradient Ascent Sampling
    public static async UniTask PerformGradientAscentSampling(Transform player, ScanConfig config, int horizCount, int vertCount,
        float gridStep, int layerMask, int maxRays, Action<RaycastHit, int, int> onHit, int scanId)
    {
        // 第一步：初始点：玩家位置附近，高度略高于玩家位置，水平随机误差
        



    }

    
    
    








}
