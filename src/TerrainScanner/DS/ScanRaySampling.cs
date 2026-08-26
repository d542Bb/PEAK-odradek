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
            // 相机视锥采样：采样点直接由相机视锥截面（FOV + 宽高比）生成，沿视线方向 raycast 地形。
            // 这样扫描区域天然是锥形、跟随镜头俯仰/摇头，不会随视角退化成平铺矩形/长方形。
            var cam = Camera.main;
            if (cam == null)
            {
                TerrainScannerPlugin.Logger?.LogWarning("[ScanRaySampling] Camera.main is null, cannot do frustum sampling.");
                return;
            }

            // jitter 减少锯齿；maxDistance 控制视线扫描距离
            float jitterScale = config?.sampling_jitterScale ?? 0.06f;
            float maxDistance = config?.sampling_maxDistanceShort ?? 12f;

            int raysDone = 0;

            for (int i = 0; i < vertCount; i++)
            {
                // v: 视锥纵向归一化坐标 [0,1]（屏幕顶→底）
                float v0 = (vertCount == 1) ? 0.5f : (i / (float)(vertCount - 1));
                // 深度分布：把样本向远处(屏幕顶, v→0)集中、近处(屏幕底, v→1)放稀，
                // 缓解视锥透视造成的"近处过密、远处几乎不采样"。
                v0 = Mathf.Pow(v0, 1.5f);

                for (int j = 0; j < horizCount; j++)
                {
                    // u: 视锥横向归一化坐标 [0,1]（屏幕左→右）
                    float u0 = (horizCount == 1) ? 0.5f : (j / (float)(horizCount - 1));

                    // 加一点抖动，减少锯齿（不离散到量变矩形边界）
                    float ju = Mathf.Clamp01(u0 + UnityEngine.Random.Range(-jitterScale, jitterScale));
                    float jv = Mathf.Clamp01(v0 + UnityEngine.Random.Range(-jitterScale, jitterScale));

                    // 从相机经视锥方向生成射线，命中地形即为采样点
                    Ray ray = cam.ViewportPointToRay(new Vector2(ju, jv));
                    RaycastHit hit;
                    if (Physics.Raycast(ray, out hit, maxDistance, layerMask))
                    {
                        try { onHit?.Invoke(hit, i, j); } catch (Exception ex) { TerrainScannerPlugin.Logger?.LogWarning($"[ScanRaySampling] onHit callback threw: {ex.Message}"); }
                        raysDone++;
                        if (raysDone >= maxRays) return;
                    }
                }

                // yield each row to avoid frame hitch and allow lights/physics to update
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
