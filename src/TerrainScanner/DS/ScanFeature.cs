using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
// This file lives under DS; use the DS namespace to match project conventions
namespace TerrainScanner.DS;

public class ScanFeature : ScriptableRendererFeature
{

    // Use external ScanConfig (created in Config.cs) instead of nested Settings class.
    public ScanConfig config = new ScanConfig();

    static ScanFeature _instance;
    CustomRenderPass _myPass;

    // diagnostics
    static int s_scanCounter = 0;

    public static void ExecuteScan(Transform player)
    {
        StartScan(player).Forget();
    }

    static async UniTaskVoid StartScan(Transform player)
    {
        if (!Application.isPlaying)
        {
            TerrainScannerPlugin.Logger?.LogWarning("[WARN] Cannot scan: not playing");
            return;
        }
        if (!canScan)
        {
            TerrainScannerPlugin.Logger?.LogInfo("[INFO] Scan already in progress");
            return;
        }
        if (_instance == null)
        {
            TerrainScannerPlugin.Logger?.LogError("[ERROR] ScanFeature instance is null. Ensure it is properly initialized.");
            return;
        }
        if (_instance.config == null)
        {
            TerrainScannerPlugin.Logger?.LogError("[ERROR] ScanFeature config is null.");
            return;
        }
        if (_instance.config.scanMaterial == null)
        {
            TerrainScannerPlugin.Logger?.LogError("[ERROR] ScanFeature scanMaterial is null. Assets may not be loaded yet.");
            return;
        }
        canScan = false;
        showMark = true;
        markTween?.Kill();

        var scanCenter = player.position - player.forward * 2;
        var material = _instance.config.scanMaterial;
        var markMaterial = _instance.config.markMaterial;
        if (material != null)
        {
            material.SetVector(ScanCenterWs, scanCenter);
            material.SetFloat(HeadScanLineDistance, 4);
            var tween1 = material.DOFloat(250, HeadScanLineDistance, 3.5f).SetEase(Ease.InSine);
            if (tween1 != null) tween1.onComplete += () => { canScan = true; };
            material.SetFloat(ScanRange, 1);
            material.DOFloat(5, ScanRange, 1.5f).SetEase(Ease.InSine).SetDelay(1);
            material.SetFloat(ScanLineBrightness, 0.3f);
            material.SetFloat(HeadScanLineBrightness, 0);
            material.DOFloat(1, ScanLineBrightness, 0.2f).SetDelay(0.25f);
            material.DOFloat(1, HeadScanLineBrightness, 0.1f).SetDelay(0.25f);
            material.DOFloat(0, ScanLineBrightness, 0.5f).SetDelay(2.25f).SetEase(Ease.Linear);
            material.DOFloat(0, HeadScanLineBrightness, 0.5f).SetDelay(2.25f).SetEase(Ease.Linear);
            material.SetFloat(OutlineBrightness, 1);
            material.SetFloat(OutlineStarDistance, 0);
            material.DOFloat(0, OutlineBrightness, 0.5f).SetDelay(2.25f).SetEase(Ease.Linear);
            material.DOFloat(30, OutlineStarDistance, 1f).SetEase(Ease.InCubic);
        }
        if (markMaterial != null)
        {
            markMaterial.SetFloat(ColorAlpha, 0);
            markMaterial.DOFloat(1, ColorAlpha, 1f);
            markTween = markMaterial.DOFloat(0, ColorAlpha, 1f).SetDelay(7);
            if (markTween != null) markTween.onComplete += () => { showMark = false; };
        }

        int scanId = ++s_scanCounter;

        try
        {
            var result = await ScanMarkRenderer.GenerateTerrainMarks(player, _instance.config, ScanFeature._instance.config.horizontalCount, ScanFeature._instance.config.verticalCount, ScanFeature._instance.config.gridStep, scanId);
            lock (_marksLock) { _marksForRender = result; }
        }
        catch (Exception ex)
        {
            TerrainScannerPlugin.Logger?.LogWarning($"[ScanFeature] GenerateTerrainMarks failed: {ex}");
            lock (_marksLock) { _marksForRender = null; }
        }
    }

    // double-buffered completed marks for rendering; swap in when a scan finishes
    static Marks[] _marksForRender;
    static readonly object _marksLock = new object();

    // constants and shader IDs
    readonly static int ScanColorHead = Shader.PropertyToID("scanColorHead");
    readonly static int ScanColor = Shader.PropertyToID("scanColor");
    readonly static int OutlineWidth = Shader.PropertyToID("outlineWidth");
    readonly static int OutlineBrightness = Shader.PropertyToID("outlineBrightness");
    readonly static int OutlineStarDistance = Shader.PropertyToID("outlineStarDistance");
    readonly static int ScanLineWidth = Shader.PropertyToID("scanLineWidth");
    readonly static int ScanLineInterval = Shader.PropertyToID("scanLineInterval");
    readonly static int ScanLineBrightness = Shader.PropertyToID("scanLineBrightness");
    readonly static int ScanRange = Shader.PropertyToID("scanRange");
    readonly static int HeadScanLineDistance = Shader.PropertyToID("headScanLineDistance");
    readonly static int HeadScanLineWidth = Shader.PropertyToID("headScanLineWidth");
    readonly static int HeadScanLineBrightness = Shader.PropertyToID("headScanLineBrightness");
    readonly static int ScanCenterWs = Shader.PropertyToID("scanCenterWS");
    readonly static int ColorAlpha = Shader.PropertyToID("colorAlpha");

    static bool canScan = true;
    static bool showMark = false;

    // Expose whether a scan can currently start (not already in progress), so ActiveScan can
    // keep sfx playback in sync with the actual scan execution (cooldown shared).
    public static bool CanScan => canScan;
    static Tween markTween;

    #region CORES
    // --- Render pass (kept simple) ---
    class CustomRenderPass : ScriptableRenderPass
    {
    GraphicsBuffer _graphicsBuffer;
    GraphicsBuffer.IndirectDrawIndexedArgs[] _commandData;
    ComputeBuffer _computeBuffer;
        Mesh mesh;
        ScanConfig settings;
        string _passName;
        public CustomRenderPass(ScanConfig settings)
        {
            // Delegate GPU resource creation to ScanMarkRenderer to keep single responsibility
            ScanMarkRenderer.CreateResources(settings, settings.horizontalCount, settings.verticalCount, out mesh, out _computeBuffer, out _graphicsBuffer, out _commandData);
            this.settings = settings;
            _passName = "ScanEffect";
            // ensure mark material supports GPU instancing
            try { if (this.settings?.markMaterial != null) this.settings.markMaterial.enableInstancing = true; } catch { }
        }

        public void DisposeResources()
        {
            try { if (_computeBuffer != null) { _computeBuffer.Dispose(); _computeBuffer = null; } } catch { }
            try { if (_graphicsBuffer != null) { _graphicsBuffer.Dispose(); _graphicsBuffer = null; } } catch { }
            try { if (mesh != null) { Destroy(mesh); mesh = null; } } catch { }
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var colorTarget = resourceData.activeColorTexture;
            var depthTarget = resourceData.activeDepthTexture;
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(_passName, out var passData))
            {
                passData.scanMaterial = settings.scanMaterial;
                passData.markMaterial = settings.markMaterial;
                passData.localShowMark = showMark;
                passData.marks = _marksForRender;
                passData.computeBuffer = _computeBuffer;
                passData.graphicsBuffer = _graphicsBuffer;
                passData.commandData = _commandData;
                passData.mesh = mesh;
                passData.localHorizontalCount = settings.horizontalCount;
                passData.localVerticalCount = settings.verticalCount;
                passData.depthTarget = depthTarget;
                builder.SetRenderAttachment(colorTarget, 0, AccessFlags.ReadWrite);
                // 绑定活跃深度为深度附件，真正启用深度测试（ZTest LEqual）。
                // 此前只用 UseTexture 把 depth 给扫描波采样，pass 没有可用的深度缓冲，
                // 导致标记 shader 的 ZTest LEqual / SV_DEPTH 全部失效 → 后端标记穿墙透视。
                builder.SetRenderAttachmentDepth(depthTarget, AccessFlags.ReadWrite);
                builder.UseTexture(depthTarget);
                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) => ExecutePass(data, ctx));
            }
        }

        class PassData
        {
            public Material scanMaterial;
            public Material markMaterial;
            public bool localShowMark;
            public Marks[] marks;
            public ComputeBuffer computeBuffer;
            public GraphicsBuffer graphicsBuffer;
            public GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;
            public Mesh mesh;
            public int localHorizontalCount;
            public int localVerticalCount;
            public TextureHandle depthTarget;
        }

        static void ExecutePass(PassData data, RasterGraphContext context)
        {
            if (data.scanMaterial == null) return;
            var cmd = context.cmd;
            RTHandle depthHdl = data.depthTarget;
            Vector2 viewportScale = Vector2.one;
            try { if (depthHdl != null && depthHdl.useScaling) viewportScale = new Vector2(depthHdl.rtHandleProperties.rtHandleScale.x, depthHdl.rtHandleProperties.rtHandleScale.y); }
            catch (Exception ex) { TerrainScannerPlugin.Logger?.LogWarning($"[ScanFeature] depth handle read failed: {ex.Message}"); }

            if (depthHdl != null) { try { Blitter.BlitTexture(cmd, depthHdl, viewportScale, data.scanMaterial, 0); } catch (Exception ex) { TerrainScannerPlugin.Logger.LogError($"[ScanFeature] ExecutePass: BlitTexture failed: {ex}"); } }

                if (data.localShowMark && data.markMaterial != null)
            {
                try
                {
                    var renderMarks = _marksForRender;
                    ScanMarkRenderer.RenderMarks(cmd, data.markMaterial, data.computeBuffer, data.graphicsBuffer, data.commandData, data.mesh, renderMarks, data.localShowMark);
                }
                catch (Exception ex)
                {
                    TerrainScannerPlugin.Logger?.LogWarning($"[ScanFeature] RenderMarks failed: {ex}");
                }
            }
        }

        // Note: Do not dispose graphics/compute buffers in finalizer here.
        // Disposal should be handled explicitly when the feature is destroyed or on editor domain unload.
    }



    public override void Create()
    {
        TerrainScannerPlugin.Logger?.LogInfo("[ScanFeature] Create() called");

        if (config.scanMaterial == null)
        {
            TerrainScannerPlugin.Logger?.LogError("[ScanFeature] scanMaterial is not assigned!");
            return;
        }
        if (config.markMaterial == null)
        {
            TerrainScannerPlugin.Logger?.LogError("[ScanFeature] markMaterial is not assigned!");
            return;
        }
        if (config.markParticle1 == null || config.markParticle2 == null || config.markParticle3 == null)
        {
            TerrainScannerPlugin.Logger?.LogError("[ScanFeature] One or more mark particles are not assigned!");
            return;
        }
        if (!Application.isPlaying)
        {
            TerrainScannerPlugin.Logger?.LogWarning("[ScanFeature] Not in play mode, skipping Create()");
            return;
        }

        // allocation no longer needed here; mark arrays are produced by ScanMarkRenderer
        _myPass = new CustomRenderPass(config);
        _instance = this;

        TerrainScannerPlugin.Logger?.LogInfo("[ScanFeature] Create() completed successfully, _instance set");
    }

    // ScriptableRendererFeature.Dispose is not virtual in this project context.
    // Use Unity lifecycle callbacks to perform cleanup when the feature asset is disabled/unloaded.
    void OnDisable()
    {
        try
        {
            if (_myPass != null)
            {
                try { _myPass.DisposeResources(); } catch (Exception ex) { TerrainScannerPlugin.Logger?.LogWarning($"[ScanFeature] DisposeResources failed: {ex}"); }
                _myPass = null;
            }
        }
        catch (Exception ex)
        {
            TerrainScannerPlugin.Logger?.LogWarning($"[ScanFeature] OnDisable cleanup failed: {ex}");
        }

        try
        {
            ScanConfigManager.UnregisterFeature(this);
        }
        catch (Exception ex)
        {
            TerrainScannerPlugin.Logger?.LogWarning($"[ScanFeature] UnregisterFeature failed: {ex}");
        }
        _instance = null;
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (config.scanMaterial == null) return; if (!Application.isPlaying) return;
        if (renderingData.cameraData.cameraType == CameraType.Game)
        {
            _myPass.renderPassEvent = config.renderEvent;
            _myPass.ConfigureInput(ScriptableRenderPassInput.Color);
            _myPass.ConfigureInput(ScriptableRenderPassInput.Normal);
            _myPass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (config.scanMaterial == null) return; if (!Application.isPlaying) return;
        renderer.EnqueuePass(_myPass);
    }

    #endregion
}
