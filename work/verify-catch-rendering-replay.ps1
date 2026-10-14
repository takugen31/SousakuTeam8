$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content (Join-Path $root 'Assets/FallingGame/Scripts/CatchMiniGameController.cs') -Raw
$start = $source.IndexOf('private void ApplyRenderingQuality()')
$end = $source.IndexOf('public void SpawnItems(', $start)
$methods = $source.Substring($start, $end-$start)
# Execute the actual lifecycle methods against per-quality-level pipeline slots.
Add-Type -TypeDefinition @"
using System;
namespace CatchRenderingReplayChecks {
public class RenderPipelineAsset { public string name; public bool destroyed; }
public enum HideFlags { HideAndDontSave }
public class UniversalRenderPipelineAsset : RenderPipelineAsset {
    public float renderScale; public int msaaSampleCount; public HideFlags hideFlags;
}
public class Camera { public bool allowMSAA, allowDynamicResolution; }
public static class ScalableBufferManager { public static void ResizeBuffers(float x, float y) {} }
public static class QualitySettings {
    public static string[] names = { "Low", "Ultra" };
    public static RenderPipelineAsset[] pipelines;
    public static int level, globalTextureMipmapLimit, antiAliasing;
    public static RenderPipelineAsset renderPipeline { get { return pipelines[level]; } set { pipelines[level] = value; } }
    public static int GetQualityLevel() { return level; }
    public static void SetQualityLevel(int value, bool apply) { level = value; }
}
public class Controller {
    private int originalQualityLevel, originalTextureMipmapLimit, originalAntiAliasing, miniGameQualityLevel;
    private RenderPipelineAsset originalRenderPipeline, miniGameOriginalRenderPipeline;
    private UniversalRenderPipelineAsset runtimeRenderPipeline;
    private bool renderingQualityApplied;
    public bool forceHighestQualityLevel = true;
    private float renderScale = 2f;
    private int antiAliasingSamples = 4;
    private Camera gameCamera = new Camera();
    private UniversalRenderPipelineAsset Instantiate(UniversalRenderPipelineAsset original) {
        if (original.destroyed) throw new Exception("Reused destroyed pipeline");
        return new UniversalRenderPipelineAsset { name = original.name };
    }
    private void Destroy(UniversalRenderPipelineAsset pipeline) {
        foreach (var slot in QualitySettings.pipelines)
            if (Object.ReferenceEquals(slot, pipeline)) throw new Exception("Destroyed pipeline still installed in quality slot");
        pipeline.destroyed = true;
    }
    public void Enter() { ApplyRenderingQuality(); }
    public void Leave() { OnDestroy(); }
$methods
} }
"@
foreach ($initialLevel in @(0, 1)) {
    foreach ($forceHighest in @($true, $false)) {
        $low = [CatchRenderingReplayChecks.UniversalRenderPipelineAsset]::new(); $low.name = 'Low source'
        $ultra = [CatchRenderingReplayChecks.UniversalRenderPipelineAsset]::new(); $ultra.name = 'Ultra source'
        [CatchRenderingReplayChecks.QualitySettings]::pipelines = @($low, $ultra)
        [CatchRenderingReplayChecks.QualitySettings]::level = $initialLevel
        [CatchRenderingReplayChecks.QualitySettings]::globalTextureMipmapLimit = 2
        [CatchRenderingReplayChecks.QualitySettings]::antiAliasing = 0
        foreach ($round in 1..5) {
            $controller = [CatchRenderingReplayChecks.Controller]::new()
            $controller.forceHighestQualityLevel = $forceHighest
            $controller.Enter()
            $controller.Leave()
            $controller.Leave()
            if (-not [object]::ReferenceEquals([CatchRenderingReplayChecks.QualitySettings]::pipelines[0], $low) -or
                -not [object]::ReferenceEquals([CatchRenderingReplayChecks.QualitySettings]::pipelines[1], $ultra)) { throw 'Quality slot was not restored' }
            if ([CatchRenderingReplayChecks.QualitySettings]::level -ne $initialLevel -or
                [CatchRenderingReplayChecks.QualitySettings]::globalTextureMipmapLimit -ne 2 -or
                [CatchRenderingReplayChecks.QualitySettings]::antiAliasing -ne 0) { throw 'Original quality state not restored' }
        }
    }
}
'PASS: actual apply/destroy lifecycle over 20 repeated entries, Low/Ultra starts, forced/unforced quality, per-level pipeline restoration before destruction and idempotent cleanup.'
