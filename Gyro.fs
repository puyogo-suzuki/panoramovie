module PanoraMovie.Gyro

open Android.Content
open Android.Runtime
open Android.Hardware
open System.IO
open System.Threading

/// <summary>ジャイロの方向</summary>
type GyroDirection =
    /// <summary>横向き</summary>
    | GyroDirectionLandscape  // X
    /// <summary>縦向き</summary>
    | GyroDirectionPortrait   // Y

/// <summary>センサアクターとのメッセージ</summary>
type SensorMsg =
    /// <summary>センサの観測値の書き込み開始</summary>
    | SensorMsgStart of string
    /// <summary>センサの観測値の通知</summary>
    | SensorMsgSensorChanged of SensorEvent
    /// <summary>スマホの向きが変化した</summary>
    | SensorMsgDirectionChanged of GyroDirection
    /// <summary>記録終了</summary>
    | SensorMsgStop

let sensorMsgToString = function
    | SensorMsgStart _ -> "SensorMsgStart"
    | SensorMsgSensorChanged _ -> "SensorMsgSensorChanged"
    | SensorMsgDirectionChanged _ -> "SensorMsgDirectionChanged"
    | SensorMsgStop -> "SensorMsgStop"

/// <summary>ジャイロの失敗理由</summary>
type GyroFailReason =
    /// <summary>ジャイロスコープが見つからなかった</summary>
    | GyroNotFound
    /// <summary>ジャイロスコープが使えなかった</summary>
    | GyroUnavailable
    /// <summary>異常な状態（バグ）</summary>
    | GyroInvalidState

/// <summary>ジャイロの制御を行う</summary>
type GyroController =
    /// <summary>未初期化</summary>
    | GyroNone
    /// <summary>失敗</summary>
    | GyroFail of GyroFailReason
    /// <summary>ジャイロによる観測が始まった</summary>
    | GyroStarted of (CSharp.SensorEventListener * CancellationTokenSource) * SensorMsg MailboxProcessor
    /// <summary>観測値の記録が始まった</summary>
    | GyroRecordingStarted of (CSharp.SensorEventListener * CancellationTokenSource) * SensorMsg MailboxProcessor

let GyroControllerToString = function
    | GyroNone -> "GyroNone"
    | GyroFail _ -> "GyroFail"
    | GyroStarted _ -> "GyroStarted"
    | GyroRecordingStarted _ -> "GyroRecordingStarted"

/// <summary>方向によってセンサイベントに格納されている値から重要な値を取り出す</summary>
let GyroValuesGetter<'a> (v : System.Collections.Generic.IList<'a>) : GyroDirection -> 'a = function
    | GyroDirectionLandscape -> v[0]
    | GyroDirectionPortrait -> v[1]

type GyroControllerUpdate = GyroController -> unit
type GyroControllerGet = unit -> GyroController

/// <summary>ISensorEventListnerに登録される．センサの観測値をセンサアクタに送る．</summary>
let sensorChanged (getter : GyroControllerGet) (e: SensorEvent) : unit =
    match getter () with
    | GyroRecordingStarted (_, m) -> m.Post <| SensorMsgSensorChanged e
    | _ -> ()

/// <summary>センサの観測値の記録を行うスレッドの処理</summary>
let writeThread (dir : GyroDirection) (inbox : SensorMsg MailboxProcessor) =
    let rec loop (dir : GyroDirection) (strm : Stream) =
        async {
            let! msg = inbox.Receive()
            match (strm, msg) with
            | (null, SensorMsgStart path) -> 
                let strm = File.OpenWrite path
                GyroData.writeHead strm
                return! loop dir strm
            | (null, _) -> return! loop dir null
            | (strm, SensorMsgStart path) -> 
                strm.Close()
                strm.Dispose()
                return! File.OpenWrite path |> loop dir
            | (strm, SensorMsgSensorChanged e) ->
                GyroData.writeValue strm e.Timestamp (GyroValuesGetter e.Values dir)
                return! loop dir strm 
            | (strm, SensorMsgStop) ->
                strm.Close()
                strm.Dispose()
                return! loop dir null
            | (strm, SensorMsgDirectionChanged dir) ->
                return! loop dir strm
        }
    loop dir null

let public GetSensorService (context : Context) : SensorManager =
    context.GetSystemService(Context.SensorService).JavaCast<SensorManager>()

/// <summary>ジャイロセンサの観測終了</summary>
let public StopGyro : GyroController -> unit = function
    | GyroStarted ((sl, cancel), _) ->
        cancel.Cancel()
    | GyroRecordingStarted ((sl, cancel), mail) ->
        mail.Post SensorMsgStop
        cancel.Cancel()
    | _ -> ()

/// <summary>ジャイロセンサの初期化</summary>
/// <param name="update">GyroControllerのsetter</param>
/// <param name="getter">GyroControllerのgetter</param>
let public InitializeGyro (dir : Res.Orientation) (update : GyroControllerUpdate) (getter : GyroControllerGet) (senseManager : SensorManager) =
    let gyro = senseManager.GetDefaultSensor(SensorType.Gyroscope)
    if isNull gyro then
        update <| GyroFail GyroNotFound
    else
        let sel = new CSharp.SensorEventListener(sensorChanged getter)
        update <|
            if senseManager.RegisterListener(sel, gyro, SensorDelay.Normal) then
                let canceller = new CancellationTokenSource()
                let orien = if dir = Res.Orientation.Portrait then GyroDirectionPortrait else GyroDirectionLandscape
                GyroStarted ((sel, canceller), MailboxProcessor.Start(writeThread orien, canceller.Token))
            else
                GyroFail GyroUnavailable

/// <summary>ジャイロセンサの観測値の記録の開始</summary>
/// <param name="update">GyroControllerのsetter</param>
/// <param name="getter">GyroControllerのgetter</param>
/// <param name="filePath">記録先のファイルパス</param>
let public StartGyroRecording (update : GyroControllerUpdate) (getter : GyroControllerGet) (filePath : string) =
    match getter () with
    | GyroStarted (misc, mail) ->
        mail.Post <| SensorMsgStart filePath
        update <| GyroRecordingStarted (misc, mail)
    | _ -> update <| GyroFail GyroInvalidState

/// <summary>ジャイロセンサの観測値の記録の停止</summary>
/// <param name="update">GyroControllerのsetter</param>
/// <param name="getter">GyroControllerのgetter</param>
let public StopGyroRecording (update : GyroControllerUpdate) (getter : GyroControllerGet) =
    match getter () with
    | GyroRecordingStarted (misc, mail) ->
        mail.Post SensorMsgStop
        update <| GyroStarted (misc, mail)
    | _ -> update <| GyroFail GyroInvalidState

/// <summary>スマホの方向が変化した</summary>
/// <param name="dir">方向/param>
/// <param name="getter">GyroControllerのgetter</param>
let public DirectionChanged (dir : Res.Orientation) (getter : GyroControllerGet) =
    match getter () with
    | GyroStarted (_, mail) ->
        mail.Post <| SensorMsgDirectionChanged (if dir = Res.Orientation.Portrait then GyroDirectionPortrait else GyroDirectionLandscape)
    | GyroRecordingStarted (_, mail) ->
        mail.Post <| SensorMsgDirectionChanged (if dir = Res.Orientation.Portrait then GyroDirectionPortrait else GyroDirectionLandscape)
    | _ -> ()