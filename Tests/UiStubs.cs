namespace UnityEngine
{
    public struct Vector2(float x,float y){public float x=x,y=y;}
    public struct Rect(float x,float y,float width,float height){public float x=x,y=y,width=width,height=height;}
    public static class GUI { public static bool enabled=true; }
    public static class Application { public static string? LastUrl; public static void OpenURL(string url)=>LastUrl=url; }
}
namespace Verse
{
    public enum GameFont { Tiny, Small, Medium }
    public static class UI { public static int screenWidth=1920,screenHeight=1080; }
    public static class Text
    {
        public static GameFont Font;
        public static int Measurements;
        public static float CalcHeight(string text,float width){Measurements++;return Math.Max(30,text.Length/50*22);}
    }
    public static class TestTranslations
    {
        public static Dictionary<string,string> Values=[];
        public static string Translate(this string key)=>Values.TryGetValue(key,out var value)?value:key;
    }
    public class FloatMenuOption(string label,Action? action) { public string Label=label;public Action? Action=action; }
    public class FloatMenu(List<FloatMenuOption> options) { public List<FloatMenuOption> Options=options; }
    public class WindowStack
    {
        public FloatMenu? LastMenu;
        public void Add(FloatMenu menu)=>LastMenu=menu;
    }
    public static class Widgets
    {
        public static string? Click;
        public static List<string> Labels=[];
        public static List<string> ButtonLabels=[];
        public static bool ButtonText(UnityEngine.Rect rect,string label)
        {
            ButtonLabels.Add(label);
            if(UnityEngine.GUI.enabled && Click==label){Click=null;return true;}return false;
        }
        public static void Label(UnityEngine.Rect r,string text)=>Labels.Add(text);
        public static void BeginScrollView(UnityEngine.Rect r,ref UnityEngine.Vector2 p,UnityEngine.Rect v){ }
        public static void EndScrollView(){ }
    }
    public static class Log {public static int Errors;public static void Error(string text)=>Errors++;}
}
namespace RimWorld
{
    public class MainTabWindow
    {
        public bool forcePause,doCloseX,absorbInputAroundWindow;
        public virtual UnityEngine.Vector2 RequestedTabSize=>new(1000,700);
        public virtual void PostOpen(){ }
        public virtual void PreClose(){ }
        public virtual void WindowUpdate(){ }
        public virtual void DoWindowContents(UnityEngine.Rect rect){ }
    }
}
