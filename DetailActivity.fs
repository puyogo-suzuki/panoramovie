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

[<Activity (Label = "Detail", Icon = "@mipmap/icon")>]
type DetailActivity () =
    inherit Activity ()
    let TAG = "PANORAMOVIE_DETAIL"

    let viewGenerator (context : Activity) (this : CSharp.ListViewAdapter<CSharp.DetailInfo>) (position : int) (convertView : View) (parent : ViewGroup) : View =
        let getViews (v : View) =
            let cat = v.FindViewById<TextView>(Resource.Id.detailItemCategory)
            let va = v.FindViewById<TextView>(Resource.Id.detailItemValue)
            (v, cat, va)
        let genViewHolder () = getViews <| context.LayoutInflater.Inflate(Resource.Layout.DetailItem, parent, false)
        let tryConvertView () =
            try getViews convertView
            with _ -> genViewHolder ()
        let (v, cat, va) = if isNull convertView then genViewHolder() else tryConvertView()
        let item = this.GetItem(position)
        cat.SetText(item.Category, TextView.BufferType.Normal)
        va.SetText(item.Value, TextView.BufferType.Normal)
        v


    override this.OnCreate (bundle) =
        base.OnCreate (bundle)
        // Set our view from the "gallary" layout resource
        this.SetContentView (Resource.Layout.Detail)
        let path = this.Intent.GetStringExtra("path")
        let remove () =
            try
                System.IO.File.Delete path
                System.IO.File.Delete <| System.IO.Path.ChangeExtension(path, ".gyro")
            with
                _ -> Toast.MakeText(this :> Context, Resource.String.error_fail_remove, ToastLength.Long).Show()
            this.Finish()
        let dlist = this.FindViewById<ListView> (Resource.Id.detailList)
        let rmBtn = this.FindViewById<Button>(Resource.Id.fileRemoveButton)
        Event.add (fun _ -> remove()) rmBtn.Click
        let mmr = new Android.Media.MediaMetadataRetriever()
        try
            mmr.SetDataSource(path)
            let duration = mmr.ExtractMetadata(Android.Media.MetadataKey.Duration)
            dlist.Adapter <- new CSharp.ListViewAdapter<CSharp.DetailInfo>(this :> Context, Resource.Layout.DetailItem, [|new CSharp.DetailInfo("duration", duration)|], viewGenerator this)
            ()
        with
        _ -> 
            Toast.MakeText(this :> Context, Resource.String.error_fail_load, ToastLength.Long).Show()
            this.Finish()

    //override this.OnDestroy () =
    //    base.OnDestroy ()
