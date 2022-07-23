namespace PanoraMovie

open Android.App
open Android.Widget
open AndroidX.ConstraintLayout.Widget
open System.Threading

/// <summary>シークバーのアニメーションのための列挙型</summary>
type SeekingSeekbarAnimationState = 
    /// <summary>非表示</summary>
    | SSASInvisible 
    /// <summary>出現アニメーション中</summary>
    | SSASAppearing
    /// <summary>表示</summary>
    | SSASAppeared
    /// <summary>消失アニメーション中</summary>
    | SSASDisappearing

[<Activity (Label = "Player", Icon = "@mipmap/icon")>]
type PlayerActivity () as self =
    inherit Activity ()
    let TAG = "PANORAMOVIE_PLAYER"
    // フェードアニメーションにかかる時間
    let APPEARING_TIME = 200.0f
    // シークバー関連
    let mutable seekBar : Option<MySurfaceView * (GyroData.GyroData -> unit) * (float32 -> float32 -> unit) * (float32 * float32 -> float32)> = None
    // @id/mainVideoView
    let mutable videoView : VideoView = null
    // @id/startPauseButton
    let mutable playPauseButton : Button = null
    // シークバーの描画更新のためのスレッド
    let mutable seekbarUpdateThread : Async<unit> option = None
    // 上のスレッドを終了する
    let mutable cancellationTokenSource : CancellationTokenSource = null
    // ポーズ状態でシークしているか？
    let mutable seekingPaused : bool = false
    // シークバーのフェードアニメーションのための変数
    let mutable seekingElasped : int = 0
    // シークバーのアニメーション状態
    let mutable seekingStatus : SeekingSeekbarAnimationState = SSASInvisible
    // シークが沿う方向
    let mutable isSeekByDirection : bool = false

    override this.OnStop () =
        base.OnStop ()
        // ループスレッドの終了
        if not <| isNull cancellationTokenSource then
            cancellationTokenSource.Cancel()

    /// <summary>シークバーのアニメーション更新</summary>
    /// <param name="diff">更新間隔（ミリ秒）</param>
    /// <returns>シークバーの透明度</returns>
    member this.SSAS (diff : int) : float32 =
        match seekingStatus with
        | SSASInvisible -> 0.0f
        | SSASAppeared -> 1.0f
        | SSASAppearing -> 
            seekingElasped <- seekingElasped + diff
            if seekingElasped > int APPEARING_TIME then // 出現完了ならば
                seekingElasped <- int APPEARING_TIME
                seekingStatus <- SSASAppeared
            (float32 seekingElasped) / APPEARING_TIME
        | SSASDisappearing -> 
            seekingElasped <- seekingElasped - diff
            if seekingElasped < 0 then
                seekingElasped <- 0
                seekingStatus <- SSASInvisible
            (float32 seekingElasped) / APPEARING_TIME

    /// <summary>シークバーの表示メソッド</summary>
    member this.SeekBarUpdateThread () = async {
        let! ct = Async.CancellationToken
        if ct.IsCancellationRequested then
            return () // ここでチェックせずにdoDrawをするとヌルポで死ぬ．雑なのでもっとちゃんとしなきゃいけない
        if (not <| isNull videoView) then
            let pos = (float32 videoView.CurrentPosition) / (float32 videoView.Duration)
            let (_,_,doDraw, _) = seekBar.Value
            doDraw pos (this.SSAS 33)
            this.RunOnUiThread (fun () -> playPauseButton.SetText(if videoView.IsPlaying then Resource.String.pause else Resource.String.start))
        do! Async.Sleep 33
        do! this.SeekBarUpdateThread ()
    }

    /// <summary>シークする．</summary>
    /// <params name="ev">タッチイベント</params>
    member this.seek (ev : Android.Views.View.TouchEventArgs) : unit =
        if not <| isNull videoView && Option.isSome seekBar then
            let (sb, _, _, near) = seekBar.Value
            let x = ev.Event.GetX() / (float32 sb.MeasuredWidth)
            let y = ev.Event.GetY() / (float32 sb.MeasuredHeight)
            match ev.Event.Action with
            | Android.Views.MotionEventActions.Down ->
                sb.Visibility <- Android.Views.ViewStates.Visible
                if videoView.IsPlaying then
                    videoView.Pause()
                    seekingPaused <- true
                seekingStatus <- SSASAppearing
            | Android.Views.MotionEventActions.Up
            | Android.Views.MotionEventActions.Cancel ->
                videoView.SeekTo <| int (float32 videoView.Duration * (if isSeekByDirection then near (x, y) else x))
                if seekingPaused then
                    videoView.Start()
                seekingStatus <- SSASDisappearing
                // 非同期に待って，フェードアウト終了後にVisibilityを変える
                Async.StartImmediate (async {
                    do! Async.Sleep ((int APPEARING_TIME) + 60)
                    this.RunOnUiThread (fun () -> sb.Visibility <- Android.Views.ViewStates.Invisible)
                })
            | Android.Views.MotionEventActions.Move ->
                videoView.SeekTo <| int (float32 videoView.Duration * (if isSeekByDirection then near (x, y) else x))
            | _ -> ()
        else
            ()

    override this.OnResume () =
        base.OnResume ()
        // 動画再生の再開
        let playerView = this.FindViewById<ConstraintLayout>(Resource.Id.playerView)
        let (sb, setter, getter, near) = PanoraSeekBar.make self
        seekBar <- Some (sb, setter, getter, near)
        let path = base.Intent.GetStringExtra "path"
        sb.Visibility <- Android.Views.ViewStates.Invisible
        playerView.AddView sb
        sb.SetZOrderOnTop true
        // 一応キャッシュを作り直しておく
        System.IO.Path.ChangeExtension(path, ".gyro") |> System.IO.File.OpenRead |> GyroData.parseGyroFile |> setter
        let thread = this.SeekBarUpdateThread ()
        let cts = new CancellationTokenSource()
        seekbarUpdateThread <- Some thread
        cancellationTokenSource <- cts
        Async.StartImmediate(thread, cts.Token)

    override this.OnCreate (bundle) =
        base.OnCreate (bundle)
        this.SetContentView (Resource.Layout.Player)
        videoView <- this.FindViewById<VideoView>(Resource.Id.mainVideoView)
        playPauseButton <- this.FindViewById<Button>(Resource.Id.startPauseButton)
        let playerView = this.FindViewById<ConstraintLayout>(Resource.Id.playerView)
        let seekByButton = this.FindViewById<Button>(Resource.Id.seekByButton)
        Event.add (fun _ ->
            if videoView.IsPlaying then
                videoView.Pause()
                playPauseButton.SetText(Resource.String.start)
            else
                videoView.Start()
                playPauseButton.SetText(Resource.String.pause)
            ) playPauseButton.Click
        Event.add (fun _ -> 
            isSeekByDirection <- not isSeekByDirection
            seekByButton.SetText (if isSeekByDirection then Resource.String.seekByTime else Resource.String.seekByDir)
            ) seekByButton.Click
        videoView.SetVideoPath(base.Intent.GetStringExtra "path")
        videoView.Start()
        Event.add this.seek playerView.Touch
        playPauseButton.SetText(Resource.String.pause)
        videoView <- videoView
