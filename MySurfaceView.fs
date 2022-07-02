namespace PanoraMovie
open Android.Views
open Android.Content
open Android.Util
open Java.Interop

[<AllowNullLiteral>]
type MySurfaceView =
    inherit SurfaceView
    val onCreated : unit -> unit
    new (onC : unit -> unit, context : Context) as this = { inherit SurfaceView(context); onCreated = onC } then base.Holder.AddCallback(this)
    new (onC : unit -> unit, context : Context, attrs : IAttributeSet) as this = { inherit SurfaceView(context, attrs); onCreated = onC; } then base.Holder.AddCallback(this)
    new (onC : unit -> unit, context : Context, attrs : IAttributeSet, defStyleAttr : int) as this = { inherit SurfaceView(context, attrs, defStyleAttr); onCreated = onC; } then base.Holder.AddCallback(this)
    new (onC : unit -> unit,context : Context, attrs : IAttributeSet, defStyleAttr : int, defStyleRes : int) as this = { inherit SurfaceView(context, attrs, defStyleAttr, defStyleRes); onCreated = onC; } then base.Holder.AddCallback(this)
    interface ISurfaceHolderCallback with
        member this.SurfaceChanged(holder: ISurfaceHolder, format: Android.Graphics.Format, width: int, height: int): unit = 
            ()
        member this.SurfaceCreated(holder: ISurfaceHolder): unit = 
            this.onCreated ()
        member this.SurfaceDestroyed(holder: ISurfaceHolder): unit = 
            ()