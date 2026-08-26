using System;
using UnityEngine;
using System.Runtime.InteropServices;
using UnityEngine.Rendering;

namespace TerrainScanner.DS;


// Public mark struct used by rendering pipeline and compute buffer uploads.
// Keeps same memory layout as previous internal struct (Vector3 + int).
[StructLayout(LayoutKind.Sequential)]
public struct Marks
{
    // Use sequential layout to guarantee memory layout matches HLSL StructuredBuffer<Marks>
    public Vector3 markPosition;
    public ScanRaySampling.RayMarkCategory markCategory;
}

// Centralize mark rendering resource creation and draw logic so ScanFeature remains focused on sampling.
public static class ScanMarkRenderer
{
    // GenerateTerrainMarks: perform ray sampling (delegates ray shooting to ScanRaySampling)
    // Returns a compacted array of Marks ready for GPU upload/draw.
    public static async Cysharp.Threading.Tasks.UniTask<Marks[]> GenerateTerrainMarks(Transform player,
        ScanConfig config, int horizCount, int vertCount, float gridStep, int scanId)
    {
        int total = Math.Max(1, horizCount * vertCount);
        var marks = new Marks[total];
        for (int i = 0; i < marks.Length; i++)
        {
            marks[i].markCategory = ScanRaySampling.RayMarkCategory.Undefined;
            marks[i].markPosition = Vector3.zero;
        }

        int maxRays = Math.Max(1, horizCount * vertCount * 2);
        int mask = LayerMask.GetMask("Scan", "Road");
        if (mask == 0) mask = Physics.DefaultRaycastLayers;

        float steepProb = config?.steepSpawnProb ?? 0.1f;
        float midProb = config?.midSpawnProb ?? 0.3f;
        float flatProb = config?.flatSpawnProb ?? 0.0002f;

        Action<RaycastHit, int, int> onHit = (hit, i, j) =>
        {
            int idx = i * horizCount + j;
            if (idx < 0 || idx >= marks.Length) return;
            if (hit.collider == null)
            {
                marks[idx].markCategory = ScanRaySampling.RayMarkCategory.Undefined;
                marks[idx].markPosition = Vector3.zero;
                return;
            }

            var normal = hit.normal;
            if (hit.collider.isTrigger)
            {
                // 走道/触发器：保留原有 Road 语义
                marks[idx].markCategory = ScanRaySampling.RayMarkCategory.Road;
                marks[idx].markPosition = Vector3.zero;
                return;
            }

            // 立足判定（复刻 Foothold）：按表面法线与竖直方向的夹角判断是否可站立。
            // < 50° 可站立（含平坡 < 30°，平坡也算可站，白点）；>= 50° 不可站立（红叉）。
            float angle = Vector3.Angle(Vector3.up, normal);
            if (angle < 50f)
            {
                marks[idx].markCategory = ScanRaySampling.RayMarkCategory.Safe;
                marks[idx].markPosition = hit.point;
                if (UnityEngine.Random.value < flatProb) ParticleSpawner.ShootParticle(hit.point, normal, 1, config);
            }
            else
            {
                marks[idx].markCategory = ScanRaySampling.RayMarkCategory.Danger;
                marks[idx].markPosition = hit.point;
                if (UnityEngine.Random.value < steepProb) ParticleSpawner.ShootParticle(hit.point, normal, 3, config);
            }
        };

        await ScanRaySampling.PerformGridSamples(player, config, horizCount, vertCount, gridStep, mask, maxRays, onHit,
            scanId);

        int validCount = 0;
        for (int i = 0; i < marks.Length; i++)
            if (marks[i].markCategory != ScanRaySampling.RayMarkCategory.Undefined ||
                marks[i].markPosition != Vector3.zero)
                validCount++;
        if (validCount <= 0) return null;
        var compact = new Marks[validCount];
        int dst = 0;
        for (int i = 0; i < marks.Length; i++)
            if (marks[i].markCategory != ScanRaySampling.RayMarkCategory.Undefined ||
                marks[i].markPosition != Vector3.zero)
                compact[dst++] = marks[i];
        return compact;
    }


    // 渲染梯度上山法采样算法结果
    public static async Cysharp.Threading.Tasks.UniTask<Marks[]> GenerateTerrainMarksPlus(Transform player,
        ScanConfig config, int horizCount, int vertCount, float gridStep, int scanId)
    {
        await Cysharp.Threading.Tasks.UniTask.Delay(1);

        return new Marks[0];
    }





    
    #region Helper

    public static void CreateResources(ScanConfig settings, int horizontalCount, int verticalCount,
        out Mesh mesh, out ComputeBuffer computeBuffer, out GraphicsBuffer graphicsBuffer,
        out GraphicsBuffer.IndirectDrawIndexedArgs[] commandData)
    {
        graphicsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1,
            GraphicsBuffer.IndirectDrawIndexedArgs.size);
        commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        computeBuffer = new ComputeBuffer(Math.Max(1, horizontalCount * verticalCount), sizeof(float) * 4);

        // create a simple quad mesh for instanced marks
        mesh = new Mesh();
        var verts = new Vector3[]
        {
            new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 0, -0.5f), new Vector3(0.5f, 0, 0.5f),
            new Vector3(-0.5f, 0, 0.5f)
        };
        var uvs = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        var tris = new int[] { 0, 1, 2, 0, 2, 3 };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);

        try
        {
            if (settings?.markMaterial != null) settings.markMaterial.enableInstancing = true;
        }
        catch
        {
        }

        // initialize indirect args: indexCountPerInstance, instanceCount, startIndex, baseVertex, startInstance
        commandData[0].indexCountPerInstance = (uint)tris.Length;
        commandData[0].instanceCount = 0u;
        commandData[0].startIndex = 0u;
        commandData[0].startInstance = 0u;
        graphicsBuffer.SetData(commandData);
    }

    public static void RenderMarks(RasterCommandBuffer cmd, Material markMaterial, ComputeBuffer computeBuffer,
        GraphicsBuffer graphicsBuffer, GraphicsBuffer.IndirectDrawIndexedArgs[] commandData, Mesh mesh,
        Marks[] renderMarks, bool showMark)
    {
        // 如果既没有显式显示标记请求，也没有实际的标记数据，则跳过
        if (!showMark && (renderMarks == null || renderMarks.Length == 0))
        {
            return;
        }

        if (markMaterial == null)
        {
            TerrainScannerPlugin.Logger?.LogError(
                "[ScanMarkRenderer] markMaterial is null - cannot render marks");
            return;
        }

        if (computeBuffer == null || graphicsBuffer == null || commandData == null || mesh == null)
        {
            TerrainScannerPlugin.Logger?.LogError(
                "[ScanMarkRenderer] missing GPU resource(s) - computeBuffer/graphicsBuffer/commandData/mesh");
            return;
        }

        int validCount = 0;
        if (renderMarks != null)
        {
            for (int i = 0; i < renderMarks.Length; i++)
                if (renderMarks[i].markCategory != ScanRaySampling.RayMarkCategory.Undefined ||
                    renderMarks[i].markPosition != Vector3.zero)
                    validCount++;
        }

        if (validCount <= 0)
        {
            return;
        }

        var compact = new Marks[validCount];
        int idx = 0;
        for (int i = 0; i < (renderMarks?.Length ?? 0); i++)
            if (renderMarks[i].markCategory != ScanRaySampling.RayMarkCategory.Undefined ||
                renderMarks[i].markPosition != Vector3.zero)
                compact[idx++] = renderMarks[i];

        try
        {
            computeBuffer.SetData(compact);
        }
        catch (Exception ex)
        {
            TerrainScannerPlugin.Logger?.LogError(
                $"[ScanMarkRenderer] computeBuffer.SetData failed: {ex.Message}");
            return;
        }

        var matProp = new MaterialPropertyBlock();
        matProp.SetBuffer("markBuffer", computeBuffer);

        commandData[0].indexCountPerInstance = 6;
        commandData[0].instanceCount = (uint)validCount;
        graphicsBuffer.SetData(commandData);

        if (commandData[0].instanceCount == 0)
        {
            return;
        }

        try
        {
            if (markMaterial != null) markMaterial.enableInstancing = true;
        }
        catch (Exception ex)
        {
            TerrainScannerPlugin.Logger?.LogError($"[ScanMarkRenderer] enableInstancing failed: {ex.Message}");
        }

        try
        {
            cmd.DrawMeshInstancedIndirect(mesh, 0, markMaterial, 0, graphicsBuffer, 0, matProp);
        }
        catch (Exception ex)
        {
            TerrainScannerPlugin.Logger?.LogError($"[ScanMarkRenderer] DrawMeshInstancedIndirect failed: {ex}");
        }
    }

    #endregion


}
