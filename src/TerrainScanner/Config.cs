using System;
using BepInEx;
using BepInEx.Configuration;
using TerrainScanner.DS;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TerrainScanner;

// Runtime configuration container backed by BepInEx plugin config values.
// This mirrors the former ScanFeature.Settings fields but lives in its own file
// and is populated by TerrainScannerPlugin during initialization.
public class ScanConfig
{
    // Render timing
    public RenderPassEvent renderEvent = RenderPassEvent.BeforeRenderingTransparents;

    // Materials / particles are provided at runtime by the plugin
    public Material scanMaterial;

    // Static Settings (defaults kept from original Settings)
    public Color scanColorHead = new Color(0.054901965f, 0.5686275f, 0.85098046f, 1f);
    public Color scanColor = new Color(0.38823533f, 0.7372549f, 0.8705883f, 1f);
    public float outlineWidth = 2.48f;
    public float scanLineWidth = 1f;
    public float scanLineInterval = 1f;
    public float headScanLineWidth = 1f;

    // Dynamics (controlled by code / config)
    public float scanLineBrightness = 2.5f;
    public float scanRange = 10f;
    public float outlineBrightness = 1.32f;
    public float headScanLineDistance = 13.2f;
    public Vector3 scanCenterWS = new Vector3(123.05f, 36.3f, 147.86f);
    public float outlineStarDistance = 30f;

    // Render mark resources
    public Material markMaterial;
    public GameObject markParticle3;
    public GameObject markParticle2;
    public GameObject markParticle1;

    // Particle probabilities
    public float steepSpawnProb = 0.1f;
    public float midSpawnProb = 0.3f;
    public float flatSpawnProb = 0.0002f;

    // Sampling / scanning tunables (sensible defaults for climbing scenarios)
    public float sampling_maxDistanceShort = 30f;      // frustum raycast length (ground / reflection)
    public float sampling_maxDistanceLong = 60f;       // angled/forward rays (walls/cliffs)
    public float sampling_centerShapeExponent = 0.75f; // exponent shaping center density (0.0..2.0)
    public float sampling_centerColumnThreshold = 0.25f; // fraction of columns considered "center"
    public float sampling_forwardOffset = 1.0f;        // how far forward from player the sample grid centers
    public float sampling_jitterScale = 0.06f;         // jitter multiplier for sampling positions
    public float sampling_rowStepMultiplier = 0.9f;    // row forward taper multiplier

    // How much above the camera/player to start ray origins. Increase to scan higher geometry.
    public float sampling_originHeightOffset = 10f;

    public int horizontalCount = 40;
    public int verticalCount = 50;
    public float gridStep = 0.5f;

    // Scan trigger cooldown (seconds): shared by the scan effect and the sfx playback
    public float scanCooldown = 0.8f;

    // Scan sfx playback volume (0..1)
    public float sfxVolume = 1f;

    // active scan key (can be changed at runtime)
    public KeyCode activeKey = KeyCode.F;

    // BepInEx config entries (populated by Bind)
    public ConfigEntry<string> cfgScanColorHead;
    public ConfigEntry<string> cfgScanColor;
    public ConfigEntry<float> cfgOutlineWidth;
    public ConfigEntry<float> cfgScanLineWidth;
    public ConfigEntry<float> cfgScanLineInterval;
    public ConfigEntry<float> cfgHeadScanLineWidth;
    public ConfigEntry<float> cfgScanLineBrightness;
    public ConfigEntry<float> cfgScanRange;
    public ConfigEntry<float> cfgOutlineBrightness;
    public ConfigEntry<float> cfgHeadScanLineDistance;
    public ConfigEntry<string> cfgScanCenterWS;
    public ConfigEntry<float> cfgOutlineStarDistance;

    public ConfigEntry<int> cfgHorizontalCount;
    public ConfigEntry<int> cfgVerticalCount;
    public ConfigEntry<float> cfgScanCooldown;
    public ConfigEntry<float> cfgSfxVolume;

    public ConfigEntry<KeyCode> cfgActiveKey;

    bool _bound = false;

    // Bind this ScanConfig to a BepInEx plugin's Config. onChanged is invoked after initial population and on any setting change.
    public void Bind(BaseUnityPlugin plugin, Action<ScanConfig> onChanged = null)
    {
        if (plugin == null) return;
        if (_bound) return;
        _bound = true;
        var cfg = plugin.Config;
        cfgActiveKey = cfg.Bind("Controls", "ActiveScanKey", activeKey,
            "作用: 按下触发一次扫描的按键。\n" +
            "Effect: Key that triggers an active scan.\n" +
            "合法值例子(Example values): Q / F / V（Unity KeyCode 键名）.");
        cfgScanColorHead = cfg.Bind("Style", "ScanColorHead", scanColorHead.r + "," + scanColorHead.g + "," + scanColorHead.b + "," + scanColorHead.a,
            "作用: 扫描头部颜色（RGBA 四通道 0-1）。\n" +
            "Effect: Scan head color as r,g,b,a.\n" +
            "合法值例子(Example values): 0.05,0.57,0.85,1 / 1,1,1,1");
        cfgScanColor = cfg.Bind("Style", "ScanColor", scanColor.r + "," + scanColor.g + "," + scanColor.b + "," + scanColor.a,
            "作用: 扫描主体颜色（RGBA 四通道 0-1）。\n" +
            "Effect: Scan body color as r,g,b,a.\n" +
            "合法值例子(Example values): 0.39,0.74,0.87,1 / 0,1,0,1");
        cfgOutlineWidth = cfg.Bind("Style", "OutlineWidth", outlineWidth,
            "作用: 扫描轮廓线宽度。\n" +
            "Effect: Width of the scan outline.\n" +
            "合法值例子(Example values): 1.0 / 2.48 / 5.0（float，>0）");
        cfgScanLineWidth = cfg.Bind("Style", "ScanLineWidth", scanLineWidth,
            "作用: 扫描线宽度。\n" +
            "Effect: Width of the scan line.\n" +
            "合法值例子(Example values): 0.5 / 1 / 2.5（float，>0）");
        cfgScanLineInterval = cfg.Bind("Style", "ScanLineInterval", scanLineInterval,
            "作用: 扫描线之间的间隔。\n" +
            "Effect: Interval between scan lines.\n" +
            "合法值例子(Example values): 0.5 / 1 / 2（float，>0）");
        cfgHeadScanLineWidth = cfg.Bind("Style", "HeadScanLineWidth", headScanLineWidth,
            "作用: 扫描头部线宽度。\n" +
            "Effect: Width of the head scan line.\n" +
            "合法值例子(Example values): 0.5 / 1 / 2（float，>0）");
        cfgScanLineBrightness = cfg.Bind("Style", "ScanLineBrightness", scanLineBrightness,
            "作用: 扫描线亮度。\n" +
            "Effect: Brightness of the scan line.\n" +
            "合法值例子(Example values): 0.5 / 2.5 / 5（float，>0）");
        cfgScanRange = cfg.Bind("Style", "ScanRange", scanRange,
            "作用: 扫描作用范围。\n" +
            "Effect: Scan reach/range.\n" +
            "合法值例子(Example values): 3 / 5 / 10（float，>0）");
        cfgOutlineBrightness = cfg.Bind("Style", "OutlineBrightness", outlineBrightness,
            "作用: 扫描轮廓亮度。\n" +
            "Effect: Brightness of the outline.\n" +
            "合法值例子(Example values): 0.5 / 1.32 / 3（float，>0）");
        cfgHeadScanLineDistance = cfg.Bind("Style", "HeadScanLineDistance", headScanLineDistance,
            "作用: 扫描头部线距离。\n" +
            "Effect: Distance of the head scan line.\n" +
            "合法值例子(Example values): 5 / 13.2 / 30（float，>0）");
        cfgScanCenterWS = cfg.Bind("Style", "ScanCenterWS", scanCenterWS.x + "," + scanCenterWS.y + "," + scanCenterWS.z,
            "作用: 扫描中心的世界坐标（x,y,z）。\n" +
            "Effect: Scan center world-space as x,y,z.\n" +
            "合法值例子(Example values): 123.05,36.3,147.86 / 0,10,0");
        cfgOutlineStarDistance = cfg.Bind("Style", "OutlineStarDistance", outlineStarDistance,
            "作用: 轮廓星标距离。\n" +
            "Effect: Outline 'star' distance.\n" +
            "合法值例子(Example values): 10 / 30 / 60（float，>0）");

        cfgHorizontalCount = cfg.Bind("Performance", "HorizontalCount", horizontalCount,
            "作用: 相机视锥截面横向样本数量（采样分辨率）。\n" +
            "Effect: Number of horizontal samples across the camera frustum.\n" +
            "合法值例子(Example values): 10 / 40 / 80（int，>=1）");
        cfgVerticalCount = cfg.Bind("Performance", "VerticalCount", verticalCount,
            "作用: 相机视锥截面纵向样本数量（采样分辨率）。\n" +
            "Effect: Number of vertical samples across the camera frustum.\n" +
            "合法值例子(Example values): 20 / 50 / 100（int，>=1）");

        cfgScanCooldown = cfg.Bind("Performance", "ScanCooldown", scanCooldown,
            "作用: 扫描触发冷却（秒），扫描效果与扫描音效共用此冷却，冷却期间不可重复触发。\n" +
            "Effect: Trigger cooldown in seconds, shared by the scan effect and its sfx.\n" +
            "合法值例子(Example values): 0.3 / 0.8 / 2.0（float，>0）");

        cfgSfxVolume = cfg.Bind("Style", "SfxVolume", sfxVolume,
            "作用: 扫描音效播放音量。\n" +
            "Effect: Playback volume of the scan sfx.\n" +
            "合法值例子(Example values): 0 / 0.5 / 1（float，0-1）");


        // parse helpers
        Color ParseColor(string s)
        {
            try
            {
                var parts = s.Split(',');
                if (parts.Length < 3) return Color.blue;
                float r = float.Parse(parts[0]);
                float g = float.Parse(parts[1]);
                float b = float.Parse(parts[2]);
                float a = parts.Length >= 4 ? float.Parse(parts[3]) : 1f;
                return new Color(r, g, b, a);
            }
            catch
            {
                return Color.blue;
            }
        }

        Vector3 ParseVec3(string s)
        {
            try
            {
                var parts = s.Split(',');
                if (parts.Length < 3) return Vector3.zero;
                float x = float.Parse(parts[0]);
                float y = float.Parse(parts[1]);
                float z = float.Parse(parts[2]);
                return new Vector3(x, y, z);
            }
            catch
            {
                return Vector3.zero;
            }
        }

        void UpdateFromConfig()
        {
            try
            {
                scanColorHead = ParseColor(cfgScanColorHead.Value);
                scanColor = ParseColor(cfgScanColor.Value);
                outlineWidth = cfgOutlineWidth.Value;
                scanLineWidth = cfgScanLineWidth.Value;
                scanLineInterval = cfgScanLineInterval.Value;
                headScanLineWidth = cfgHeadScanLineWidth.Value;
                scanLineBrightness = cfgScanLineBrightness.Value;
                scanRange = cfgScanRange.Value;
                outlineBrightness = cfgOutlineBrightness.Value;
                headScanLineDistance = cfgHeadScanLineDistance.Value;
                scanCenterWS = ParseVec3(cfgScanCenterWS.Value);
                outlineStarDistance = cfgOutlineStarDistance.Value;
                scanCooldown = cfgScanCooldown.Value;
                sfxVolume = cfgSfxVolume.Value;
            }
            catch
            {
            }

            try
            {
                onChanged?.Invoke(this);
            }
            catch
            {
            }
        }

        // initial population
        UpdateFromConfig();

        // subscribe to changes
        try
        {
            cfgScanColorHead.SettingChanged += (s, e) => UpdateFromConfig();
            cfgScanColor.SettingChanged += (s, e) => UpdateFromConfig();
            cfgOutlineWidth.SettingChanged += (s, e) => UpdateFromConfig();
            cfgScanLineWidth.SettingChanged += (s, e) => UpdateFromConfig();
            cfgScanLineInterval.SettingChanged += (s, e) => UpdateFromConfig();
            cfgHeadScanLineWidth.SettingChanged += (s, e) => UpdateFromConfig();
            cfgScanLineBrightness.SettingChanged += (s, e) => UpdateFromConfig();
            cfgScanRange.SettingChanged += (s, e) => UpdateFromConfig();
            cfgOutlineBrightness.SettingChanged += (s, e) => UpdateFromConfig();
            cfgHeadScanLineDistance.SettingChanged += (s, e) => UpdateFromConfig();
            cfgScanCenterWS.SettingChanged += (s, e) => UpdateFromConfig();
            cfgOutlineStarDistance.SettingChanged += (s, e) => UpdateFromConfig();
            cfgScanCooldown.SettingChanged += (s, e) => UpdateFromConfig();
            cfgSfxVolume.SettingChanged += (s, e) => UpdateFromConfig();
        }
        catch
        {
        }
    }

    // Helper: copy simple values from another ScanConfig (used when plugin updates values)
    public void CopyFrom(ScanConfig other)
    {
        if (other == null) return;
        scanColorHead = other.scanColorHead;
        scanColor = other.scanColor;
        outlineWidth = other.outlineWidth;
        scanLineWidth = other.scanLineWidth;
        scanLineInterval = other.scanLineInterval;
        headScanLineWidth = other.headScanLineWidth;
        scanLineBrightness = other.scanLineBrightness;
        scanRange = other.scanRange;
        outlineBrightness = other.outlineBrightness;
        headScanLineDistance = other.headScanLineDistance;
        scanCenterWS = other.scanCenterWS;
        outlineStarDistance = other.outlineStarDistance;
        steepSpawnProb = other.steepSpawnProb;
        midSpawnProb = other.midSpawnProb;
        flatSpawnProb = other.flatSpawnProb;
    }
}

// Central manager to own BepInEx binding and push config updates to registered features
public static class ScanConfigManager
{
    static ScanConfig _current;
    public static ScanConfig Current => _current;

    static event Action<ScanConfig> _onChanged;

    static readonly System.Collections.Generic.List<ScanFeature> _registered =
        new System.Collections.Generic.List<ScanFeature>();

    // Initialize and bind to plugin config. Safe to call multiple times.
    public static void Initialize(BaseUnityPlugin plugin)
    {
        if (plugin == null) return;
        if (_current != null) return;
        _current = new ScanConfig();
        try
        {
            _current.Bind(plugin, (cfg) =>
            {
                // notify manager and push to registered features
                try
                {
                    _onChanged?.Invoke(_current);
                }
                catch
                {
                }
            });
        }
        catch
        {
        }

        // ensure initial push
        try
        {
            _onChanged?.Invoke(_current);
        }
        catch
        {
        }
    }

    // Register a ScanFeature to receive config updates. Copies current config immediately.
    public static void RegisterFeature(ScanFeature feature)
    {
        if (feature == null) return;
        if (_current == null) return; // not initialized yet
        if (!_registered.Contains(feature)) _registered.Add(feature);
        try
        {
            feature.config.CopyFrom(_current);
        }
        catch
        {
        }

        // inject runtime assets (materials/particles) from the central config
        try
        {
            if (_current.scanMaterial != null) feature.config.scanMaterial = _current.scanMaterial;
            if (_current.markMaterial != null)
            {
                try { _current.markMaterial.enableInstancing = true; } catch { }
                feature.config.markMaterial = _current.markMaterial;
            }
            feature.config.markParticle1 = _current.markParticle1;
            feature.config.markParticle2 = _current.markParticle2;
            feature.config.markParticle3 = _current.markParticle3;
        }
        catch
        {
        }

        // subscribe to future updates
        _onChanged -= feature_OnConfigChanged;
        _onChanged += feature_OnConfigChanged;
    }

    // Unregister feature
    public static void UnregisterFeature(ScanFeature feature)
    {
        if (feature == null) return;
        _registered.Remove(feature);
        _onChanged -= feature_OnConfigChanged;
    }

    static void feature_OnConfigChanged(ScanConfig cfg)
    {
        // push to all registered features
        for (int i = 0; i < _registered.Count; i++)
        {
            var f = _registered[i];
            if (f == null) continue;
            try
            {
                f.config.CopyFrom(cfg);
            }
            catch
            {
            }
        }
    }
}
