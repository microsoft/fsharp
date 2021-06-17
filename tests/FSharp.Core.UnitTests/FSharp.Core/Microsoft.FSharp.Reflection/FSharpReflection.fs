// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

// Various tests for Microsoft.FSharp.Reflection

namespace FSharp.Core.UnitTests.Reflection

open System
open System.Reflection

open FSharp.Core.UnitTests.LibraryTestFx
open Xunit

open Microsoft.FSharp.Reflection

(*
[Test Strategy]
Make sure each method works on:
* Generic types
* Null    
* DiscriminatedUnion  
* Delegate    
* Record
* Exception
* Tuple
* Fuction
* Struct versions of the above
*)

module IsModule = 
    type IsModuleType () = 
        member _.M = 1

type FSharpDelegate = delegate of int -> string

type RecordType = { field1 : string; field2 : RecordType option; field3 : (unit -> RecordType * string) }
type GenericRecordType<'T, 'U> = { field1 : 'T; field2 : 'U; field3 : (unit -> GenericRecordType<'T, 'U>) }

[<Struct>]
type StructRecordType = { field1 : string; field2 : StructRecordType option; field3 : (unit -> StructRecordType * string) }

type SingleNullaryCaseDiscUnion = SingleNullaryCaseTag
type SingleCaseDiscUnion = SingleCaseTag of float * float * float

[<Struct>]
type SingleNullaryCaseDiscStructUnion = SingleNullaryCaseTagStruct
[<Struct>]
type SingleCaseDiscStructUnion = SingleCaseTagStruct of float * float * float

type DiscUnionType<'T> =
        | A // No data associated with tag
        | B of 'T * DiscUnionType<'T> option
        | C of float * string

type DiscStructUnionType<'T> =
        | A // No data associated with tag
        | B of 'T 
        | C of float * string

exception ExceptionInt of int

exception DatalessException

module FSharpModule = 
    type ModuleType() =
        class
        end


type FSharpValueTests() =
    
    // global variables
    let rec record1 : RecordType = { field1 = "field1"; field2 = Some(record1); field3 = ( fun () -> (record1, "")  )}
    let record2 : RecordType = { field1 = "field2"; field2 = Some(record1); field3 = ( fun () -> (record1, "")  )}
    
    // global variables
    let rec structRecord1 : StructRecordType = { field1 = "field1"; field2 = None; field3 = ( fun () -> (structRecord1, "")  )}
    let structRecord2 : StructRecordType = { field1 = "field2"; field2 = Some(structRecord1); field3 = ( fun () -> (structRecord1, "")  )}

    let rec genericRecordType1 : GenericRecordType<string, int> = 
        { 
            field1 = "field1"
            field2 = 1
            field3 = (fun () -> genericRecordType1)
        }
    
    let genericRecordType2 : GenericRecordType<string, int> = { field1 = "field1"; field2 = 1; field3 = ( fun () -> genericRecordType1 )}
    
    let nullValue = null
    
    let singleCaseUnion1 = SingleCaseDiscUnion.SingleCaseTag(1.0, 2.0, 3.0)
    let singleCaseUnion2 = SingleCaseDiscUnion.SingleCaseTag(4.0, 5.0, 6.0)
    
    let singleCaseStructUnion1 = SingleCaseDiscStructUnion.SingleCaseTagStruct(1.0, 2.0, 3.0)
    let singleCaseStructUnion2 = SingleCaseDiscStructUnion.SingleCaseTagStruct(4.0, 5.0, 6.0)
    
    let discUnionCaseA = DiscUnionType.A
    let discUnionCaseB = DiscUnionType.B(1, Some(discUnionCaseA))
    let discUnionCaseC = DiscUnionType.C(1.0, "stringparam")
    
    let discUnionRecCaseB = DiscUnionType.B(1, Some(discUnionCaseB))
    
    let discStructUnionCaseA = DiscStructUnionType.A
    let discStructUnionCaseB = DiscStructUnionType.B(1)
    let discStructUnionCaseC = DiscStructUnionType.C(1.0, "stringparam")
    
    let optionSome = Some(3)
    let optionNone: int option = None

    let voptionSome = ValueSome("stringparam")
    let voptionNone: string voption = ValueNone

    let list1 = [ 1; 2 ]
    let list2: int list = []
    
    let fsharpDelegate1 = new FSharpDelegate(fun (x:int) -> "delegate1")
    let fsharpDelegate2 = new FSharpDelegate(fun (x:int) -> "delegate2")
   
    let tuple1 = ( 1, "tuple1")
    let tuple2 = ( 2, "tuple2", (fun x -> x + 1))
    let tuple3 = ( 1, ( 2, "tuple"))
    let longTuple = (("yup", 1s), 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, Some 12, 13, "nope", struct (15, 16), 17, 18, ValueSome 19)
    
    let structTuple1 = struct ( 1, "tuple1")
    let structTuple2 = struct ( 2, "tuple2", (fun x -> x + 1))
    let structTuple3 = struct ( 1, struct ( 2, "tuple"))
    let longStructTuple = struct (("yup", 1s), 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, Some 12, 13, "nope", struct (15, 16), 17, 18, ValueSome 19)
    
    let func1  param  = param + 1
    let func2  param  = param + ""
    
    let exInt = ExceptionInt(1)
    let exDataless = DatalessException
 
    [<Fact>]
    member _.Equals1() =
        // Record value                
        Assert.True(FSharpValue.Equals(record1, record1))
        Assert.False(FSharpValue.Equals(record1, record2))

    [<Fact>]
    member _.Equals2() =
        Assert.True(FSharpValue.Equals(structRecord1, structRecord1))
        Assert.False(FSharpValue.Equals(structRecord1, structRecord2))
        Assert.False(FSharpValue.Equals(structRecord1, record2))
        Assert.False(FSharpValue.Equals(record1, structRecord1))
        Assert.False(FSharpValue.Equals(record2, structRecord2))

    [<Fact>]
    member _.Equals3() =
        // Generic Record value
        Assert.True(FSharpValue.Equals(genericRecordType1, genericRecordType1))
        Assert.False(FSharpValue.Equals(genericRecordType1, genericRecordType2))
        
    [<Fact>]
    member _.Equals4() =
        // null value
        Assert.True(FSharpValue.Equals(nullValue, nullValue))
        Assert.False(FSharpValue.Equals(nullValue, 1))
        
    [<Fact>]
    member _.Equals5() =
        // Single Case Union
        Assert.True(FSharpValue.Equals(singleCaseUnion1, singleCaseUnion1))
        Assert.False(FSharpValue.Equals(singleCaseUnion1, singleCaseUnion2))
        
    [<Fact>]
    member _.Equals6() =
        // Single Case Union
        Assert.True(FSharpValue.Equals(singleCaseStructUnion1, singleCaseStructUnion1))
        Assert.False(FSharpValue.Equals(singleCaseStructUnion1, singleCaseStructUnion2))
        
    [<Fact>]
    member _.Equals7() =
        // Discriminated Union
        Assert.True(FSharpValue.Equals(discUnionCaseA, discUnionCaseA))
        Assert.False(FSharpValue.Equals(discUnionCaseB, discUnionCaseC))
      
    [<Fact>]
    member _.Equals8() =
        // Discriminated Union
        Assert.True(FSharpValue.Equals(discStructUnionCaseA, discStructUnionCaseA))
        Assert.False(FSharpValue.Equals(discStructUnionCaseB, discStructUnionCaseC))
      
    [<Fact>]
    member _.Equals9() =
        // FSharpDelegate
        Assert.True(FSharpValue.Equals(fsharpDelegate1, fsharpDelegate1))
        Assert.False(FSharpValue.Equals(fsharpDelegate1, fsharpDelegate2))
        
    [<Fact>]
    member _.Equals10() =
        // Tuple
        Assert.True(FSharpValue.Equals(tuple1, tuple1))
        Assert.False(FSharpValue.Equals( (1, 2, 3), (4, 5, 6) ))
        
    [<Fact>]
    member _.Equals10b() =
        // Tuple
        Assert.True(FSharpValue.Equals(structTuple1, structTuple1))
        Assert.False(FSharpValue.Equals( struct (1, 2, 3), struct (4, 5, 6) ))

    [<Fact>]
    member _.Equals11() =
        // Tuples of differing types
        Assert.False(FSharpValue.Equals(tuple1, tuple2))
     
    [<Fact>]
    member _.Equals12() =
        // Exception
        Assert.True(FSharpValue.Equals(exInt, exInt))
        Assert.False(FSharpValue.Equals(exInt, exDataless))      

    [<Fact>]
    member _.GetExceptionFields() =
        
        // int 
        Assert.AreEqual(FSharpValue.GetExceptionFields(exInt), ([|1|] : obj []))
        
        // dataless
        Assert.AreEqual(FSharpValue.GetExceptionFields(exDataless), [||])
        
        // invalid type
        CheckThrowsArgumentException(fun () -> FSharpValue.GetExceptionFields(1) |> ignore)
        CheckThrowsArgumentException(fun () -> FSharpValue.GetExceptionFields( () ) |> ignore)
        
        // System Exception
        CheckThrowsArgumentException(fun () -> FSharpValue.GetExceptionFields(new System.Exception("ex message")) |> ignore)
        
        // null
        CheckThrowsArgumentException(fun () -> FSharpValue.GetExceptionFields(null) |> ignore)
        
    [<Fact>]
    member _.GetRecordField() =
         
        // Record
        let propertyinfo1 = (typeof<RecordType>).GetProperty("field1")
        Assert.AreEqual((FSharpValue.GetRecordField(record1, propertyinfo1)), "field1")
        
        // Generic Record value
        let propertyinfo2 = (typeof<GenericRecordType<string, int>>).GetProperty("field2")
        Assert.AreEqual((FSharpValue.GetRecordField(genericRecordType1, propertyinfo2)), 1)
        
        // null value
        CheckThrowsArgumentException(fun () ->FSharpValue.GetRecordField(null, propertyinfo1)|> ignore)
        CheckThrowsArgumentException(fun () ->FSharpValue.GetRecordField( () , propertyinfo1)|> ignore)
        
        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpValue.GetRecordField("invalid", propertyinfo1) |> ignore)
        
        // invalid property info
        let propertyinfoint = (typeof<RecordType>).GetProperty("fieldstring")
        CheckThrowsArgumentException(fun () -> FSharpValue.GetRecordField("invalid", propertyinfoint) |> ignore)
        
    [<Fact>]
    member _.GetStructRecordField() =
         
        // Record
        let propertyinfo1 = (typeof<StructRecordType>).GetProperty("field1")
        Assert.AreEqual((FSharpValue.GetRecordField(structRecord1, propertyinfo1)), "field1")
        
        // null value
        CheckThrowsArgumentException(fun () ->FSharpValue.GetRecordField(null, propertyinfo1)|> ignore)
        CheckThrowsArgumentException(fun () ->FSharpValue.GetRecordField( () , propertyinfo1)|> ignore)
        
        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpValue.GetRecordField("invalid", propertyinfo1) |> ignore)
        
        // invalid property info
        let propertyinfoint = (typeof<StructRecordType>).GetProperty("fieldstring")
        CheckThrowsArgumentException(fun () -> FSharpValue.GetRecordField("invalid", propertyinfoint) |> ignore)
        
    [<Fact>]
    member _.GetRecordFields() =
        // Record
        let propertyinfo1 = (typeof<RecordType>).GetProperty("field1")
        Assert.AreEqual((FSharpValue.GetRecordFields(record1)).[0], "field1")
        
        // Generic Record value
        let propertyinfo2 = (typeof<GenericRecordType<string, int>>).GetProperty("field1")
        Assert.AreEqual((FSharpValue.GetRecordFields(genericRecordType1)).[0], "field1")
        
        // null value
        CheckThrowsArgumentException(fun () -> FSharpValue.GetRecordFields(null)|> ignore)
        CheckThrowsArgumentException(fun () -> FSharpValue.GetRecordFields( () )|> ignore)
        
        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpValue.GetRecordFields("invalid") |> ignore)
    
    [<Fact>]
    member _.GetStructRecordFields() =
        let propertyinfo1 = (typeof<StructRecordType>).GetProperty("field1")
        Assert.AreEqual((FSharpValue.GetRecordFields(structRecord1)).[0], "field1")
        
    [<Fact>]
    member _.GetTupleField() =
        // Tuple
        Assert.AreEqual((FSharpValue.GetTupleField(tuple1, 0)), 1)
        
        // Tuple with function element
        Assert.AreEqual( FSharpValue.GetTupleField(tuple2, 1), "tuple2")
        
        // null value
        CheckThrowsArgumentException(fun () -> FSharpValue.GetTupleField(null, 3)|> ignore)
        CheckThrowsArgumentException(fun () -> FSharpValue.GetTupleField( () , 3)|> ignore)
        
        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpValue.GetTupleField("Invalid", 3)|> ignore)
        
        // index out of range
        CheckThrowsArgumentException(fun () -> FSharpValue.GetTupleField(tuple2, 8)|> ignore)
      
    [<Fact>]
    member _.GetStructTupleField() =
        // Tuple
        Assert.AreEqual((FSharpValue.GetTupleField(structTuple1, 0)), 1)
        
        // Tuple with function element
        Assert.AreEqual( FSharpValue.GetTupleField(structTuple2, 1), "tuple2")
        
        // index out of range
        CheckThrowsArgumentException(fun () -> FSharpValue.GetTupleField(structTuple2, 8)|> ignore)
      
    [<Fact>]
    member _.GetTupleFields() =
        // Tuple
        Assert.AreEqual(FSharpValue.GetTupleFields(tuple1).[0], 1)
        
        // Tuple with function element
        Assert.AreEqual( (FSharpValue.GetTupleFields(tuple2)).[1], "tuple2")
        
        // null value
        CheckThrowsArgumentException(fun () -> FSharpValue.GetTupleFields(null)|> ignore)
        CheckThrowsArgumentException(fun () -> FSharpValue.GetTupleFields( () )|> ignore)
        
        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpValue.GetTupleFields("Invalid")|> ignore)
      
    [<Fact>]
    member _.GetStructTupleFields() =
        // Tuple
        Assert.AreEqual(FSharpValue.GetTupleFields(structTuple1).[0], 1)
        
        // Tuple with function element
        Assert.AreEqual( (FSharpValue.GetTupleFields(structTuple2)).[1], "tuple2")
        
    [<Fact>]
    member _.GetUnionFields() =
        // single case union  
        let (singlecaseinfo, singlevaluearray) = FSharpValue.GetUnionFields(singleCaseUnion1, typeof<SingleCaseDiscUnion>)
        Assert.AreEqual(singlevaluearray, ([|1.0;2.0;3.0|] : obj []))
        
        // DiscUnionType
        let (duCaseinfo, duValueArray) = FSharpValue.GetUnionFields(discUnionCaseB, typeof<DiscUnionType<int>>)
        Assert.AreEqual(duValueArray.[0], 1)
                
        // null value
        CheckThrowsArgumentException(fun () ->  FSharpValue.GetUnionFields(null, null)|> ignore)
        CheckThrowsArgumentException(fun () ->  FSharpValue.GetUnionFields( () , null)|> ignore)
        
    [<Fact>]
    member _.GetStructUnionFields() =
        // single case union  
        let (_singlecaseinfo, singlevaluearray) = FSharpValue.GetUnionFields(singleCaseStructUnion1, typeof<SingleCaseDiscStructUnion>)
        Assert.AreEqual(singlevaluearray, ([|1.0;2.0;3.0|] : obj []))
        
        // DiscUnionType
        let (_duCaseinfo, duValueArray) = FSharpValue.GetUnionFields(discStructUnionCaseB, typeof<DiscStructUnionType<int>>)
        Assert.AreEqual(duValueArray.[0], 1)
                
    [<Fact>]
    member _.MakeFunction() =
    
        // Int function
        let implementationInt (x:obj) = box( unbox<int>(x) + 1)
        let resultFuncIntObj  = FSharpValue.MakeFunction(typeof<int -> int>, implementationInt )
        let resultFuncInt = resultFuncIntObj :?> (int -> int)
        Assert.AreEqual(resultFuncInt(5), 6)
        
        // String funcion
        let implementationString (x:obj) = box( unbox<string>(x) + " function")
        let resultFuncStringObj  = FSharpValue.MakeFunction(typeof<string -> string>, implementationString )
        let resultFuncString = resultFuncStringObj :?> (string -> string)
        Assert.AreEqual(resultFuncString("parameter"), "parameter function")
        
    [<Fact>]
    member _.MakeRecord() =
        // Record
        let makeRecord = FSharpValue.MakeRecord(typeof<RecordType>, [| box"field1"; box(Some(record1)); box( fun () -> (record1, "")) |])
        Assert.AreEqual(FSharpValue.GetRecordFields(makeRecord).[0], "field1")
        
        // Generic Record value
        let makeRecordGeneric = FSharpValue.MakeRecord(typeof<GenericRecordType<string, int>>, [| box"field1"; box 1; box( fun () -> genericRecordType1) |])
        Assert.AreEqual(FSharpValue.GetRecordFields(makeRecordGeneric).[0], "field1")
        
        // null value
        CheckThrowsArgumentException(fun () ->FSharpValue.MakeRecord(null, null)|> ignore)
        
        // invalid value        
        CheckThrowsArgumentException(fun () ->  FSharpValue.MakeRecord(typeof<GenericRecordType<string, int>>, [| box 1; box("invalid param"); box("invalid param") |])|> ignore)
        
    [<Fact>]
    member _.MakeStructRecord() =
        // Record
        let makeRecord = FSharpValue.MakeRecord(typeof<StructRecordType>, [| box"field1"; box(Some(structRecord1)); box( fun () -> (structRecord1, "")) |])
        Assert.AreEqual(FSharpValue.GetRecordFields(makeRecord).[0], "field1")
        
    [<Fact>]
    member _.MakeTuple() =
        // Tuple
        let makeTuple = FSharpValue.MakeTuple([| box 1; box("tuple") |], typeof<Tuple<int, string>>)
        Assert.AreEqual(FSharpValue.GetTupleFields(makeTuple).[0], 1)
        
        // Tuple with function
        let makeTuplewithFunc = FSharpValue.MakeTuple([| box 1; box "tuple with func"; box (fun x -> x + 1) |], typeof<Tuple<int, string, (int -> int)>>)
        Assert.AreEqual(FSharpValue.GetTupleFields(makeTuplewithFunc).[1], "tuple with func")
        
        // null value
        CheckThrowsArgumentNullException(fun () ->FSharpValue.MakeTuple(null, null)|> ignore)
        
        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpValue.MakeTuple([| box"invalid param"; box"invalid param"|], typeof<Tuple<int, string>>)  |> ignore)
        
    [<Fact>]
    member _.MakeStructTuple() =
        // Tuple
        let makeTuple = FSharpValue.MakeTuple([| box 1; box("tuple") |], typeof<struct (int * string)>)
        Assert.AreEqual(FSharpValue.GetTupleFields(makeTuple).[0], 1)
        Assert.AreEqual(FSharpValue.GetTupleFields(makeTuple).[1], "tuple")
        
        // Tuple with function
        let makeTuplewithFunc = FSharpValue.MakeTuple([| box 1; box "tuple with func"; box (fun x -> x + 1) |], typeof<struct (int * string * (int -> int))>)
        Assert.AreEqual(FSharpValue.GetTupleFields(makeTuplewithFunc).[0], 1)
        Assert.AreEqual(FSharpValue.GetTupleFields(makeTuplewithFunc).[1], "tuple with func")
        
        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpValue.MakeTuple([| box"invalid param"; box"invalid param"|], typeof<struct (int * string)>)  |> ignore)
        
    [<Fact>]
    member _.MakeUnion() =
        // single case union  
        let (singlecaseinfo, _singlevaluearray) = FSharpValue.GetUnionFields(singleCaseUnion1, typeof<SingleCaseDiscUnion>)
        let resultSingleCaseUnion=FSharpValue.MakeUnion(singlecaseinfo, [| box 1.0; box 2.0; box 3.0|])
        Assert.AreEqual(resultSingleCaseUnion, singleCaseUnion1)
        
        // DiscUnionType
        let (duCaseinfo, _duValueArray) = FSharpValue.GetUnionFields(discUnionCaseB, typeof<DiscUnionType<int>>)
        let resultDiscUnion=FSharpValue.MakeUnion(duCaseinfo, [| box 1; box (Some discUnionCaseB) |])
        Assert.AreEqual(resultDiscUnion, discUnionRecCaseB)
        
    [<Fact>]
    member _.MakeStructUnion() =
        // single case union  
        let (singlecaseinfo, _singlevaluearray) = FSharpValue.GetUnionFields(singleCaseStructUnion1, typeof<SingleCaseDiscStructUnion>)
        let resultSingleCaseUnion=FSharpValue.MakeUnion(singlecaseinfo, [| box 1.0; box 2.0; box 3.0|])
        Assert.AreEqual(resultSingleCaseUnion, singleCaseStructUnion1)
        
        // DiscUnionType
        let (duCaseinfo, duValueArray) = FSharpValue.GetUnionFields(discStructUnionCaseB, typeof<DiscStructUnionType<int>>)
        FSharpValue.MakeUnion(duCaseinfo, [| box 1|]) |> ignore
        
    [<Fact>]
    member _.PreComputeRecordConstructor() =
        // Record
        let recCtor = FSharpValue.PreComputeRecordConstructor(typeof<RecordType>)
        let resultRecordType   = recCtor([| box("field1"); box(Some(record1)); box(fun () -> (record1, "")) |])
        Assert.AreEqual( (unbox<RecordType>(resultRecordType)).field1 , record1.field1)
        
        // Generic Record value
        let genericRecCtor = FSharpValue.PreComputeRecordConstructor(typeof<GenericRecordType<string, int>>)
        let resultGenericRecordType = genericRecCtor([| box("field1"); box 2; box( fun () -> genericRecordType1) |])
        Assert.AreEqual( (unbox<GenericRecordType<string, int>>(resultGenericRecordType)).field1, genericRecordType1.field1)
        
        // null value
        CheckThrowsArgumentException(fun () ->FSharpValue.PreComputeRecordConstructor(null)|> ignore)
        
        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpValue.PreComputeRecordConstructor(typeof<DiscUnionType<string>>) |> ignore)        
        
    [<Fact>]
    member _.PreComputeStructRecordConstructor() =
        // Record
        let recCtor = FSharpValue.PreComputeRecordConstructor(typeof<StructRecordType>)
        let resultRecordType   = recCtor([| box("field1"); box(Some(structRecord1)); box(fun () -> (structRecord1, "")) |])
        Assert.AreEqual( (unbox<StructRecordType>(resultRecordType)).field1 , structRecord1.field1)
        
       
    [<Fact>]
    member _.PreComputeRecordConstructorInfo() =
        // Record
        let recordCtorInfo = FSharpValue.PreComputeRecordConstructorInfo(typeof<RecordType>)
        Assert.AreEqual(recordCtorInfo.ReflectedType, typeof<RecordType> )
        
        // Generic Record value
        let genericrecordCtorInfo = FSharpValue.PreComputeRecordConstructorInfo(typeof<GenericRecordType<string, int>>)
        Assert.AreEqual(genericrecordCtorInfo.ReflectedType, typeof<GenericRecordType<string, int>>)
        
        // null value
        CheckThrowsArgumentException(fun () ->FSharpValue.PreComputeRecordConstructorInfo(null)|> ignore)
        
        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpValue.PreComputeRecordConstructorInfo(typeof<DiscUnionType<string>>) |> ignore)        
        
    [<Fact>]
    member _.PreComputeStructRecordConstructorInfo() =
        // Record
        let recordCtorInfo = FSharpValue.PreComputeRecordConstructorInfo(typeof<StructRecordType>)
        Assert.AreEqual(recordCtorInfo.ReflectedType, typeof<StructRecordType> )
        
    [<Fact>]
    member _.PreComputeRecordFieldReader() =
        // Record
        let recordFieldReader = FSharpValue.PreComputeRecordFieldReader((typeof<RecordType>).GetProperty("field1"))
        Assert.AreEqual(recordFieldReader(record1), box("field1"))
        
        // Generic Record value
        let recordFieldReader = FSharpValue.PreComputeRecordFieldReader((typeof<GenericRecordType<string, int>>).GetProperty("field1"))
        Assert.AreEqual(recordFieldReader(genericRecordType1), box("field1"))
        
        // null value
        CheckThrowsArgumentException(fun () -> FSharpValue.PreComputeRecordFieldReader(null)|> ignore)    
        
    [<Fact>]
    member _.PreComputeStructRecordFieldReader() =
        // Record
        let recordFieldReader = FSharpValue.PreComputeRecordFieldReader((typeof<StructRecordType>).GetProperty("field1"))
        Assert.AreEqual(recordFieldReader(structRecord1), box("field1"))
        
    [<Fact>]
    member _.PreComputeRecordReader() =
        // Record
        let recordReader = FSharpValue.PreComputeRecordReader(typeof<RecordType>)
        Assert.AreEqual( (recordReader(record1)).[0], "field1")
        
        // Generic Record value
        let genericrecordReader = FSharpValue.PreComputeRecordReader(typeof<GenericRecordType<string, int>>)
        Assert.AreEqual( (genericrecordReader(genericRecordType1)).[0], "field1")
        
        // null value
        CheckThrowsArgumentException(fun () ->FSharpValue.PreComputeRecordReader(null)|> ignore)
        
        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpValue.PreComputeRecordReader(typeof<DiscUnionType<string>>) |> ignore)        
    
    [<Fact>]
    member _.PreComputeStructRecordReader() =
        // Record
        let recordReader = FSharpValue.PreComputeRecordReader(typeof<StructRecordType>)
        Assert.AreEqual( (recordReader(structRecord1)).[0], "field1")
    
    [<Fact>]
    member _.PreComputeTupleConstructor() =
        // Tuple
        let tupleCtor = FSharpValue.PreComputeTupleConstructor(tuple1.GetType())    
        Assert.AreEqual( tupleCtor([| box 1; box "tuple1" |]) , box(tuple1))

        let tupleCtor = FSharpValue.PreComputeTupleConstructor(longTuple.GetType())    
        Assert.AreEqual( tupleCtor([| box ("yup", 1s); box 2; box 3; box 4; box 5; box 6; box 7; box 8; box 9; box 10; box 11; box (Some 12); box 13; box "nope"; box (struct (15, 16)); box 17; box 18; box (ValueSome 19) |]) , box(longTuple))
        
        // Tuple with function member
        let tuplewithFuncCtor = FSharpValue.PreComputeTupleConstructor(tuple2.GetType())  
        let resultTuplewithFunc = tuplewithFuncCtor([| box 2; box "tuple2"; box (fun x -> x + 1) |])
        Assert.AreEqual( FSharpValue.GetTupleFields( box(resultTuplewithFunc)).[1] , "tuple2")
        
        // nested tuple
        let tupleNestedCtor = FSharpValue.PreComputeTupleConstructor(tuple3.GetType())
        Assert.AreEqual( tupleNestedCtor([| box 1; box(2, "tuple") |] ), box(tuple3))
         
        // null value
        CheckThrowsArgumentException(fun () -> FSharpValue.PreComputeTupleConstructor(null)|> ignore)
        
        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpValue.PreComputeTupleConstructor(typeof<DiscUnionType<string>>) |> ignore)        
        CheckThrowsArgumentException(fun () -> FSharpValue.PreComputeTupleConstructor(typeof<unit>) |> ignore)        
    
    [<Fact>]
    member _.PreComputeStructTupleConstructor() =
        // Tuple
        let tupleCtor = FSharpValue.PreComputeTupleConstructor(structTuple1.GetType())    
        Assert.AreEqual( tupleCtor([| box 1; box "tuple1" |]) , box(structTuple1))
        
        let tupleCtor = FSharpValue.PreComputeTupleConstructor(longStructTuple.GetType())    
        Assert.AreEqual( tupleCtor([| box ("yup", 1s); box 2; box 3; box 4; box 5; box 6; box 7; box 8; box 9; box 10; box 11; box (Some 12); box 13; box "nope"; box (struct (15, 16)); box 17; box 18; box (ValueSome 19) |]) , box(longStructTuple))
 
        // Tuple with function member
        let tuplewithFuncCtor = FSharpValue.PreComputeTupleConstructor(structTuple2.GetType())  
        let resultTuplewithFunc = tuplewithFuncCtor([| box 2; box "tuple2"; box (fun x -> x + 1) |])
        Assert.AreEqual( FSharpValue.GetTupleFields( box(resultTuplewithFunc)).[1] , "tuple2")
        
        // nested tuple
        let tupleNestedCtor = FSharpValue.PreComputeTupleConstructor(structTuple3.GetType())
        Assert.AreEqual( tupleNestedCtor([| box 1; box (struct (2, "tuple")) |] ), box(structTuple3))
         
        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpValue.PreComputeTupleConstructor(typeof<DiscStructUnionType<string>>) |> ignore)        

    [<Fact>]
    member _.PreComputeTupleConstructorInfo() =
        // Tuple
        let (tupleCtorInfo, _tupleType) = FSharpValue.PreComputeTupleConstructorInfo(typeof<Tuple<int, string>>)    
        Assert.AreEqual(tupleCtorInfo.ReflectedType, typeof<Tuple<int, string>> )
        
        // Nested 
        let (nestedTupleCtorInfo, _nestedTupleType) = FSharpValue.PreComputeTupleConstructorInfo(typeof<Tuple<int, Tuple<int, string>>>)    
        Assert.AreEqual(nestedTupleCtorInfo.ReflectedType, typeof<Tuple<int, Tuple<int, string>>>)
        
        // null value
        CheckThrowsArgumentException(fun () ->FSharpValue.PreComputeTupleConstructorInfo(null)|> ignore)
        
        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpValue.PreComputeTupleConstructorInfo(typeof<RecordType>) |> ignore)        
        
        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpValue.PreComputeTupleConstructorInfo(typeof<StructRecordType>) |> ignore)        
        
    [<Fact>]
    member _.PreComputeStructTupleConstructorInfo() =
        // Tuple
        let (tupleCtorInfo, _tupleType) = FSharpValue.PreComputeTupleConstructorInfo(typeof<struct (int * string)>)    
        Assert.AreEqual(tupleCtorInfo.ReflectedType, typeof<struct (int * string)> )
        
        // Nested 
        let (nestedTupleCtorInfo, _nestedTupleType) = FSharpValue.PreComputeTupleConstructorInfo(typeof<struct (int * struct (int * string))>)    
        Assert.AreEqual(nestedTupleCtorInfo.ReflectedType, typeof<struct (int * struct (int * string))>)
        
    [<Fact>]
    member _.PreComputeTuplePropertyInfo() =
    
        // Tuple
        let (tuplePropInfo, _typeindex) = FSharpValue.PreComputeTuplePropertyInfo(typeof<Tuple<int, string>>, 0)    
        Assert.AreEqual(tuplePropInfo.PropertyType, typeof<int>)
        
        // Nested 
        let (tupleNestedPropInfo, _typeindex) = FSharpValue.PreComputeTuplePropertyInfo(typeof<Tuple<int, Tuple<int, string>>>, 1)    
        Assert.AreEqual(tupleNestedPropInfo.PropertyType, typeof<Tuple<int, string>>)
        
        // null value
        CheckThrowsArgumentException(fun () ->FSharpValue.PreComputeTuplePropertyInfo(null, 0)|> ignore)
        
        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpValue.PreComputeTuplePropertyInfo(typeof<RecordType>, 0) |> ignore)        

        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpValue.PreComputeTuplePropertyInfo(typeof<StructRecordType>, 0) |> ignore)        

    // This fails as no PropertyInfo's actually exist for struct tuple types. 
    //
    //[<Fact>]
    //member _.PreComputeStructTuplePropertyInfo() =
    //
    //    // Tuple
    //    let (tuplePropInfo, _typeindex) = FSharpValue.PreComputeTuplePropertyInfo(typeof<struct (int * string)>, 0)    
    //    Assert.AreEqual(tuplePropInfo.PropertyType, typeof<int>)
    //    
    //    // Nested 
    //    let (tupleNestedPropInfo, _typeindex) = FSharpValue.PreComputeTuplePropertyInfo(typeof<struct (int * struct (int * string))>, 1)    
    //    Assert.AreEqual(tupleNestedPropInfo.PropertyType, typeof<struct (int * string)>)
        
    [<Fact>]
    member _.PreComputeTupleReader() =
    
        // Tuple
        let tuplereader = FSharpValue.PreComputeTupleReader(typeof<Tuple<int, string>>)    
        Assert.AreEqual(tuplereader(tuple1).[0], 1)
        
        // Nested 
        let nestedtuplereader = FSharpValue.PreComputeTupleReader(typeof<Tuple<int, Tuple<int, string>>>)    
        Assert.AreEqual(nestedtuplereader(tuple3).[1], box(2, "tuple"))

        let longTupleReader = FSharpValue.PreComputeTupleReader (longTuple.GetType ())
        Assert.AreEqual (longTupleReader longTuple, [| box ("yup", 1s); box 2; box 3; box 4; box 5; box 6; box 7; box 8; box 9; box 10; box 11; box (Some 12); box 13; box "nope"; box (struct (15, 16)); box 17; box 18; box (ValueSome 19) |])
        
        // null value
        CheckThrowsArgumentException(fun () ->FSharpValue.PreComputeTupleReader(null)|> ignore)
        
        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpValue.PreComputeTupleReader(typeof<RecordType>) |> ignore)        

    [<Fact>]
    member _.PreComputeStructTupleReader() =
    
        // Tuple
        let tuplereader = FSharpValue.PreComputeTupleReader(typeof<struct (int  * string)>)    
        Assert.AreEqual(tuplereader(structTuple1).[0], 1)
        
        // Nested 
        let nestedtuplereader = FSharpValue.PreComputeTupleReader(typeof<struct (int * struct (int * string))>)    
        Assert.AreEqual(nestedtuplereader(structTuple3).[1], box (struct (2, "tuple")))
        
        let longTupleReader = FSharpValue.PreComputeTupleReader (longStructTuple.GetType ())
        Assert.AreEqual (longTupleReader longStructTuple, [| box ("yup", 1s); box 2; box 3; box 4; box 5; box 6; box 7; box 8; box 9; box 10; box 11; box (Some 12); box 13; box "nope"; box (struct (15, 16)); box 17; box 18; box (ValueSome 19) |])

        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpValue.PreComputeTupleReader(typeof<StructRecordType>) |> ignore)        
        
    [<Fact>]
    member _.PreComputeUnionConstructor() =
    
        // SingleCaseUnion
        let (singlecaseinfo, _singlevaluearray) = FSharpValue.GetUnionFields(singleCaseUnion1, typeof<SingleCaseDiscUnion>)
        let singleUnionCtor = FSharpValue.PreComputeUnionConstructor(singlecaseinfo)    
        let resuleSingleCaseUnion = singleUnionCtor([| box 1.0; box 2.0; box 3.0|])
        Assert.AreEqual(resuleSingleCaseUnion, singleCaseUnion1)
        
        // DiscUnion
        let (discunioninfo, _discunionvaluearray) = FSharpValue.GetUnionFields(discUnionCaseB, typeof<DiscUnionType<int>>)
        let discUnionCtor = FSharpValue.PreComputeUnionConstructor(discunioninfo)    
        let resuleDiscUnionB = discUnionCtor([| box 1; box(Some(discUnionCaseB)) |])
        Assert.AreEqual(resuleDiscUnionB, discUnionRecCaseB)

    [<Fact>]
    member _.PreComputeStructUnionConstructor() =
    
        // SingleCaseUnion
        let (singlecaseinfo, _singlevaluearray) = FSharpValue.GetUnionFields(singleCaseStructUnion1, typeof<SingleCaseDiscStructUnion>)
        let singleUnionCtor = FSharpValue.PreComputeUnionConstructor(singlecaseinfo)    
        let resuleSingleCaseUnion = singleUnionCtor([| box 1.0; box 2.0; box 3.0|])
        Assert.AreEqual(resuleSingleCaseUnion, singleCaseStructUnion1)
        
        // DiscUnion
        let (discunioninfo, _discunionvaluearray) = FSharpValue.GetUnionFields(discStructUnionCaseB, typeof<DiscStructUnionType<int>>)
        let discUnionCtor = FSharpValue.PreComputeUnionConstructor(discunioninfo)    
        let resuleDiscUnionB = discUnionCtor([| box 1|])
        Assert.AreEqual(resuleDiscUnionB, discStructUnionCaseB)
    
    [<Fact>]
    member _.PreComputeUnionConstructorInfo() =
    
        // SingleCaseUnion
        let (singlecaseinfo, _singlevaluearray) = FSharpValue.GetUnionFields(singleCaseUnion1, typeof<SingleCaseDiscUnion>)
        let singlecaseMethodInfo = FSharpValue.PreComputeUnionConstructorInfo(singlecaseinfo)    
        Assert.AreEqual(singlecaseMethodInfo.ReflectedType, typeof<SingleCaseDiscUnion>)
        
        // DiscUnion
        let (discUnionInfo, _discvaluearray) = FSharpValue.GetUnionFields(discUnionCaseB, typeof<DiscUnionType<int>>)
        let discUnionMethodInfo = FSharpValue.PreComputeUnionConstructorInfo(discUnionInfo)    
        Assert.AreEqual(discUnionMethodInfo.ReflectedType, typeof<DiscUnionType<int>>)
    
    [<Fact>]
    member _.PreComputeStructUnionConstructorInfo() =
    
        // SingleCaseUnion
        let (singlecaseinfo, _singlevaluearray) = FSharpValue.GetUnionFields(singleCaseStructUnion1, typeof<SingleCaseDiscStructUnion>)
        let singlecaseMethodInfo = FSharpValue.PreComputeUnionConstructorInfo(singlecaseinfo)    
        Assert.AreEqual(singlecaseMethodInfo.ReflectedType, typeof<SingleCaseDiscStructUnion>)
        
        // DiscUnion
        let (discUnionInfo, _discvaluearray) = FSharpValue.GetUnionFields(discStructUnionCaseB, typeof<DiscStructUnionType<int>>)
        let discUnionMethodInfo = FSharpValue.PreComputeUnionConstructorInfo(discUnionInfo)    
        Assert.AreEqual(discUnionMethodInfo.ReflectedType, typeof<DiscStructUnionType<int>>)

    [<Fact>]
    member _.PreComputeUnionReader() =
    
        // SingleCaseUnion
        let (singlecaseinfo, singlevaluearray) = FSharpValue.GetUnionFields(singleCaseUnion1, typeof<SingleCaseDiscUnion>)
        let singlecaseUnionReader = FSharpValue.PreComputeUnionReader(singlecaseinfo)    
        Assert.AreEqual(singlecaseUnionReader(box(singleCaseUnion1)), [| box 1.0; box 2.0; box 3.0|])
        
        // DiscUnion
        let (discUnionInfo, discvaluearray) = FSharpValue.GetUnionFields(discUnionRecCaseB, typeof<DiscUnionType<int>>)
        let discUnionReader = FSharpValue.PreComputeUnionReader(discUnionInfo)    
        Assert.AreEqual(discUnionReader(box(discUnionRecCaseB)) , [| box 1; box(Some(discUnionCaseB)) |])

        // Option
        let (optionCaseInfo, _) = FSharpValue.GetUnionFields(optionSome, typeof<int option>)
        let optionReader = FSharpValue.PreComputeUnionReader(optionCaseInfo)
        Assert.AreEqual(optionReader(box(optionSome)), [| box 3 |])

        let (optionCaseInfo, _) = FSharpValue.GetUnionFields(optionNone, typeof<int option>)
        let optionReader = FSharpValue.PreComputeUnionReader(optionCaseInfo)
        Assert.AreEqual(optionReader(box(optionNone)), [| |])

        // List
        let (listCaseInfo, _) = FSharpValue.GetUnionFields(list1, typeof<int list>)
        let listReader = FSharpValue.PreComputeUnionReader(listCaseInfo)
        Assert.AreEqual(listReader(box(list1)), [| box 1; box [ 2 ] |])

        let (listCaseInfo, _) = FSharpValue.GetUnionFields(list2, typeof<int list>)
        let listReader = FSharpValue.PreComputeUnionReader(listCaseInfo)
        Assert.AreEqual(listReader(box(list2)), [| |])
        
    [<Fact>]
    member _.PreComputeStructUnionReader() =
    
        // SingleCaseUnion
        let (singlecaseinfo, singlevaluearray) = FSharpValue.GetUnionFields(singleCaseStructUnion1, typeof<SingleCaseDiscStructUnion>)
        let singlecaseUnionReader = FSharpValue.PreComputeUnionReader(singlecaseinfo)    
        Assert.AreEqual(singlecaseUnionReader(box(singleCaseStructUnion1)), [| box 1.0; box 2.0; box 3.0|])
        
        // DiscUnion
        let (discUnionInfo, discvaluearray) = FSharpValue.GetUnionFields(discStructUnionCaseB, typeof<DiscStructUnionType<int>>)
        let discUnionReader = FSharpValue.PreComputeUnionReader(discUnionInfo)    
        Assert.AreEqual(discUnionReader(box(discStructUnionCaseB)) , [| box 1|])

        // Value Option
        let (voptionCaseInfo, _) = FSharpValue.GetUnionFields(voptionSome, typeof<string voption>)
        let voptionReader = FSharpValue.PreComputeUnionReader(voptionCaseInfo)
        Assert.AreEqual(voptionReader(box(voptionSome)), [| box "stringparam" |])

        let (voptionCaseInfo, _) = FSharpValue.GetUnionFields(voptionNone, typeof<string voption>)
        let voptionReader = FSharpValue.PreComputeUnionReader(voptionCaseInfo)
        Assert.AreEqual(voptionReader(box(voptionNone)), [| |])
        
    [<Fact>]
    member _.PreComputeUnionTagMemberInfo() =
    
        // SingleCaseUnion
        let singlecaseUnionMemberInfo = FSharpValue.PreComputeUnionTagMemberInfo(typeof<SingleCaseDiscUnion>) 
        Assert.AreEqual(singlecaseUnionMemberInfo.ReflectedType, typeof<SingleCaseDiscUnion>)
   
        // DiscUnion
        let discUnionMemberInfo = FSharpValue.PreComputeUnionTagMemberInfo(typeof<DiscUnionType<int>>) 
        Assert.AreEqual(discUnionMemberInfo.ReflectedType, typeof<DiscUnionType<int>>)
        
         // null value
        CheckThrowsArgumentException(fun () ->FSharpValue.PreComputeUnionTagMemberInfo(null)|> ignore)
        
        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpValue.PreComputeUnionTagMemberInfo(typeof<RecordType>) |> ignore)        

    [<Fact>]
    member _.PreComputeStructUnionTagMemberInfo() =
    
        // SingleCaseUnion
        let singlecaseUnionMemberInfo = FSharpValue.PreComputeUnionTagMemberInfo(typeof<SingleCaseDiscStructUnion>) 
        Assert.AreEqual(singlecaseUnionMemberInfo.ReflectedType, typeof<SingleCaseDiscStructUnion>)
   
        // DiscUnion
        let discUnionMemberInfo = FSharpValue.PreComputeUnionTagMemberInfo(typeof<DiscStructUnionType<int>>) 
        Assert.AreEqual(discUnionMemberInfo.ReflectedType, typeof<DiscStructUnionType<int>>)
        
    [<Fact>]
    member _.PreComputeUnionTagReader() =
    
        // SingleCaseUnion
        let singlecaseUnionTagReader = FSharpValue.PreComputeUnionTagReader(typeof<SingleCaseDiscUnion>) 
        Assert.AreEqual(singlecaseUnionTagReader(box(singleCaseUnion1)), 0)
   
        // DiscUnion
        let discUnionTagReader = FSharpValue.PreComputeUnionTagReader(typeof<DiscUnionType<int>>) 
        Assert.AreEqual(discUnionTagReader(box(discUnionCaseB)), 1)

        // Option
        let optionTagReader = FSharpValue.PreComputeUnionTagReader(typeof<int option>)
        Assert.AreEqual(optionTagReader(box(optionSome)), 1)
        Assert.AreEqual(optionTagReader(box(optionNone)), 0)

        // Value Option
        let voptionTagReader = FSharpValue.PreComputeUnionTagReader(typeof<string voption>)
        Assert.AreEqual(voptionTagReader(box(voptionSome)), 1)
        Assert.AreEqual(voptionTagReader(box(voptionNone)), 0)
        
         // null value
        CheckThrowsArgumentException(fun () ->FSharpValue.PreComputeUnionTagReader(null)|> ignore)
        
        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpValue.PreComputeUnionTagReader(typeof<RecordType>) |> ignore)        
        
    [<Fact>]
    member _.PreComputeStructUnionTagReader() =
    
        // SingleCaseUnion
        let singlecaseUnionTagReader = FSharpValue.PreComputeUnionTagReader(typeof<SingleCaseDiscStructUnion>) 
        Assert.AreEqual(singlecaseUnionTagReader(box(singleCaseStructUnion1)), 0)
   
        // DiscUnion
        let discUnionTagReader = FSharpValue.PreComputeUnionTagReader(typeof<DiscStructUnionType<int>>) 
        Assert.AreEqual(discUnionTagReader(box(discStructUnionCaseB)), 1)


type FSharpTypeTests() =    
    
    // instance for member _.ObjectEquals
    let rec recordtype1 : RecordType = { field1 = "field1"; field2 = Some(recordtype1); field3 = ( fun () -> (recordtype1, "")  )}
    let recordtype2 : RecordType = { field1 = "field2"; field2 = Some(recordtype1); field3 = ( fun () -> (recordtype1, "")  )}
    let rec genericRecordType1 : GenericRecordType<string, int> = { field1 = "field1"; field2 = 1; field3 = ( fun () -> genericRecordType1 )}
    let genericRecordType2 : GenericRecordType<string, int> = { field1 = "field1"; field2 = 1; field3 = ( fun () -> genericRecordType1 )}
    
    let nullValue = null
    
    let singlecaseunion1 = SingleCaseDiscUnion.SingleCaseTag(1.0, 2.0, 3.0)
    let singlecaseunion2 = SingleCaseDiscUnion.SingleCaseTag(4.0, 5.0, 6.0)
    
    let discUniontypeA = DiscUnionType.A
    let discUniontypeB = DiscUnionType.B(1, Some(discUniontypeA))
    let discUniontypeC = DiscUnionType.C(1.0, "stringparam")
    
    let fsharpdelegate1 = new FSharpDelegate(fun (x:int) -> "delegate1")
    let fsharpdelegate2 = new FSharpDelegate(fun (x:int) -> "delegate2")
    
    
    let tuple1 = ( 1, "tuple1")
    let tuple2 = ( 2, "tuple2")
    
    let func1  param  = param + 1
    let func2  param  = param + ""
    
    let exInt = ExceptionInt(1)
    let exDataless = DatalessException
    
    // Base class methods
    [<Fact>]
    member _.ObjectEquals() =       
        
        // Record value                
        Assert.True(FSharpValue.Equals(recordtype1, recordtype1))
        Assert.False(FSharpValue.Equals(recordtype1, recordtype2))

        // Generic Record value
        Assert.True(FSharpValue.Equals(genericRecordType1, genericRecordType1))
        Assert.False(FSharpValue.Equals(genericRecordType1, genericRecordType2))
        
        // null value
        Assert.True(FSharpValue.Equals(nullValue, nullValue))
        Assert.False(FSharpValue.Equals(nullValue, 1))
        
        // Single Case Union
        Assert.True(FSharpValue.Equals(singlecaseunion1, singlecaseunion1))
        Assert.False(FSharpValue.Equals(singlecaseunion1, singlecaseunion2))
        
        // Discriminated Union
        Assert.True(FSharpValue.Equals(discUniontypeA, discUniontypeA))
        Assert.False(FSharpValue.Equals(discUniontypeB, discUniontypeC))
      
        // FSharpDelegate
        Assert.True(FSharpValue.Equals(fsharpdelegate1, fsharpdelegate1))
        Assert.False(FSharpValue.Equals(fsharpdelegate1, fsharpdelegate2))
        
        // Tuple
        Assert.True(FSharpValue.Equals(tuple1, tuple1))
        Assert.False(FSharpValue.Equals(tuple1, tuple2))
     
        // Exception
        Assert.True(FSharpValue.Equals(exInt, exInt))
        Assert.False(FSharpValue.Equals(exInt, exDataless))   
       
    
    // Static methods
    [<Fact>]
    member _.GetExceptionFields() =        
        
        // positive               
        let forallexistedInt = 
            FSharpType.GetExceptionFields(typeof<ExceptionInt>) 
            |> Array.forall (fun property -> (Array.IndexOf<Reflection.PropertyInfo>(typeof<ExceptionInt>.GetProperties(), property) > -1))

        Assert.True(forallexistedInt)
        
        let forallexistedDataless = 
            FSharpType.GetExceptionFields(typeof<DatalessException>) 
            |> Array.forall (fun property -> (Array.IndexOf<Reflection.PropertyInfo>(typeof<DatalessException>.GetProperties(), property) > -1))
        Assert.True(forallexistedDataless)
       
        // Argument Exception
        CheckThrowsArgumentException(fun () ->FSharpType.GetExceptionFields(typeof<RecordType>) |> ignore )
        
        // null
        CheckThrowsArgumentNullException(fun () ->FSharpType.GetExceptionFields(null) |> ignore )
        
        
    [<Fact>]
    member _.GetFunctionElements() =    
               
        // positive
        Assert.AreEqual(FSharpType.GetFunctionElements(typeof<int -> string>), (typeof<Int32>, typeof<String>))  
        Assert.AreEqual(FSharpType.GetFunctionElements(typeof<int -> int -> string>), (typeof<Int32>, typeof<Microsoft.FSharp.Core.FSharpFunc<int, string>>))
        
        // argument exception
        CheckThrowsArgumentException(fun () ->FSharpType.GetFunctionElements(typeof<int>) |> ignore )
        
        // null
        CheckThrowsArgumentNullException(fun () ->FSharpType.GetFunctionElements(null) |> ignore )
        
    [<Fact>]
    member _.GetRecordFields() =    
               
        // positive
        Assert.AreEqual(FSharpType.GetRecordFields(typeof<RecordType>), (typeof<RecordType>.GetProperties()))        
        Assert.AreEqual(FSharpType.GetRecordFields(typeof<GenericRecordType<int, string>>), (typeof<GenericRecordType<int, string>>.GetProperties()))
        
        // argument exception
        CheckThrowsArgumentException(fun () ->FSharpType.GetRecordFields(typeof<int>) |> ignore )
        
        // null
        CheckThrowsArgumentNullException(fun () ->FSharpType.GetRecordFields(null) |> ignore )
        
        
    [<Fact>]
    member _.GetTupleElements() =    
               
        // positive
        Assert.AreEqual(FSharpType.GetTupleElements(typeof<Tuple<string, int>>), [|typeof<System.String>; typeof<System.Int32>|])
        Assert.AreEqual(FSharpType.GetTupleElements(typeof<Tuple<string, int, int>>), [|typeof<System.String>; typeof<System.Int32>;typeof<System.Int32>|])
        
        // argument exception
        CheckThrowsArgumentException(fun () ->FSharpType.GetTupleElements(typeof<int>) |> ignore )
        
        // null
        CheckThrowsArgumentNullException(fun () ->FSharpType.GetTupleElements(null) |> ignore )
        
    [<Fact>]
    member _.GetUnionCases() =    
        // SingleCaseUnion
        let singlecaseUnionCaseInfoArray = FSharpType.GetUnionCases(typeof<SingleCaseDiscUnion>)  
        let (expectedSinglecaseinfo, singlevaluearray) = FSharpValue.GetUnionFields(singlecaseunion1, typeof<SingleCaseDiscUnion>)
        Assert.AreEqual(singlecaseUnionCaseInfoArray.[0], expectedSinglecaseinfo)
        
        // DiscUnionType
        let discunionCaseInfoArray = FSharpType.GetUnionCases(typeof<DiscUnionType<int>>) 
        let (expectedDuCaseinfoArray, duValueArray) = FSharpValue.GetUnionFields(discUniontypeB, typeof<DiscUnionType<int>>)
        Assert.AreEqual(discunionCaseInfoArray.[1], expectedDuCaseinfoArray)
        
         // null value
        CheckThrowsArgumentNullException(fun () ->FSharpType.GetUnionCases(null)|> ignore)
        
        // invalid value
        CheckThrowsArgumentException(fun () -> FSharpType.GetUnionCases(typeof<RecordType>) |> ignore)  
        
    [<Fact>]
    member _.IsExceptionRepresentation() =    
        
        // positive
        Assert.True(FSharpType.IsExceptionRepresentation(typeof<ExceptionInt>))
        Assert.True(FSharpType.IsExceptionRepresentation(typeof<DatalessException>))

        // negative
        Assert.False(FSharpType.IsExceptionRepresentation(typeof<int>))
        Assert.False(FSharpType.IsExceptionRepresentation(typeof<unit>))
        
        // null
        CheckThrowsArgumentNullException(fun () -> FSharpType.IsExceptionRepresentation(null) |> ignore )
        
    [<Fact>]
    member _.IsFunction() =    
        
        // positive       
        Assert.True(FSharpType.IsFunction(typeof<string -> int>))
        Assert.True(FSharpType.IsFunction(typeof<string -> int -> int>))
        
        // negative
        Assert.False(FSharpType.IsFunction(typeof<string>))
        
        // null
        CheckThrowsArgumentNullException(fun () -> FSharpType.IsFunction(null) |> ignore )

    [<Fact>]
    member _.IsModule() =   
    
        let getasm (t : Type) = t.Assembly
    
        // Positive Test
        let assemblyTypesPositive = (getasm (typeof<IsModule.IsModuleType>)).GetTypes()
        
        let moduleType =
            assemblyTypesPositive 
            |> Array.filter (fun ty -> ty.Name = "IsModule")
            |> (fun arr -> arr.[0])
        
        Assert.True(FSharpType.IsModule(moduleType)) //assemblyTypesPositive.[3] is Microsoft_FSharp_Reflection.FSharpModule which is module type
               
        // Negative Test 
        // FSharp Assembly
        let asmCore = getasm (typeof<Microsoft.FSharp.Collections.List<int>>)
        Assert.False(FSharpType.IsModule(asmCore.GetTypes().[0]))
        
        // .Net Assembly
        let asmSystem = getasm (typeof<System.DateTime>)
        Assert.False(FSharpType.IsModule(asmSystem.GetTypes().[0]))
        
        // custom Assembly
        let asmCustom = getasm (typeof<SingleCaseDiscUnion>)
        Assert.False(FSharpType.IsModule(asmCustom.GetTypes().[0]))
        
        // null
        CheckThrowsArgumentNullException(fun () -> FSharpType.IsModule(null) |> ignore )

    [<Fact>]
    member _.IsRecord() =    
        
        // positive       
        Assert.True(FSharpType.IsRecord(typeof<RecordType>))
        Assert.True(FSharpType.IsRecord(typeof<StructRecordType>))
        Assert.True(FSharpType.IsRecord(typeof<GenericRecordType<int, string>>))
        
        // negative
        Assert.False(FSharpType.IsRecord(typeof<int>))
        
        // null
        CheckThrowsArgumentNullException(fun () ->FSharpType.IsRecord(null) |> ignore )

    // Regression for 5588, Reflection: unit is still treated as a record type, but only if you pass BindingFlags.NonPublic
    [<Fact>]
    member _.``IsRecord.Regression5588``() =
        // negative
        Assert.False(FSharpType.IsRecord(typeof<unit>))
        Assert.False( FSharpType.IsRecord(typeof<unit>, System.Reflection.BindingFlags.NonPublic) )
        ()

    [<Fact>]
    member _.IsTuple() =
        // positive
        Assert.True(FSharpType.IsTuple(typeof<Tuple<int, int>>))
        Assert.True(FSharpType.IsTuple(typeof<Tuple<int, int, string>>))

        Assert.True(FSharpType.IsTuple(typeof<struct (int * int)>))
        Assert.True(FSharpType.IsTuple(typeof<struct (int* int * string)>))
        
        // negative
        Assert.False(FSharpType.IsTuple(typeof<int>))
        Assert.False(FSharpType.IsTuple(typeof<unit>))
        
        // null
        CheckThrowsArgumentNullException(fun () ->FSharpType.IsTuple(null) |> ignore )
        
    [<Fact>]
    member _.IsUnion() =    
        
        // positive       
        Assert.True(FSharpType.IsUnion(typeof<SingleCaseDiscUnion>))
        Assert.True(FSharpType.IsUnion(typeof<SingleCaseDiscStructUnion>))
        Assert.True(FSharpType.IsUnion(typeof<DiscUnionType<int>>))
        Assert.True(FSharpType.IsUnion(typeof<DiscStructUnionType<int>>))
        
        // negative
        Assert.False(FSharpType.IsUnion(typeof<int>))
        Assert.False(FSharpType.IsUnion(typeof<unit>))
        
        // null
        CheckThrowsArgumentNullException(fun () ->FSharpType.IsUnion(null) |> ignore )
        
    [<Fact>]
    member _.MakeFunctionType() =    
        
        // positive       
        Assert.AreEqual(FSharpType.MakeFunctionType(typeof<int>, typeof<string>), typeof<int ->string>)       
       
        // negative 
        Assert.AreNotEqual(FSharpType.MakeFunctionType(typeof<int>, typeof<string>), typeof<int ->string->int>)    
        
        // null
        CheckThrowsArgumentNullException(fun () ->FSharpType.MakeFunctionType(null, null) |> ignore )
        
    [<Fact>]
    member _.MakeTupleType() =    
               
        // positive
        Assert.AreEqual(FSharpType.MakeTupleType([|typeof<System.String>; typeof<System.Int32>|]), typeof<Tuple<string, int>>)
        
        // negative
        Assert.AreNotEqual(FSharpType.MakeTupleType([|typeof<System.String>; typeof<System.Int32>|]), typeof<Tuple<string, int, string>>)
        
        // null
        CheckThrowsArgumentException(fun () ->FSharpType.MakeTupleType([|null;null|]) |> ignore )

    [<Fact>]
    member _.MakeStructTupleType() =    
        let asm = typeof<struct (string * int)>.Assembly
        // positive
        Assert.AreEqual(FSharpType.MakeStructTupleType(asm, [|typeof<System.String>; typeof<System.Int32>|]), typeof<struct (string * int)>)
        
        // negative
        Assert.AreNotEqual(FSharpType.MakeStructTupleType(asm, [|typeof<System.String>; typeof<System.Int32>|]), typeof<struct (string * int * string)>)
        
        // null
        CheckThrowsArgumentException(fun () ->FSharpType.MakeStructTupleType(asm, [|null;null|]) |> ignore )

type UnionCaseInfoTests() =    
    
    let singlenullarycaseunion = SingleNullaryCaseDiscUnion.SingleNullaryCaseTag

    let singlecaseunion1 = SingleCaseDiscUnion.SingleCaseTag(1.0, 2.0, 3.0)
    let singlecaseunion2 = SingleCaseDiscUnion.SingleCaseTag(4.0, 5.0, 6.0)
    
    let discUniontypeA = DiscUnionType<int>.A
    let discUniontypeB = DiscUnionType<int>.B(1, Some(discUniontypeA))
    let discUniontypeC = DiscUnionType<float>.C(1.0, "stringparam")
    
    let recDiscUniontypeB = DiscUnionType<int>.B(1, Some(discUniontypeB))
    
    let ((singlenullarycaseinfo:UnionCaseInfo), singlenullaryvaluearray) = FSharpValue.GetUnionFields(singlenullarycaseunion, typeof<SingleNullaryCaseDiscUnion>)

    let ((singlecaseinfo:UnionCaseInfo), singlevaluearray) = FSharpValue.GetUnionFields(singlecaseunion1, typeof<SingleCaseDiscUnion>)
    
    let ((discUnionInfoA:UnionCaseInfo), discvaluearray) = FSharpValue.GetUnionFields(discUniontypeA, typeof<DiscUnionType<int>>)
    let ((discUnionInfoB:UnionCaseInfo), discvaluearray) = FSharpValue.GetUnionFields(discUniontypeB, typeof<DiscUnionType<int>>)
    let ((discUnionInfoC:UnionCaseInfo), discvaluearray) = FSharpValue.GetUnionFields(discUniontypeC, typeof<DiscUnionType<float>>)
    
    let ((recDiscCaseinfo:UnionCaseInfo), recDiscCasevaluearray) = FSharpValue.GetUnionFields(recDiscUniontypeB, typeof<DiscUnionType<int>>)
    
    [<Fact>]
    member _.Equals() =   
        //positive
        // single case
        Assert.True(singlecaseinfo.Equals(singlecaseinfo))
        
        // disc union
        Assert.True(discUnionInfoA.Equals(discUnionInfoA))
        
        // rec disc union
        Assert.True(recDiscCaseinfo.Equals(recDiscCaseinfo))
                
        // negative
        // single case
        Assert.False(singlecaseinfo.Equals(discUnionInfoA))
        
        // disc union
        Assert.False(discUnionInfoA.Equals(discUnionInfoB))
        
        // rec disc union
        Assert.False(recDiscCaseinfo.Equals(discUnionInfoA))
        
        // null
        Assert.False(singlecaseinfo.Equals(null))
        
    [<Fact>]
    member _.GetCustomAttributes() =   
        
        // single case
        let singlecaseAttribute  = (singlecaseinfo.GetCustomAttributes()).[0] :?> Attribute
        Assert.AreEqual(singlecaseAttribute.ToString(), "Microsoft.FSharp.Core.CompilationMappingAttribute" )
        
        // disc union
        let discunionAttribute  = (discUnionInfoA.GetCustomAttributes()).[0] :?> Attribute
        Assert.AreEqual(discunionAttribute.ToString(), "Microsoft.FSharp.Core.CompilationMappingAttribute" )
        
        // rec disc union
        let recdiscAttribute  = (recDiscCaseinfo.GetCustomAttributes()).[0] :?> Attribute
        Assert.AreEqual(recdiscAttribute.ToString(), "Microsoft.FSharp.Core.CompilationMappingAttribute" )
        
        // null
        CheckThrowsArgumentNullException(fun () -> singlecaseinfo.GetCustomAttributes(null) |> ignore )    
        
    [<Fact>]
    member _.GetFields() =   
        
        // single case
        let singlecaseFieldInfo  = (singlecaseinfo.GetFields()).[0]
        Assert.AreEqual(singlecaseFieldInfo.PropertyType , typeof<(float)>)
        
        // disc union null empty
        let discunionFieldInfoEpt  = discUnionInfoA.GetFields()
        Assert.AreEqual(discunionFieldInfoEpt.Length , 0)
        
        // disc union int
        let discunionFieldInfo  = (discUnionInfoB.GetFields()).[0] 
        Assert.AreEqual(discunionFieldInfo.PropertyType , typeof<int>)
        
        // rec disc union
        let recdiscFieldInfo  = (recDiscCaseinfo.GetFields()).[0] 
        Assert.AreEqual(recdiscFieldInfo.PropertyType , typeof<int>)
        
    [<Fact>]
    member _.GetHashCode() =   
        
        // positive
        // single case
        Assert.AreEqual(singlecaseinfo.GetHashCode(), singlecaseinfo.GetHashCode())
        
        // disc union
   
        Assert.AreEqual(discUnionInfoA.GetHashCode(), discUnionInfoA.GetHashCode())
        
        // rec disc union
  
        Assert.AreEqual(recDiscCaseinfo.GetHashCode(), recDiscCaseinfo.GetHashCode())
        
        // negative
        // disc union
        Assert.AreNotEqual(discUnionInfoA.GetHashCode(), discUnionInfoB.GetHashCode())
        
    [<Fact>]
    member _.GetType() =   
  
        // single case
        Assert.AreEqual(singlecaseinfo.GetType(), typeof<UnionCaseInfo> )
        
        // disc union
        Assert.AreEqual(discUnionInfoA.GetType(), typeof<UnionCaseInfo> )
        
        // rec disc union
        Assert.AreEqual(recDiscCaseinfo.GetType(), typeof<UnionCaseInfo> )  
    
    [<Fact>]
    member _.ToString() =   
        
        // single case
        Assert.AreEqual(singlenullarycaseinfo.ToString(), "SingleNullaryCaseDiscUnion.SingleNullaryCaseTag")

        // single case
        Assert.AreEqual(singlecaseinfo.ToString(), "SingleCaseDiscUnion.SingleCaseTag")
        
        // disc union
        Assert.True((discUnionInfoA.ToString()).Contains("DiscUnionType") )
        
        // rec disc union
        Assert.True((recDiscCaseinfo.ToString()).Contains("DiscUnionType"))
