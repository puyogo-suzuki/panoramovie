namespace PanoraMovie

open System

open Android.App
open Android.Content
open Android.OS
open Android.Runtime
open Android.Views
open Android.Widget
open Android.Hardware.Camera2
open PanoraMovie.Camera
open PanoraMovie.Gyro
open Android.Util
open AndroidX.ConstraintLayout.Widget
open System.Threading

[<Activity (Label = "panoramovie", MainLauncher = true, Icon = "@mipmap/icon", ConfigurationChanges = (PM.ConfigChanges.Orientation ||| PM.ConfigChanges.ScreenSize))>]
type MainActivity () as self =
    inherit Activity ()
    let TAG = "PANORAMOVIE_MAINACTIVITY"

    let mutable cameraController : CameraController = CameraNone
    let mutable gyroController : GyroController = GyroNone
    let mutable handler : Handler = null
    /// �r�f�I�v���r���[�̂��߂̔r������
    [<VolatileField>]
    let mutable initializeSem : SemaphoreSlim = new SemaphoreSlim(1)
    let mutable preview : MySurfaceView = null
    // @id/mainView
    let mainView : ConstraintLayout Lazy = lazy self.FindViewById<ConstraintLayout>(Resource.Id.mainView)
    // @id/recordButton
    let recordButton : Button Lazy = lazy self.FindViewById<Button>(Resource.Id.recordButton)
    // @id/gallaryButton
    let gallaryButton : Button Lazy = lazy self.FindViewById<Button>(Resource.Id.gallaryButton)
    let cameraManager : CameraManager Lazy = lazy GetCameraService (self)
    let windowManager : IWindowManager Lazy = lazy self.GetSystemService(Context.WindowService).JavaCast<IWindowManager>()

    /// <summary>�G���[���b�Z�[�W��\�����ďI������D</summary>
    /// <params name="titleId">�^�C�g���ɕ\�����郊�\�[�XID</params>
    /// <params name="msgId">���b�Z�[�W�ɕ\�����郊�\�[�XID</params>
    let errorFinish (titleId : int) (msgId : int) : unit =
        Log.Error(TAG, System.Environment.StackTrace) |> ignore
        handler.Post( fun () -> (new AlertDialog.Builder(self)).SetTitle(titleId).SetMessage(msgId)
                                    .SetPositiveButton(Resource.String.close_app, EventHandler<DialogClickEventArgs> (fun _ _ -> self.Finish())).Show() |> ignore) |> ignore

    /// <summary>cameraController��setter�D�G���[���N������errorFinish���ĂԁD</summary>
    let cameraControllerUpdate (cc : CameraController) : unit =
        match cc with
        | CameraFail CameraNotFound -> errorFinish Resource.String.error_error Resource.String.error_camera_missing
        | CameraFail CameraDisconnected -> errorFinish Resource.String.error_error Resource.String.error_camera_unavailable
        | CameraFail CameraUnavailable -> errorFinish Resource.String.error_error Resource.String.error_camera_unavailable
        | CameraFail CameraInvalidState -> errorFinish Resource.String.error_error Resource.String.error_camera_illegalState
        | CameraFail CameraRecordFailure -> errorFinish Resource.String.error_error Resource.String.error_camera_recordfailure
        | RecordingStarted _ -> handler.Post( fun () -> recordButton.Value.SetText(Resource.String.stop_record) ) |> ignore
        | CameraOpened _ -> handler.Post( fun () -> recordButton.Value.SetText(Resource.String.start_record) ) |> ignore
        | _ -> ()
        match cc with
        | CameraFail _ -> ()
        | _ ->
            Log.Debug(TAG, CameraControllerToString cc) |> ignore
            cameraController <- cc

    /// <summary>gyroController��setter�D�G���[���N������errorFinish���ĂԁD</summary>
    let gyroControllerUpdate (gc : GyroController) : unit =
        match gc with
        | GyroFail GyroNotFound -> errorFinish Resource.String.error_error Resource.String.error_gyro_missing
        | GyroFail GyroUnavailable -> errorFinish Resource.String.error_error Resource.String.error_gyro_unavailable
        | GyroFail GyroInvalidState -> errorFinish Resource.String.error_error Resource.String.error_gyro_illegalState
        | _ -> 
            Log.Debug(TAG, GyroControllerToString gc) |> ignore
            gyroController <- gc

    /// <summary>gyroController��getter</summary>
    let cameraControllerGetter () : CameraController = cameraController
    /// <summary>gyroController��getter</summary>
    let gyroControllerGetter () : GyroController = gyroController

    /// <summary>�^��{�^���������ꂽ�Ƃ��̏���</summary>
    let recordButtonClicked (_ : EventArgs) : unit =
        let nowTime = DateTime.Now
        let filename = sprintf "%d-%d-%d-%d-%d" nowTime.Year nowTime.Month nowTime.Day nowTime.Hour nowTime.Minute
        let directory = if Environment.ExternalStorageState = Environment.MediaMounted then self.GetExternalFilesDir(null) else self.FilesDir
        let finalFilePath = IO.Path.Combine [|directory.AbsolutePath;filename|]
        match (cameraController, gyroController) with
        | (SessionStarted _, GyroStarted _) -> // �L�^�J�n
            let orientation = windowManager.Value.DefaultDisplay.Rotation
            StartGyroRecording gyroControllerUpdate gyroControllerGetter (finalFilePath + ".gyro")
            StartVideoRecording cameraControllerUpdate cameraControllerGetter preview.Holder.Surface orientation (finalFilePath + ".mp4") cameraManager.Value
        | (RecordingStarted _, GyroRecordingStarted _) -> // �L�^�I��
            StopVideoRecording cameraControllerUpdate cameraControllerGetter preview.Holder.Surface
            StopGyroRecording gyroControllerUpdate gyroControllerGetter
        | _ -> // �ُ�
            StopCamera cameraController
            cameraControllerUpdate <| CameraFail CameraInvalidState

    /// <summary>�M�������[�{�^���������ꂽ�Ƃ��̏���</summary>
    let gallaryButtonClicked (_ : EventArgs) : unit =
        self.StartActivity(new Intent(self, typeof<GallaryActivity>))

    /// <summary>�J�����̏������DSurfaceView�Ăяo�����\��������</summary>
    member this.initializeCamera (holder : ISurfaceHolder) =
        initializeSem.Wait()
        if HasCameraPermission this.ApplicationContext then
            InitializeCamera cameraControllerUpdate cameraControllerGetter holder.Surface (this :> Context) cameraManager.Value
        else
            RequestCameraPermission this
        initializeSem.Release() |> ignore

    /// <summary>�W���C���X�R�[�v�̏������D</summary>
    member this.initializeGyro () =
        InitializeGyro (base.Resources.Configuration.Orientation) gyroControllerUpdate gyroControllerGetter (GetSensorService (this :> Context))

    /// <summary>���C�A�E�g�̐ݒ�</summary>
    member this.setLayout () =
        let cs = new ConstraintSet()
        cs.Clone(self, Resource.Layout.Main)
        if base.Resources.Configuration.Orientation = Res.Orientation.Portrait then
            cs.Connect(recordButton.Value.Id, ConstraintSet.Bottom, ConstraintSet.ParentId, ConstraintSet.Bottom)
            cs.Connect(recordButton.Value.Id, ConstraintSet.Left, ConstraintSet.ParentId, ConstraintSet.Left)
            cs.Connect(recordButton.Value.Id, ConstraintSet.Right, ConstraintSet.ParentId, ConstraintSet.Right)
            cs.Connect(gallaryButton.Value.Id, ConstraintSet.Bottom, ConstraintSet.ParentId, ConstraintSet.Bottom)
            cs.Connect(gallaryButton.Value.Id, ConstraintSet.Right, ConstraintSet.ParentId, ConstraintSet.Right)
        else
            cs.Connect(recordButton.Value.Id, ConstraintSet.Bottom, ConstraintSet.ParentId, ConstraintSet.Bottom)
            cs.Connect(recordButton.Value.Id, ConstraintSet.Top, ConstraintSet.ParentId, ConstraintSet.Top)
            cs.Connect(recordButton.Value.Id, ConstraintSet.Right, ConstraintSet.ParentId, ConstraintSet.Right)
            cs.Connect(gallaryButton.Value.Id, ConstraintSet.Top, ConstraintSet.ParentId, ConstraintSet.Top)
            cs.Connect(gallaryButton.Value.Id, ConstraintSet.Right, ConstraintSet.ParentId, ConstraintSet.Right)
        cs.ApplyTo(mainView.Value)
        ()

    override this.OnRequestPermissionsResult (reqcode, perms, results) =
        if results |> Seq.exists(fun v -> int v <> int PM.Permission.Granted) then
            errorFinish Resource.String.error_error Resource.String.error_camera_permission
        else
            this.initializeCamera preview.Holder

    /// <summary>��]���ꂽ�Ƃ��ɂ������Ă΂��D</summary>
    override this.OnConfigurationChanged (newConf : Res.Configuration) =
        base.OnConfigurationChanged(newConf)
        DirectionChanged newConf.Orientation gyroControllerGetter
        this.setLayout ()
        ()

    override this.OnResume () =
        base.OnResume ()
        preview <- new MySurfaceView(self.initializeCamera, (fun _ _ _ _ -> ()), self :> Context)
        this.FindViewById<ConstraintLayout>(Resource.Id.mainView).AddView(preview)
        this.initializeGyro ()

    override this.OnStop () =
        base.OnStop ()
        this.FindViewById<ConstraintLayout>(Resource.Id.mainView).RemoveView(preview)
        preview.Dispose()
        preview <- null
        StopCamera cameraController
        StopGyro gyroController
        cameraController <- CameraNone
        gyroController <- GyroNone

    override this.OnCreate (bundle) =
        base.OnCreate (bundle)
        handler <- new Handler(this.MainLooper)
        this.SetContentView (Resource.Layout.Main)
        recordButton.Value.Click.Add recordButtonClicked
        gallaryButton.Value.Click.Add gallaryButtonClicked
        this.setLayout ()
        ()

    override this.OnDestroy () =
        base.OnDestroy ()
        if cameraManager.IsValueCreated then
            cameraManager.Value.Dispose ()
        initializeSem.Dispose()