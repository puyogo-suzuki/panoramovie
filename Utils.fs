module PanoraMovie.Utils

open System

module Array =
    let inline minMax (f : ^a -> ^b) (ary : ^a array) : (^b * ^b) when ^b : (static member (<) :  ^b *  ^b -> bool) =
        let mapper ((minVal, maxVal) : ^b * ^b) (next : ^a) : (^b * ^b) =
            let v = f next
            let newMaxVal = max maxVal v
            let newMinVal = min minVal v
            (newMinVal, newMaxVal)
        let initVal = f ary[0]
        Array.fold mapper (initVal, initVal) ary