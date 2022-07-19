namespace PanoraMovie

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

    let viewHolderCreator (v : View) =
        let tv = v.FindViewById<TextView>(Resource.Id.gallary_item_text)
        let iv = v.FindViewById<ImageView>(Resource.Id.gallary_item_image)
        if not <| isNull iv then
            iv.LayoutParameters <- new LinearLayout.LayoutParams(contentSize.Value / 2, contentSize.Value / 2)
        if isNull tv || isNull iv then null else new CSharp.GallaryAdapter.ViewHolder(tv, iv)

    let onItemClick (parent : AdapterView) (v : View) (position : int) (id : int64) = 
        let s = parent.GetItemAtPosition(position).JavaCast<Java.Lang.String>()
        if isNull s then
            ()
        else
            let i = new Intent(self, typeof<PlayerActivity>)
            i.PutExtra("path", s.ToString()) |> ignore
            self.StartActivity(i)

    override this.OnCreate (bundle) =
        base.OnCreate (bundle)
        // Set our view from the "gallary" layout resource
        this.SetContentView (Resource.Layout.Gallary)
        //preview.Id <- View.GenerateViewId()
        let gridView = this.FindViewById<GridView>(Resource.Id.gallaryView)
        let gridAdapter = new CSharp.GallaryAdapter(this, Resource.Layout.GallaryItem, getData(), viewHolderCreator)
        gridView.Adapter <- gridAdapter
        gridView.OnItemClickListener <- new CSharp.ItemClickListener(onItemClick)
        ()

    //override this.OnDestroy () =
    //    base.OnDestroy ()
