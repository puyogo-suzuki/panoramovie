module PanoraMovie.Camera

open Android.Hardware.Camera2
open AndroidX.Core.Content
open Android
open Android.Runtime
open Android.Content
open Android.App
open AndroidX.Core.App
open Android.Views
open Android.OS
open Android.Media
open Android.Util
open Android.Hardware.Camera2.Params
open PanoraMovie.CSharp

type CameraFailReason =
    | CameraNotFound
    | CameraDisconnected
    | CameraUnavailable
    | CameraInvalidState
    | CameraRecordFailure

type CameraStruct = CameraStateListener * HandlerThread
type SessionStruct = SessionStateListener * CameraDevice * CaptureRequest.Builder * SessionCaptureListener
type CameraController =
    | CameraNone
    | CameraFail of CameraFailReason
    | CameraOpening of CameraStruct * Handler
    | CameraOpened of CameraStruct * Handler * SessionStruct
    | SessionStarted of CameraStruct * Handler * SessionStruct * CameraCaptureSession
    | RecordingStarting of CameraStruct * Handler * SessionStruct * MediaRecorder
    | RecordingStarted of CameraStruct * Handler * SessionStruct * MediaRecorder * CameraCaptureSession

let public CameraControllerToString = function
    | CameraNone -> "CameraNone"
    | CameraFail _ -> "CameraFail"
    | CameraOpening _ -> "CameraOpening"
    | CameraOpened _ -> "CameraOpened"
    | SessionStarted _ -> "SessionStarted"
    | RecordingStarting _ -> "RecordingStarting"
    | RecordingStarted _ -> "RecordingStarted"

type CameraControllerUpdate = CameraController -> unit
type CameraControllerGetter = unit -> CameraController

let public HasCameraPermission (context : Context) : bool=
    let camPermission = ContextCompat.CheckSelfPermission(context, Manifest.Permission.Camera)
    let audPermission = ContextCompat.CheckSelfPermission(context, Manifest.Permission.RecordAudio)
    camPermission = PM.Permission.Granted && audPermission = PM.Permission.Granted

let public RequestCameraPermission (activity : Activity) : unit =
    ActivityCompat.RequestPermissions(activity, [|Manifest.Permission.Camera; Manifest.Permission.RecordAudio|], 0)
    
let isBackCamera (camMan : CameraManager) (camId : string) : bool =
    let tmp = ((camMan.GetCameraCharacteristics camId).Get CameraCharacteristics.LensFacing) in
    (not <| isNull tmp) && ((tmp :?> Java.Lang.Integer).IntValue()) = (int LensFacing.Back)

let public StopCamera : CameraController -> unit =
    let stopCamStruct ((csl, ht) : CameraStruct) : unit =
        csl.Dispose()
        ht.Dispose()
    let stopSesStruct ((ssl, cd, crb, scl) : SessionStruct) : unit =
        ssl.Dispose()
        cd.Dispose()
        crb.Dispose()
        scl.Dispose()
    function
    | CameraOpening (cs, h) ->
        stopCamStruct cs
        h.Dispose()
    | CameraOpened(cs, h, ss) ->
        stopSesStruct ss
        stopCamStruct cs
        h.Dispose()
    | SessionStarted(cs, h, ss, s) ->
        s.Close()
        s.Dispose()
        stopSesStruct ss
        stopCamStruct cs
        h.Dispose()
    | RecordingStarting(cs, h, ss, mr) ->
        mr.Dispose()
        stopSesStruct ss
        stopCamStruct cs
        h.Dispose()
    | RecordingStarted(cs, h, ss, mr, s) ->
        mr.Stop()
        mr.Dispose()
        s.Close()
        s.Dispose()
        stopSesStruct ss
        stopCamStruct cs
        h.Dispose()
    | _ -> ()

let createCameraThread () : HandlerThread * Handler =
    let th = new HandlerThread("CameraBackground")
    th.Start ()
    let handle = new Handler(th.Looper)
    (th, handle)
    
let sessionConfigured (update : CameraControllerUpdate) (getter : CameraControllerGetter) (session : CameraCaptureSession) =
    match getter () with
    | CameraOpened(cs, h, (sl, c, r, scl)) -> 
        session.SetRepeatingRequest(r.Build(), scl, h) |> ignore
        update <| CameraController.SessionStarted(cs, h, (sl, c, r, scl), session)
    | RecordingStarting(cs, h, (sl, c, r, scl), mr) -> 
        session.SetRepeatingRequest(r.Build(), scl, h) |> ignore
        update <| CameraController.RecordingStarted(cs, h, (sl, c, r, scl), mr, session)
    | _ -> update <| CameraFail CameraInvalidState

let sessionConfigureFailed (update : CameraControllerUpdate) (getter : CameraControllerGetter) (session : CameraCaptureSession) =
    session.Close()
    StopCamera <| getter ()
    update <| CameraFail CameraUnavailable

let createCaptureRequest (camDev : CameraDevice) (surface : Surface) : CaptureRequest.Builder =
    let captureRequest = camDev.CreateCaptureRequest(CameraTemplate.Record)
    captureRequest.AddTarget(surface)
    captureRequest

let cameraOpened (update : CameraControllerUpdate) (getter : CameraControllerGetter) (surface : Surface) (context : Context) (camDev : CameraDevice) =
    match getter () with
    | CameraOpening(cs, h) -> 
        let captureRequest = createCaptureRequest camDev surface
        let sessionStateListener = new SessionStateListener(sessionConfigured update getter, sessionConfigureFailed update getter)
        update <| CameraOpened(cs, h, (sessionStateListener, camDev, captureRequest, new SessionCaptureListener()))
        camDev.CreateCaptureSession([|surface;|], sessionStateListener, h)
    | _ -> update <| CameraFail CameraInvalidState

let cameraDisconnected (update : CameraControllerUpdate) (camDev : CameraDevice) =
    camDev.Close ()
    update <| CameraFail CameraDisconnected
let cameraError (update : CameraControllerUpdate) (camDev : CameraDevice) (err : CameraError) =
    update <| CameraFail CameraUnavailable

let public GetCameraService (activity : Activity) : CameraManager =
    activity.GetSystemService(Context.CameraService).JavaCast<CameraManager>()

let public InitializeCamera (updater : CameraControllerUpdate) (getter : CameraControllerGetter) (surface : Surface) (context : Context) (cameraManager : CameraManager) : unit =
    if getter () = CameraNone then
        match cameraManager.GetCameraIdList () |> Seq.filter (isBackCamera cameraManager) |> Seq.tryHead with
        | Some(cam) ->
            let (thread, handler) = createCameraThread ()
            let cameraStateListener = new CameraStateListener(cameraOpened updater getter surface context, cameraDisconnected updater, cameraError updater)
            updater <| CameraOpening ((cameraStateListener,thread),handler)
            try
                cameraManager.OpenCamera(cam, cameraStateListener, handler)
            with
            | _ -> updater CameraNone
        | None -> updater <| CameraFail CameraNotFound
    else
        ()

let public StartVideoRecording (updater : CameraControllerUpdate) (getter : CameraControllerGetter) (surface : Surface) (orientation : SurfaceOrientation) (filePath : string) (cameraManager : CameraManager) : unit =
    match getter () with
    | SessionStarted(cs, h, (sl, c, r, scl), s) ->
        let charac = (cameraManager.GetCameraCharacteristics(c.Id).Get CameraCharacteristics.ScalerStreamConfigurationMap).JavaCast<StreamConfigurationMap>()
        try
            let outputsize = charac.GetOutputSizes(Java.Lang.Class.FromType(typeof<MediaRecorder>)) |> Seq.filter (fun x -> x.Height <= 1080) |> Seq.maxBy (fun x -> x.Width * x.Height)
            let mr = new MediaRecorder() // Warning出るけど，これ以外のコンストラクタは正しく動かない！！
            mr.SetVideoSource(VideoSource.Surface)
            mr.SetOutputFile(filePath)
            mr.SetVideoEncodingBitRate(10000000)
            mr.SetOrientationHint(match orientation with
                                    | SurfaceOrientation.Rotation90 -> 180
                                    | SurfaceOrientation.Rotation180 -> 270
                                    | SurfaceOrientation.Rotation270 -> 0
                                    | _ -> 90)
            mr.SetAudioSource(AudioSource.Camcorder)
            mr.SetAudioChannels(2)
            mr.SetAudioSamplingRate(44100)
            mr.SetOutputFormat(OutputFormat.Default)
            mr.SetAudioEncoder(AudioEncoder.Default)
            mr.SetVideoEncoder(VideoEncoder.Default)
            mr.SetVideoSize(outputsize.Width, outputsize.Height)
            mr.SetVideoFrameRate(30)
            mr.Prepare()
            r.AddTarget(mr.Surface)
            s.Close ()
            updater <| RecordingStarting (cs, h, (sl, c, r, scl), mr)
            c.CreateCaptureSession([|surface; mr.Surface|], sl, h)
            mr.Start()
        with
        | a -> 
            Log.Error("CAMERA", a.ToString()) |> ignore
            updater <| CameraFail CameraRecordFailure // TODO
    | cc ->
        StopCamera cc
        updater <| CameraFail CameraInvalidState

let public StopVideoRecording (updater : CameraControllerUpdate) (getter : CameraControllerGetter) (surface : Surface) : unit =
    match getter () with
    | RecordingStarted(cs, h, (sl, c, r, scl), mr, s) ->
        mr.Stop()
        s.Close()
        r.Dispose()
        let captureRequest = createCaptureRequest c surface
        updater <| CameraOpened(cs, h, (sl, c, captureRequest, scl))
        c.CreateCaptureSession([|surface;|], sl, h)
    | cc ->
        StopCamera cc
        updater <| CameraFail CameraInvalidState