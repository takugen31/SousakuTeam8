using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class BrowserGameBuild
{
    [MenuItem("Build/Browser Playtest (16:9)")]
    public static void Build()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            throw new System.InvalidOperationException("Install Web Build Support for this Unity version first.");
        var oldTemplate = PlayerSettings.WebGL.template;
        var oldCompression = PlayerSettings.WebGL.compressionFormat;
        var oldFallback = PlayerSettings.WebGL.decompressionFallback;
        try
        {
            PlayerSettings.WebGL.template = "PROJECT:FixedAspect";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = "Builds/WebPlaytest",
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.InvalidOperationException("Web build failed: " + report.summary.result);
        }
        finally
        {
            PlayerSettings.WebGL.template = oldTemplate;
            PlayerSettings.WebGL.compressionFormat = oldCompression;
            PlayerSettings.WebGL.decompressionFallback = oldFallback;
        }
    }
}
