// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.ComponentTests.ErrorMessages

open Xunit
open FSharp.Test.Utilities.Compiler


module Modules =

    [<Fact>]
    let ``Public Module Abbreviation``() =
        FSharp "module public L1 = List"
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 536, Line 1, Col 1, Line 1, Col 7,
                                 "The 'Public' accessibility attribute is not allowed on module abbreviation. Module abbreviations are always private.")

    [<Fact>]
    let ``Private Module Abbreviation``() =
        FSharp "module private L1 = List"
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 536, Line 1, Col 1, Line 1, Col 7,
                                 "The 'Private' accessibility attribute is not allowed on module abbreviation. Module abbreviations are always private.")
                                 
    [<Fact>]
    let ``Internal Module Abbreviation``() =
        FSharp "module internal L1 = List"
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 536, Line 1, Col 1, Line 1, Col 7,
                                 "The 'Internal' accessibility attribute is not allowed on module abbreviation. Module abbreviations are always private.")
    [<Fact>]
    let ``Rec Module Abbreviation``() =
        FSharp "module rec L1 = List"
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 3203, Line 1, Col 1, Line 1, Col 14,
                                 "Invalid use of 'rec' keyword")

    [<Fact>]
    let ``Left Attribute Module Abbreviation``() =
        FSharp """[<Experimental "Hello">] module L1 = List"""
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 535, Line 1, Col 1, Line 1, Col 32,
                                 "Ignoring attributes on module abbreviation")
                                 
    [<Fact>]
    let ``Right Attribute Module Abbreviation``() =
        FSharp """module [<Experimental "Hello">] L1 = List"""
        |> withLangVersionPreview
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 535, Line 1, Col 1, Line 1, Col 35,
                                 "Ignoring attributes on module abbreviation")
                                 
    [<Fact>]
    let ``Right Attribute Module Abbreviation without preview (typecheck)``() =
        FSharp """module [<Experimental "Hello">] L1 = List"""
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 535, Line 1, Col 1, Line 1, Col 35,
                                 "Ignoring attributes on module abbreviation")
    [<Fact>]
    let ``Right Attribute Module Abbreviation without preview (compile)``() =
        FSharp """module [<Experimental "Hello">] L1 = List"""
        |> compile
        |> shouldFail
        |> withDiagnostics [
            Error 3350, Line 1, Col 33, Line 1, Col 35, "Feature 'attributes to the right of the 'module' keyword' is not available in F# 5.0. Please use language version 'preview' or greater."
            Error 535, Line 1, Col 1, Line 1, Col 35, "Ignoring attributes on module abbreviation"
            Error 222, Line 1, Col 1, Line 1, Col 42, "Files in libraries or multiple-file applications must begin with a namespace or module declaration, e.g. 'namespace SomeNamespace.SubNamespace' or 'module SomeNamespace.SomeModule'. Only the last source file of an application may omit such a declaration."
        ]
                                 
    [<Fact>]
    let ``Attribute Module Abbreviation``() =
        FSharp """[<System.Obsolete "Hi">] module [<Experimental "Hello">] internal L1 = List"""
        |> withLangVersionPreview
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 535, Line 1, Col 1, Line 1, Col 32,
                                 "Ignoring attributes on module abbreviation")
                                 
    [<Fact>]
    let ``Attributes applied successfully``() =
        Fsx """
[<AutoOpen>] module [<Experimental "Hello">] rec L1 = type L2() = do ()
match typeof<L2>.DeclaringType.GetCustomAttributes false with
| [|:? AutoOpenAttribute; :? ExperimentalAttribute as experimental; :? CompilationMappingAttribute as compilationMapping|] ->
    if compilationMapping.SourceConstructFlags <> SourceConstructFlags.Module then failwithf "CompilationMapping attribute did not contain the correct SourceConstructFlags: %A" compilationMapping.SourceConstructFlags
    if experimental.Message <> "Hello" then failwithf "Experimental attribute did not contain the correct message: %s" experimental.Message
| t -> failwithf "Attribute array is not of length 3 and correct types: %A" t
         """
        |> withLangVersionPreview
        |> compileExeAndRun
        |> shouldSucceed
    [<Fact>]
    let ``Attributes applied successfully 2``() =
        Fsx """
open System
open System.ComponentModel
[<Obsolete "Hi"; Browsable false>] [<Bindable true; AmbientValue false>] module [<Experimental "Hello"; Category "Oi">][<DefaultValue "Howdy"; AutoOpen>] private rec L1 = type L2() = do ()
match typeof<L2>.DeclaringType.GetCustomAttributes false with
| [|:? ObsoleteAttribute as obsolete
    :? BrowsableAttribute as browsable
    :? BindableAttribute as bindable
    :? AmbientValueAttribute as ambientValue
    :? ExperimentalAttribute as experimental
    :? CategoryAttribute as category
    :? DefaultValueAttribute as defaultValue
    :? AutoOpenAttribute
    :? CompilationMappingAttribute as compilationMapping|] ->
    if obsolete.Message <> "Hi" then failwithf "Obsolete attribute did not contain the correct message: %s" obsolete.Message
    if browsable.Browsable <> false then failwithf "Browsable attribute did not contain the correct flag: %b" browsable.Browsable
    if bindable.Bindable <> true then failwithf "Bindable attribute did not contain the correct flag: %b" bindable.Bindable
    if ambientValue.Value <> box false then failwithf "AmbientValue attribute did not contain the correct value: %O" ambientValue.Value
    if experimental.Message <> "Hello" then failwithf "Experimental attribute did not contain the correct message: %s" experimental.Message
    if category.Category <> "Oi" then failwithf "Category attribute did not contain the correct category: %s" category.Category
    if defaultValue.Value <> box "Howdy" then failwithf "DefaultValue attribute did not contain the correct value: %O" defaultValue.Value
    if compilationMapping.SourceConstructFlags <> SourceConstructFlags.Module then failwithf "CompilationMapping attribute did not contain the correct SourceConstructFlags: %O" compilationMapping.SourceConstructFlags
| t -> failwithf "Attribute array is not of length 9 and correct types: %A" t
         """
        |> withLangVersionPreview
        |> compileExeAndRun
        |> shouldSucceed
    [<Fact>]
    let ``Fun attribute indentation``() =
        Fsx """
open System
open System.ComponentModel
[<Obsolete "Hi"; Browsable false>][<Bindable true; AmbientValue false>] module
                                                                         [<Experimental "Hello";
                                                                         Category "Oi"
                                                                         >]
                                                                         [<DefaultValue "Howdy"; AutoOpen>]
                                                                         private
                                                                         rec
                                                                         L1
                                                                         =
                                                                         type
                                                                          [<Bindable true;
                                                                          AmbientValue false
                                                                          >] 
                                                                          [<DefaultValue "Howdy"; AutoOpen>]
                                                                          L2
                                                                          ()
                                                                          =
                                                                          let
                                                                           [<DefaultValue "Howdy";
                                                                           Browsable false
                                                                           >]
                                                                           [<AmbientValue false; Category "Oi">]
                                                                           a
                                                                           =
                                                                           1
                                                                          member _.A = a
match typeof<L2>.DeclaringType.GetCustomAttributes false with
| [|:? ObsoleteAttribute as obsolete
    :? BrowsableAttribute as browsable
    :? BindableAttribute as bindable
    :? AmbientValueAttribute as ambientValue
    :? ExperimentalAttribute as experimental
    :? CategoryAttribute as category
    :? DefaultValueAttribute as defaultValue
    :? AutoOpenAttribute
    :? CompilationMappingAttribute as compilationMapping|] ->
    if obsolete.Message <> "Hi" then failwithf "Obsolete attribute did not contain the correct message: %s" obsolete.Message
    if browsable.Browsable <> false then failwithf "Browsable attribute did not contain the correct flag: %b" browsable.Browsable
    if bindable.Bindable <> true then failwithf "Bindable attribute did not contain the correct flag: %b" bindable.Bindable
    if ambientValue.Value <> box false then failwithf "AmbientValue attribute did not contain the correct value: %O" ambientValue.Value
    if experimental.Message <> "Hello" then failwithf "Experimental attribute did not contain the correct message: %s" experimental.Message
    if category.Category <> "Oi" then failwithf "Category attribute did not contain the correct category: %s" category.Category
    if defaultValue.Value <> box "Howdy" then failwithf "DefaultValue attribute did not contain the correct value: %O" defaultValue.Value
    if compilationMapping.SourceConstructFlags <> SourceConstructFlags.Module then failwithf "CompilationMapping attribute did not contain the correct SourceConstructFlags: %O" compilationMapping.SourceConstructFlags
| t -> failwithf "Attribute array is not of length 9 and correct types: %A" t
         """
        |> withLangVersionPreview
        |> compileExeAndRun
        |> shouldSucceed
    
    [<Fact>]
    let ``Offside rule works for attributes inside module declarations``() =
        Fsx """
module [<
AutoOpen>] L1 = do ()
        """
        |> typecheck
        |> shouldFail
        |> withSingleDiagnostic (Error 10, Line 3, Col 1, Line 3, Col 9,
                                 "Unexpected start of structured construct in attribute list")
    [<Fact>]
    let ``Offside rule works for attributes inside module declarations without preview``() =
        Fsx """
module [<
AutoOpen>] L1 = do ()
        """
        |> compile
        |> shouldFail
        |> withDiagnostics [
            Error 10, Line 3, Col 1, Line 3, Col 9, "Unexpected start of structured construct in attribute list"
            Error 3350, Line 3, Col 1, Line 3, Col 9, "Feature 'attributes to the right of the 'module' keyword' is not available in F# 5.0. Please use language version 'preview' or greater."
        ]