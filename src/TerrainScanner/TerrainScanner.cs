using System;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using Cysharp.Threading.Tasks;
using PEAKLib.Core;
using TerrainScanner.DS;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TerrainScanner;

[BepInAutoPlugin]
[BepInDependency(CorePlugin.Id)]
public partial class TerrainScannerPlugin : BaseUnityPlugin
{
    public static TerrainScannerPlugin Instance;
    internal static new ManualLogSource Logger;

    // track the active ScanFeature instance we created/configured
    ScanFeature activeScanFeature = null;

    // 声明加载的资源变量

    private bool assetsLoaded = false;
    private bool scanFeatureInitialized = false;

    void Awake()
    {
        Instance = this;
        Logger = base.Logger;

        // Initialize centralized ScanConfig manager which will bind BepInEx config
        try
        {
            ScanConfigManager.Initialize(this);
            Logger.LogInfo("[INFO] ScanConfigManager initialized.");
        }
        catch (Exception ex) { Logger.LogWarning($"[WARN] ScanConfigManager.Initialize failed: {ex.Message}"); }

        // 初始化 UniTask PlayerLoop 系统
        try
        {
            var playerLoop = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
            PlayerLoopHelper.Initialize(ref playerLoop);
            Logger.LogInfo("[INFO] UniTask PlayerLoop initialized successfully.");
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[WARN] UniTask PlayerLoop initialization failed: {ex.Message}");
        }


        LoadPeakBundle();
        SceneManager.sceneLoaded += OnSceneLoaded;

    }

    public void LoadPeakBundle()
    {
        // 加载 AssetBundle 并加载扫描材质
        this.LoadBundleWithName(
            "TerrainScanner.peakbundle",
            peakBundle =>
            {
#if DEBUG
                peakBundle.GetAllAssetNames().ToList().ForEach(assetName =>
                {
                    Logger.LogInfo($"[DEBUG] Found asset: {assetName}");
                });
#endif
                // 保存加载的资源并写入中心化配置（ScanConfigManager.Current）
                var loadedScanMaterial = peakBundle.LoadAsset<Material>("Assets/Material/Scan.mat");
                // 不再从 bundle 加载 ParticleTerrainMark.mat — 我们直接使用 bundle 中的 TerrianMarks.mat（instanced shader material）
                var loadedMarkMaterial = peakBundle.LoadAsset<Material>("Assets/Shader/TerrianMarks.mat");
                var loadedMarkParticle1 = peakBundle.LoadAsset<GameObject>("Assets/Particles/LightParticle1.prefab");
                var loadedMarkParticle2 = peakBundle.LoadAsset<GameObject>("Assets/Particles/LightParticle2.prefab");
                var loadedMarkParticle3 = peakBundle.LoadAsset<GameObject>("Assets/Particles/LightParticle3.prefab");


                // 检查是否为空
                if (loadedScanMaterial == null)
                {
                    Logger.LogError("[ERROR] Scan material failed to load from AssetBundle.");
                    return;
                }
                // Bundle 必含 TerrianMarks.mat，直接赋值并进行空检查
                if (loadedMarkMaterial == null)
                {
                    Logger.LogError("[ERROR] TerrianMarks.mat failed to load from AssetBundle.");
                    return;
                }

                if (loadedMarkParticle1 == null || loadedMarkParticle2 == null || loadedMarkParticle3 == null)
                {
                    Logger.LogError("[ERROR] One or more mark particles failed to load from AssetBundle.");
                    return;
                }

                Logger.LogInfo("[INFO] All assets loaded successfully from AssetBundle.");
                // write loaded assets into central ScanConfig
                try
                {
                    if (ScanConfigManager.Current != null)
                    {
                        ScanConfigManager.Current.scanMaterial = loadedScanMaterial;
                        ScanConfigManager.Current.markMaterial = loadedMarkMaterial;
                        ScanConfigManager.Current.markParticle1 = loadedMarkParticle1;
                        ScanConfigManager.Current.markParticle2 = loadedMarkParticle2;
                        ScanConfigManager.Current.markParticle3 = loadedMarkParticle3;
                    }
                }
                catch (Exception ex) { Logger.LogWarning($"[WARN] Failed to assign loaded assets to ScanConfigManager.Current: {ex.Message}"); }

                assetsLoaded = true;
                // 运行时诊断：打印材质/Shader 信息和是否包含关键属性
                try
                {
                    if (loadedScanMaterial != null)
                    {
                        Logger.LogInfo($"[DIAG] scanMaterial shader={loadedScanMaterial.shader?.name ?? "null"}");
                        Logger.LogInfo($"[DIAG] scanMaterial has scanRange? {loadedScanMaterial.HasProperty("scanRange")}");
                        Logger.LogInfo($"[DIAG] scanMaterial has scanLineBrightness? {loadedScanMaterial.HasProperty("scanLineBrightness")}");
                        Logger.LogInfo($"[DIAG] scanMaterial renderQueue={loadedScanMaterial.renderQueue} instancing={loadedScanMaterial.enableInstancing}");
                    }
                    if (loadedMarkMaterial != null)
                    {
                        Logger.LogInfo($"[DIAG] markMaterial shader={loadedMarkMaterial.shader?.name ?? "null"}");
                        Logger.LogInfo($"[DIAG] markMaterial instancing={loadedMarkMaterial.enableInstancing} renderQueue={loadedMarkMaterial.renderQueue}");
                    }
                }
                catch (Exception ex) { Logger.LogWarning($"[WARN] Material diagnostics failed: {ex.Message}"); }

                // 如果已经在场景中，立即初始化 ScanFeature
                if (Camera.main != null)
                {
                    InitializeScanFeature();
                }
            }
        );

    }

    public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            // 确保不重复添加 ActiveScan 组件
            if (mainCamera.gameObject.GetComponent<ActiveScan>() == null)
            {
                mainCamera.gameObject.AddComponent<ActiveScan>();
                Logger.LogInfo("[INFO] ActiveScan component added to main camera");
            }

            // 只有在资源已加载且 ScanFeature 未初始化时才进行初始化
            if (assetsLoaded && !scanFeatureInitialized)
            {
                InitializeScanFeature();
            }
        }
        else
        {
            Logger.LogWarning("[WARN] Main camera not found in scene");
        }
    }

    private void InitializeScanFeature()
    {
        // 确保资源已加载
        if (!assetsLoaded)
        {
            Logger.LogWarning("[WARN] Assets not loaded yet. Waiting for AssetBundle to finish loading...");
            return;
        }

        // 防止重复初始化
        if (scanFeatureInitialized)
        {
            Logger.LogInfo("[DEBUG] ScanFeature already initialized. Skipping...");
            return;
        }

        var pipelineAsset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        if (pipelineAsset == null)
        {
            Logger.LogError("[ERROR] UniversalRenderPipelineAsset is null!");
            return;
        }

        // 获取 UniversalRendererData
        var rendererDataList = typeof(UniversalRenderPipelineAsset)
            .GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (rendererDataList == null)
        {
            Logger.LogError("[ERROR] Unable to access m_RendererDataList via reflection.");
            return;
        }

        var rendererDataArray = rendererDataList.GetValue(pipelineAsset) as ScriptableRendererData[];
        if (rendererDataArray == null || rendererDataArray.Length == 0)
        {
            Logger.LogError("[ERROR] RendererData array is null or empty.");
            return;
        }

        var rendererData = rendererDataArray[0] as UniversalRendererData;
        if (rendererData == null)
        {
            Logger.LogError("[ERROR] First renderer is not UniversalRendererData.");
            return;
        }

        // 检查是否已经存在 ScanFeature
        foreach (var feature in rendererData.rendererFeatures)
        {
            if (feature is ScanFeature existingScanFeature)
            {
                Logger.LogInfo("[DEBUG] ScanFeature already exists. Configuring...");

                activeScanFeature = existingScanFeature;

                try
                {
                    // register existing feature with centralized config manager
                    ScanConfigManager.RegisterFeature(existingScanFeature);
                }
                catch (Exception ex) { Logger.LogWarning($"[WARN] RegisterFeature failed: {ex.Message}"); }

                scanFeatureInitialized = true;
                return;
            }
        }

        // 创建新的 ScanFeature
        var scanFeature = ScriptableObject.CreateInstance<ScanFeature>();
        scanFeature.name = "TerrainScanner_ScanFeature";


        // 添加到渲染器
        rendererData.rendererFeatures.Add(scanFeature);
        Logger.LogInfo("[SUCCESS] ScanFeature created and added to renderer!");

        // 标记为脏数据以触发更新
#if UNITY_EDITOR
    UnityEditor.EditorUtility.SetDirty(rendererData);
#endif

        // 手动调用 Create 方法
        // scanFeature.Create();
        activeScanFeature = scanFeature;

        try
        {
            // register newly created feature with centralized config manager
            ScanConfigManager.RegisterFeature(scanFeature);
        }
        catch (Exception ex) { Logger.LogWarning($"[WARN] RegisterFeature failed: {ex.Message}"); }

        scanFeatureInitialized = true;
        Logger.LogInfo("[SUCCESS] ScanFeature initialized!");
    }


    // ConfigureScanFeature removed: resource/config injection is handled centrally by ScanConfigManager.RegisterFeature

}