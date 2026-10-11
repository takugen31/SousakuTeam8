$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
foreach ($name in @('NovelScene','NovelScene_Kayo','NovelScene_Yowashi')) {
    $scene = Get-Content (Join-Path $projectRoot "Assets/Scenes/GameMap/$name.unity") -Raw
    $template = [regex]::Match($scene,'(?ms)^--- !u!1 &1900000210\r?\n.*?(?=^--- !u!|\z)').Value
    if (-not $template.Contains('component: {fileID: 1900000215}')) { throw 'Hover must be attached to the editable template' }
    $hover = [regex]::Match($scene,'(?ms)^--- !u!114 &1900000215\r?\n.*?(?=^--- !u!|\z)').Value
    if (-not ($hover.Contains('hoverScale: 1.05') -and $hover.Contains('transitionDuration: 0.12'))) { throw 'Wrong hover settings' }
}
$harness = @'
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace UnityEngine {
    public class SerializeField : Attribute {}
    public class RangeAttribute : Attribute { public RangeAttribute(float a,float b) {} }
    public class Tooltip : Attribute { public Tooltip(string s) {} }
    public class MinAttribute : Attribute { public MinAttribute(float a) {} }
    public class RequireComponent : Attribute { public RequireComponent(Type t) {} }
    public class AddComponentMenu : Attribute { public AddComponentMenu(string s) {} }
    public struct Vector3 {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
        public static Vector3 operator *(Vector3 v,float s) => new Vector3(v.x*s,v.y*s,v.z*s);
    }
    public class Transform { public Vector3 localScale=new Vector3(2,3,1); }
    public class MonoBehaviour {
        public Transform transform=new Transform();
        public object component=new Button();
        public T GetComponent<T>() => (T)component;
    }
    public static class Time { public static float unscaledDeltaTime=0.06f; }
    public static class Mathf {
        public static float Clamp01(float x) => Math.Max(0,Math.Min(1,x));
        public static float Max(float a,float b) => Math.Max(a,b);
        public static float Lerp(float a,float b,float t) => a+(b-a)*Clamp01(t);
        public static float MoveTowards(float a,float b,float d) => a<b ? Math.Min(b,a+d) : Math.Max(b,a-d);
    }
}
namespace UnityEngine.EventSystems {
    public class PointerEventData {}
    public interface IPointerEnterHandler { void OnPointerEnter(PointerEventData e); }
    public interface IPointerExitHandler { void OnPointerExit(PointerEventData e); }
}
namespace UnityEngine.UI {
    public class Button {
        public bool interactable=true;
        public bool IsInteractable() => interactable;
    }
}
public static class HoverCheck {
    private static void Call(object o,string name) {
        o.GetType().GetMethod(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(o,null);
    }
    private static void Check(bool c,string s) { if(!c)throw new Exception(s); }
    public static void Run() {
        var hover=new ChoiceButtonHoverScale();
        Call(hover,"Awake");
        hover.OnPointerEnter(null);
        Call(hover,"Update");
        Check(Math.Abs(hover.transform.localScale.x-2.05f)<0.0001f,"Smooth halfway growth");
        Call(hover,"Update");
        Check(Math.Abs(hover.transform.localScale.x-2.1f)<0.0001f,"Five percent growth");
        Check(Math.Abs(hover.transform.localScale.y-3.15f)<0.0001f,"Preserve original scale ratio");
        hover.OnPointerExit(null);
        Call(hover,"Update");Call(hover,"Update");
        Check(hover.transform.localScale.x==2,"Exit reset");
        ((Button)hover.component).interactable=false;
        hover.OnPointerEnter(null);Call(hover,"Update");
        Check(hover.transform.localScale.x==2,"Disabled buttons cannot hover");
        ((Button)hover.component).interactable=true;
        Call(hover,"Update");Call(hover,"Update");
        Check(Math.Abs(hover.transform.localScale.x-2.1f)<0.0001f,"Delayed activation can hover");
        Call(hover,"OnDisable");
        Check(hover.transform.localScale.x==2,"Hidden button resets scale");
        Call(hover,"Update");
        Check(hover.transform.localScale.x==2,"Hidden state clears pointer");
        Check(ChoiceButtonHoverScale.EvaluateScale(-1,1.05f)==1,"Progress lower clamp");
        Check(ChoiceButtonHoverScale.EvaluateScale(2,1.05f)==1.05f,"Progress upper clamp");
    }
}
'@
$source = Get-Content (Join-Path $projectRoot 'Assets/Scripts/Dialogue/Runtime/ChoiceButtonHoverScale.cs') -Raw
Add-Type -TypeDefinition ($harness + ($source -replace '(?m)^using .*;\s*$',''))
[HoverCheck]::Run()
Write-Output 'PASS: Three editable templates have 5% hover animation. Actual lifecycle checks pass for enter/exit, disabled buttons, delayed activation and cleanup.'
