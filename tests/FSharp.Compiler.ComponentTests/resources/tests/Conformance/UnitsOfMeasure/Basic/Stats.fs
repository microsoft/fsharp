// #Conformance #UnitsOfMeasure 
#light

let rec sum xs = match xs with [] -> 0.0<_> | (x::xs) -> x + sum xs

let rec sumMap f xs = match xs with [] -> 0.0<_> | (x::xs) -> f x + sumMap f xs
let rec sumMap2 f xs ys = match xs,ys with (x::xs,y::ys) -> f x y + sumMap2 f xs ys | _ -> 0.0<_>

let mean xs = sum xs / float (List.length xs)

let meanMap f xs = sumMap f xs / float (List.length xs)

let sqr (x:float<_>) = x*x

let cube x = x*sqr x

let variance xs = let m = mean xs in meanMap (fun x -> sqr (x-m)) xs
let sdeviation xs = sqrt (variance xs)

let skewness xs = 
  let _n = float (List.length xs)
  let m = mean xs
  let s = sdeviation xs
  meanMap (fun x -> cube(x-m)) xs / (cube s)

let covariance xs ys =
  let m = mean xs
  let n = mean ys
  sumMap2 (fun x y -> (x-m)*(y-n)) xs ys / (float n)

let correlation xs ys =
  covariance xs ys / (mean xs * mean ys)
