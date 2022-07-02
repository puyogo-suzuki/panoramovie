using Android.Hardware.Camera2;
using Android.Runtime;

namespace PanoraMovie.CSharp
{
    public class CameraStateListener : CameraDevice.StateCallback
    {
        public Action<CameraDevice>? OnOpenedFunc { get; }
        public Action<CameraDevice>? OnDisconnectedFunc { get; }
        public Action<CameraDevice, CameraError>? OnErrorFunc { get; }

        public CameraStateListener(Action<CameraDevice> onopened, Action<CameraDevice> ondisconnected, Action<CameraDevice, CameraError> onerror) : base()
        {
            this.OnOpenedFunc = onopened;
            this.OnDisconnectedFunc = ondisconnected;
            this.OnErrorFunc = onerror;
        }
        public CameraStateListener(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public override void OnDisconnected(CameraDevice camera) => OnOpenedFunc?.Invoke(camera);

        public override void OnError(CameraDevice camera, [GeneratedEnum] CameraError error) => OnErrorFunc?.Invoke(camera, error);

        public override void OnOpened(CameraDevice camera) => OnOpenedFunc?.Invoke(camera);
    }
}
