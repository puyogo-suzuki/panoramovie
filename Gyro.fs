module PanoraMovie.Gyro

#nowarn "9"

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

let GyroControllerToString = function
    | GyroNone -> "GyroNone"
    | GyroFail _ -> "GyroFail"
    | GyroStarted _ -> "GyroStarted"
    | GyroRecordingStarted _ -> "GyroRecordingStarted"

type GyroControllerUpdate = GyroController -> unit
type GyroControllerGet = unit -> GyroController

let conv<'A , 'B when 'A : unmanaged and 'B : unmanaged> (src : 'A nativeptr) : 'B nativeptr = NativeInterop.NativePtr.toVoidPtr src |> NativeInterop.NativePtr.ofVoidPtr<'B>
let sensorChanged (getter : GyroControllerGet) (e: SensorEvent) : unit =
    match getter () with
    | GyroRecordingStarted (_, m) -> m.Post <| SensorMsgSensorChanged e
    | _ -> ()

let writeThread (inbox : SensorMsg MailboxProcessor) =
    let openStream (path : string) = File.OpenWrite(path)
    let writeTo (sw : Stream) (e : SensorEvent) =
        let mutable buf = NativeInterop.NativePtr.stackalloc<byte>(5)
        NativeInterop.NativePtr.set (conv buf) 0 e.Timestamp
        NativeInterop.NativePtr.set (conv buf) 2 e.Values[0]
        NativeInterop.NativePtr.set (conv buf) 3 e.Values[1]
        NativeInterop.NativePtr.set (conv buf) 4 e.Values[2]
        sw.Write(System.ReadOnlySpan<byte>(NativeInterop.NativePtr.toVoidPtr buf, 5))
    let rec loop (strm : Stream) =
        async {
            let! msg = inbox.Receive()
            match (strm, msg) with
            | (null, SensorMsgStart path) -> 
                return! openStream path |> loop
            | (null, _) -> return! loop null
            | (strm, SensorMsgStart path) -> 
                strm.Close()
                strm.Dispose()
                return! openStream path |> loop
            | (strm, SensorMsgSensorChanged e) ->
                writeTo strm e
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

let public InitializeGyro (update : GyroControllerUpdate) (getter : GyroControllerGet) (senseManager : SensorManager) =
    let gyro = senseManager.GetDefaultSensor(SensorType.Gyroscope)
    if isNull gyro then
        update <| GyroFail GyroNotFound
    else
        let sel = new CSharp.SensorEventListener(sensorChanged getter)
        update <|
            if senseManager.RegisterListener(sel, gyro, SensorDelay.Normal) then
                let canceller = new CancellationTokenSource()
                GyroStarted ((sel, canceller), MailboxProcessor.Start(writeThread, canceller.Token))
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
