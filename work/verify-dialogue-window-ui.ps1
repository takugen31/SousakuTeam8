$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
function Assert-True([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
function Get-Block([string]$scene, [string]$id) {
    return [regex]::Match($scene,'(?ms)^--- !u!\d+ &'+$id+'\r?\n.*?(?=^--- !u!|\z)').Value
}
foreach ($name in @('NovelScene','NovelScene_Kayo','NovelScene_Yowashi')) {
    $scene = Get-Content (Join-Path $projectRoot "Assets/Scenes/GameMap/$name.unity") -Raw
    $panel = Get-Block $scene '2100000001'
    $plate = Get-Block $scene '2100000101'
    Assert-True ($panel.Contains('m_AnchorMin: {x: 0.075, y: 0.0148875}') -and $panel.Contains('m_AnchorMax: {x: 0.925, y: 0.2758625}')) 'Window must retain original width and enlarge height 30 percent about the original center'
    Assert-True ($plate.Contains('m_AnchorMin: {x: 0.10475, y: 0.24575}') -and $plate.Contains('m_AnchorMax: {x: 0.2875, y: 0.3145}')) 'Name plate original screen position/size lost'
    Assert-True ($plate.Contains('m_Father: {fileID: 1164511897}')) 'Name box cannot remain relative to resized panel'
    Assert-True ((Get-Block $scene '1164511897').Contains('  - {fileID: 2100000101}') -and -not $panel.Contains('  - {fileID: 2100000101}')) 'Hierarchy parent/children mismatch'
    $image = Get-Block $scene '2100000003'
    Assert-True ($image.Contains('m_Color: {r: 0, g: 0, b: 0, a: 0.72}') -and $image.Contains('m_Sprite: {fileID: 0}')) 'Background must be translucent black without a new image'
    $effect = Get-Block $scene '2100000004'
    Assert-True ($effect.Contains('guid: e280b580b54c44b8a16472794c088976') -and $effect.Contains('featherWidth: {x: 64, y: 24}')) 'Feather effect missing'
    Assert-True (-not $effect.Contains('UnityEngine.UI.Outline')) 'Hard outline remains on message window'
    $body = Get-Block $scene '1664412071'
    Assert-True ($body.Contains('m_AnchorMin: {x: 0.05, y: 0.1}') -and $body.Contains('m_AnchorMax: {x: 0.95, y: 0.86}')) 'Text needs readable edge padding'
    foreach ($height in 720,1080,1440,2160) {
        $oldCenter = (0.045 + 0.24575) * 0.5 * $height
        $newCenter = (0.0148875 + 0.2758625) * 0.5 * $height
        Assert-True ([Math]::Abs($oldCenter-$newCenter) -lt 0.00001) 'Window center moved under screen scaling'
        Assert-True ([Math]::Abs((0.2758625-0.0148875)/(0.24575-0.045)-1.3) -lt 0.00001) 'Height must increase exactly 30 percent'
        Assert-True ($panel.Contains('m_AnchorMin: {x: 0.075,') -and $panel.Contains('m_AnchorMax: {x: 0.925,')) 'Width must match the original window'
        $bodyHeight = (0.2758625-0.0148875)*($height)*(0.86-0.1)
        Assert-True ($bodyHeight -gt 100) 'Text area collapsed'
    }
    $ids = @([regex]::Matches($scene,'(?m)^--- !u!\d+ &(\d+)') | ForEach-Object { $_.Groups[1].Value })
    Assert-True (($ids | Sort-Object -Unique).Count -eq $ids.Count) 'Duplicate object ID'
    foreach ($match in [regex]::Matches($scene,'\{fileID: (\d+)\}')) {
        if ($match.Groups[1].Value -ne '0') { Assert-True ($ids -contains $match.Groups[1].Value) 'Missing scene object reference' }
    }
}
$moteruHash = (Get-FileHash (Join-Path $projectRoot 'Assets/Scenes/GameMap/NovelScene_Moteru.unity') -Algorithm SHA256).Hash
Assert-True ($moteruHash -ceq 'DE0C96BD54F54957C2AE77E76A3524B826F3048858BBF3D9902B204A5C8B50B8') 'Moteru scene must not change'

# Compile/run the actual mesh generator with a minimal non-rendering UI harness.
$harness = @'
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace UnityEngine {
    public class SerializeField : Attribute {}
    public class Tooltip : Attribute { public Tooltip(string s) {} }
    public class RangeAttribute : Attribute { public RangeAttribute(int a,int b) {} }
    public class AddComponentMenu : Attribute { public AddComponentMenu(string s) {} }
    public struct Vector2 { public float x,y; public Vector2(float a,float b) { x=a; y=b; } }
    public struct Vector3 { public float x,y,z; public Vector3(float a,float b,float c) { x=a; y=b; z=c; } }
    public struct Rect {
        public float xMin,yMin,width,height;
        public Rect(float x,float y,float w,float h) { xMin=x;yMin=y;width=w;height=h; }
    }
    public struct Color {
        public float r,g,b,a;
        public Color(float r,float g,float b,float a) { this.r=r;this.g=g;this.b=b;this.a=a; }
    }
    public struct UIVertex {
        public Vector3 position;
        public Color color;
        public static UIVertex simpleVert => new UIVertex();
    }
    public static class Mathf {
        public static float Min(float a,float b) => Math.Min(a,b);
        public static float Clamp01(float v) => Clamp(v,0f,1f);
        public static float Clamp(float v,float a,float b) => Math.Max(a,Math.Min(b,v));
        public static int Clamp(int v,int a,int b) => Math.Max(a,Math.Min(b,v));
    }
}
namespace UnityEngine.UI {
    public class Graphic {
        public Rect rect = new Rect(-816,-108.405f,1632,216.81f);
        public Color color = new Color(0,0,0,0.72f);
        public Rect GetPixelAdjustedRect() => rect;
    }
    public class BaseMeshEffect {
        public Graphic graphic = new Graphic();
        public bool enabled = true;
        public bool IsActive() => enabled;
        public virtual void ModifyMesh(VertexHelper v) {}
    }
    public class VertexHelper {
        public List<UIVertex> vertices = new List<UIVertex>();
        public List<int> indices = new List<int>();
        public int currentVertCount => vertices.Count;
        public void PopulateUIVertex(ref UIVertex v,int i) { v=vertices[i]; }
        public void Clear() { vertices.Clear();indices.Clear(); }
        public void AddVert(UIVertex v) { vertices.Add(v); }
        public void AddTriangle(int a,int b,int c) { indices.Add(a);indices.Add(b);indices.Add(c); }
    }
}
public static class FeatherMeshCheck {
    private static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    private static VertexHelper Seed() { var v=new VertexHelper();v.AddVert(UIVertex.simpleVert);return v; }
    public static void Run() {
        var effect=new DialogueWindowFeather();
        var mesh=Seed();
        effect.ModifyMesh(mesh);
        Check(mesh.currentVertCount==324 && mesh.indices.Count==1734,"Mesh resolution");
        float maxAlpha=0;
        var rect=effect.graphic.rect;
        foreach(var v in mesh.vertices) {
            Check(v.color.r==0 && v.color.g==0 && v.color.b==0,"Not black");
            Check(v.color.a>=0 && v.color.a<=0.72001f,"Invalid alpha");
            maxAlpha=Math.Max(maxAlpha,v.color.a);
            float x=v.position.x-rect.xMin, y=v.position.y-rect.yMin;
            if(x==0 || y==0 || x==rect.width || y==rect.height)Check(v.color.a<0.00001f,"Visible outer edge");
        }
        Check(Math.Abs(maxAlpha-0.72f)<0.00001f,"Center opacity");
        foreach(int i in mesh.indices)Check(i>=0 && i<mesh.currentVertCount,"Bad triangle index");
        float previous=0;
        for(int i=0;i<=64;i++) {
            float a=DialogueWindowFeather.EdgeOpacity(i,1632,64);
            Check(a>=previous,"Alpha must increase toward center");
            Check(Math.Abs(a-DialogueWindowFeather.EdgeOpacity(1632-i,1632,64))<0.00001f,"Asymmetric edge fade");
            previous=a;
        }
        Check(DialogueWindowFeather.EdgeOpacity(32,1632,64)==0.5f,"Smooth half-fade");
        Check(DialogueWindowFeather.EdgeOpacity(0,1632,0)==1,"Disabled feather axis");
        effect.graphic.rect=new Rect(0,0,20,10);
        mesh=Seed();effect.ModifyMesh(mesh);
        Check(mesh.currentVertCount>0,"Tiny panel");
        foreach(var v in mesh.vertices)Check(v.position.x>=0 && v.position.x<=20 && v.position.y>=0 && v.position.y<=10,"Tiny panel overflow");
        effect.graphic.rect=new Rect(0,0,0,10);
        mesh=Seed();effect.ModifyMesh(mesh);Check(mesh.currentVertCount==0,"Zero-sized panel");
        effect.enabled=false;mesh=Seed();effect.ModifyMesh(mesh);Check(mesh.currentVertCount==1,"Disabled effect should preserve original image");
    }
}
'@
$source = Get-Content (Join-Path $projectRoot 'Assets/Scripts/Dialogue/Runtime/DialogueWindowFeather.cs') -Raw
Add-Type -TypeDefinition ($harness + ($source -replace '(?m)^using .*;\s*$',''))
[FeatherMeshCheck]::Run()
Write-Output 'PASS: Three scene windows retain original width and are 30 percent taller with the same center at four resolutions; name-box position is unchanged.'
Write-Output 'PASS: Actual mesh generator has transparent edges, 72% black center, smooth symmetric fade and valid triangles; tiny/zero/disabled cases pass.'
Write-Output 'PASS: Scene hierarchy/references are intact; Moteru scene is unchanged.'
