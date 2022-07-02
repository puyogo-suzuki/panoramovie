using Android.Hardware.Camera2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PanoraMovie.CSharp
{
    public class SessionStateListener : CameraCaptureSession.StateCallback
    {
        public Action<CameraCaptureSession> OnConfiguredFunc { get; }
        public Action<CameraCaptureSession> OnConfigureFailedFunc { get; }
        
        public SessionStateListener(Action<CameraCaptureSession> onconfigured, Action<CameraCaptureSession> onfailed)
        {
            this.OnConfiguredFunc = onconfigured;
            this.OnConfigureFailedFunc = onfailed;
        }

        public override void OnConfigured(CameraCaptureSession session) => OnConfiguredFunc(session);

        public override void OnConfigureFailed(CameraCaptureSession session) => OnConfigureFailedFunc(session);
    }
}
