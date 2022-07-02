using Android.Hardware;
using Android.Runtime;
using Android.Views;
using Java.Interop;

namespace PanoraMovie.CSharp
{
    public class SensorEventListener : Java.Lang.Object, ISensorEventListener
    {
        public Action<Sensor?, SensorStatus> onAccuracyChanged;
        public Action<SensorEvent?> onSensorChanged;
        public SensorEventListener() : this((_, _) => { }, _ => { }) { }
        public SensorEventListener(Action<Sensor?, SensorStatus> oac) : this(oac, _ => { }) {}
        public SensorEventListener(Action<SensorEvent?> osc) : this((_, _) => { }, osc) {}
        public SensorEventListener(Action<Sensor?, SensorStatus> oac, Action<SensorEvent?> osc)
        {
            onAccuracyChanged = oac;
            onSensorChanged = osc;
        }
        public void OnAccuracyChanged(Sensor? sensor, [GeneratedEnum] SensorStatus accuracy) =>
            onAccuracyChanged(sensor, accuracy);

        public void OnSensorChanged(SensorEvent? e) => onSensorChanged(e);
    }
}