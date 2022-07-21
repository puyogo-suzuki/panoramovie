namespace PanoraMovie
#nowarn "44"

open System

open Android.App
open Android.Content
open Android.OS
open Android.Runtime
open Android.Views
open Android.Widget
open Android.Hardware.Camera2
open Android.Util
open AndroidX.ConstraintLayout.Widget
open System.Threading
open Android.Media

[<Activity (Label = "Gallary", Icon = "@mipmap/icon")>]
type GallaryActivity () as self =
    inherit Activity ()
    let TAG = "PANORAMOVIE_GALLARY"
    let contentSize = lazy self.WindowManager.CurrentWindowMetrics.Bounds.Width()

    let getData () =
        let directory = if Environment.ExternalStorageState = Environment.MediaMounted then self.GetExternalFilesDir(null).Path else self.FilesDir.Path
        let files = System.IO.Directory.GetFiles directory
        let filtered = files |> Seq.filter (fun a -> System.IO.Path.GetExtension a = ".mp4")
                             |> Seq.filter (fun a -> System.IO.Path.ChangeExtension(a, ".gyro") |> System.IO.File.Exists)
        filtered |> Seq.map (fun s -> new Java.Lang.String(s)) |> Seq.toArray

    //override this.OnResume () =
    //    base.OnResume ()

    //override this.OnStop () =
    //    base.OnStop ()

    let viewGenerator (context : Activity) (this : CSharp.ListViewAdapter<Java.Lang.String>) (position : int) (convertView : View) (parent : ViewGroup) : View =
        let getViews (v : View) =
            let tv = v.FindViewById<TextView>(Resource.Id.gallary_item_text)
            let iv = v.FindViewById<ImageView>(Resource.Id.gallary_item_image)
            if not <| isNull iv then
                iv.LayoutParameters <- new LinearLayout.LayoutParams(contentSize.Value / 2, contentSize.Value / 2)
            (v, tv, iv)
        let genViewHolder () = getViews <| context.LayoutInflater.Inflate(Resource.Layout.GallaryItem, parent, false)
        let tryConvertView () =
            try getViews convertView
            with _ -> genViewHolder ()
        let (ret, tv, iv) = if isNull convertView then genViewHolder() else tryConvertView()
        let item = this.GetItem(position).ToString()
        let createThumbnail () =
            if OperatingSystem.IsAndroidVersionAtLeast 29 then
                ThumbnailUtils.CreateVideoThumbnail(new Java.IO.File(item), new Android.Util.Size(128, 128), null)
            else
                ThumbnailUtils.CreateVideoThumbnail(item, Android.Provider.ThumbnailKind.MiniKind)
        tv.Text <- System.IO.Path.GetFileName(item)
        try
            iv.ContentDescription <- System.IO.Path.GetFileName(item)
            iv.SetImageBitmap (createThumbnail ())
        with
            _ -> ()
        ret

    let onItemClick (parent : AdapterView) (v : View) (position : int) (id : int64) = 
        let s = parent.GetItemAtPosition(position).JavaCast<Java.Lang.String>()
        if isNull s then
            ()
        else
            let i = new Intent(self, typeof<PlayerActivity>)
            i.PutExtra("path", s.ToString()) |> ignore
            self.StartActivity(i)

    let onItemLongClick (parent : AdapterView) (v : View) (position : int) (id : int64) : bool = 
        let s = parent.GetItemAtPosition(position).JavaCast<Java.Lang.String>()
        if isNull s then
            false
        else
            let i = new Intent(self, typeof<DetailActivity>)
            i.PutExtra("path", s.ToString()) |> ignore
            self.StartActivity(i)
            true


    override this.OnResume () =
        base.OnResume ()
        let gridView = this.FindViewById<GridView>(Resource.Id.gallaryView)
        let gridAdapter = new CSharp.ListViewAdapter<Java.Lang.String>(self :> Context, Resource.Layout.GallaryItem, getData(), viewGenerator this)
        gridView.Adapter <- gridAdapter
        ()
        
    override this.OnCreate (bundle) =
        base.OnCreate (bundle)
        // Set our view from the "gallary" layout resource
        this.SetContentView (Resource.Layout.Gallary)
        //preview.Id <- View.GenerateViewId()
        let gridView = this.FindViewById<GridView>(Resource.Id.gallaryView)
        gridView.OnItemClickListener <- new CSharp.ItemClickListener(onItemClick)
        gridView.OnItemLongClickListener <- new CSharp.ItemLongClickListener(onItemLongClick)
        ()

    //override this.OnDestroy () =
    //    base.OnDestroy ()
