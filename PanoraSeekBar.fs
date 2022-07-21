module PanoraMovie.PanoraSeekBar
open Android.Views
open Android.Content
open Android.Util
open Java.Interop
open System.IO
open System
open Android.Graphics

let make (cont : Context) : MySurfaceView * (GyroData.GyroData -> unit) * (float32 option -> float32 -> unit) * (float32 * float32 -> float32) =
    let painter : Paint = new Paint()
    let mutable holder : ISurfaceHolder = null
    let mutable size : Option<int * int> = None
    let mutable gyroData : Option<GyroData.GyroData> = None
    let mutable currentAlpha : float32 = 0.0f
    let mutable prevPos : float32 = 0.0f
    let getXY (minV : float32) (maxV : float32) (firstTimeStamp : int64) (lastTimeStamp : int64) (width : int) (height : int) (gs : GyroData.GyroSegment) : (float32 * float32) =
        let duration = lastTimeStamp - firstTimeStamp
        let vDist = maxV - minV
        let ts = (double (gs.TimeStamp - firstTimeStamp)) / double duration
        let v = (gs.Value - minV) / vDist
        let x = float width * ts
        let y = float32 height * v
        (float32 x, y)
    let doDraw (spos : float32 option) alpha : unit =
        currentAlpha <- alpha
        match (gyroData, size) with
        | (Some gd, Some (width, height)) ->
            let (minV, maxV) = Utils.Array.minMax (fun (gs : GyroData.GyroSegment) -> gs.Value) gd.Values
            let getXY = getXY minV maxV gd.Values[0].TimeStamp gd.Values[gd.Values.Length - 1].TimeStamp width height
            let cur = 
                match spos with
                | None -> prevPos
                | Some t -> t * float32 width
                   
            let canvas = holder.LockCanvas ()
            if not <| isNull canvas then
                canvas.DrawColor(0, BlendMode.Clear)
                canvas.DrawColor(new Color(0, 0, 0, int (128.0f * alpha)))
                painter.Color <- new Color(255, 255, 255, int (255.0f * alpha))
                painter.StrokeWidth <- 3.0f

                let mutable (px, py) = getXY gd.Values[0]
                // Seq.foldでできるけど…
                for gs in gd.Values |> Seq.tail do
                    let (x, y) = getXY gs
                    canvas.DrawLine(px, py, x, y, painter)  
                    if px <= cur && cur < x then // 入っていたら
                        let yy = py + (y - py) / (x - px) * (cur - px)
                        canvas.DrawCircle(cur, yy, 12.0f, painter)
                    px <- x
                    py <- y
                if cur >= px then
                    canvas.DrawCircle(float32 width, float32 height, 3.0f, painter)

                holder.UnlockCanvasAndPost canvas
            else
                ()
        | _ -> ()

    let points = new System.Collections.Generic.List<float32>()

    let seekByDirection (x, y) = 
        match (gyroData, size) with
        | (Some gd, Some (width, height)) ->
            let (minV, maxV) = Utils.Array.minMax (fun (gs : GyroData.GyroSegment) -> gs.Value) gd.Values
            let xx = x * float32 width
            let yy = y * float32 height
            let getXY = getXY minV maxV gd.Values[0].TimeStamp gd.Values[gd.Values.Length - 1].TimeStamp width height
            let mutable (px, py) = getXY gd.Values[0]
            points.Clear()
            for gs in gd.Values |> Seq.tail do
                let (x, y) = getXY gs
                if (py <= yy && yy <= y) || (yy <= py && y <= yy) then // 入っていたら
                    points.Add (px + ((x - px) * (yy - py) / (y - py)))
                px <- x
                py <- y
            try
                (points |> Seq.minBy (fun x -> abs(x - xx))) / float32 width
            with
                | _ -> 0.0f
        | _ -> x
    let gyroSet gd =
        gyroData <- Some gd
        doDraw None currentAlpha
    let onChanged (h : ISurfaceHolder) _ (width : int) (height : int) =
        holder <- h
        size <- Some (width, height)
        doDraw None currentAlpha

    (new MySurfaceView((fun h -> h.SetFormat(Format.Translucent)), onChanged, cont), gyroSet, doDraw, seekByDirection)
