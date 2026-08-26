using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
namespace TerrainScanner.DS;
public class ActiveScan : MonoBehaviour
{
    public static KeyCode activeKey = ScanConfigManager.Current.cfgActiveKey?.Value ?? KeyCode.Q;

    // 扫描音效：从插件目录加载 mp3 后缓存，每次扫描触发时播放
    const string SfxFileName = "奥卓德克扫描音效.mp3";
    static AudioClip scanSfx;
    static bool sfxLoading = false;
    static float lastScanTime = float.MinValue; // 与 ScanCooldown 配合的共用冷却计时
    AudioSource audioSource;

    // Start is called before the first frame update
    void Start()
    {
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        if (scanSfx == null && !sfxLoading) StartCoroutine(LoadScanSfx());
    }

    IEnumerator LoadScanSfx()
    {
        sfxLoading = true;
        try
        {
            string dir = null;
            if (TerrainScannerPlugin.Instance?.Info != null && !string.IsNullOrEmpty(TerrainScannerPlugin.Instance.Info.Location))
                dir = Path.GetDirectoryName(TerrainScannerPlugin.Instance.Info.Location);
            else if (!string.IsNullOrEmpty(System.Reflection.Assembly.GetExecutingAssembly().Location))
                dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            else
                dir = BepInEx.Paths.PluginPath;

            string file = Path.Combine(dir, SfxFileName);
            if (!File.Exists(file))
            {
                TerrainScannerPlugin.Logger?.LogWarning("[ScanAudio] sfx not found: " + file);
                yield break;
            }

            using (var req = UnityWebRequestMultimedia.GetAudioClip(new System.Uri(file).AbsoluteUri, AudioType.MPEG))
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    scanSfx = DownloadHandlerAudioClip.GetContent(req);
                    TerrainScannerPlugin.Logger?.LogInfo("[ScanAudio] loaded: " + file);
                }
                else
                {
                    TerrainScannerPlugin.Logger?.LogError("[ScanAudio] load failed: " + req.error);
                }
            }
        }
        finally
        {
            sfxLoading = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!Input.GetKeyDown(activeKey)) return;

        // 共用冷却：扫描效果与扫描音效使用同一个 ScanCooldown，冷却期间不重复触发
        float cd = ScanConfigManager.Current?.scanCooldown ?? 0.8f;
        if (Time.time - lastScanTime < cd) return;

        // 只有在扫描真正可以启动时才响应（canScan），避免扫描被占用时音效仍一直触发
        if (!ScanFeature.CanScan || scanSfx == null || audioSource == null) return;
        lastScanTime = Time.time;

        // 音效立即播放（提前约 0.5s）；扫描效果在 0.5s 后才执行，实现"先响后扫描"
        float vol = ScanConfigManager.Current?.sfxVolume ?? 1f;
        audioSource.PlayOneShot(scanSfx, vol);
        StartCoroutine(RunScanDelayed(0.5f));
    }

    IEnumerator RunScanDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        // 使用摄像机的 transform（在第一人称游戏中这就是玩家视角）
        ScanFeature.ExecuteScan(transform);
        TerrainScannerPlugin.Logger.LogInfo("TerrainScanner : scan executed");
    }
}
