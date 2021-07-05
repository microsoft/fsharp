// #Conformance #Arrays #Stress #Structs #Mutable #ControlFlow #LetBindings 
#if TESTS_AS_APP
module Core_array
#endif

let mutable failures = []
let report_failure (s) = 
  stderr.WriteLine " NO"; failures <- s :: failures
let test s b = if not b then (stderr.Write(s:string);   report_failure(s) )
let check s b1 b2 = test s (b1 = b2)


(* TEST SUITE FOR Array *)

let test_make_get_set_length () = 
  let arr = Array.create 3 0 in 
  test "fewoih" (Array.get arr 0 = 0);
  test "vvrew0" (Array.get arr 2 = 0);
  ignore (Array.set arr 0 4);
  test "vsdiuvs" (Array.get arr 0 = 4);
  test "vropivrwe" (Array.length arr = 3)

let test_const () = 
  let arr =  [| 4;3;2 |]  in 
  test "sdvjk2" (Array.get arr 0 = 4);
  test "cedkj" (Array.get arr 2 = 2);
  ignore (Array.set arr 0 4);
  test "ds9023" (Array.get arr 0 = 4);
  test "sdio2" (Array.length arr = 3)

let test_const_empty () = 
  let arr =  [| |]  in 
  test "sdio2" (Array.length arr = 0)

let test_map () = 
  let arr = Array.map (fun x -> x + 1) ( [| 4;3;2 |]) in 
  test "test2927: sdvjk2" (Array.get arr 0 = 5);
  test "test2927: cedkj" (Array.get arr 2 = 3)

let test_iter () = 
  Array.iter (fun x -> test "fuo" (x <= 4)) ( [| 4;3;2 |])

let test_iteri () = 
  let arr =  [| 4;3;2 |] in 
  Array.iteri (fun i x -> test "fuo" (arr.[i] = x)) arr

let test_mapi () = 
  let arr = [| 4;3;2 |] in 
  let arr2 = Array.mapi (fun i x -> test "dwqfuo" (arr.[i] = x); i + x) arr in 
  test "test2927: sdvjk2" (Array.get arr2 0 = 4);
  test "test2927: cedkj" (Array.get arr2 2 = 4)

let test_isEmpty () =
  test "isEmpty a" (Array.isEmpty [||])
  test "isEmpty b" (Array.isEmpty <| Array.create 0 42)
  test "isEmpty c" <| not (Array.isEmpty <| [| 1 |])
  test "isEmpty d" (Array.isEmpty <| Array.empty)

let test_create () =
  let arr = Array.create 10 10
  for i in 0 .. 9 do
      test "test_create" (arr.[i] = 10)

let test_concat () =
    let make n = [| for i in n .. n + 9 -> i |]
    let arr = [| for i in 0..+10..50 -> make i|]
    test "concat a" (Array.concat arr = [|0..59|])

    let arr2 = [| for i in 0..50 -> [||] |]
    test "concat b" (Array.concat arr2 = [| |])

    let arr3 = [| [||]; [||]; [|1; 2|]; [||] |]
    test "concat c" (Array.concat arr3 = [|1; 2|])

let test_sub () =
    test "sub a" (Array.sub [|0..100|] 10 20 = [|10..29|])
    test "sub b" (Array.sub [|0..100|] 0 101 = [|0..100|])
    test "sub c" (Array.sub [|0..100|] 0 1 = [|0|])
    test "sub d" (Array.sub [|0..100|] 0 0 = [||])

let test_fold2 () =
    test "fold2 a"
        (Array.fold2 (fun i j k -> i+j+k) 100 [|1;2;3|] [|1;2;3|] = 112)

    test "fold2_b"
        (Array.fold2 (fun i j k -> i-j-k) 100 [|1;2;3|] [|1;2;3|] = 100-12)

let test_foldBack2 () =
    test "foldBack2 a"
        (Array.foldBack2 (fun i j k -> i+j+k) [|1;2;3|] [|1;2;3|] 100 = 112)

    test "foldBack2_b"
        (Array.foldBack2 (fun i j k -> k-i-j) [|1;2;3|] [|1;2;3|] 100 = 100-12)

let test_scan () =
    test "scan"
        (Array.scan (+) 0 [|1..5|] = [|0; 1; 3; 6; 10; 15|])

    test "scanBack"
        (Array.scanBack (+) [|1..5|] 0 = [|15; 14; 12; 9; 5; 0|])

let test_iter2 () =
    let c = ref -1
    Array.iter2 (fun x y -> incr c; test "iter2" (!c = x && !c = y)) [|0..100|] [|0..100|]
    test "iter2" (!c = 100)

let test_iteri2 () =
    let c = ref 0
    Array.iteri2 (fun i j k -> c := !c+i+j+k) [|1;2;3|] [|10;20;30|]
    test "iteri2" (!c = 6+60+3)

let test_map2 () =
    test "map2"
        (Array.map2 (+) [|0..100|] [|0..100|] = [|0..+2..200|])

let test_mapi2 () =
    test "mapi2 a"
        (Array.mapi2 (fun i j k -> i+j+k) [|1..10|] [|1..10|] = [|2..+3..29|])

    test "mapi2_b"
        (try Array.mapi2 (fun i j k -> i+j+k) [||] [|1..10|] |> ignore; false
         with _ -> true)

let test_exists () =
    test "exists a"
        ([|1..100|] |> Array.exists ((=) 50))

    test "exists b" <| not
        ([|1..100|] |> Array.exists ((=) 150))

let test_forall () =
    test "forall a"
        ([|1..100|] |> Array.forall (fun x -> x < 150))

    test "forall b" <| not
        ([|1..100|] |> Array.forall (fun x -> x < 80))

let test_exists2 () =
    test "exists2 a" <| Array.exists2 (=)
        [|1; 2; 3; 4; 5; 6|]
        [|2; 3; 4; 5; 6; 6|]

    test "exists2 b" <| not (Array.exists2 (=)
        [|1; 2; 3; 4; 5; 6|]
        [|2; 3; 4; 5; 6; 7|])

let test_forall2 () =
    test "forall2 a"
        (Array.forall2 (=) [|1..10|] [|1..10|])

    test "forall2_b" <| not
        (Array.forall2 (=) [|1;2;3;4;5|] [|1;2;3;0;5|])

let test_filter () =
    test "filter a"
        (Array.filter (fun x -> x % 2 = 0) [|0..100|] = [|0..+2..100|])

    test "filter b"
        (Array.filter (fun x -> false) [|0..100|] = [||])

    test "filter c"
        (Array.filter (fun x -> true) [|0..100|] = [|0..100|])


let test_partition () =
    let p1, p2 = Array.partition (fun x -> x % 2 = 0) [|0..100|]
    test "partition"
        (p1 = [|0..+2..100|] && p2 = [|1..+2..100|])

let test_choose () =
    test "choose"
        (Array.choose (fun x -> if x % 2 = 0 then Some (x/2) else None) [|0..100|] = [|0..50|])

let test_find () =
    test "find a"
        ([|1..100|] |> Array.find (fun x -> x > 50) = 51)

    test "find b"
        (try [|1..100|] |> Array.find (fun x -> x > 180) |> ignore; false
         with _ -> true)

module Array = 
    let findIndexi f (array : array<_>) = 
        let len = array.Length 
        let rec go n = 
            if n >= len then 
                failwith "fail"
            elif f n array.[n] then 
                n 
            else 
                go (n+1)
        go 0

    let tryFindIndexi f (array : array<_>) = 
        let len = array.Length 
        let rec go n = if n >= len then None elif f n array.[n] then Some n else go (n+1)
        go 0 

let test_findIndex () =
    test "findIndex a"
        (Array.findIndex (fun i -> i >= 4) [|0..10|] = 4)

    test "findIndex b"
        (try Array.findIndex (fun i -> i >= 20) [|0..10|] |> ignore; false
         with _ -> true)
   
    test "findIndexi a"
        (Array.findIndexi (=) [|1; 2; 3; 3; 2; 1|] = 3)

    test "findIndexi b"
        (try Array.findIndexi (=) [|1..10|] |> ignore; false
         with _ -> true)

let test_tryfind () =
    test "tryFind"
        ([|1..100|] |> Array.tryFind (fun x -> x > 50) = Some 51)

    test "tryFind b"
        ([|1..100|] |> Array.tryFind (fun x -> x > 180) = None)

    test "tryfind_index a"
        (Array.tryFindIndex (fun x -> x = 4) [|0..10|] = Some 4)

    test "tryfind_index b"
        (Array.tryFindIndex (fun x -> x = 42) [|0..10|] = None)

    test "tryFindIndexi a"
        (Array.tryFindIndexi (=) [|1;2;3;4;4;3;2;1|] = Some 4)

    test "tryFindIndexi b"
        (Array.tryFindIndexi (=) [|1..10|] = None)

let test_first () =
    test "first a"
        ([|1..100|] |> Array.tryPick (fun x -> if x > 50 then Some (x*x) else None) = Some (51*51))

    test "first b"
        ([|1..100|] |> Array.tryPick (fun x -> None) = None)
        
    test "first c"
        ([||] |> Array.tryPick (fun _ -> Some 42) = None)

let test_sort () =

    test "sort a" (Array.sort [||] = [||])
    test "sort b" (Array.sort [|1|] = [|1|])
    test "sort c" (Array.sort [|1;2|] = [|1;2|])
    test "sort d" (Array.sort [|2;1|] = [|1;2|])
    test "sort e" (Array.sort [|1..1000|] = [|1..1000|])
    test "sort f" (Array.sort [|1000..-1..1|] = [|1..1000|])

let test_sort_by () =

    test "Array.sortBy a" (Array.sortBy int [||] = [||])
    test "Array.sortBy b" (Array.sortBy int [|1|] = [|1|])
    test "Array.sortBy c" (Array.sortBy int [|1;2|] = [|1;2|])
    test "Array.sortBy d" (Array.sortBy int [|2;1|] = [|1;2|])
    test "Array.sortBy e" (Array.sortBy int [|1..1000|] = [|1..1000|])
    test "Array.sortBy f" (Array.sortBy int [|1000..-1..1|] = [|1..1000|])

    let testGen s f = 
        test ("Array.sortBy a "+s) (Array.sortBy f [||] = [||])
        test ("Array.sortBy b "+s) (Array.sortBy f [|1|] = [|1|])
        test ("Array.sortBy c "+s) (Array.sortBy f [|1;2|] = [|1;2|])
        test ("Array.sortBy d "+s) (Array.sortBy f [|2;1|] = [|1;2|])
        test ("Array.sortBy e "+s) (Array.sortBy f [|1..1000|] = [|1..1000|])
        test ("Array.sortBy f "+s) (Array.sortBy f [|1000..-1..1|] = [|1..1000|])

    // All these projects from integers preserve the expected key ordering for the tests in 'testGen()'
    testGen "int" int
    testGen "uint32" uint32
    testGen "int16" int16
    testGen "uint16" uint16
    testGen "int64" int64
    testGen "uint64" uint64
    testGen "nativeint" nativeint
    testGen "unativeint" unativeint
    testGen "float" float
    testGen "float32" float32
    testGen "decimal" decimal

    test "Array.sortBy g" (Array.sortBy int [|"4";"2";"3";"1";"5"|] = [|"1";"2";"3";"4";"5"|])
    test "Array.sortBy h" (Array.sortBy abs [|1;-2;5;-4;0;-6;3|] = [|0;1;-2;3;-4;5;-6|])
    test "Array.sortBy i" (Array.sortBy String.length [|"a";"abcd";"ab";"";"abc"|] = [|"";"a";"ab";"abc";"abcd"|])


let test_list_stableSortBy() = 
    for lo in 0 .. 100 do 
      for hi in lo .. 100 do
         test (sprintf "vre9u0rejkn, lo = %d, hi = %d" lo hi) (List.sortBy snd [ for i in lo .. hi -> (i, i % 17) ] = [ for key in 0 .. 16 do for i in lo .. hi do if i % 17 = key then yield (i, i % 17) ])

test_list_stableSortBy()         


[<CustomEquality;CustomComparison>]
type Key = 
    | Key of int * int
    interface System.IComparable with 
        member x.CompareTo(yobj:obj) =
            match yobj with 
            | :? Key as y -> 
                let (Key(y1,y2)) = y in
                let (Key(x1,x2)) = x in
                compare x2 y2
            | _ -> failwith "failure"

    override x.Equals(yobj) = 
        match yobj with 
        | :? Key as y -> 
            let (Key(y1,y2)) = y in
            let (Key(x1,x2)) = x in
            x2 = y2
        | _ -> false

    override x.GetHashCode() = 
        let (Key(x1,x2)) = x in
        hash x2 

let test_list_stableSort() = 
    for lo in 0 .. 100 do 
      for hi in lo .. 100 do
         test (sprintf "vre9u0rejkn, lo = %d, hi = %d" lo hi) (List.sort [ for i in lo .. hi -> Key(i, i % 17) ] = [ for key in 0 .. 16 do for i in lo .. hi do if i % 17 = key then yield Key(i, i % 17) ])

test_list_stableSort()         

let test_list_stableSortByNonIntegerKey() = 
    for lo in 0 .. 100 do 
      for hi in lo .. 100 do
         test (sprintf "vre9u0rejkn, lo = %d, hi = %d" lo hi) (List.sortBy (fun (Key(a,b)) -> Key(0,b)) [ for i in lo .. hi -> Key(i, i % 17) ] = [ for key in 0 .. 16 do for i in lo .. hi do if i % 17 = key then yield Key(i, i % 17) ])

test_list_stableSortByNonIntegerKey()         


let test_zip () =
    test "zip"
        (Array.zip [|1..10|] [|1..10|] = [|for i in 1..10 -> i, i|])

    let unzip1, unzip2 = Array.unzip <| [|for i in 1..10 -> i, i+1|]
    test "unzip" (unzip1 = [|1..10|] && unzip2 = [|2..11|])

let test_zip3 () =
    test "zip3"
        (Array.zip3 [|1..10|] [|1..10|] [|1..10|] = [|for i in 1..10 -> i, i, i|])

    let unzip1, unzip2, unzip3 = Array.unzip3 <| [|for i in 1..10 -> i, i+1, i+2|]
    test "unzip3" (unzip1 = [|1..10|] && unzip2 = [|2..11|] && unzip3 = [|3..12|])


let test_rev () =
    test "rev a"
        (Array.rev [|0..100|] = [|100..-1 ..0|])

    test "rev b"
        (Array.rev [|1|] = [|1|])

    test "rev c"
        (Array.rev [||] = [||])

    test "rev d"
        (Array.rev [|1; 2|] = [|2; 1|])

let test_sum () =
    test "sum a" (Array.sum [||] = 0)
    test "sum b" (Array.sum [|42|] = 42)
    test "sum c" (Array.sum [|42;-21|] = 21)
    test "sum d" (Array.sum [|1..1000|] = (1000*1001) / 2)
    test "sum e" (Array.sum [|1.;2.;3.|] = 6.)
    test "sum f" (Array.sum [|1.;2.;infinity;3.|] = infinity)

let test_sum_by () =
    test "sum_by a" (Array.sumBy int [||] = 0)
    test "sum_by b" (Array.sumBy int [|42|] = 42)
    test "sum_by c" (Array.sumBy int [|42;-21|] = 21)
    test "sum_by d" (Array.sumBy int [|1..1000|] = (1000*1001) / 2)
    test "sum_by e" (Array.sumBy float [|1.;2.;3.|] = 6.)
    test "sum_by f" (Array.sumBy float [|1.;2.;infinity;3.|] = infinity)
    test "sum_by g" (Array.sumBy abs [|1; -2; 3; -4|] = 10)
    test "sum_by h" (Array.sumBy String.length [|"abcd";"efg";"hi";"j";""|] = 10)

let test_average () =
    test "average a1" (try Array.average ([||]: float array) |> ignore; false with :? System.ArgumentException -> true)
    test "average a2" (try Array.average ([||]: float32 array) |> ignore; false with :? System.ArgumentException -> true)
    test "average a3" (try Array.average ([||]: decimal array) |> ignore; false with :? System.ArgumentException -> true)
    test "average a4" (Array.average [|0.|] = 0.)
    test "average b" (Array.average [|4.|] = 4.)
    test "average c" (Array.average [|4.;6.|] = 5.)

    test "average_by a1" (try Array.averageBy id ([||]: float array) |> ignore; false with :? System.ArgumentException -> true)
    test "average_by a2" (try Array.averageBy id ([||]: float32 array) |> ignore; false with :? System.ArgumentException -> true)
    test "average_by a3" (try Array.averageBy id ([||]: decimal array) |> ignore; false with :? System.ArgumentException -> true)
    test "average_by a4" (Array.averageBy float [|0..1000|] = 500.)
    test "average_by b" (Array.averageBy (String.length >> float) [|"ab";"cdef"|] = 3.)

let test_min () =
    test "min a" (Array.min [|42|] = 42)
    test "min b" (Array.min [|42;21|] = 21)
    test "min c" (Array.min [|'a';'b'|] = 'a')

    test "max a" (Array.max [|42|] = 42)
    test "max b" (Array.max [|42;21|] = 42)
    test "max c" (Array.max [|'a';'b'|] = 'b')

let test_min_by () =
    test "min_by a" (Array.minBy int [|42|] = 42)
    test "min_by b" (Array.minBy abs [|-42;-21|] = -21)
    test "min_by c" (Array.minBy int [|'a';'b'|] = 'a')

    test "max_by a" (Array.maxBy int [|42|] = 42)
    test "max_by b" (Array.maxBy abs [|-42;-21|] = -42)
    test "max_by c" (Array.maxBy int [|'a';'b'|] = 'b')

let test_seq () =
    test "to_seq" (Array.ofSeq [1..100] = [|1..100|])
    test "to_seq" ([|1..100|] |> Array.toSeq |> Array.ofSeq = [|1..100|])


let test_zero_create () = 
  let arr = Array.zeroCreate 3 in 
  ignore (Array.set arr 0 4);
  ignore (Array.set arr 1 3);
  ignore (Array.set arr 2 2);
  test "fewoih" (Array.get arr 0 = 4);
  test "vvrew0" (Array.get arr 1 = 3);
  test "vvrew0" (Array.get arr 2 = 2)

let test_zero_create_2 () = 
  let arr = Array.zeroCreate 0 in 
  test "sdio2" (Array.length arr = 0)

let test_init () = 
  let arr = Array.init 4 (fun x -> x + 1) in 
  test "test2927: sdvjk2" (Array.get arr 0 = 1);
  test "test2927: cedkj" (Array.get arr 2 = 3)

let test_init_empty () = 
  let arr = Array.init 0 (fun x -> x + 1) in 
  test "test2927: sdvjk2" (Array.length arr = 0)

let test_append () = 
  let arr = Array.append ( [| "4";"3" |]) ( [| "2" |]) in
  test "test2928: sdvjk2" (Array.get arr 0 = "4");
  test "test2928: cedkj" (Array.get arr 2 = "2");
  test "test2928: cedkj" (Array.length arr = 3)

let test_append_empty () = 
  let arr = Array.append ( [| |]) ( [| |]) in
  test "test2928: cedkj" (Array.length arr = 0)

let test_fill () = 
  let arr =  [| "4";"3";"2" |] in
  Array.fill arr 1 2 "1";
  test "test2929: sdvjk2" (Array.get arr 0 = "4");
  test "test2929: cedkj" (Array.get arr 2 = "1")

let test_copy () = 
  let arr =  [| "4";"3";"2" |] in
  let arr2 =  Array.copy arr  in
  test "test2929: sdvjk2" (Array.get arr2 0 = "4");
  test "test2929: cedkj" (Array.get arr2 2 = "2");
  test "feio" (not (LanguagePrimitives.PhysicalEquality arr arr2))

let test_blit () = 
  let arr =  [| "4";"3";"2";"0" |] in
  let arr2 =  [| "4";"3";"-1"; "-1" |] in
  Array.blit arr 1 arr2 2 2;
  test "test2930: sdvjk2" (Array.get arr2 0 = "4");
  test "test2930: cedkj" (Array.get arr2 1 = "3");
  test "test2930: ceddwkj" (Array.get arr2 2 = "3");
  test "test2930: ceqwddkj" (Array.get arr2 3 = "2")

let test_of_list () = 
  let arr = Array.ofList [ "4";"3";"2";"0" ] in
  test "test2931: sdvjk2" (Array.get arr 0 = "4");
  test "test2931: cedkj" (Array.get arr 1 = "3");
  test "test2931: ceddwkj" (Array.get arr 2 = "2");
  test "test2931: ceqwddkj" (Array.get arr 3 = "0")

let test_to_list () = 
  test "test2932" (Array.toList ( [| "4";"3";"2";"0" |]) =  [ "4";"3";"2";"0" ])

let test_to_list_of_list () = 
  test "test2933" (Array.toList (Array.ofList [ "4";"3";"2";"0" ]) = [ "4";"3";"2";"0" ])

let test_fold_left () = 
  let arr = Array.ofList [ 4;3;2;1 ] in
  test "test2931: sdvjk2few" (Array.fold (fun x y -> x/y) (5*4*3*2*1) arr = 5)

let test_fold_right () = 
  let arr = Array.ofList [ 4;3;2;1 ] in
  test "test2931: sdvjk2ew" (Array.foldBack (fun y x -> x/y) arr (6*4*3*2*1) = 6)

let test_reduce_left () = 
  test "test2931: array.reduce" (Array.reduce (fun x y -> x/y) [|5*4*3*2; 4;3;2;1|] = 5)

let test_reduce_right () = 
  let arr = Array.ofList [ 4;3;2;1;5 ] in
  test "test2931: array.reduceBack" (Array.reduceBack (fun y x -> x/y) [|4;3;2;1; 5*4*3*2|] = 5)


let _ = test_make_get_set_length ()
let _ = test_const ()
let _ = test_const_empty ()
let _ = test_map ()
let _ = test_mapi ()
let _ = test_iter ()
let _ = test_iteri ()
let _ = test_mapi ()
let _ = test_isEmpty ()
let _ = test_create ()
let _ = test_concat ()
let _ = test_sub ()
let _ = test_fold2 ()
let _ = test_foldBack2 ()
let _ = test_scan ()
let _ = test_iter2 ()
let _ = test_iteri2 ()
let _ = test_iter ()
let _ = test_map2 ()
let _ = test_mapi2 ()
let _ = test_exists ()
let _ = test_forall ()
let _ = test_iter ()
let _ = test_exists2 ()
let _ = test_forall2 ()
let _ = test_filter ()
let _ = test_partition ()
let _ = test_choose ()
let _ = test_find ()
let _ = test_findIndex ()
let _ = test_tryfind ()
let _ = test_first ()
let _ = test_sort ()
let _ = test_sort_by ()
let _ = test_zip ()
let _ = test_zip3 ()
let _ = test_rev ()
let _ = test_sum ()
let _ = test_sum_by ()
let _ = test_average ()
let _ = test_min ()
let _ = test_min_by ()
let _ = test_seq ()
let _ = test_zero_create ()
let _ = test_zero_create_2 ()
let _ = test_append ()
let _ = test_append_empty ()
let _ = test_init ()
let _ = test_init_empty ()
let _ = test_fill ()
let _ = test_blit ()
let _ = test_of_list ()
let _ = test_to_list ()
let _ = test_to_list_of_list ()
let _ = test_copy ()
let _ = test_iter ()
let _ = test_iteri ()
let _ = test_fold_left ()
let _ = test_fold_right ()
let _ = test_reduce_left ()
let _ = test_reduce_right ()

module Array2Tests = begin

  let test_make_get_set_length () = 
    let arr = Array2D.create 3 4 0 in 
    test "fewoih1" (Array2D.get arr 0 0 = 0);
    test "fewoih2" (Array2D.get arr 0 1 = 0);
    test "vvrew03" (Array2D.get arr 2 2 = 0);
    test "vvrew04" (Array2D.get arr 2 3 = 0);
    ignore (Array2D.set arr 0 2 4);
    test "vsdiuvs5" (Array2D.get arr 0 2 = 4);
    arr.[0,2] <- 2;
    test "vsdiuvs6" (arr.[0,2] = 2);
    test "vropivrwe7" (Array2D.length1 arr = 3);
    test "vropivrwe8" (Array2D.length2 arr = 4)

    let a = Array2D.init 10 10 (fun i j -> i,j)
    let b = Array2D.init 2 2 (fun i j -> i+1,j+1)
    //test "a2_sub"
    //    (Array2D.sub a 1 1 2 2 = b)


    Array2D.blit b 0 0 a 0 0 2 2
    //test "a2_blit"
    //      (Array2D.sub a 0 0 2 2 = b)

  let _ = test_make_get_set_length ()


end

module ArrayNonZeroBasedTestsSlice = 
  let runTest () = 
    let arr = (Array2D.initBased 5 4 3 2 (fun i j -> (i,j)))
    test "fewoih1" (arr.[6,*] = [|(6, 4); (6, 5)|])
    test "fewoih2" (arr.[*,*].[1,*] = [|(6, 4); (6, 5)|])
    test "fewoih3" (arr.[*,5] =  [|(5, 5); (6, 5); (7, 5)|])
    test "fewoih4" (arr.[*,*].[*,1] =  [|(5, 5); (6, 5); (7, 5)|])
    test "fewoih5" (arr.GetLowerBound(0) = 5)
    test "fewoih6" (arr.GetLowerBound(1) = 4)
    test "fewoih7" (arr.[*,*].GetLowerBound(0) = 0)
    test "fewoih8" (arr.[*,*].GetLowerBound(1) = 0)
    test "fewoih9" (arr.[*,*].[0..,1] =  [|(5, 5); (6, 5); (7, 5)|])
    test "fewoih10" (arr.[*,*].[1..,1] =  [|(6, 5); (7, 5)|])
    let arr2d = 
        let arr = Array2D.zeroCreateBased 5 4 3 2 
        for i in 5..7 do for j in 4..5 do arr.[i,j] <- (i,j)
        arr
    let arr2d2 = 
        let arr = Array2D.zeroCreate 3 2 
        for i in 0..2 do for j in 0..1 do arr.[i,j] <- (j,i)
        arr
    test "fewoih11" (arr2d.[6..6,5] =  [|(6, 5)|])
    test "fewoih11" (arr2d.[..6,5] =  [|(5, 5); (6, 5)|])
    test "fewoih11" (arr2d.[6..,5] =  [|(6, 5); (7, 5)|])
    test "fewoih12" (arr2d.[*,*].[1..,1] =  [|(6, 5); (7, 5)|])
    arr2d.[*,*] <- arr2d2
    test "fewoih13" (arr2d.[*,*].[0..0,1] =  [|(1, 0)|])
    test "fewoih13" (arr2d.[*,*].[1..,1] =  [|(1, 1); (1, 2)|])
    test "fewoih13" (arr2d.[*,*].[1,1..] =  [|(1, 1)|])
    test "fewoih13" (arr2d.[*,*].[1,0..0] =  [|(0, 1)|])
    let arr3d = 
        let arr = System.Array.CreateInstance(typeof<int*int*int>, [| 3;2;1 |], [|5;4;3|]) :?> (int*int*int)[,,]
        for i in 5..7 do for j in 4..5 do for k in 3..3 do arr.[i,j,k] <- (i,j,k)
        arr
    let arr3d2 = 
        let arr = System.Array.CreateInstance(typeof<int*int*int>, [| 3;2;1 |]) :?> (int*int*int)[,,]
        for i in 0..2 do for j in 0..1 do for k in 0..0 do arr.[i,j,k] <- (k,j,i)
        arr

    test "fewoih14" (arr3d.[5,4,3] = (5,4,3))
    test "fewoih15" (arr3d.[*,*,*].[0,0,0] =  (5,4,3))
    arr3d.[*,*,*] <- arr3d2
    test "fewoih16" (arr3d.[5,4,3] =  (0,0,0))
    test "fewoih16" (arr3d.[5,5,3] =  (0,1,0))
    test "fewoih16" (arr3d.[6,5,3] =  (0,1,1))
  let _ = runTest()

module Array3Tests = begin

  let test_make_get_set_length () = 
    let arr = Array3D.create 3 4 5 0 in 
    test "fewoih1" (Array3D.get arr 0 0 0 = 0);
    test "fewoih2" (Array3D.get arr 0 1 0 = 0);
    test "vvrew03" (Array3D.get arr 2 2 2 = 0);
    test "vvrew04" (Array3D.get arr 2 3 4 = 0);
    ignore (Array3D.set arr 0 2 3 4);
    test "vsdiuvs5" (Array3D.get arr 0 2 3 = 4);
    arr.[0,2,3] <- 2;
    test "vsdiuvs6" (arr.[0,2,3] = 2);
    arr.[0,2,3] <- 3;
    test "vsdiuvs" (arr.[0,2,3] = 3);
    test "vropivrwe7" (Array3D.length1 arr = 3);
    test "vropivrwe8" (Array3D.length2 arr = 4);
    test "vropivrwe9" (Array3D.length3 arr = 5)

  let _ = test_make_get_set_length ()

end

module Array4Tests = begin

  let test_make_get_set_length () = 
    let arr = Array4D.create 3 4 5 6 0 in 
    arr.[0,2,3,4] <- 2;
    test "vsdiuvsq" (arr.[0,2,3,4] = 2);
    arr.[0,2,3,4] <- 3;
    test "vsdiuvsw" (arr.[0,2,3,4] = 3);
    test "vsdiuvsw" (Array4D.get arr 0 2 3 4 = 3);
    Array4D.set arr 0 2 3 4 5;
    test "vsdiuvsw" (Array4D.get arr 0 2 3 4 = 5);
    test "vropivrwee" (Array4D.length1 arr = 3);
    test "vropivrwer" (Array4D.length2 arr = 4);
    test "vropivrwet" (Array4D.length3 arr = 5)
    test "vropivrwey" (Array4D.length4 arr = 6)

  let test_init () = 
    let arr = Array4D.init 3 4 5 6 (fun i j k m -> i+j+k+m) in 
    test "vsdiuvs1" (arr.[0,2,3,4] = 9);
    test "vsdiuvs2" (arr.[0,2,3,3] = 8);
    test "vsdiuvs3" (arr.[0,0,0,0] = 0);
    arr.[0,2,3,4] <- 2;
    test "vsdiuvs4" (arr.[0,2,3,4] = 2);
    arr.[0,2,3,4] <- 3;
    test "vsdiuvs5" (arr.[0,2,3,4] = 3);
    test "vropivrwe1" (Array4D.length1 arr = 3);
    test "vropivrwe2" (Array4D.length2 arr = 4);
    test "vropivrwe3" (Array4D.length3 arr = 5)
    test "vropivrwe4" (Array4D.length4 arr = 6)

  let _ = test_make_get_set_length ()
  let _ = test_init ()

end

module Array2TestsNoDot = begin

  let test_make_get_set_length () = 
    let arr = Array2D.create 3 4 0 in 
    test "fewoih1" (Array2D.get arr 0 0 = 0);
    test "fewoih2" (Array2D.get arr 0 1 = 0);
    test "vvrew03" (Array2D.get arr 2 2 = 0);
    test "vvrew04" (Array2D.get arr 2 3 = 0);
    ignore (Array2D.set arr 0 2 4);
    test "vsdiuvs5" (Array2D.get arr 0 2 = 4);
    arr[0,2] <- 2;
    test "vsdiuvs6" (arr[0,2] = 2);
    test "vropivrwe7" (Array2D.length1 arr = 3);
    test "vropivrwe8" (Array2D.length2 arr = 4)

    let a = Array2D.init 10 10 (fun i j -> i,j)
    let b = Array2D.init 2 2 (fun i j -> i+1,j+1)
    //test "a2_sub"
    //    (Array2D.sub a 1 1 2 2 = b)


    Array2D.blit b 0 0 a 0 0 2 2
    //test "a2_blit"
    //      (Array2D.sub a 0 0 2 2 = b)

  let _ = test_make_get_set_length ()


end

module ArrayNonZeroBasedTestsSliceNoDot = 
  let runTest () = 
    let arr = (Array2D.initBased 5 4 3 2 (fun i j -> (i,j)))
    test "fewoih1" (arr[6,*] = [|(6, 4); (6, 5)|])
    //TODO: problems here, see https://github.com/dotnet/fsharp/pull/11749#issuecomment-874204962
    //test "fewoih2" (arr[*,*][1,*] = [|(6, 4); (6, 5)|])
    test "fewoih3" (arr[*,5] =  [|(5, 5); (6, 5); (7, 5)|])
    //TODO: problems here, see https://github.com/dotnet/fsharp/pull/11749#issuecomment-874204962
    //test "fewoih4" (arr[*,*][*,1] =  [|(5, 5); (6, 5); (7, 5)|])
    test "fewoih5" (arr.GetLowerBound(0) = 5)
    test "fewoih6" (arr.GetLowerBound(1) = 4)
    test "fewoih7" (arr[*,*].GetLowerBound(0) = 0)
    test "fewoih8" (arr[*,*].GetLowerBound(1) = 0)
    //TODO: problems here, see https://github.com/dotnet/fsharp/pull/11749#issuecomment-874204962
    //test "fewoih9" (arr[*,*][0..,1] =  [|(5, 5); (6, 5); (7, 5)|])
    //test "fewoih10" (arr[*,*][1..,1] =  [|(6, 5); (7, 5)|])
    let arr2d = 
        let arr = Array2D.zeroCreateBased 5 4 3 2 
        for i in 5..7 do for j in 4..5 do arr[i,j] <- (i,j)
        arr
    let arr2d2 = 
        let arr = Array2D.zeroCreate 3 2 
        for i in 0..2 do for j in 0..1 do arr[i,j] <- (j,i)
        arr
    test "fewoih11" (arr2d[6..6,5] =  [|(6, 5)|])
    test "fewoih11" (arr2d[..6,5] =  [|(5, 5); (6, 5)|])
    test "fewoih11" (arr2d[6..,5] =  [|(6, 5); (7, 5)|])
    //TODO: problems here, see https://github.com/dotnet/fsharp/pull/11749#issuecomment-874204962
    //test "fewoih12" (arr2d[*,*][1..,1] =  [|(6, 5); (7, 5)|])
    //arr2d[*,*] <- arr2d2
    //test "fewoih13" (arr2d[*,*][0..0,1] =  [|(1, 0)|])
    //test "fewoih13" (arr2d[*,*][1..,1] =  [|(1, 1); (1, 2)|])
    //test "fewoih13" (arr2d[*,*][1,1..] =  [|(1, 1)|])
    //test "fewoih13" (arr2d[*,*][1,0..0] =  [|(0, 1)|])
    let arr3d = 
        let arr = System.Array.CreateInstance(typeof<int*int*int>, [| 3;2;1 |], [|5;4;3|]) :?> (int*int*int)[,,]
        for i in 5..7 do for j in 4..5 do for k in 3..3 do arr[i,j,k] <- (i,j,k)
        arr
    let arr3d2 = 
        let arr = System.Array.CreateInstance(typeof<int*int*int>, [| 3;2;1 |]) :?> (int*int*int)[,,]
        for i in 0..2 do for j in 0..1 do for k in 0..0 do arr[i,j,k] <- (k,j,i)
        arr

    test "fewoih14" (arr3d[5,4,3] = (5,4,3))
    //TODO: problems here, see https://github.com/dotnet/fsharp/pull/11749#issuecomment-874204962
    //test "fewoih15" (arr3d[*,*,*][0,0,0] =  (5,4,3))
    arr3d[*,*,*] <- arr3d2
    test "fewoih16" (arr3d[5,4,3] =  (0,0,0))
    test "fewoih16" (arr3d[5,5,3] =  (0,1,0))
    test "fewoih16" (arr3d[6,5,3] =  (0,1,1))
  let _ = runTest()

module Array3TestsNoDot = begin

  let test_make_get_set_length () = 
    let arr = Array3D.create 3 4 5 0 in 
    test "fewoih1" (Array3D.get arr 0 0 0 = 0);
    test "fewoih2" (Array3D.get arr 0 1 0 = 0);
    test "vvrew03" (Array3D.get arr 2 2 2 = 0);
    test "vvrew04" (Array3D.get arr 2 3 4 = 0);
    ignore (Array3D.set arr 0 2 3 4);
    test "vsdiuvs5" (Array3D.get arr 0 2 3 = 4);
    arr[0,2,3] <- 2;
    test "vsdiuvs6" (arr[0,2,3] = 2);
    arr[0,2,3] <- 3;
    test "vsdiuvs" (arr[0,2,3] = 3);
    test "vropivrwe7" (Array3D.length1 arr = 3);
    test "vropivrwe8" (Array3D.length2 arr = 4);
    test "vropivrwe9" (Array3D.length3 arr = 5)

  let _ = test_make_get_set_length ()

end

module Array4TestsNoDot = begin

  let test_make_get_set_length () = 
    let arr = Array4D.create 3 4 5 6 0 in 
    arr[0,2,3,4] <- 2;
    test "vsdiuvsq" (arr[0,2,3,4] = 2);
    arr[0,2,3,4] <- 3;
    test "vsdiuvsw" (arr[0,2,3,4] = 3);
    test "vsdiuvsw" (Array4D.get arr 0 2 3 4 = 3);
    Array4D.set arr 0 2 3 4 5;
    test "vsdiuvsw" (Array4D.get arr 0 2 3 4 = 5);
    test "vropivrwee" (Array4D.length1 arr = 3);
    test "vropivrwer" (Array4D.length2 arr = 4);
    test "vropivrwet" (Array4D.length3 arr = 5)
    test "vropivrwey" (Array4D.length4 arr = 6)

  let test_init () = 
    let arr = Array4D.init 3 4 5 6 (fun i j k m -> i+j+k+m) in 
    test "vsdiuvs1" (arr[0,2,3,4] = 9);
    test "vsdiuvs2" (arr[0,2,3,3] = 8);
    test "vsdiuvs3" (arr[0,0,0,0] = 0);
    arr[0,2,3,4] <- 2;
    test "vsdiuvs4" (arr[0,2,3,4] = 2);
    arr[0,2,3,4] <- 3;
    test "vsdiuvs5" (arr[0,2,3,4] = 3);
    test "vropivrwe1" (Array4D.length1 arr = 3);
    test "vropivrwe2" (Array4D.length2 arr = 4);
    test "vropivrwe3" (Array4D.length3 arr = 5)
    test "vropivrwe4" (Array4D.length4 arr = 6)

  let _ = test_make_get_set_length ()
  let _ = test_init ()

end

#if PERF
let test_map_perf () = 
  let arr1 = [| 4;3;2 |] in 
  let res = ref (Array.map (fun x -> x + 1) arr1) in
  for i = 1 to 20000000 do 
    res := Array.map (fun x -> x + 1) arr1
  done;
  test "test2927: sdvjk2" (Array.get !res 0 = 5)

let _ = test_map_perf()
#endif

module SeqCacheAllTest = 
    let s2 = 
       let count = ref 0 
       let s = Seq.cache (seq { for i in 0 .. 10 -> (incr count; i) }) :> seq<_>
       let test0 = (!count = 0)
       let e1 = s.GetEnumerator()
       let test1 = (!count = 0)
       printf "test1 = %b\n" test1;
       for i = 1 to 1 do (e1.MoveNext() |> ignore; e1.Current |> ignore)
       let test2 = (!count = 1)
       printf "test2 = %b\n" test2;
       let e2 = s.GetEnumerator()
       for i = 1 to 5 do (e2.MoveNext() |> ignore; e2.Current |> ignore)
       let test3 = (!count = 5)
       printf "test3 = %b\n" test3;
       let e3 = s.GetEnumerator()
       for i = 1 to 5 do (e3.MoveNext() |> ignore; e3.Current |> ignore)
       let test4 = (!count = 5)
       printf "test4 = %b\n" test4;
       let e4 = s.GetEnumerator()
       for i = 1 to 3 do (e4.MoveNext() |> ignore; e4.Current |> ignore)
       let test5 = (!count = 5)
       printf "test5 = %b\n" test5;

       let test6 = [ for x in s -> x ] = [ 0 .. 10 ]
       printf "test6 = %b\n" test6;
       for x in s do ()
       let test7 = (!count = 11)
       let test8 = [ for x in s -> x ] = [ 0 .. 10 ]
       let test9 = !count = 11
       test "test0" test0
       test "test1" test1
       test "test2" test2
       test "test3" test3
       test "test4" test4
       test "test5" test5
       test "test6" test6
       test "test7" test7
       test "test8" test8
       test "test9" test9

module MiscIndexNotationTests =
    [<Measure>]
    type foo
    let m, n = 1<foo>, 10<foo>
    let vs1 = seq { 1 .. 2 .. 3 }         
    let vs2 = [ 1 .. 2 .. 4 ]         
    let vs3 = [m .. 1<foo> .. n]         
    let vs4 = [| 1 .. 2 .. 4 |]         
    let vs5 = [| m .. 1<foo> .. n |]         

    let arr = [| 1;2;3;4;5 |]
    let arr2 : int[,] = Array2D.zeroCreate 5 5

    let v1 = arr[1] 
    let v2 = arr2[1,1] 
    let v3 = arr[1..3] 

    let v4 = arr[..3]
    let v5 = arr[1..]
    let v6 = arr[*]

    let v7  = arr2[1..3,1..3]
    let v8  = arr2[..3,1..3]
    let v9  = arr2[1..,1..3]
    let v10 = arr2[*,1..3]
    let v11 = arr2[1..3,1..]
    let v12 = arr2[..3,1..]
    let v13 = arr2[1..,1..]
    let v14 = arr2[*,1..]
    let v15 = arr2[1..3,..3]
    let v16 = arr2[..3,..3]
    let v17 = arr2[1..,..3]
    let v18 = arr2[*,..3]
    let v19 = arr2[1..3,*]
    let v20 = arr2[..3,*]
    let v21 = arr2[1..,*]
    let v22 = arr2[*,*]

(* TODO: problems here, see https://github.com/dotnet/fsharp/pull/11749#issuecomment-874204962

    module Apps =
        let arrf () = arr
        let arrf2 () = arr2
        let v1 = arrf()[1] 
        let v2 = arrf2()[1,1] 
        let v3 = arrf()[1..3] 

        let v4 = arrf()[..3]
        let v5 = arrf()[1..]
        let v6 = arrf()[*]

        let v7  = arrf2()[1..3,1..3]
        let v8  = arrf2()[..3,1..3]
        let v9  = arrf2()[1..,1..3]
        let v10 = arrf2()[*,1..3]
        let v11 = arrf2()[1..3,1..]
        let v12 = arrf2()[..3,1..]
        let v13 = arrf2()[1..,1..]
        let v14 = arrf2()[*,1..]
        let v15 = arrf2()[1..3,..3]
        let v16 = arrf2()[..3,..3]
        let v17 = arrf2()[1..,..3]
        let v18 = arrf2()[*,..3]
        let v19 = arrf2()[1..3,*]
        let v20 = arrf2()[..3,*]
        let v21 = arrf2()[1..,*]
        let v22 = arrf2()[*,*]
*)

module ArrayStructMutation = 
    module Array1D = 
        module Test1 = 
            [<Struct>]
            type T =
               val mutable i : int
            let a = Array.create 10 Unchecked.defaultof<T>
            a.[0].i <- 27
            check "wekvw0301" 27 a.[0].i


        module Test2 = 

            [<Struct>]
            type T =
               val mutable public i  : int
               member public this.Set i = this.i <- i
            let a  = Array.create 10 Unchecked.defaultof<T>
            a.[0].Set 27
            a.[2].Set 27
            check "wekvw0302" 27 a.[0].i
            check "wekvw0303" 27 a.[2].i
            
    module Array2D = 
        module Test1 = 
            [<Struct>]
            type T =
               val mutable i : int
            let a = Array2D.create 10 10 Unchecked.defaultof<T>
            a.[0,0].i <- 27
            check "wekvw0304" 27 a.[0,0].i


        module Test2 = 

            [<Struct>]
            type T =
               val mutable public i  : int
               member public this.Set i = this.i <- i
            let a  = Array2D.create 10 10 Unchecked.defaultof<T>
            a.[0,0].Set 27
            a.[0,2].Set 27
            check "wekvw0305" 27 a.[0,0].i
            check "wekvw0306" 27 a.[0,2].i
            

    module Array3D = 
        module Test1 = 
            [<Struct>]
            type T =
               val mutable i : int
            let a = Array3D.create 10 10 10 Unchecked.defaultof<T>
            a.[0,0,0].i <- 27
            a.[0,2,3].i <- 27
            check "wekvw0307" 27 a.[0,0,0].i
            check "wekvw0308" 27 a.[0,2,3].i


        module Test2 = 

            [<Struct>]
            type T =
               val mutable public i  : int
               member public this.Set i = this.i <- i
            let a  = Array3D.create 10 10 10 Unchecked.defaultof<T>
            a.[0,0,0].Set 27
            a.[0,2,3].Set 27
            check "wekvw0309" 27 a.[0,0,0].i
            check "wekvw030q" 27 a.[0,2,3].i
            
    module Array4D = 
        module Test1 = 
            [<Struct>]
            type T =
               val mutable i : int
            let a = Array4D.create 10 10 10 10 Unchecked.defaultof<T>
            a.[0,0,0,0].i <- 27
            a.[0,2,3,4].i <- 27
            check "wekvw030w" 27 a.[0,0,0,0].i
            check "wekvw030e" 27 a.[0,2,3,4].i


        module Test2 = 

            [<Struct>]
            type T =
               val mutable public i  : int
               member public this.Set i = this.i <- i
            let a  = Array4D.create 10 10 10 10 Unchecked.defaultof<T>
            a.[0,0,0,0].Set 27
            a.[0,2,3,4].Set 27
            check "wekvw030r" 27 a.[0,0,0,0].i 
            check "wekvw030t" 27 a.[0,2,3,4].i

module LoopTests = 
    let loop3 a N = 
       let mutable x = 0 in
       // In this loop, the types of 'a' and 'N' are not known prior to the loop
       for i in (min a a) ..  N do
          x <- x + 1
       done;
       check (sprintf "clkrerev90-%A" (a,N)) x  (if N < a then 0 else N - a + 1) 


    do loop3 0 10
    do loop3 0 0
    do loop3 0 -1
    do loop3 10  9

    let loop4 a N = 
       let mutable x = 0 in
       for i in OperatorIntrinsics.RangeInt32 a 1 N do
          x <- x + 1
       done;
       check (sprintf "clkrerev91-%A" (a,N)) x (if N < a then 0 else N - a + 1) 

    do loop4 0 10
    do loop4 0 0
    do loop4 0 -1
    do loop4 10  9

    let loop5 a N = 
       let mutable x = 0 in
       // In this loop, the types of 'a' and 'N' are not known prior to the loop
       for i in (min a a) ..  2 .. (min N N) do
          x <- x + 1
       done;
       check (sprintf "clkrerev92-%A" (a,N))  x ((if N < a then 0 else N - a + 2) / 2)

    do loop5 0 10
    do loop5 0 0
    do loop5 0 -1
    do loop5 10  9


    let loop6 a N = 
       let mutable x = 0 in
       // In this loop, the types of 'a' and 'N' are not known prior to the loop
       for i in (min a a) ..  200 .. (min N N) do
          x <- x + 1
       done;
       check (sprintf "clkrerev93-%A" (a,N)) x ((if N < a then 0 else N - a + 200) / 200)

    do loop6 0 10
    do loop6 0 0
    do loop6 0 -1
    do loop6 10  9


    let loop7 a step N = 
       let mutable x = 0 in
       // In this loop, the types of 'a' and 'N' are not known prior to the loop
       for i in (min a a) ..  step .. (min N N) do
          x <- x + 1
       done;
       check (sprintf "clkrerev95-%A" (a,step,N)) x (if step < 0 then (if a < N then 0 else (a - N + abs step) / abs step) else (if N < a then 0 else N - a + step) / step)

    do loop7 0 1 10
    do loop7 0 -1 0
    do loop7 0 2 -1
    do loop7 10  -2 9

    let loop8 a N = 
       let mutable x = 0 in
       // In this loop, the types of 'a' and 'N' are not known prior to the loop
       for i in (min a a) ..  -1 .. (min N N) do
          x <- x + 1
       done;
       check (sprintf "clkrerev96-%A" (a,N))  x (abs (if a < N then 0 else (a - N + 1) / 1))

    do loop8 0 10
    do loop8 0 0
    do loop8 0 -1
    do loop8 10 9

// Some more adhoc testing - the use of 'min' gives rise to a let binding in optimized code
module MoreLoopTestsWithLetBindings = 
    let loop3 a N = 
       let mutable x = 0 in
       // In this loop, the types of 'a' and 'N' are not known prior to the loop
       for i in (min a a) ..  (min N N) do
          x <- x + 1
       done;
       check (sprintf "ffclkrerev90-%A" (a,N)) x  (if N < a then 0 else N - a + 1) 


    do loop3 0 10
    do loop3 0 0
    do loop3 0 -1
    do loop3 10  9
    do for start in -3 .. 3 do for finish in -3 .. 3 do loop3 start finish

    let loop4 a N = 
       let mutable x = 0 in
       for i in OperatorIntrinsics.RangeInt32 a 1 N do
          x <- x + 1
       done;
       check (sprintf "ffclkrerev91-%A" (a,N)) x (if N < a then 0 else N - a + 1) 

    do loop4 0 10
    do loop4 0 0
    do loop4 0 -1
    do loop4 10  9
    do for start in -3 .. 3 do for finish in -3 .. 3 do loop4 start finish

    let loop5 a N = 
       let mutable x = 0 in
       // In this loop, the types of 'a' and 'N' are not known prior to the loop
       for i in (min a a) ..  2 .. (min N N) do
          x <- x + 1
       done;
       check (sprintf "ffclkrerev92-%A" (a,N))  x ((if N < a then 0 else N - a + 2) / 2)

    do loop5 0 10
    do loop5 0 0
    do loop5 0 -1
    do loop5 10  9
    do for start in -3 .. 3 do for finish in -3 .. 3 do loop5 start finish


    let loop6 a N = 
       let mutable x = 0 in
       // In this loop, the types of 'a' and 'N' are not known prior to the loop
       for i in (min a a) ..  200 .. (min N N) do
          x <- x + 1
       done;
       check (sprintf "ffclkrerev93-%A" (a,N)) x ((if N < a then 0 else N - a + 200) / 200)

    do loop6 0 10
    do loop6 0 0
    do loop6 0 -1
    do loop6 10  9
    do for start in -3 .. 3 do for finish in -3 .. 3 do loop6 start finish


    let loop7 a step N = 
       let mutable x = 0 in
       // In this loop, the types of 'a' and 'N' are not known prior to the loop
       for i in (min a a) ..  step .. (min N N) do
          x <- x + 1
       done;
       check (sprintf "ffclkrerev95-%A" (a,step,N)) x (if step < 0 then (if a < N then 0 else (a - N + abs step) / abs step) else (if N < a then 0 else N - a + step) / step)

    do loop7 0 1 10
    do loop7 0 -1 0
    do loop7 0 2 -1
    do loop7 10  -2 9
    do for start in -3 .. 3 do for finish in -3 .. 3 do for step in [-2; -1; 1; 2] do loop7 start step finish

    let loop8 a N = 
       let mutable x = 0 in
       // In this loop, the types of 'a' and 'N' are not known prior to the loop
       for i in (min a a) ..  -1 .. (min N N) do
          x <- x + 1
       done;
       check (sprintf "ffclkrerev96-%A" (a,N))  x (abs (if a < N then 0 else (a - N + 1) / 1))

    do loop8 0 10
    do loop8 0 0
    do loop8 0 -1
    do loop8 10 9
    do for start in -3 .. 3 do for finish in -3 .. 3 do loop8 start finish

module bug872632 =
    type MarkerStyle = 
        | None      = 0
        | Square    = 1
        | Circle    = 2
        | Diamond    = 3
        | Triangle    = 4
        | Triangle1   = 10
        | Cross    = 5
        | Star4    = 6
        | Star5    = 7
        | Star6    = 8
        | Star10    = 9

     

    module Foo =    
        let x = [| 
                    MarkerStyle.Circle
                    MarkerStyle.Cross
                    MarkerStyle.Star6
                    MarkerStyle.Diamond
                    MarkerStyle.Square        
                    MarkerStyle.Star10
                    MarkerStyle.Triangle
                    MarkerStyle.Triangle1
                |] 

    do check "bug872632" Foo.x.Length 8

module CheckUnionTypesAreSealed =
    open System
#if NETCOREAPP
    open System.Reflection
    type System.Type with
        member this.IsSealed
            with get () = this.GetTypeInfo().IsSealed
#endif

    do check "vwllfewlkefw1" (typedefof<list<int>>.IsSealed) true
    do check "vwllfewlkefw2" (typedefof<option<int>>.IsSealed) true
    type X1 = A | B
    do check "vwllfewlkefw3" (typedefof<X1>.IsSealed) true
    type X2 = A | B of string
    do check "vwllfewlkefw4" (typedefof<X2>.IsSealed) false
    type X3 = A | B | C
    do check "vwllfewlkefw5" (typedefof<X3>.IsSealed) true
    type X4 = A | B | C | D | E | F | G | H | I
    do check "vwllfewlkefw5" (typedefof<X4>.IsSealed) true

    [<CompilationRepresentation(CompilationRepresentationFlags.UseNullAsTrueValue)>]
    type SetTree<'T> = 
        | SetEmpty                                          
        | SetNode of 'T * SetTree<'T> *  SetTree<'T> 
    do check "vwllfewlkefw6" (typedefof<SetTree<int>>.IsSealed) true

    type SetTree2<'T> = 
        | SetEmpty                                          
        | SetNode of 'T * SetTree2<'T> *  SetTree2<'T> 
    do check "vwllfewlkefw6" (typedefof<SetTree2<int>>.IsSealed) false

module manyIndexes =
    open System
    
    // Bug in F# 3.1: Indexer Properties was incorrectly limited to 4 arguments. There were no limits in previous versions of F#, and shouldn't be in future versions
    // Repro code for bug in F# 3.1.  This compiles perfectly in F# 3.0
    
    // ----------------------------------------------------------------------------
    type Test () =
        /// Variable number of arguments with indexer property
        member x.Item with get ([<ParamArray>] objs: obj[]) = objs
        
        /// PASS: Variable number of arguments with member function
        member x.Foo ([<ParamArray>] objs: obj[]) = objs

    // ----------------------------------------------------------------------------
    let CompileIndexerTest = 
        let test = Test ()
    
        // No problems with method having vaiable number of parameters
        let u1 = test.Foo(null, null, null, null)
        let u2 = test.Foo(null, null, null, null, null)
        let u3 = test.Foo(null, null, null, null, null, null, null, null, null)
    
        // Bug was that the indexer Property was limited to 4 parameters (Issue introduced by matrix slicing code)
        let u4 = test.[null]
        let u5 = test.[null, null]
        let u6 = test.[null, null, null]
        let u7 = test.[null, null, null, null]
        let u8 = test.[null, null, null, null, null]                                                    // Ensure that F# 3.1 is not unhappy with more than 4 arguments
        let u9 = test.[null, null, null, null, null, null, null, null, null, null, null, null, null]    // Ensure that F# 3.1 is not unhappy with many more than 4 arguments, 13 arguments would be really unlucky
        0


#if !NETCOREAPP
module bug6447 =
    let a = System.Array.CreateInstance(typeof<int>, [|1|], [|1|])
    let a1 = System.Array.CreateInstance(typeof<int>, [|1|], [|3|])
    let a2 = System.Array.CreateInstance(typeof<int>, [|3|], [|1|])
    
    do check "bug6447_bound1" a a
    do check "bug6447_bound3" a1 a1   
    do check "bug6447_bound1_3" a2 a2
    do check "bug6447_a_lt_a" (Unchecked.compare a a) 0
    do check "bug6447_a_eq_a1" (Unchecked.equals a a1) false
    do check "bug6447_a_lt_a1" (Unchecked.compare a a1) -1
    do check "bug6447_a_lt_a1" (Unchecked.compare a1 a) 1
    do check "bug6447_a_eq_a2" (Unchecked.equals a a2) false
    do check "bug6447_a_lt_a2" (Unchecked.compare a a2) -1
    do check "bug6447_a_lt_a2" (Unchecked.compare a2 a) 1
    do check "bug6447_a1_eq_a2" (Unchecked.equals a1 a2) false
    do check "bug6447_a1_gt_a2" (Unchecked.compare a2 a1) 1
    do check "bug6447_a1_lt_a2" (Unchecked.compare a1 a2) -1
    do check "bug6447_a1_lt_a2" (Unchecked.compare a2 a1) 1
    do check "bug6447_a2_eq_a1" (Unchecked.equals a2 a1) false
    do check "bug6447_a2_gt_a2" (Unchecked.compare a2 a1) 1
    do check "bug6447_a2_lt_a1" (Unchecked.compare a1 a2) -1
    do check "bug6447_hash_a" (hash a) 631
    do check "bug6447_hash_a1" (hash a1) 1893
    do check "bug6447_hash_a2" (hash a2) 10727    
#endif    
    
#if TESTS_AS_APP
let RUN() = failures
#else
let aa =
  match failures with 
  | [] -> 
      stdout.WriteLine "Test Passed"
      System.IO.File.WriteAllText("test.ok","ok")
      exit 0
  | _ -> 
      stdout.WriteLine "Test Failed"
      exit 1
#endif

