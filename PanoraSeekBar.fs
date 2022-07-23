module PanoraMovie.PanoraSeekBar
open Android.Views
open Android.Content
open Android.Util
open Java.Interop
open System.IO
open System
open Android.Graphics
open System.Threading.Tasks

/// <summary>シークバーを作る</summary>
/// <params name="cont">アクティビティなどのコンテキストを渡す</params>
/// <returns>(SurfaceView, ジャイロデータのsetter, 描画（引数は時間，透明度）, 方向でシークするときに選ばれる時間（引数はタップ座標）)</returns>
let make (cont : Context) : MySurfaceView * (GyroData.GyroData -> unit) * (float32 -> float32 -> unit) * (float32 * float32 -> float32) =
    let painter : Paint = new Paint()
    let mutable holder : ISurfaceHolder = null
    // 表示領域の大きさ
    let mutable size : Option<int * int> = None
    // ジャイロデータ
    let mutable gyroData : Option<GyroData.GyroData> = None
    let mutable (minV, maxV) = (0f, 0f)
    let mutable background : Option<Bitmap> = None
    let mutable backgroundTask : Task = Task.CompletedTask
    // そのGyroSegmentの座標を求める
    let getXY (firstTimeStamp : int64) (lastTimeStamp : int64) (width : int) (height : int) (gs : GyroData.GyroSegment) : (float32 * float32) =
        let duration = lastTimeStamp - firstTimeStamp
        let vDist = maxV - minV
        let ts = (double (gs.TimeStamp - firstTimeStamp)) / double duration
        let v = (gs.Value - minV) / vDist
        let x = float width * ts
        let y = float32 height * v
        (float32 x, y)

    // GyroSegmentに対するforeach
    let forEachGyroSegment (width : int) (height : int) (gd : GyroData.GyroData) (f : float32 -> float32 -> float32 -> float32 -> unit) =
        let mutable (px, py) = getXY gd.Values[0].TimeStamp gd.Values[gd.Values.Length - 1].TimeStamp width height gd.Values[0]
        // Seq.foldでできるけど…
        for gs in gd.Values |> Seq.tail do
            let (x, y) = getXY gd.Values[0].TimeStamp gd.Values[gd.Values.Length - 1].TimeStamp width height gs
            f x y px py
            px <- x
            py <- y

    // 背景キャッシュの生成
    let generateBackground () =
        match (gyroData, size) with
        | (Some gd, Some (width, height)) ->
            let bmp =
                match background with
                | Some(bmp) ->
                    background <- None
                    if bmp.Width = width && bmp.Height = height then
                        bmp
                    else // リサイズが起きたとき
                        bmp.Dispose()
                        Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888)
                | None -> Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888)
            let canvas = new Canvas(bmp)
            let painter = new Paint()
                   
            canvas.DrawColor(0, BlendMode.Clear)
            canvas.DrawColor(new Color(0, 0, 0, 128))
            painter.Color <- new Color(255, 255, 255)
            painter.StrokeWidth <- 8.0f
            forEachGyroSegment width height gd (fun x y px py -> 
                canvas.DrawLine(px, py, x, y, painter)
            )
            background <- Some bmp
        | _ -> ()

    // 時間と透明度をもらって背景キャッシュと丸を描画する
    let doDraw (t : float32) alpha : unit =
        match (backgroundTask.IsCompleted, gyroData, size, background) with
        | (true, Some gd, Some (width, height), Some bg) ->
            let cur = t * float32 width
            let canvas = holder.LockCanvas ()
            if not <| isNull canvas then
                canvas.DrawColor(0, BlendMode.Clear)
                painter.Color <- new Color(255, 255, 255, int (255.0f * alpha))
                canvas.DrawBitmap(bg, 0.0f, 0.0f, painter)

                forEachGyroSegment width height gd (fun x y px py -> 
                    if px <= cur && cur < x then // 入っていたら
                        let yy = py + (y - py) / (x - px) * (cur - px)
                        canvas.DrawCircle(cur, yy, 24.0f, painter)
                )
                if cur >= float32 width then
                    canvas.DrawCircle(float32 width, float32 height, 24.0f, painter)

                holder.UnlockCanvasAndPost canvas
        | _ -> ()

    let points = new System.Collections.Generic.List<float32>()
    // 方向によるシークの時，タッチ位置から時間を求める
    let seekByDirection (x, y) = 
        match (gyroData, size) with
        | (Some gd, Some (width, height)) ->
            let xx = x * float32 width
            let yy = y * float32 height
            points.Clear()
            forEachGyroSegment width height gd (fun x y px py -> 
                if (py <= yy && yy <= y) || (yy <= py && y <= yy) then // 入っていたら
                    points.Add (px + ((x - px) * (yy - py) / (y - py)))
            )
            try
                (points |> Seq.minBy (fun x -> abs(x - xx))) / float32 width
            with
                | _ -> 0.0f
        | _ -> x
    // ジャイロデータのsetter
    let gyroSet (gd : GyroData.GyroData) =
        let minmaxV = Utils.Array.minMax (fun (gs : GyroData.GyroSegment) -> gs.Value) gd.Values
        minV <- fst minmaxV
        maxV <- snd minmaxV
        gyroData <- Some gd
        backgroundTask <- backgroundTask.ContinueWith(fun _ -> generateBackground ())
    let onChanged (h : ISurfaceHolder) _ (width : int) (height : int) =
        holder <- h
        size <- Some (width, height)
        backgroundTask <- backgroundTask.ContinueWith(fun _ -> generateBackground ())

    (new MySurfaceView((fun h -> h.SetFormat(Format.Translucent)), onChanged, cont), gyroSet, doDraw, seekByDirection)
