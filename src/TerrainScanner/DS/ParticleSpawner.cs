using System;
using UnityEngine;

namespace TerrainScanner.DS;

public static class ParticleSpawner
{
    // Centralized particle spawn helper. index chooses one of three configured prefabs.
    public static void ShootParticle(Vector3 position, Vector3 normal, int index, ScanConfig config)
    {
        try
        {
            if (config == null) return;
            GameObject prefab = index switch { 3 => config.markParticle3, 2 => config.markParticle2, _ => config.markParticle1 };
            if (prefab == null) return;
            var instance = UnityEngine.Object.Instantiate(prefab);
            if (instance == null) return;
            instance.transform.position = position;
            float scale = UnityEngine.Random.Range(0.5f, 1.5f);
            instance.transform.localScale = Vector3.one * scale;
            var ps = instance.GetComponentInChildren<ParticleSystem>();
            ps?.Play();
        }
        catch (Exception ex)
        {
            TerrainScannerPlugin.Logger?.LogWarning($"[ParticleSpawner] ShootParticle failed: {ex}");
        }
    }
}
