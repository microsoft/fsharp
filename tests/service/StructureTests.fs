#if INTERACTIVE
#r "../../artifacts/bin/fcs/net461/FSharp.Compiler.Service.dll" // note, build FSharp.Compiler.Service.Tests.fsproj to generate this, this DLL has a public API so can be used from F# Interactive
#r "../../artifacts/bin/fcs/net461/nunit.framework.dll"
#load "FsUnit.fs"
#load "Common.fs"
#else
module Tests.Service.StructureTests
#endif

open System.IO
open NUnit.Framework
open FSharp.Compiler.EditorServices
open FSharp.Compiler.EditorServices.Structure
open FSharp.Compiler.Service.Tests.Common
open FSharp.Compiler.Text
open System.Text

let fileName = Path.Combine (__SOURCE_DIRECTORY__, __SOURCE_FILE__)
type Line = int
type Col = int
type Range = Line * Col * Line * Col

let (=>) (source: string) (expectedRanges: (Range * Range) list) =
    let lines =
        use reader = new StringReader(source)
        [| let line = ref (reader.ReadLine())
           while not (isNull !line) do
               yield !line
               line := reader.ReadLine()
           if source.EndsWith "\n" then
               // last trailing space not returned
               // http://stackoverflow.com/questions/19365404/stringreader-omits-trailing-linebreak
               yield "" |]

    let formatList (xs: _ list) =
        let sb = StringBuilder("[ ")
        for r in xs do
            sb.AppendLine (sprintf "%A" r) |> ignore
        sprintf "%O ]" sb

    let getRange (r: range) = (r.StartLine, r.StartColumn, r.EndLine, r.EndColumn)

    let ast = parseSourceCode(fileName, source)
    try
        let actual =
            Structure.getOutliningRanges lines ast
            |> Seq.filter (fun sr -> sr.Range.StartLine <> sr.Range.EndLine)
            |> Seq.map (fun sr -> getRange sr.Range, getRange sr.CollapseRange)
            |> Seq.sort
            |> List.ofSeq
        let expected = List.sort expectedRanges
        if actual <> expected then
            failwithf "Expected %s, but was %s" (formatList expected) (formatList actual)
    with _ ->
        printfn "AST:\n%+A" ast
        reraise()

[<Test>]
let ``empty file``() = "" => []

[<Test>]
let ``nested module``() =
    """
module MyModule =
    ()

[<Foo>]
module Module =
    ()
"""
    => [ (2, 0, 3, 6), (2, 15, 3, 6)
         (5, 0, 7, 6), (6, 13, 7, 6) ]

[<Test>]
let ``module with multiline function``() =
    """
module MyModule =
    let foo() =
        foo()
"""
    => [ (2, 0, 4, 13), (2, 15, 4, 13)
         (3, 4, 4, 13), (3, 13, 4, 13)
         (3, 8, 4, 13), (3, 13, 4, 13) ]

[<Test>]
let ``DU``() =
    """
type Color =
    | Red
    | Green
    | Blue
"""
    => [ (2, 5, 5, 10), (2, 11, 5, 10)
         (3, 4, 5, 10), (3, 4, 5, 10) ]

[<Test>]
let ``DU with interface``() =
    """
type Color =
    | Red
    | Green
    | Blue

    interface IDisposable with
        member __.Dispose() =
            (docEventListener :> IDisposable).Dispose()
"""
    => [ (2, 5, 9, 55), (2, 11, 9, 55)
         (3, 4, 5, 10), (3, 4, 5, 10)
         (7, 4, 9, 55), (7, 25, 9, 55)
         (8, 8, 9, 55), (8, 27, 9, 55)
         (8, 15, 9, 55), (8, 27, 9, 55) ]

[<Test>]
let ``record with interface``() =
    """
type Color =
    { Red:
          int
      mutable Blue:
          int }

    interface IDisposable with
        member __.Dispose() =
            (docEventListener :> IDisposable).Dispose()
"""
    =>
    [ (2, 5, 10, 55), (2, 11, 10, 55)
      (3, 4, 6, 15), (3, 4, 6, 15)
      (3, 6, 4, 13), (3, 6, 4, 13)
      (5, 6, 6, 13), (5, 6, 6, 13)
      (8, 4, 10, 55), (8, 25, 10, 55)
      (9, 8, 10, 55), (9, 27, 10, 55)
      (9, 15, 10, 55), (9, 27, 10, 55) ]

[<Test>]
let ``type with a do block``() =
    """
type Color() =   // 2
    let foo() =
        ()

    do
        foo()
        ()       // 8
"""
    => [ (2, 5, 8, 10), (2, 11, 8, 10)
         (3, 8, 4, 10), (3, 13, 4, 10)
         (6, 4, 8, 10), (6, 6, 8, 10) ]

[<Test>]
let ``complex outlining test``() =
    """
module MyModule =       // 2
    let foo() = ()
    let bar() =
        ()

    type Color =        // 7
        { Red: int
          Green: int
          Blue: int 
        }

        interface IDisposable with      // 13
            member __.Dispose() =
                (docEventListener :> IDisposable).Dispose()

    module MyInnerModule =              // 17

        type RecordColor =              // 19
            { Red: int
              Green: int
              Blue: int 
            }

            interface IDisposable with  // 25
                member __.Dispose() =
                    (docEventListener :> IDisposable).Dispose()
""" 
    => [ (2, 0, 27, 63), (2, 15, 27, 63)
         (4, 4, 5, 10), (4, 13, 5, 10)
         (4, 8, 5, 10), (4, 13, 5, 10)
         (7, 9, 15, 59), (7, 15, 15, 59)
         (8, 8, 11, 9), (8, 8, 11, 9)
         (13, 8, 15, 59), (13, 29, 15, 59)
         (14, 12, 15, 59), (14, 31, 15, 59)
         (14, 19, 15, 59), (14, 31, 15, 59)
         (17, 4, 27, 63), (17, 24, 27, 63)
         (19, 13, 27, 63), (19, 25, 27, 63)
         (20, 12, 23, 13), (20, 12, 23, 13)
         (25, 12, 27, 63), (25, 33, 27, 63)
         (26, 16, 27, 63), (26, 35, 27, 63)
         (26, 23, 27, 63), (26, 35, 27, 63) ]

    
[<Test>]
let ``open statements``() =
    """
open M             
open N             
                   
module M =         
    let x = 1      
                   
    open M         
    open N         
                   
    module M =     
        open M     
                   
        let x = 1  
                   
    module M =     
        open M     
        open N     
        let x = 1  
                   
open M             
open N             
open H             
                   
open G             
open H              
"""
    => [ (2, 0, 3, 6), (2, 0, 3, 6)
         (5, 0, 19, 17), (5, 8, 19, 17)
         (8, 4, 9, 10), (8, 4, 9, 10)
         (11, 4, 14, 17), (11, 12, 14, 17)
         (16, 4, 19, 17), (16, 12, 19, 17)
         (17, 8, 18, 14), (17, 8, 18, 14)
         (21, 0, 26, 6), (21, 0, 26, 6) ]

[<Test>]
let ``hash directives``() =
    """
#r @"a"   
#r "b"    
          
#r "c"    
          
#r "d"    
#r "e"    
let x = 1 
          
#r "f"    
#r "g"    
#load "x" 
#r "y"    
          
#load "a" 
      "b" 
      "c" 
          
#load "a" 
      "b" 
      "c" 
#r "d"     
"""
    => [ (2, 3, 8, 6), (2, 3, 8, 6)
         (11, 3, 23, 6), (11, 3, 23, 6) ]

[<Test>]
let ``nested let bindings``() =
    """
let f x =       // 2
    let g x =   // 3
        let h = // 4
            ()  // 5
        ()      // 6
    x           // 7
"""
    => [ (2, 0, 7, 5), (2, 7, 7, 5)
         (2, 4, 7, 5), (2, 7, 7, 5)
         (3, 8, 6, 10), (3, 11, 6, 10)
         (4, 12, 5, 14), (4, 13, 5, 14) ]

[<Test>]
let ``match``() =
    """
match None with     // 2
| Some _ ->         // 3
    ()              // 4
| None ->           // 5
    match None with // 6
    | Some _ -> ()  // 7
    | None ->       // 8
        let x = ()  // 9
        ()          // 10
"""
    => [ (2, 0, 10, 10), (2, 15, 10, 10)
         (6, 4, 10, 10), (5, 6, 10, 10)
         (6, 4, 10, 10), (6, 19, 10, 10)
         (9, 8, 10, 10), (8, 10, 10, 10) ]

[<Test>]
let ``matchbang``() =
    """
async {                                   // 2
    match! async { return None } with     // 3
    | Some _ ->                           // 4
        ()                                // 5
    | None ->                             // 6
        match None with                   // 7
        | Some _ -> ()                    // 8
        | None ->                         // 9
            let x = ()                    // 10
            ()                            // 11
}                                         // 12
"""
    => [ (2, 0, 12, 1), (2, 7, 12, 0)
         (3, 4, 11, 14), (3, 37, 11, 14)
         (7, 8, 11, 14), (6, 10, 11, 14)
         (7, 8, 11, 14), (7, 23, 11, 14)
         (10, 12, 11, 14), (9, 14, 11, 14) ]
         
[<Test>]
let ``computation expressions``() =
    """
seq {              // 2
    yield ()       // 3
    let f x =      // 4
        ()         // 5
    yield! seq {   // 6
        yield () } // 7
}                  // 8
"""
    => [ (2, 0, 8, 1), (2, 5, 8, 0)
         (4, 8, 5, 10), (4, 11, 5, 10)
         (6, 4, 7, 18), (6, 4, 7, 18)
         (6, 11, 7, 18), (6, 16, 7, 17) ]

[<Test>]
let ``list``() =
    """
let _ = 
    [ 1; 2
      3 ]
"""
  => [ (2, 0, 4, 9), (2, 5, 4, 9)
       (2, 4, 4, 9), (2, 5, 4, 9)
       (3, 4, 4, 9), (3, 5, 4, 8) ]

[<Test>]
let ``object expressions``() =
    """
let _ =
    { new System.IDisposable with
        member __.Dispose() = () }
"""
    => [ (2, 0, 4, 34), (2, 5, 4, 34)
         (2, 4, 4, 34), (2, 5, 4, 34)
         (3, 4, 4, 34), (3, 28, 4, 34) ]
         
[<Test>]
let ``try - with``() =
    """
try           // 2
    let f x = // 3
        ()    // 4
with _ ->     // 5
    let f x = // 6
        ()    // 7
    ()        // 8
"""
    => [ (2, 0, 5, 0), (2, 3, 5, 0)
         (2, 0, 8, 6), (2, 3, 8, 6)
         (3, 8, 4, 10), (3, 11, 4, 10)
         (5, 0, 8, 6), (5, 4, 8, 6)
         (6, 4, 8, 6), (5, 6, 8, 6)
         (6, 8, 7, 10), (6, 11, 7, 10) ]

[<Test>]
let ``try - finally``() =
    """
try           // 2
    let f x = // 3
        ()    // 4
finally       // 5
    let f x = // 6
        ()    // 7
    ()        // 8
"""
    => [ (2, 0, 8, 6), (2, 3, 8, 6)
         (3, 8, 4, 10), (3, 11, 4, 10)
         (5, 0, 8, 6), (5, 7, 8, 6)
         (6, 8, 7, 10), (6, 11, 7, 10) ]

[<Test>]
let ``if - then - else``() =
    """
if true then
    let f x = 
        ()
    ()
else
    let f x =
        ()
    ()
"""
    => [ (2, 0, 9, 6), (2, 7, 9, 6)
         (2, 8, 5, 6), (2, 12, 5, 6)
         (3, 8, 4, 10), (3, 11, 4, 10)
         (7, 8, 8, 10), (7, 11, 8, 10) ]

[<Test>]
let ``code quotation``() =
    """
<@
  "code"
        @>
"""
    => [ (2, 0, 4, 10), (2, 2, 4, 8) ]

[<Test>]
let ``raw code quotation``() =
    """
<@@
  "code"
        @@>
"""
    => [ (2, 0, 4, 11), (2, 3, 4, 8) ]

[<Test>]
let ``match lambda aka function``() =
    """
function
| 0 ->  ()
        ()
"""
    => [ (2, 0, 4, 10), (2, 8, 4, 10)
         (3, 8, 4, 10), (3, 3, 4, 10) ]

[<Test>]
let ``match guarded clause``() =
    """
let matchwith num =
    match num with
    | 0 -> ()
           ()
"""
    =>  [ (2, 0, 5, 13), (2, 17, 5, 13)
          (2, 4, 5, 13), (2, 17, 5, 13)
          (3, 4, 5, 13), (3, 18, 5, 13)
          (4, 11, 5, 13), (4, 7, 5, 13) ]

[<Test>]
let ``for loop``() =
    """
for x = 100 downto 10 do
    ()
    ()
"""
    => [ (2, 0, 4, 6), (2, 0, 4, 6) ]

[<Test>]
let ``for each``() =
    """
for x in 0 .. 100 -> 
            ()
            ()
"""
    =>  [ (2, 0, 4, 14), (2, 0, 4, 14)
          (2, 18, 4, 14), (2, 18, 4, 14) ]
   
[<Test>]
let ``tuple``() =
    """
( 20340
, 322
, 123123 )
"""
    => [ (2, 2, 4, 8), (2, 2, 4, 8) ]

[<Test>]
let ``do!``() =
    """
do! 
    printfn "allo"
    printfn "allo"
"""
    =>  [ (2, 0, 4, 18), (2, 3, 4, 18) ]

[<Test>]
let ``cexpr yield yield!``() =
    """
cexpr{
    yield! 
        cexpr{
                    yield 
                                
                        10
                }
    }
"""
    =>  [ (2, 0, 9, 5), (2, 6, 9, 4)
          (3, 4, 8, 17), (3, 4, 8, 17)
          (4, 8, 8, 17), (4, 14, 8, 16)
          (5, 20, 7, 26), (5, 20, 7, 26) ]

[<Test>]
let ``XML doc comments``() =
    """
/// Line 1
/// Line 2
module M =
    /// Line 3
    /// Line 4
    type T() =
        /// Line 5
        /// Line 6
        /// Line 7
        let f x = x
    /// Single line comment
    let f x = x
"""
    => [ (2, 0, 3, 10), (2, 0, 3, 10)
         (4, 0, 13, 15), (4, 8, 13, 15)
         (5, 4, 6, 14), (5, 4, 6, 14)
         (7, 9, 11, 19), (7, 11, 11, 19)
         (8, 8, 10, 18), (8, 8, 10, 18) ]
         
[<Test>]
let ``regular comments``() =
    """
// Line 1
// Line 2
module M =
    // Line 3
    // Line 4
    type T() =
        // Line 5
        // Line 6
        // Line 7
        let f x = x
    // Single line comment
    let f x = x
"""
    => [ (2, 0, 3, 9), (2, 0, 3, 9)
         (4, 0, 13, 15), (4, 8, 13, 15)
         (5, 4, 6, 13), (5, 4, 6, 13)
         (7, 9, 11, 19), (7, 11, 11, 19)
         (8, 8, 10, 17), (8, 8, 10, 17) ]
         
[<Test>]
let ``XML doc and regular comments in one block``() =
    """
// Line 1
// Line 2
/// Line 3
/// Line 4
// Line 5
/// Line 6
/// Line 7
/// Line 8
/// Line 9
"""
    => [ (2, 0, 3, 9), (2, 0, 3, 9)
         (4, 0, 5, 10), (4, 0, 5, 10)
         (7, 0, 10, 10), (7, 0, 10, 10) ]

[<Test>]
let ``constructor call``() =
    """
module M =
    let s =
        new System.String(
            'c',
            1)
"""
    => [ (2, 0, 6, 14), (2, 8, 6, 14)
         (3, 4, 6, 14), (3, 9, 6, 14)
         (3, 8, 6, 14), (3, 9, 6, 14)
         (4, 8, 6, 14), (4, 25, 6, 14)
         (5, 12, 6, 13), (5, 12, 6, 13) ]

[<Test>]
let ``Top level module`` () =
    """
module TopLevelModule

module Nested =
    let x = 123
"""
    => [ (2, 7, 5, 15), (2, 21, 5, 15)
         (4, 0, 5, 15), (4, 13, 5, 15) ]

[<Test>]
let ``Top level namespace`` () =
    """
namespace TopLevelNamespace.Another

module Nested =
    let x = 123
"""
    => [ (4, 0, 5, 15), (4, 13, 5, 15) ]

[<Test>]
let ``Multiple namespaces`` () =
    """
namespace TopLevelNamespace.Another

module Nested =
    let x = 123

namespace AnotherTopLevel.Nested

module NestedModule =
    let x = 123
"""
    => [ (4, 0, 5, 15), (4, 13, 5, 15)
         (9, 0, 10, 15), (9, 19, 10, 15) ]

[<Test>]
let ``Member val`` () =
    """
type T() =
    member val field1 =
        ()

    [<CompiledName("Field2")>]
    member val field2 =
        ()

    static member val field3 =
        ()

    [<CompiledName("Field4")>]
    static member val field4 =
        ()
"""
    => [ (2, 5, 15, 10), (2, 7, 15, 10)
         (3, 4, 4, 10), (3, 4, 4, 10)
         (6, 4, 8, 10), (6, 4, 8, 10)
         (10, 4, 11, 10), (10, 4, 11, 10)
         (13, 4, 15, 10), (13, 4, 15, 10) ]

[<Test>]
let ``Secondary constructors`` () =
    """
type T() =
    new () =
        T ()

    internal new () =
        T ()

    [<Foo>]
    new () =
        T ()
"""
    => [ (2, 5, 11, 12), (2, 7, 11, 12)
         (3, 4, 4, 12), (3, 7, 4, 12)
         (3, 4, 4, 12), (3, 10, 4, 12)
         (6, 4, 7, 12), (6, 16, 7, 12)
         (6, 4, 7, 12), (6, 19, 7, 12)
         (9, 4, 11, 12), (10, 7, 11, 12)
         (9, 4, 11, 12), (10, 10, 11, 12) ]


[<Test>]
let ``Abstract members`` () =
    """
type T() =
    abstract Foo:
        int
    
    [<Foo>]
    abstract Foo:
        int
"""
    => [ (2, 5, 8, 11), (2, 7, 8, 11)
         (3, 4, 4, 11), (4, 8, 4, 11)
         (6, 4, 8, 11), (8, 8, 8, 11) ]
