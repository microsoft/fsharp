// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.FSharp.Collections

    open System
    open System.Diagnostics
    open System.Collections.Generic
    open Microsoft.FSharp.Core
    open Microsoft.FSharp.Collections
    open Microsoft.FSharp.Core.Operators
    open Microsoft.FSharp.Core.CompilerServices
    open Microsoft.FSharp.Core.LanguagePrimitives.IntrinsicOperators

    /// Basic operations on arrays
    [<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
    [<RequireQualifiedAccess>]
    module Array = 

        let inline checkNonNull argName arg =
            if isNull arg then
                nullArg argName

        let inline indexNotFound() = raise (KeyNotFoundException(SR.GetString(SR.keyNotFoundAlt)))

        [<CompiledName("Length")>]
        let length (array: _[])    = 
            checkNonNull "array" array
            array.Length
        
        [<CompiledName("Last")>]
        let inline last (array: 'T[]) =
            checkNonNull "array" array
            if array.Length = 0 then invalidArg "array" LanguagePrimitives.ErrorStrings.InputArrayEmptyString
            array.[array.Length-1]

        [<CompiledName("TryLast")>]
        let tryLast (array: 'T[]) =
            checkNonNull "array" array
            if array.Length = 0 then None 
            else Some array.[array.Length-1]

        [<CompiledName("Initialize")>]
        let inline init count initializer = Microsoft.FSharp.Primitives.Basics.Array.init count initializer

        [<CompiledName("ZeroCreate")>]
        let zeroCreate count = 
            if count < 0 then invalidArgInputMustBeNonNegative "count" count
            Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked count

        [<CompiledName("Create")>]
        let create (count: int) (value: 'T) =
            if count < 0 then invalidArgInputMustBeNonNegative "count" count
            let array: 'T[] = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked count
            for i = 0 to Operators.Checked.(-) array.Length 1 do // use checked arithmetic here to satisfy FxCop
                array.[i] <- value
            array

        [<CompiledName("TryHead")>]
        let tryHead (array: 'T[]) =
            checkNonNull "array" array
            if array.Length = 0 then None
            else Some array.[0]

        [<CompiledName("IsEmpty")>]
        let isEmpty (array: 'T[]) = 
            checkNonNull "array" array
            array.Length = 0

        [<CompiledName("Tail")>]
        let tail (array: 'T[]) =
            checkNonNull "array" array
            if array.Length = 0 then invalidArg "array" (SR.GetString(SR.notEnoughElements))            
            Microsoft.FSharp.Primitives.Basics.Array.subUnchecked 1 (array.Length - 1) array

        [<CompiledName("Empty")>]
        let empty<'T> : 'T [] = [| |]

        [<CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704: IdentifiersShouldBeSpelledCorrectly")>]
        [<CompiledName("CopyTo")>]
        let inline blit (source: 'T[]) (sourceIndex: int) (target: 'T[]) (targetIndex: int) (count: int) = 
            Array.Copy(source, sourceIndex, target, targetIndex, count)
                       
        let concatArrays (arrs: 'T[][]) : 'T[] =
            let mutable acc = 0    
            for h in arrs do
                acc <- acc + h.Length
                
            let res = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked acc  
                
            let mutable j = 0
            for i = 0 to arrs.Length-1 do
                let h = arrs.[i]
                let len = h.Length
                Array.Copy(h, 0, res, j, len)
                j <- j + len
            res               

        [<CompiledName("Concat")>]
        let concat (arrays: seq<'T[]>) = 
            checkNonNull "arrays" arrays
            match arrays with
            | :? ('T[][]) as ts -> ts |> concatArrays // avoid a clone, since we only read the array
            | _ -> arrays |> Seq.toArray |> concatArrays
            
        [<CompiledName("Replicate")>]
        let replicate count initial = 
            if count < 0 then invalidArgInputMustBeNonNegative "count" count
            let arr: 'T array = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked count
            for i = 0 to arr.Length-1 do 
                arr.[i] <- initial
            arr

        [<CompiledName("Collect")>]
        let collect (mapping: 'T -> 'U[])  (array: 'T[]) : 'U[]=
            checkNonNull "array" array
            let len = array.Length
            let result = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked<'U[]> len
            for i = 0 to result.Length-1 do
                result.[i] <- mapping array.[i]
            concatArrays result
        
        [<CompiledName("SplitAt")>]
        let splitAt index (array: 'T[]) =
            checkNonNull "array" array
            if index < 0 then invalidArgInputMustBeNonNegative "index" index
            if array.Length < index then raise <| InvalidOperationException (SR.GetString(SR.notEnoughElements))
            if index = 0 then
                let right = Microsoft.FSharp.Primitives.Basics.Array.subUnchecked 0 array.Length array
                [||], right
            elif index = array.Length then
                let left = Microsoft.FSharp.Primitives.Basics.Array.subUnchecked 0 array.Length array
                left, [||] 
            else
                let res1 = Microsoft.FSharp.Primitives.Basics.Array.subUnchecked 0 index array
                let res2 = Microsoft.FSharp.Primitives.Basics.Array.subUnchecked index (array.Length-index) array

                res1, res2

        [<CompiledName("Take")>]
        let take count (array: 'T[]) =
            checkNonNull "array" array
            if count < 0 then invalidArgInputMustBeNonNegative "count" count
            if count = 0 then 
                empty
            else
                if count > array.Length then
                    raise <| InvalidOperationException (SR.GetString(SR.notEnoughElements))

                Microsoft.FSharp.Primitives.Basics.Array.subUnchecked 0 count array

        [<CompiledName("TakeWhile")>]
        let takeWhile predicate (array: 'T[]) = 
            checkNonNull "array" array
            if array.Length = 0 then 
                empty 
            else
                let mutable count = 0
                while count < array.Length && predicate array.[count] do
                    count <- count + 1

                Microsoft.FSharp.Primitives.Basics.Array.subUnchecked 0 count array

        let inline countByImpl (comparer: IEqualityComparer<'SafeKey>) (projection: 'T->'SafeKey) (getKey: 'SafeKey->'Key) (array: 'T[]) =
            let length = array.Length
            if length = 0 then Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked 0 else

            let dict = Dictionary comparer

            // Build the groupings
            for v in array do
                let safeKey = projection v
                let mutable prev = Unchecked.defaultof<_>
                if dict.TryGetValue(safeKey, &prev) then dict.[safeKey] <- prev + 1 else dict.[safeKey] <- 1

            let res = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked dict.Count
            let mutable i = 0
            for group in dict do
                res.[i] <- getKey group.Key, group.Value
                i <- i + 1
            res

        // We avoid wrapping a StructBox, because under 64 JIT we get some "hard" tailcalls which affect performance
        let countByValueType (projection: 'T -> 'Key) (array: 'T[]) =
            countByImpl HashIdentity.Structural<'Key> projection id array

        // Wrap a StructBox around all keys in case the key type is itself a type using null as a representation
        let countByRefType (projection: 'T -> 'Key) (array: 'T[]) =
            countByImpl RuntimeHelpers.StructBox<'Key>.Comparer (fun t -> RuntimeHelpers.StructBox (projection t)) (fun sb -> sb.Value) array

        [<CompiledName("CountBy")>]
        let countBy (projection: 'T->'Key) (array: 'T[]) =
            checkNonNull "array" array
            if typeof<'Key>.IsValueType
                then countByValueType projection array
                else countByRefType   projection array

        [<CompiledName("Append")>]
        let append (array1: 'T[]) (array2: 'T[]) = 
            checkNonNull "array1" array1
            checkNonNull "array2" array2
            let n1 = array1.Length 
            let n2 = array2.Length 
            let res: 'T[] = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked (n1 + n2)
            Array.Copy(array1, 0, res, 0, n1)
            Array.Copy(array2, 0, res, n1, n2)
            res   

        [<CompiledName("Head")>]
        let head (array: 'T[]) =
            checkNonNull "array" array
            if array.Length = 0 then invalidArg "array" LanguagePrimitives.ErrorStrings.InputArrayEmptyString else array.[0]

        [<CompiledName("Copy")>]
        let copy (array: 'T[]) =
            checkNonNull "array" array
            (array.Clone() :?> 'T[]) // this is marginally faster
            //let len = array.Length 
            //let res = zeroCreate len 
            //for i = 0 to len - 1 do 
            //    res.[i] <- array.[i]
            //res

        [<CompiledName("ToList")>]
        let toList array = 
            checkNonNull "array" array
            List.ofArray array

        [<CompiledName("OfList")>]
        let ofList list =
            List.toArray list

        [<CompiledName("Indexed")>]
        let indexed (array: 'T[]) =
            checkNonNull "array" array            
            let res = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked array.Length
            for i = 0 to res.Length-1 do
                res.[i] <- (i, array.[i])
            res

        [<CompiledName("Iterate")>]
        let inline iter ([<InlineIfLambda>] action) (array: 'T[]) = 
            checkNonNull "array" array            
            for i = 0 to array.Length-1 do 
                action array.[i]

        [<CompiledName("Distinct")>]
        let distinct (array: 'T[]) =
            checkNonNull "array" array
            let temp = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked array.Length
            let mutable i = 0

            let hashSet = HashSet<'T>(HashIdentity.Structural<'T>)
            for v in array do 
                if hashSet.Add(v) then
                    temp.[i] <- v
                    i <- i + 1

            Microsoft.FSharp.Primitives.Basics.Array.subUnchecked 0 i temp

        [<CompiledName("Map")>]
        let inline map ([<InlineIfLambda>] mapping: 'T -> 'U) (array: 'T[]) =
            checkNonNull "array" array            
            let res: 'U[] = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked array.Length
            for i = 0 to res.Length-1 do 
                res.[i] <- mapping array.[i]
            res

        [<CompiledName("Iterate2")>]
        let iter2 action (array1: 'T[]) (array2: 'U[]) = 
            checkNonNull "array1" array1
            checkNonNull "array2" array2
            let f = OptimizedClosures.FSharpFunc<_, _, _>.Adapt(action)
            if array1.Length <> array2.Length then invalidArgDifferentArrayLength "array1" array1.Length "array2" array2.Length
            for i = 0 to array1.Length-1 do 
                f.Invoke(array1.[i], array2.[i])

        [<CompiledName("DistinctBy")>]
        let distinctBy projection (array: 'T[]) =
            checkNonNull "array" array
            let length = array.Length
            if length = 0 then Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked 0 else

            let temp = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked array.Length
            let mutable i = 0 
            let hashSet = HashSet<_>(HashIdentity.Structural<_>)
            for v in array do
                if hashSet.Add(projection v) then
                    temp.[i] <- v
                    i <- i + 1

            Microsoft.FSharp.Primitives.Basics.Array.subUnchecked 0 i temp

        [<CompiledName("Map2")>]
        let map2 mapping (array1: 'T[]) (array2: 'U[]) = 
            checkNonNull "array1" array1
            checkNonNull "array2" array2
            let f = OptimizedClosures.FSharpFunc<_, _, _>.Adapt(mapping)
            if array1.Length <> array2.Length then invalidArgDifferentArrayLength "array1" array1.Length "array2" array2.Length
            let res = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked array1.Length
            for i = 0 to res.Length-1 do 
                res.[i] <- f.Invoke(array1.[i], array2.[i])
            res

        [<CompiledName("Map3")>]
        let map3 mapping (array1: 'T1[]) (array2: 'T2[]) (array3: 'T3[]) = 
            checkNonNull "array1" array1
            checkNonNull "array2" array2
            checkNonNull "array3" array3
            let f = OptimizedClosures.FSharpFunc<_, _, _, _>.Adapt(mapping)
            let len1 = array1.Length
            if len1 <> array2.Length || len1 <> array3.Length then invalidArg3ArraysDifferent "array1" "array2" "array3" len1 array2.Length array3.Length
            
            let res = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked len1
            for i = 0 to res.Length-1 do
                res.[i] <- f.Invoke(array1.[i], array2.[i], array3.[i])
            res

        [<CompiledName("MapIndexed2")>]
        let mapi2 mapping (array1: 'T[]) (array2: 'U[]) = 
            checkNonNull "array1" array1
            checkNonNull "array2" array2
            let f = OptimizedClosures.FSharpFunc<_, _, _, _>.Adapt(mapping)
            if array1.Length <> array2.Length then invalidArgDifferentArrayLength "array1" array1.Length "array2" array2.Length
            let res = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked array1.Length 
            for i = 0 to res.Length-1 do 
                res.[i] <- f.Invoke(i, array1.[i], array2.[i])
            res

        [<CompiledName("IterateIndexed")>]
        let iteri action (array: 'T[]) =
            checkNonNull "array" array
            let f = OptimizedClosures.FSharpFunc<_, _, _>.Adapt(action)            
            for i = 0 to array.Length-1 do 
                f.Invoke(i, array.[i])

        [<CompiledName("IterateIndexed2")>]
        let iteri2 action (array1: 'T[]) (array2: 'U[]) = 
            checkNonNull "array1" array1
            checkNonNull "array2" array2
            let f = OptimizedClosures.FSharpFunc<_, _, _, _>.Adapt(action)
            if array1.Length <> array2.Length then invalidArgDifferentArrayLength "array1" array1.Length "array2" array2.Length
            for i = 0 to array1.Length-1 do 
                f.Invoke(i, array1.[i], array2.[i])

        [<CompiledName("MapIndexed")>]
        let mapi (mapping: int -> 'T -> 'U) (array: 'T[]) =
            checkNonNull "array" array
            let f = OptimizedClosures.FSharpFunc<_, _, _>.Adapt(mapping)            
            let res = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked array.Length
            for i = 0 to array.Length-1 do 
                res.[i] <- f.Invoke(i, array.[i])
            res

        [<CompiledName("MapFold")>]
        let mapFold<'T, 'State, 'Result> (mapping: 'State -> 'T -> 'Result * 'State) state array =
            checkNonNull "array" array
            Microsoft.FSharp.Primitives.Basics.Array.mapFold mapping state array

        [<CompiledName("MapFoldBack")>]
        let mapFoldBack<'T, 'State, 'Result> (mapping: 'T -> 'State -> 'Result * 'State) array state =
            checkNonNull "array" array
            Microsoft.FSharp.Primitives.Basics.Array.mapFoldBack mapping array state

        [<CompiledName("Exists")>]
        let exists (predicate: 'T -> bool) (array: 'T[]) =
            checkNonNull "array" array
            let len = array.Length
            let rec loop i = i < len && (predicate array.[i] || loop (i+1))
            len > 0 && loop 0

        [<CompiledName("Contains")>]
        let inline contains value (array: 'T[]) =
            checkNonNull "array" array
            let mutable state = false
            let mutable i = 0
            while not state && i < array.Length do
                state <- value = array.[i]
                i <- i + 1
            state

        [<CompiledName("Exists2")>]
        let exists2 predicate (array1: _[]) (array2: _[]) = 
            checkNonNull "array1" array1
            checkNonNull "array2" array2
            let f = OptimizedClosures.FSharpFunc<_, _, _>.Adapt(predicate)
            let len1 = array1.Length
            if len1 <> array2.Length then invalidArgDifferentArrayLength "array1" array1.Length "array2" array2.Length
            let rec loop i = i < len1 && (f.Invoke(array1.[i], array2.[i]) || loop (i+1))
            loop 0

        [<CompiledName("ForAll")>]
        let forall (predicate: 'T -> bool) (array: 'T[]) =
            checkNonNull "array" array
            let len = array.Length
            let rec loop i = i >= len || (predicate array.[i] && loop (i+1))
            loop 0

        [<CompiledName("ForAll2")>]
        let forall2 predicate (array1: _[]) (array2: _[]) = 
            checkNonNull "array1" array1
            checkNonNull "array2" array2
            let f = OptimizedClosures.FSharpFunc<_, _, _>.Adapt(predicate)
            let len1 = array1.Length
            if len1 <> array2.Length then invalidArgDifferentArrayLength "array1" array1.Length "array2" array2.Length
            let rec loop i = i >= len1 || (f.Invoke(array1.[i], array2.[i]) && loop (i+1))
            loop 0
        
        [<CompiledName("ForAll3")>]
        let internal forall3 predicate (array1: _[]) (array2: _[]) (array3: _[]) = 
            if isNull array1 then nullArg (nameof array1)
            if isNull array2 then nullArg (nameof array2)
            if isNull array3 then nullArg (nameof array3)
            let f = OptimizedClosures.FSharpFunc<_, _, _, _>.Adapt(predicate)
            let len1 = array1.Length
            if len1 <> array2.Length || len1 <> array3.Length then invalidArg3ArraysDifferent "array1" "array2" "array3" array1.Length array2.Length array3.Length
            let rec loop i = i >= len1 || (f.Invoke(array1.[i], array2.[i], array3.[i]) && loop (i+1))
            loop 0

        let inline groupByImpl (comparer: IEqualityComparer<'SafeKey>) (keyf: 'T->'SafeKey) (getKey: 'SafeKey->'Key) (array: 'T[]) =
            let length = array.Length
            if length = 0 then Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked 0 else
            let dict = Dictionary<_, ResizeArray<_>> comparer

            // Build the groupings
            for i = 0 to length - 1 do
                let v = array.[i]
                let safeKey = keyf v
                let mutable prev = Unchecked.defaultof<_>
                if dict.TryGetValue(safeKey, &prev) then
                    prev.Add v
                else 
                    let prev = ResizeArray ()
                    dict.[safeKey] <- prev
                    prev.Add v
                     
            // Return the array-of-arrays.
            let result = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked dict.Count
            let mutable i = 0
            for group in dict do
                result.[i] <- getKey group.Key, group.Value.ToArray()
                i <- i + 1

            result

        // We avoid wrapping a StructBox, because under 64 JIT we get some "hard" tailcalls which affect performance
        let groupByValueType (keyf: 'T->'Key) (array: 'T[]) = groupByImpl HashIdentity.Structural<'Key> keyf id array

        // Wrap a StructBox around all keys in case the key type is itself a type using null as a representation
        let groupByRefType   (keyf: 'T->'Key) (array: 'T[]) = groupByImpl RuntimeHelpers.StructBox<'Key>.Comparer (fun t -> RuntimeHelpers.StructBox (keyf t)) (fun sb -> sb.Value) array

        [<CompiledName("GroupBy")>]
        let groupBy (projection: 'T->'Key) (array: 'T[]) =
            checkNonNull "array" array
            if typeof<'Key>.IsValueType
                then groupByValueType projection array
                else groupByRefType   projection array

        [<CompiledName("Pick")>]
        let pick chooser (array: _[]) = 
            checkNonNull "array" array
            let rec loop i = 
                if i >= array.Length then 
                    indexNotFound()
                else 
                    match chooser array.[i] with 
                    | None -> loop(i+1)
                    | Some res -> res
            loop 0 

        [<CompiledName("TryPick")>]
        let tryPick chooser (array: _[]) = 
            checkNonNull "array" array
            let rec loop i = 
                if i >= array.Length then None else 
                match chooser array.[i] with 
                | None -> loop(i+1)
                | res -> res
            loop 0 
        
        [<CompiledName("Choose")>]
        let choose (chooser: 'T -> 'U Option) (array: 'T[]) =             
            checkNonNull "array" array                    
            
            let mutable i = 0
            let mutable first = Unchecked.defaultof<'U>
            let mutable found = false
            while i < array.Length && not found do
                let element = array.[i]
                match chooser element with 
                | None -> i <- i + 1
                | Some b -> first <- b; found <- true                            
                
            if i <> array.Length then

                let chunk1: 'U[] = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked ((array.Length >>> 2) + 1)
                chunk1.[0] <- first
                let mutable count = 1            
                i <- i + 1                                
                while count < chunk1.Length && i < array.Length do
                    let element = array.[i]                                
                    match chooser element with
                    | None -> ()
                    | Some b -> chunk1.[count] <- b
                                count <- count + 1                            
                    i <- i + 1
                
                if i < array.Length then                            
                    let chunk2: 'U[] = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked (array.Length-i)                        
                    count <- 0
                    while i < array.Length do
                        let element = array.[i]                                
                        match chooser element with
                        | None -> ()
                        | Some b -> chunk2.[count] <- b
                                    count <- count + 1                            
                        i <- i + 1

                    let res: 'U[] = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked (chunk1.Length + count)
                    Array.Copy(chunk1, res, chunk1.Length)
                    Array.Copy(chunk2, 0, res, chunk1.Length, count)
                    res
                else
                    Microsoft.FSharp.Primitives.Basics.Array.subUnchecked 0 count chunk1                
            else
                empty

        // The filter module is a space and performance for Array.filter based optimization that uses 
        // a bitarray to store the results of the filtering of every element of the array. This means 
        // that the only additional temporary garbage that needs to be allocated is {array.Length/8} bytes.
        //
        // Other optimizations include: 
        // - arrays < 32 elements don't allocate any garbage at all
        // - when the predicate yields consecutive runs of true data that is >= 32 elements (and fall
        //   into maskArray buckets) are copied in chunks using System.Array.Copy
        module Filter =
            let private populateMask<'a> (f: 'a->bool) (src: array<'a>) (maskArray: array<uint32>) =
                let mutable count = 0
                for maskIdx = 0 to maskArray.Length-1 do
                    let srcIdx = maskIdx * 32
                    let mutable mask = 0u
                    if f src.[srcIdx+0x00] then mask <- mask ||| (1u <<< 0x00); count <- count + 1
                    if f src.[srcIdx+0x01] then mask <- mask ||| (1u <<< 0x01); count <- count + 1
                    if f src.[srcIdx+0x02] then mask <- mask ||| (1u <<< 0x02); count <- count + 1
                    if f src.[srcIdx+0x03] then mask <- mask ||| (1u <<< 0x03); count <- count + 1
                    if f src.[srcIdx+0x04] then mask <- mask ||| (1u <<< 0x04); count <- count + 1
                    if f src.[srcIdx+0x05] then mask <- mask ||| (1u <<< 0x05); count <- count + 1
                    if f src.[srcIdx+0x06] then mask <- mask ||| (1u <<< 0x06); count <- count + 1
                    if f src.[srcIdx+0x07] then mask <- mask ||| (1u <<< 0x07); count <- count + 1
                    if f src.[srcIdx+0x08] then mask <- mask ||| (1u <<< 0x08); count <- count + 1
                    if f src.[srcIdx+0x09] then mask <- mask ||| (1u <<< 0x09); count <- count + 1
                    if f src.[srcIdx+0x0A] then mask <- mask ||| (1u <<< 0x0A); count <- count + 1
                    if f src.[srcIdx+0x0B] then mask <- mask ||| (1u <<< 0x0B); count <- count + 1
                    if f src.[srcIdx+0x0C] then mask <- mask ||| (1u <<< 0x0C); count <- count + 1
                    if f src.[srcIdx+0x0D] then mask <- mask ||| (1u <<< 0x0D); count <- count + 1
                    if f src.[srcIdx+0x0E] then mask <- mask ||| (1u <<< 0x0E); count <- count + 1
                    if f src.[srcIdx+0x0F] then mask <- mask ||| (1u <<< 0x0F); count <- count + 1
                    if f src.[srcIdx+0x10] then mask <- mask ||| (1u <<< 0x10); count <- count + 1
                    if f src.[srcIdx+0x11] then mask <- mask ||| (1u <<< 0x11); count <- count + 1
                    if f src.[srcIdx+0x12] then mask <- mask ||| (1u <<< 0x12); count <- count + 1
                    if f src.[srcIdx+0x13] then mask <- mask ||| (1u <<< 0x13); count <- count + 1
                    if f src.[srcIdx+0x14] then mask <- mask ||| (1u <<< 0x14); count <- count + 1
                    if f src.[srcIdx+0x15] then mask <- mask ||| (1u <<< 0x15); count <- count + 1
                    if f src.[srcIdx+0x16] then mask <- mask ||| (1u <<< 0x16); count <- count + 1
                    if f src.[srcIdx+0x17] then mask <- mask ||| (1u <<< 0x17); count <- count + 1
                    if f src.[srcIdx+0x18] then mask <- mask ||| (1u <<< 0x18); count <- count + 1
                    if f src.[srcIdx+0x19] then mask <- mask ||| (1u <<< 0x19); count <- count + 1
                    if f src.[srcIdx+0x1A] then mask <- mask ||| (1u <<< 0x1A); count <- count + 1
                    if f src.[srcIdx+0x1B] then mask <- mask ||| (1u <<< 0x1B); count <- count + 1
                    if f src.[srcIdx+0x1C] then mask <- mask ||| (1u <<< 0x1C); count <- count + 1
                    if f src.[srcIdx+0x1D] then mask <- mask ||| (1u <<< 0x1D); count <- count + 1
                    if f src.[srcIdx+0x1E] then mask <- mask ||| (1u <<< 0x1E); count <- count + 1
                    if f src.[srcIdx+0x1F] then mask <- mask ||| (1u <<< 0x1F); count <- count + 1
                    maskArray.[maskIdx] <- mask
                count 

            let private createMask<'a> (f: 'a->bool) (src: array<'a>) (maskArrayOut: byref<array<uint32>>) (leftoverMaskOut: byref<uint32>) =
                let maskArrayLength = src.Length / 0x20

                // null when there are less than 32 items in src array.
                let maskArray =
                    if maskArrayLength = 0 then Unchecked.defaultof<_>
                    else Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked<uint32> maskArrayLength

                let mutable count =
                    match maskArray with
                    | null -> 0
                    | maskArray -> populateMask f src maskArray

                let leftoverMask =
                    match src.Length % 0x20 with
                    | 0 -> 0u
                    | _ -> 
                        let mutable mask = 0u
                        let mutable elementMask = 1u
                        for arrayIdx = maskArrayLength*0x20 to src.Length-1 do
                            if f src.[arrayIdx] then mask <- mask ||| elementMask; count <- count + 1
                            elementMask <- elementMask <<< 1
                        mask

                maskArrayOut    <- maskArray
                leftoverMaskOut <- leftoverMask
                count

            let private populateDstViaMask<'a> (src: array<'a>) (maskArray: array<uint32>) (dst: array<'a>)  =
                let mutable dstIdx = 0
                let mutable batchCount = 0
                for maskIdx = 0 to maskArray.Length-1 do
                    let mask = maskArray.[maskIdx]
                    if mask = 0xFFFFFFFFu then
                        batchCount <- batchCount + 1
                    else
                        let srcIdx = maskIdx * 0x20

                        if batchCount <> 0 then
                            let batchSize = batchCount * 0x20
                            System.Array.Copy (src, srcIdx-batchSize, dst, dstIdx, batchSize)
                            dstIdx <- dstIdx + batchSize
                            batchCount <- 0

                        if mask <> 0u then
                            if mask &&& (1u <<< 0x00) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x00]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x01) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x01]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x02) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x02]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x03) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x03]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x04) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x04]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x05) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x05]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x06) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x06]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x07) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x07]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x08) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x08]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x09) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x09]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x0A) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x0A]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x0B) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x0B]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x0C) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x0C]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x0D) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x0D]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x0E) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x0E]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x0F) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x0F]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x10) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x10]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x11) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x11]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x12) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x12]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x13) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x13]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x14) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x14]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x15) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x15]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x16) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x16]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x17) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x17]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x18) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x18]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x19) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x19]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x1A) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x1A]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x1B) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x1B]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x1C) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x1C]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x1D) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x1D]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x1E) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x1E]; dstIdx <- dstIdx + 1
                            if mask &&& (1u <<< 0x1F) <> 0u then dst.[dstIdx] <- src.[srcIdx+0x1F]; dstIdx <- dstIdx + 1

                if batchCount <> 0 then
                    let srcIdx = maskArray.Length * 0x20
                    let batchSize = batchCount * 0x20
                    System.Array.Copy (src, srcIdx-batchSize, dst, dstIdx, batchSize)
                    dstIdx <- dstIdx + batchSize

                dstIdx

            let private filterViaMask (maskArray: array<uint32>) (leftoverMask: uint32) (count: int) (src: array<_>) =
                let dst = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked count

                let mutable dstIdx = 0
                let srcIdx =
                    match maskArray with
                    | null -> 0
                    | _ ->
                        dstIdx <- populateDstViaMask src maskArray dst
                        maskArray.Length*0x20

                let mutable elementMask = 1u
                for srcIdx = srcIdx to src.Length-1 do
                    if leftoverMask &&& elementMask <> 0u then dst.[dstIdx] <- src.[srcIdx]; dstIdx <- dstIdx + 1
                    elementMask <- elementMask <<< 1

                dst

            let filter f (src: array<_>) =
                let mutable maskArray    = Unchecked.defaultof<_>
                let mutable leftOverMask = Unchecked.defaultof<_>
                match createMask f src &maskArray &leftOverMask with
                | 0     -> empty
                | count -> filterViaMask maskArray leftOverMask count src

        [<CompiledName("Filter")>]
        let filter predicate (array: _[]) =         
            checkNonNull "array" array
            Filter.filter predicate array
            
        [<CompiledName("Where")>]
        let where predicate (array: _[]) = filter predicate array

        [<CompiledName("Except")>]
        let except (itemsToExclude: seq<_>) (array: _[]) =
            checkNonNull "itemsToExclude" itemsToExclude
            checkNonNull "array" array

            if array.Length = 0 then
                array
            else
                let cached = HashSet(itemsToExclude, HashIdentity.Structural)
                array |> filter cached.Add

        [<CompiledName("Partition")>]
        let partition predicate (array: _[]) = 
            checkNonNull "array" array
            let res = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked array.Length        
            let mutable upCount = 0
            let mutable downCount = array.Length-1    
            for x in array do                
                if predicate x then 
                    res.[upCount] <- x
                    upCount <- upCount + 1
                else
                    res.[downCount] <- x
                    downCount <- downCount - 1
                
            let res1 = Microsoft.FSharp.Primitives.Basics.Array.subUnchecked 0 upCount res
            let res2 = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked (array.Length - upCount)    
        
            downCount <- array.Length-1
            for i = 0 to res2.Length-1 do
                res2.[i] <- res.[downCount]
                downCount <- downCount - 1
        
            res1, res2

        [<CompiledName("Find")>]
        let find predicate (array: _[]) = 
            checkNonNull "array" array
            let rec loop i = 
                if i >= array.Length then indexNotFound() else
                if predicate array.[i] then array.[i]  else loop (i+1)
            loop 0 

        [<CompiledName("TryFind")>]
        let tryFind predicate (array: _[]) = 
            checkNonNull "array" array
            let rec loop i = 
                if i >= array.Length then None else 
                if predicate array.[i] then Some array.[i]  else loop (i+1)
            loop 0 

        [<CompiledName("Skip")>]
        let skip count (array: 'T[]) =
            checkNonNull "array" array
            if count > array.Length then invalidArgOutOfRange "count" count "array.Length" array.Length
            if count = array.Length then
                empty
            else
                let count = max count 0
                Microsoft.FSharp.Primitives.Basics.Array.subUnchecked count (array.Length - count) array

        [<CompiledName("SkipWhile")>]
        let skipWhile predicate (array: 'T[]) =        
            checkNonNull "array" array
            let mutable i = 0            
            while i < array.Length && predicate array.[i] do i <- i + 1

            match array.Length - i with
            | 0 -> empty
            | resLen -> Microsoft.FSharp.Primitives.Basics.Array.subUnchecked i resLen array

        [<CompiledName("FindBack")>]
        let findBack predicate (array: _[]) =
            checkNonNull "array" array
            Microsoft.FSharp.Primitives.Basics.Array.findBack predicate array

        [<CompiledName("TryFindBack")>]
        let tryFindBack predicate (array: _[]) =
            checkNonNull "array" array
            Microsoft.FSharp.Primitives.Basics.Array.tryFindBack predicate array

        [<CompiledName("FindIndexBack")>]
        let findIndexBack predicate (array: _[]) =
            checkNonNull "array" array
            Microsoft.FSharp.Primitives.Basics.Array.findIndexBack predicate array

        [<CompiledName("TryFindIndexBack")>]
        let tryFindIndexBack predicate (array: _[]) =
            checkNonNull "array" array
            Microsoft.FSharp.Primitives.Basics.Array.tryFindIndexBack predicate array

        [<CompiledName("Windowed")>]
        let windowed windowSize (array: 'T[]) =
            checkNonNull "array" array
            if windowSize <= 0 then invalidArgInputMustBePositive "windowSize" windowSize
            let len = array.Length
            if windowSize > len then
                empty
            else
                let res: 'T[][] = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked (len - windowSize + 1) 
                for i = 0 to len - windowSize do
                    res.[i] <- Microsoft.FSharp.Primitives.Basics.Array.subUnchecked i windowSize array
                res

        [<CompiledName("ChunkBySize")>]
        let chunkBySize chunkSize (array: 'T[]) =
            checkNonNull "array" array
            if chunkSize <= 0 then invalidArgInputMustBePositive "chunkSize" chunkSize
            let len = array.Length
            if len = 0 then
                empty
            else if chunkSize > len then
                [| copy array |]
            else
                let chunkCount = (len - 1) / chunkSize + 1
                let res = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked chunkCount: 'T[][]
                for i = 0 to len / chunkSize - 1 do
                    res.[i] <- Microsoft.FSharp.Primitives.Basics.Array.subUnchecked (i * chunkSize) chunkSize array
                if len % chunkSize <> 0 then
                    res.[chunkCount - 1] <- Microsoft.FSharp.Primitives.Basics.Array.subUnchecked ((chunkCount - 1) * chunkSize) (len % chunkSize) array
                res

        [<CompiledName("SplitInto")>]
        let splitInto count (array: _[]) =
            checkNonNull "array" array
            if count <= 0 then invalidArgInputMustBePositive "count" count
            Microsoft.FSharp.Primitives.Basics.Array.splitInto count array

        [<CompiledName("Zip")>]
        let zip (array1: _[]) (array2: _[]) = 
            checkNonNull "array1" array1
            checkNonNull "array2" array2
            let len1 = array1.Length 
            if len1 <> array2.Length then invalidArgDifferentArrayLength "array1" array1.Length "array2" array2.Length
            let res = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked len1 
            for i = 0 to res.Length-1 do 
                res.[i] <- (array1.[i], array2.[i])
            res

        [<CompiledName("Zip3")>]
        let zip3 (array1: _[]) (array2: _[]) (array3: _[]) = 
            checkNonNull "array1" array1
            checkNonNull "array2" array2
            checkNonNull "array3" array3
            let len1 = array1.Length
            if len1 <> array2.Length || len1 <> array3.Length then invalidArg3ArraysDifferent "array1" "array2" "array3" len1 array2.Length array3.Length
            let res = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked len1 
            for i = 0 to res.Length-1 do 
                res.[i] <- (array1.[i], array2.[i], array3.[i])
            res

        [<CompiledName("AllPairs")>]
        let allPairs (array1: _[]) (array2: _[]) =
            checkNonNull "array1" array1
            checkNonNull "array2" array2
            let len1 = array1.Length
            let len2 = array2.Length
            let res = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked (len1 * len2)
            for i = 0 to array1.Length-1 do
                for j = 0 to array2.Length-1 do
                    res.[i * len2 + j] <- (array1.[i], array2.[j])
            res

        [<CompiledName("Unfold")>]
        let unfold<'T, 'State> (generator: 'State -> ('T*'State) option) (state: 'State) =
            let res = ResizeArray<_>()
            let rec loop state =
                match generator state with
                | None -> ()
                | Some (x, s') ->
                    res.Add(x)
                    loop s'
            loop state
            res.ToArray()

        [<CompiledName("Unzip")>]
        let unzip (array: _[]) = 
            checkNonNull "array" array
            let len = array.Length 
            let res1 = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked len 
            let res2 = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked len 
            for i = 0 to array.Length-1 do 
                let x, y = array.[i] 
                res1.[i] <- x
                res2.[i] <- y
            res1, res2

        [<CompiledName("Unzip3")>]
        let unzip3 (array: _[]) = 
            checkNonNull "array" array
            let len = array.Length 
            let res1 = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked len 
            let res2 = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked len 
            let res3 = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked len 
            for i = 0 to array.Length-1 do 
                let x, y, z = array.[i] 
                res1.[i] <- x
                res2.[i] <- y
                res3.[i] <- z
            res1, res2, res3

        [<CompiledName("Reverse")>]
        let rev (array: _[]) = 
            checkNonNull "array" array
            let res = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked array.Length
            let mutable j = array.Length-1
            for i = 0 to array.Length-1 do 
                res.[j] <- array.[i]
                j <- j - 1
            res

        [<CompiledName("Fold")>]
        let fold<'T, 'State> (folder: 'State -> 'T -> 'State) (state: 'State) (array: 'T[]) =
            checkNonNull "array" array
            let f = OptimizedClosures.FSharpFunc<_, _, _>.Adapt(folder)
            let mutable state = state
            for i = 0 to array.Length-1 do 
                state <- f.Invoke(state, array.[i])
            state

        [<CompiledName("FoldBack")>]
        let foldBack<'T, 'State> (folder: 'T -> 'State -> 'State) (array: 'T[]) (state: 'State) =
            checkNonNull "array" array
            let f = OptimizedClosures.FSharpFunc<_, _, _>.Adapt(folder)
            let mutable res = state
            for i = array.Length-1 downto 0 do 
                res <- f.Invoke(array.[i], res)
            res

        [<CompiledName("FoldBack2")>]
        let foldBack2<'T1, 'T2, 'State>  folder (array1: 'T1[]) (array2: 'T2 []) (state: 'State) =
            checkNonNull "array1" array1
            checkNonNull "array2" array2
            let f = OptimizedClosures.FSharpFunc<_, _, _, _>.Adapt(folder)
            let mutable res = state 
            let len = array1.Length
            if len <> array2.Length then invalidArgDifferentArrayLength "array1" len "array2" array2.Length
            for i = len-1 downto 0 do 
                res <- f.Invoke(array1.[i], array2.[i], res)
            res

        [<CompiledName("Fold2")>]
        let fold2<'T1, 'T2, 'State>  folder (state: 'State) (array1: 'T1[]) (array2: 'T2 []) =
            checkNonNull "array1" array1
            checkNonNull "array2" array2
            let f = OptimizedClosures.FSharpFunc<_, _, _, _>.Adapt(folder)
            let mutable state = state 
            if array1.Length <> array2.Length then invalidArgDifferentArrayLength "array1" array1.Length "array2" array2.Length
            for i = 0 to array1.Length-1 do 
                state <- f.Invoke(state, array1.[i], array2.[i])
            state

        let foldSubRight f (array: _[]) start fin acc = 
            checkNonNull "array" array
            let f = OptimizedClosures.FSharpFunc<_, _, _>.Adapt(f)
            let mutable res = acc 
            for i = fin downto start do
                res <- f.Invoke(array.[i], res)
            res

        let scanSubLeft f initState (array: _[]) start fin = 
            checkNonNull "array" array
            let f = OptimizedClosures.FSharpFunc<_, _, _>.Adapt(f)
            let mutable state = initState 
            let res = create (2+fin-start) initState 
            for i = start to fin do
                state <- f.Invoke(state, array.[i])
                res.[i - start+1] <- state
            res

        [<CompiledName("Scan")>]
        let scan<'T, 'State> folder (state: 'State) (array: 'T[]) = 
            checkNonNull "array" array
            let len = array.Length
            scanSubLeft folder state array 0 (len - 1)

        [<CompiledName("ScanBack")>]
        let scanBack<'T, 'State> folder (array: 'T[]) (state: 'State) = 
            checkNonNull "array" array
            Microsoft.FSharp.Primitives.Basics.Array.scanSubRight folder array 0 (array.Length - 1) state

        [<CompiledName("Singleton")>]
        let inline singleton value = [|value|]

        [<CompiledName("Pairwise")>]
        let pairwise (array: 'T[]) =
            checkNonNull "array" array
            if array.Length < 2 then empty else
            init (array.Length-1) (fun i -> array.[i], array.[i+1])

        [<CompiledName("Reduce")>]
        let reduce reduction (array: _[]) = 
            checkNonNull "array" array
            let len = array.Length
            if len = 0 then 
                invalidArg "array" LanguagePrimitives.ErrorStrings.InputArrayEmptyString
            else 
                let f = OptimizedClosures.FSharpFunc<_, _, _>.Adapt(reduction)
                let mutable res = array.[0]
                for i = 1 to array.Length-1 do
                    res <- f.Invoke(res, array.[i])
                res

        [<CompiledName("ReduceBack")>]
        let reduceBack reduction (array: _[]) = 
            checkNonNull "array" array
            let len = array.Length
            if len = 0 then invalidArg "array" LanguagePrimitives.ErrorStrings.InputArrayEmptyString
            else foldSubRight reduction array 0 (len - 2) array.[len - 1]

        [<CompiledName("SortInPlaceWith")>]
        let sortInPlaceWith comparer (array: 'T[]) =
            checkNonNull "array" array
            let len = array.Length 
            if len < 2 then () 
            elif len = 2 then 
                let c = comparer array.[0] array.[1] 
                if c > 0 then
                    let tmp = array.[0] 
                    array.[0] <- array.[1]
                    array.[1] <- tmp
            else 
                Array.Sort(array, ComparisonIdentity.FromFunction(comparer))

        [<CompiledName("SortInPlaceBy")>]
        let sortInPlaceBy (projection: 'T -> 'U) (array: 'T[]) = 
            checkNonNull "array" array
            Microsoft.FSharp.Primitives.Basics.Array.unstableSortInPlaceBy projection array

        [<CompiledName("SortInPlace")>]
        let sortInPlace (array: 'T[]) = 
            checkNonNull "array" array
            Microsoft.FSharp.Primitives.Basics.Array.unstableSortInPlace array

        [<CompiledName("SortWith")>]
        let sortWith (comparer: 'T -> 'T -> int) (array: 'T[]) =
            checkNonNull "array" array
            let result = copy array
            sortInPlaceWith comparer result
            result

        [<CompiledName("SortBy")>]
        let sortBy projection array =
            checkNonNull "array" array
            let result = copy array
            sortInPlaceBy projection result
            result

        [<CompiledName("Sort")>]
        let sort array = 
            checkNonNull "array" array
            let result = copy array
            sortInPlace result
            result

        [<CompiledName("SortByDescending")>]
        let inline sortByDescending projection array =
            checkNonNull "array" array
            let inline compareDescending a b = compare (projection b) (projection a)
            sortWith compareDescending array

        [<CompiledName("SortDescending")>]
        let inline sortDescending array =
            checkNonNull "array" array
            let inline compareDescending a b = compare b a
            sortWith compareDescending array

        [<CompiledName("ToSeq")>]
        let toSeq array = 
            checkNonNull "array" array
            Seq.ofArray array

        [<CompiledName("OfSeq")>]
        let ofSeq source = 
            checkNonNull "source" source
            Seq.toArray source

        [<CompiledName("FindIndex")>]
        let findIndex predicate (array: _[]) = 
            checkNonNull "array" array
            let len = array.Length 
            let rec go n = 
                if n >= len then 
                    indexNotFound()
                elif predicate array.[n] then
                    n 
                else go (n+1)
            go 0

        [<CompiledName("TryFindIndex")>]
        let tryFindIndex predicate (array: _[]) = 
            checkNonNull "array" array
            let len = array.Length 
            let rec go n = if n >= len then None elif predicate array.[n] then Some n else go (n+1)
            go 0 

        [<CompiledName("Permute")>]
        let permute indexMap (array: _[]) =  
            checkNonNull "array" array
            Microsoft.FSharp.Primitives.Basics.Array.permute indexMap array

        [<CompiledName("Sum")>]
        let inline sum (array: ^T[] ) : ^T = 
            checkNonNull "array" array
            let mutable acc = LanguagePrimitives.GenericZero< ^T>
            for i = 0 to array.Length - 1 do
                acc <- Checked.(+) acc array.[i]
            acc

        [<CompiledName("SumBy")>]
        let inline sumBy (projection: 'T -> ^U) (array: 'T[]) : ^U = 
            checkNonNull "array" array
            let mutable acc = LanguagePrimitives.GenericZero< ^U>
            for i = 0 to array.Length - 1 do
                acc <- Checked.(+) acc (projection array.[i])
            acc

        [<CompiledName("Min")>]
        let inline min (array: _[]) = 
            checkNonNull "array" array
            if array.Length = 0 then invalidArg "array" LanguagePrimitives.ErrorStrings.InputArrayEmptyString
            let mutable acc = array.[0]
            for i = 1 to array.Length - 1 do
                let curr = array.[i]
                if curr < acc then 
                    acc <- curr
            acc

        [<CompiledName("MinBy")>]
        let inline minBy ([<InlineIfLambda>] projection) (array: _[]) = 
            checkNonNull "array" array
            if array.Length = 0 then invalidArg "array" LanguagePrimitives.ErrorStrings.InputArrayEmptyString
            let mutable accv = array.[0]
            let mutable acc = projection accv
            for i = 1 to array.Length - 1 do
                let currv = array.[i]
                let curr = projection currv
                if curr < acc then
                    acc <- curr
                    accv <- currv
            accv

        [<CompiledName("Max")>]
        let inline max (array: _[]) = 
            checkNonNull "array" array
            if array.Length = 0 then invalidArg "array" LanguagePrimitives.ErrorStrings.InputArrayEmptyString
            let mutable acc = array.[0]
            for i = 1 to array.Length - 1 do
                let curr = array.[i]
                if curr > acc then 
                    acc <- curr
            acc

        [<CompiledName("MaxBy")>]
        let inline maxBy projection (array: _[]) = 
            checkNonNull "array" array
            if array.Length = 0 then invalidArg "array" LanguagePrimitives.ErrorStrings.InputArrayEmptyString
            let mutable accv = array.[0]
            let mutable acc = projection accv
            for i = 1 to array.Length - 1 do
                let currv = array.[i]
                let curr = projection currv
                if curr > acc then
                    acc <- curr
                    accv <- currv
            accv

        [<CompiledName("Average")>]
        let inline average (array: 'T[]) = 
            checkNonNull "array" array
            if array.Length = 0 then invalidArg "array" LanguagePrimitives.ErrorStrings.InputArrayEmptyString
            let mutable acc = LanguagePrimitives.GenericZero< ^T>
            for i = 0 to array.Length - 1 do
                acc <- Checked.(+) acc array.[i]
            LanguagePrimitives.DivideByInt< ^T> acc array.Length

        [<CompiledName("AverageBy")>]
        let inline averageBy (projection: 'T -> ^U) (array: 'T[]) : ^U = 
            checkNonNull "array" array
            if array.Length = 0 then invalidArg "array" LanguagePrimitives.ErrorStrings.InputArrayEmptyString
            let mutable acc = LanguagePrimitives.GenericZero< ^U>
            for i = 0 to array.Length - 1 do
                acc <- Checked.(+) acc (projection array.[i])
            LanguagePrimitives.DivideByInt< ^U> acc array.Length

        [<CompiledName("CompareWith")>]
        let inline compareWith ([<InlineIfLambda>] comparer: 'T -> 'T -> int) (array1: 'T[]) (array2: 'T[]) = 
            checkNonNull "array1" array1
            checkNonNull "array2" array2

            let length1 = array1.Length
            let length2 = array2.Length
            
            let mutable i = 0
            let mutable result = 0
            
            if length1 < length2 then
                while i < array1.Length && result = 0 do
                    result <- comparer array1.[i] array2.[i]
                    i <- i + 1
            else
                while i < array2.Length && result = 0 do
                    result <- comparer array1.[i] array2.[i]
                    i <- i + 1

            if result <> 0 then result
            elif length1 = length2 then 0
            elif length1 < length2 then -1
            else 1

        [<CompiledName("GetSubArray")>]
        let sub (array: 'T[]) (startIndex: int) (count: int) =
            checkNonNull "array" array
            if startIndex < 0 then invalidArgInputMustBeNonNegative "startIndex" startIndex
            if count < 0 then invalidArgInputMustBeNonNegative "count" count
            if startIndex + count > array.Length then invalidArgOutOfRange "count" count "array.Length" array.Length
            Microsoft.FSharp.Primitives.Basics.Array.subUnchecked startIndex count array

        [<CompiledName("Item")>]
        let item index (array: _[]) =
            array.[index]

        [<CompiledName("TryItem")>]
        let tryItem index (array: 'T[]) =
            checkNonNull "array" array
            if index < 0 || index >= array.Length then None
            else Some(array.[index])

        [<CompiledName("Get")>]
        let get (array: _[]) index = 
            array.[index]

        [<CompiledName("Set")>]
        let set (array: _[]) index value = 
            array.[index] <- value

        [<CompiledName("Fill")>]
        let fill (target: 'T[]) (targetIndex: int) (count: int) (value: 'T) =
            checkNonNull "target" target
            if targetIndex < 0 then invalidArgInputMustBeNonNegative "targetIndex" targetIndex
            if count < 0 then invalidArgInputMustBeNonNegative "count" count
            for i = targetIndex to targetIndex + count - 1 do 
                target.[i] <- value

        [<CompiledName("ExactlyOne")>]
        let exactlyOne (array: 'T[]) =
            checkNonNull "array" array
            if array.Length = 1 then array.[0]
            elif array.Length = 0 then invalidArg "array" LanguagePrimitives.ErrorStrings.InputSequenceEmptyString
            else invalidArg "array" (SR.GetString(SR.inputSequenceTooLong))

        [<CompiledName("TryExactlyOne")>]
        let tryExactlyOne (array: 'T[]) =
            checkNonNull "array" array
            if array.Length = 1 then Some array.[0]
            else None

        let transposeArrays (array: 'T[][]) =
            let len = array.Length
            if len = 0 then Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked 0 else
            let lenInner = array.[0].Length

            for j in 1..len-1 do
                if lenInner <> array.[j].Length then
                    invalidArgDifferentArrayLength "array.[0]" lenInner (String.Format("array.[{0}]", j)) array.[j].Length

            let result: 'T[][] = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked lenInner
            for i in 0..lenInner-1 do
                result.[i] <- Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked len
                for j in 0..len-1 do
                    result.[i].[j] <- array.[j].[i]
            result

        [<CompiledName("Transpose")>]
        let transpose (arrays: seq<'T[]>) =
            checkNonNull "arrays" arrays
            match arrays with
            | :? ('T[][]) as ts -> ts |> transposeArrays // avoid a clone, since we only read the array
            | _ -> arrays |> Seq.toArray |> transposeArrays

        [<CompiledName("Truncate")>]
        let truncate count (array: 'T[]) =
            checkNonNull "array" array
            if count <= 0 then empty
            else
                let len = array.Length
                let count' = Operators.min count len
                Microsoft.FSharp.Primitives.Basics.Array.subUnchecked 0 count' array

        module Parallel =
            open System.Threading.Tasks
            
            [<CompiledName("Choose")>]
            let choose chooser (array: 'T[]) = 
                checkNonNull "array" array
                let inputLength = array.Length

                let isChosen: bool [] = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked inputLength
                let results: 'U [] = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked inputLength                
                let mutable outputLength = 0        
                Parallel.For(0, 
                             inputLength, 
                             (fun () ->0),
                             (fun i _ count -> 
                                match chooser array.[i] with 
                                | None -> count 
                                | Some v -> 
                                    isChosen.[i] <- true; 
                                    results.[i] <- v
                                    count+1),
                             Action<int> (fun x -> System.Threading.Interlocked.Add(&outputLength, x) |> ignore )
                             ) |> ignore

                let output = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked outputLength
                let mutable curr = 0
                for i = 0 to isChosen.Length-1 do 
                    if isChosen.[i] then 
                        output.[curr] <- results.[i]
                        curr <- curr + 1
                output
                
            [<CompiledName("Collect")>]
            let collect (mapping: 'T -> 'U[])  (array: 'T[]) : 'U[]=
                checkNonNull "array" array
                let inputLength = array.Length
                let result = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked inputLength
                Parallel.For(0, inputLength, 
                    (fun i -> result.[i] <- mapping array.[i])) |> ignore
                concatArrays result
                
            [<CompiledName("Map")>]
            let map (mapping: 'T -> 'U) (array: 'T[]) : 'U[]=
                checkNonNull "array" array
                let inputLength = array.Length
                let result = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked inputLength
                Parallel.For(0, inputLength, fun i ->
                    result.[i] <- mapping array.[i]) |> ignore
                result
                
            [<CompiledName("MapIndexed")>]
            let mapi mapping (array: 'T[]) =
                checkNonNull "array" array
                let f = OptimizedClosures.FSharpFunc<_, _, _>.Adapt(mapping)
                let inputLength = array.Length
                let result = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked inputLength 
                Parallel.For(0, inputLength, fun i ->
                    result.[i] <- f.Invoke (i, array.[i])) |> ignore
                result
                
            [<CompiledName("Iterate")>]
            let iter action (array: 'T[]) =
                checkNonNull "array" array
                Parallel.For (0, array.Length, fun i -> action array.[i]) |> ignore  
                
            [<CompiledName("IterateIndexed")>]
            let iteri action (array: 'T[]) =
                checkNonNull "array" array
                let f = OptimizedClosures.FSharpFunc<_, _, _>.Adapt(action)
                Parallel.For (0, array.Length, fun i -> f.Invoke(i, array.[i])) |> ignore        
                
            [<CompiledName("Initialize")>]
            let init count initializer =
                let result = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked count
                Parallel.For (0, count, fun i -> result.[i] <- initializer i) |> ignore
                result
                
            [<CompiledName("Partition")>]
            let partition predicate (array: 'T[]) =
                checkNonNull "array" array
                let inputLength = array.Length
               
                let isTrue = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked inputLength
                let mutable trueLength = 0
                Parallel.For(0, 
                             inputLength, 
                             (fun () -> 0), 
                             (fun i _ trueCount -> 
                                if predicate array.[i] then
                                    isTrue.[i] <- true
                                    trueCount + 1
                                else
                                    trueCount),
                             Action<int> (fun x -> System.Threading.Interlocked.Add(&trueLength, x) |> ignore) ) |> ignore
                                
                let res1 = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked trueLength
                let res2 = Microsoft.FSharp.Primitives.Basics.Array.zeroCreateUnchecked (inputLength - trueLength)

                let mutable iTrue = 0
                let mutable iFalse = 0
                for i = 0 to isTrue.Length-1 do
                    if isTrue.[i] then
                        res1.[iTrue] <- array.[i]
                        iTrue <- iTrue + 1
                    else
                        res2.[iFalse] <- array.[i]
                        iFalse <- iFalse + 1

                res1, res2
