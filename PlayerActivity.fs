namespace PanoraMovie

open System

open Android.App
open Android.Widget
open Android.Media
open AndroidX.ConstraintLayout.Widget
open System.Threading

type SeekingSeekbarAnimationState = SSASInvisible | SSASAppearing | SSASAppeared | SSASDisappearing

[<Activity (Label = "Player", Icon = "@mipmap/icon")>]
type PlayerActivity () as self =
    inherit Activity ()
    let TAG = "PANORAMOVIE_PLAYER"
    let APPEARING_TIME = 200.0f
    let mutable seekBar : Option<MySurfaceView * (GyroData.GyroData -> unit) * (float32 option -> float32 -> unit) * (float32 * float32 -> float32)> = None
    let mutable videoView : VideoView = null
    let mutable playPauseButton : Button = null
    let mutable seekbarUpdateThread : Async<unit> option = None
    let mutable cancellationTokenSource : CancellationTokenSource = null
    let mutable seekingPaused : bool = false
    let mutable seekingElasped : int = 0
    let mutable seekingStatus : SeekingSeekbarAnimationState = SSASInvisible
    let mutable isSeekByDirection : bool = false

    override this.OnStop () =
        base.OnStop ()
        if not <| isNull cancellationTokenSource then
            cancellationTokenSource.Cancel()

    member this.SSAS (diff : int) : float32 =
        match seekingStatus with
        | SSASInvisible -> 0.0f
        | SSASAppeared -> 1.0f
        | SSASAppearing -> 
            seekingElasped <- seekingElasped + diff
            if seekingElasped > int APPEARING_TIME then
                seekingElasped <- int APPEARING_TIME
                seekingStatus <- SSASAppeared
            (float32 seekingElasped) / APPEARING_TIME
        | SSASDisappearing -> 
            seekingElasped <- seekingElasped - diff
            if seekingElasped < 0 then
                seekingElasped <- 0
                seekingStatus <- SSASInvisible
            (float32 seekingElasped) / APPEARING_TIME

    member this.SeekBarUpdateThread () = async {
        let! ct = Async.CancellationToken
        if ct.IsCancellationRequested then
            return () // ここでチェックせずにdoDrawをするとヌルポで死ぬ
        if (not <| isNull videoView) then
            let pos = (float32 videoView.CurrentPosition) / (float32 videoView.Duration)
            let (_,_,doDraw, _) = seekBar.Value
            doDraw (Some pos) (this.SSAS 33)
            this.RunOnUiThread (fun () ->
                    playPauseButton.SetText(if videoView.IsPlaying then Resource.String.pause else Resource.String.start)
                )
        do! Async.Sleep 33
        do! this.SeekBarUpdateThread ()
    }

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
        let playerView = this.FindViewById<ConstraintLayout>(Resource.Id.playerView)
        let (sb, setter, getter, near) = PanoraSeekBar.make self
        seekBar <- Some (sb, setter, getter, near)
        let path = base.Intent.GetStringExtra "path"
        sb.Visibility <- Android.Views.ViewStates.Invisible
        playerView.AddView sb
        sb.SetZOrderOnTop true
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
        // SurfaceViewはダメみたい
        //sb.Id <- Android.Views.View.GenerateViewId ()
        //let constraintSet = new ConstraintSet()
        //constraintSet.Clone playerView
        //constraintSet.Connect(sb.Id, ConstraintSet.Top, ConstraintSet.ParentId, ConstraintSet.Top)
        //constraintSet.Connect(sb.Id, ConstraintSet.Bottom, btn.Id, ConstraintSet.Top)
        //constraintSet.Connect(sb.Id, ConstraintSet.Left, ConstraintSet.ParentId, ConstraintSet.Left)
        //constraintSet.Connect(sb.Id, ConstraintSet.Right, ConstraintSet.ParentId, ConstraintSet.Right)
        //constraintSet.ApplyTo playerView
        videoView.SetVideoPath(base.Intent.GetStringExtra "path")
        videoView.Start()
        Event.add this.seek videoView.Touch
        playPauseButton.SetText(Resource.String.pause)
        videoView <- videoView
