module PanoraMovie.Gyro

open Android.Content
open AndroidX.Core.Content
open Android
open Android.Runtime
open Android.Hardware
open Android.Util
open System.IO
open System.Threading

type SensorMsg =
    | SensorMsgStart of string
    | SensorMsgSensorChanged of SensorEvent
    | SensorMsgStop

let sensorMsgToString = function
    | SensorMsgStart _ -> "SensorMsgStart"
    | SensorMsgSensorChanged _ -> "SensorMsgSensorChanged"
    | SensorMsgStop -> "SensorMsgStop"

type GyroFailReason =
    | GyroNotFound
    | GyroUnavailable
    | GyroInvalidState

type GyroController =
    | GyroNone
    | GyroFail of GyroFailReason
    | GyroStarted of (CSharp.SensorEventListener * CancellationTokenSource) * SensorMsg MailboxProcessor
    | GyroRecordingStarted of (CSharp.SensorEventListener * CancellationTokenSource) * SensorMsg MailboxProcessor

type GyroDirection =
    | GyroDirectionLandscape  // X
    | GyroDirectionPortrait   // Y

let GyroControllerToString = function
    | GyroNone -> "GyroNone"
    | GyroFail _ -> "GyroFail"
    | GyroStarted _ -> "GyroStarted"
    | GyroRecordingStarted _ -> "GyroRecordingStarted"

let GyroValuesGetter<'a> (v : System.Collections.Generic.IList<'a>) = function
    | GyroDirectionLandscape -> v[0]
    | GyroDirectionPortrait -> v[1]

type GyroControllerUpdate = GyroController -> unit
type GyroControllerGet = unit -> GyroController

let sensorChanged (getter : GyroControllerGet) (e: SensorEvent) : unit =
    match getter () with
    | GyroRecordingStarted (_, m) -> m.Post <| SensorMsgSensorChanged e
    | _ -> ()

let writeThread (dir : GyroDirection) (inbox : SensorMsg MailboxProcessor) =
    let rec loop (strm : Stream) =
        async {
            let! msg = inbox.Receive()
            match (strm, msg) with
            | (null, SensorMsgStart path) -> 
                let strm = File.OpenWrite path
                GyroData.writeHead strm
                return! loop strm
            | (null, _) -> return! loop  null
            | (strm, SensorMsgStart path) -> 
                strm.Close()
                strm.Dispose()
                return! File.OpenWrite path |> loop
            | (strm, SensorMsgSensorChanged e) ->
                GyroData.writeValue strm e.Timestamp (GyroValuesGetter e.Values dir)
                return! loop strm 
            | (strm, SensorMsgStop) ->
                strm.Close()
                strm.Dispose()
                return! loop null
        }
    loop null

let public GetSensorService (context : Context) : SensorManager =
    context.GetSystemService(Context.SensorService).JavaCast<SensorManager>()

let public StopGyro : GyroController -> unit = function
    | GyroStarted ((sl, cancel), _) ->
        cancel.Cancel()
    | GyroRecordingStarted ((sl, cancel), mail) ->
        mail.Post SensorMsgStop
        cancel.Cancel()
    | _ -> ()

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

let public StartGyroRecording (update : GyroControllerUpdate) (getter : GyroControllerGet) (filePath : string) =
    match getter () with
    | GyroStarted (misc, mail) ->
        mail.Post <| SensorMsgStart filePath
        update <| GyroRecordingStarted (misc, mail)
    | _ -> update <| GyroFail GyroInvalidState

let public StopGyroRecording (update : GyroControllerUpdate) (getter : GyroControllerGet) =
    match getter () with
    | GyroRecordingStarted (misc, mail) ->
        mail.Post SensorMsgStop
        update <| GyroStarted (misc, mail)
    | _ -> update <| GyroFail GyroInvalidState
