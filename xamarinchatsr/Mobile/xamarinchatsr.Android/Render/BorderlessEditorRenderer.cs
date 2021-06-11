using Android.Content;
using Android.Graphics.Drawables;
using xamarinchatsr.Controls;
using xamarinchatsr.Droid.Render;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android;

[assembly: ExportRenderer(typeof(BorderlessEditor), typeof(BorderlessEditorRenderer))]

namespace xamarinchatsr.Droid.Render
{
    public class BorderlessEditorRenderer : EditorRenderer
    {
        public BorderlessEditorRenderer(Context context) : base(context)
        {
        }

        protected override void OnElementChanged(ElementChangedEventArgs<Editor> e)
        {
            base.OnElementChanged(e);
            if (Control != null)
            {
                Control.Background = new ColorDrawable(Android.Graphics.Color.Transparent);
                //Control.SetPadding(0, 10, 0, 30);
                Control.SetPadding(0, 0, 0, 0);
            }
        }
    }
}