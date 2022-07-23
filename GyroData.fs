module PanoraMovie.GyroData
#nowarn "9"

open System.Runtime.CompilerServices
open System.IO
open System
open FSharp.NativeInterop

/// <summary>ジャイロファイルデータの1パケット</summary>
/// <params name="timestamp">値が観測されたときの時間</params>
/// <params name="value">値</params>
[<Struct>]
type GyroSegment(timestamp : int64, value : float32) =
    /// <summary>値が観測されたときの時間</summary>
    member this.TimeStamp = timestamp
    /// <summary>値</summary>
    member this.Value = value

/// <summary>ジャイロファイルデータ</summary>
/// <params name="size">ジャイロのパケット数</params>
/// <params name="startTime">観測の開始時間（未使用）</params>
type GyroData(size : int32, startTime : DateTime) =
    /// <summary>開始時間</summary>
    member val StartTime : DateTime = startTime
    /// <summary>ジャイロの観測データ</summary>
    member val Values : GyroSegment array = Array.zeroCreate size with get, set

/// <summary>ファイルヘッダの書き込みを行う</summary>
/// <params name="strm">書き込み先のファイルストリーム</summary>
let public writeHead (strm : Stream) : unit=
    let (nanos, now) = (Android.OS.SystemClock.ElapsedRealtimeNanos(), System.DateTime.Now)
    let buf = System.Span<byte>(NativePtr.stackalloc<byte> (8 + 8) |> NativePtr.toVoidPtr, 8+8)
    BitConverter.TryWriteBytes(buf, nanos) |> ignore
    BitConverter.TryWriteBytes(buf.Slice(4), (now - DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local)).TotalSeconds) |> ignore
    strm.Write buf

/// <summary>パケットを書き込む</summary>
/// <params name="strm">書き込み先のファイルストリーム</summary>
/// <params name="timestamp">値が観測されたときの時間</summary>
/// <params name="value">値</summary>
let public writeValue (strm : Stream) (timestamp : int64) (value : float32) : unit =
    let gyroSegment = NativeInterop.NativePtr.stackalloc<GyroSegment> 1
    NativePtr.set gyroSegment 0 <| GyroSegment(timestamp, value)
    strm.Write(ReadOnlySpan<byte>(NativePtr.toVoidPtr gyroSegment, sizeof<GyroSegment>))

/// <summary>ジャイロファイルの読み込み</summary>
/// <params name="strm">読み込むファイルストリーム</summary>
/// <returns>ジャイロデータ</returns>
let public parseGyroFile (strm : Stream) : GyroData =
    let len = strm.Length
    if (len - 16L) % (int64 sizeof<GyroSegment>) <> 0 then // パケットサイズで割り切れない
        raise (new InvalidDataException("Bad File."))
    let head = System.Span<byte>(NativeInterop.NativePtr.stackalloc<byte>(16) |> NativePtr.toVoidPtr, 16)
    if strm.Read head <> 16 then
        raise (new IOException())
    let starttime = (new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Local)).AddSeconds(BitConverter.ToDouble(head.Slice(4)))
    let mutable ret = GyroData (int32 ((len - 16L) / int64 sizeof<GyroSegment>), starttime)
    let v = fixed &ret.Values[0]
    // そのままメモリにぶっこめばOK
    if strm.Read (new Span<byte>(v |> NativePtr.toVoidPtr, sizeof<GyroSegment> * ret.Values.Length)) <> (int32 len - 16) then
        raise (new IOException())
    // 角加速度から角度へ変換
    let alpha = 0.001
    let nanostart = ret.Values[0].TimeStamp //BitConverter.ToInt64(head) : 長すぎっぽい
    let mapper (lasttime, omega, theta) (v: GyroSegment) =
        let time = v.TimeStamp - nanostart
        let newomega = (omega * alpha) + (float v.Value * (1.0 - alpha))
        let newtheta = theta + (newomega * (float (time - lasttime)) / 1000.0 / 1000.0 / 1000.0)
        (GyroSegment(time, float32 newtheta), (time, newomega, newtheta))
    let (newvalues, _) = Array.mapFold mapper (0, 0.0, 0.0) ret.Values
    ret.Values <- newvalues
    ret