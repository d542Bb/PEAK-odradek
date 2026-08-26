using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TerrainScanner.DS;

// Lightweight sampling helpers and shared enum used by the scanning pipeline.
public static class ScanRaySampling
{
    public enum RayMarkCategory
    {
        Road = 0,
        Flat = 1,
        MidSlope = 2,
        Steep = 3,
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
            var forward = player.forward;
            var right = player.right;

            // Ray origin height: prefer camera eye height (if available), otherwise player's height.
            float originBaseY = player.position.y;
            if (Camera.main != null) originBaseY = Camera.main.transform.position.y;
            originBaseY += config?.sampling_originHeightOffset ?? 0.9f; // configurable offset above camera/player

            // Move the origin slightly forward so sampling concentrates in front of the player (climbing direction)
            Vector3 originCenter = new Vector3(player.position.x, originBaseY, player.position.z) + forward * (config?.sampling_forwardOffset ?? 1.0f);

            // Read tunables from config (fall back to constants if config is null)
            float centerShapeExp = config?.sampling_centerShapeExponent ?? 0.75f;
            float jitterScale = config?.sampling_jitterScale ?? 0.06f;
            float rowStepMultiplier = config?.sampling_rowStepMultiplier ?? 0.9f;

            // For climbing scenes we care most about the center-forward area. Use a non-linear mapping
            // for horizontal sample positions so samples concentrate near the center.
            float halfWidth = (horizCount * gridStep) * 0.5f;

            // Control distances: shorter primary downward rays, longer angled rays for walls (from config)
            float maxDistanceShort = config?.sampling_maxDistanceShort ?? 12f; // ground / ledge detection

            int raysDone = 0;

            // Starting offset backward so first rows are near player and subsequent rows step forward
            Vector3 rowBase = originCenter - forward * (gridStep * 0.5f);

            for (int i = 0; i < vertCount; i++)
            {
                // Move row forward; rows closer to the player are sampled more finely (use configurable taper)
                float rowOffset = i * gridStep * rowStepMultiplier;
                Vector3 rowOrigin = rowBase + forward * rowOffset;

                for (int j = 0; j < horizCount; j++)
                {
                    // map j from [0,h-1] to [-0.5,0.5] then apply a power curve to concentrate near center
                    float u = (horizCount == 1) ? 0f : (j / (float)(horizCount - 1)) - 0.5f;
                    float shaped = Mathf.Sign(u) * Mathf.Pow(Mathf.Abs(u), centerShapeExp);
                    float xOffset = shaped * halfWidth;

                    // jitter a little to reduce aliasing
                    float jitterX = UnityEngine.Random.Range(-jitterScale, jitterScale) * gridStep;
                    float jitterZ = UnityEngine.Random.Range(-jitterScale, jitterScale) * gridStep;

                    Vector3 rayOrigin = rowOrigin - right * (xOffset + jitterX) + forward * jitterZ;

                    // Primary: straight down to detect ground/ledges
                    RaycastHit hitDown;
                    Physics.Raycast(rayOrigin, Vector3.down, out hitDown, maxDistanceShort, layerMask);
                    try { onHit?.Invoke(hitDown, i, j); } catch (Exception ex) { TerrainScannerPlugin.Logger?.LogWarning($"[ScanRaySampling] onHit callback threw: {ex.Message}"); }
                    raysDone++;
                    if (raysDone >= maxRays) return;

                    // NOTE: Angled/forward rays have been removed because they caused angle misclassification
                    // (flat ground being detected as steep when hitting near-vertical faces). We now do
                    // strictly vertical downward sampling to ensure normals are sampled consistently.

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
