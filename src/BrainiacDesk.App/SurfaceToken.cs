using System.Windows;
using System.Windows.Controls;
namespace BrainiacDesk.App;
public static class SurfaceToken
{
    public static readonly DependencyProperty KeyProperty=DependencyProperty.RegisterAttached("Key",typeof(string),typeof(SurfaceToken),new PropertyMetadata(null,(o,e)=> { if(o is Control control && e.NewValue is string key) control.SetResourceReference(Control.BackgroundProperty,key); }));
    public static void SetKey(DependencyObject o,string value)=>o.SetValue(KeyProperty,value);
    public static string GetKey(DependencyObject o)=>(string)o.GetValue(KeyProperty);
    public static readonly DependencyProperty IconProperty=DependencyProperty.RegisterAttached("Icon",typeof(string),typeof(SurfaceToken),new PropertyMetadata(null,(o,e)=> { if(o is Image image && e.NewValue is string key) image.SetResourceReference(Image.SourceProperty,"Icon."+key); }));
    public static void SetIcon(DependencyObject o,string value)=>o.SetValue(IconProperty,value);
    public static string GetIcon(DependencyObject o)=>(string)o.GetValue(IconProperty);
}
