    // 梯度上山法采样 -- 搭配 GenerateTerrainMarksPlus 函数使用！
    // 英文名：Gradient Ascent Sampling
    public static async UniTask PerformGradientAscentSampling(Transform player, ScanConfig config, int horizCount, int vertCount,
        float gridStep, int layerMask, int maxRays, Action<RaycastHit, int, int> onHit, int scanId)
    {
        // 第一步：初始点：玩家位置附近，高度略高于玩家位置，水平随机误差
        



    }


    // 渲染梯度上山法采样算法结果
    public static async Cysharp.Threading.Tasks.UniTask<Marks[]> GenerateTerrainMarksPlus(Transform player,
        ScanConfig config, int horizCount, int vertCount, float gridStep, int scanId)
    {
        await Cysharp.Threading.Tasks.UniTask.Delay(1);

        return new Marks[0];
    }


    // 最终渲染
    public static void RenderMarks(RasterCommandBuffer cmd, Material markMaterial, ComputeBuffer computeBuffer,
        GraphicsBuffer graphicsBuffer, GraphicsBuffer.IndirectDrawIndexedArgs[] commandData, Mesh mesh,
        Marks[] renderMarks, bool showMark)


