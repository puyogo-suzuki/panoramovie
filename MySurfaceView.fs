namespace PanoraMovie
open Android.Views
open Android.Content
open Android.Util
open Java.Interop

[<AllowNullLiteral>]
type MySurfaceView =
    inherit SurfaceView
    val onCreated : ISurfaceHolder -> unit
    val onSurfaceChanged : ISurfaceHolder -> Android.Graphics.Format -> int -> int -> unit 
    new (onC : ISurfaceHolder -> unit, onSC : ISurfaceHolder -> Android.Graphics.Format -> int -> int -> unit,  context : Context) as this =
        { inherit SurfaceView(context); onCreated = onC; onSurfaceChanged = onSC } then base.Holder.AddCallback(this)
    new (onC : ISurfaceHolder -> unit, onSC : ISurfaceHolder -> Android.Graphics.Format -> int -> int -> unit,  context : Context, attrs : IAttributeSet) as this =
        { inherit SurfaceView(context, attrs); onCreated = onC; onSurfaceChanged = onSC } then base.Holder.AddCallback(this)
    new (onC : ISurfaceHolder -> unit, onSC : ISurfaceHolder -> Android.Graphics.Format -> int -> int -> unit, context : Context, attrs : IAttributeSet, defStyleAttr : int) as this =
        { inherit SurfaceView(context, attrs, defStyleAttr); onCreated = onC; onSurfaceChanged = onSC } then base.Holder.AddCallback(this)
    new (onC : ISurfaceHolder -> unit, onSC : ISurfaceHolder -> Android.Graphics.Format -> int -> int -> unit, context : Context, attrs : IAttributeSet, defStyleAttr : int, defStyleRes : int) as this =
        { inherit SurfaceView(context, attrs, defStyleAttr, defStyleRes); onCreated = onC; onSurfaceChanged = onSC } then base.Holder.AddCallback(this)
    interface ISurfaceHolderCallback with
        member this.SurfaceChanged(holder: ISurfaceHolder, format: Android.Graphics.Format, width: int, height: int): unit = 
            this.onSurfaceChanged holder format width height
        member this.SurfaceCreated(holder: ISurfaceHolder): unit = 
            this.onCreated holder
        member this.SurfaceDestroyed(holder: ISurfaceHolder): unit = 
            ()