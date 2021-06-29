// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

//----------------------------------------------------------------------------
// Open up the compiler as an incremental service for parsing,
// type checking and intellisense-like environment-reporting.
//--------------------------------------------------------------------------

namespace FSharp.Compiler.EditorServices

open System
open System.Collections.Generic
open FSharp.Compiler.Syntax
open FSharp.Compiler.SyntaxTreeOps
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range

/// Represents the different kinds of items that can appear in the navigation bar
[<RequireQualifiedAccess>]
type NavigationItemKind =
    | Namespace
    | ModuleFile
    | Exception
    | Module
    | Type
    | Method
    | Property
    | Field
    | Other

[<RequireQualifiedAccess>]
type NavigationEntityKind =
    | Namespace
    | Module
    | Class
    | Exception
    | Interface
    | Record
    | Enum
    | Union

/// Represents an item to be displayed in the navigation bar
[<Sealed>]
type NavigationItem(uniqueName: string, name: string, kind: NavigationItemKind, glyph: FSharpGlyph, range: range, 
                                     bodyRange: range, singleTopLevel: bool, enclosingEntityKind: NavigationEntityKind, isAbstract: bool, access: SynAccess option) = 
    
    member _.bodyRange = bodyRange
    member _.UniqueName = uniqueName
    member _.Name = name
    member _.Glyph = glyph
    member _.Kind = kind
    member _.Range = range
    member _.BodyRange = bodyRange 
    member _.IsSingleTopLevel = singleTopLevel
    member _.EnclosingEntityKind = enclosingEntityKind
    member _.IsAbstract = isAbstract
    
    member _.Access = access
    
    member _.WithUniqueName(uniqueName: string) =
      NavigationItem(uniqueName, name, kind, glyph, range, bodyRange, singleTopLevel, enclosingEntityKind, isAbstract, access)
    static member Create(name: string, kind, glyph: FSharpGlyph, range: range, bodyRange: range, singleTopLevel: bool, enclosingEntityKind, isAbstract, access: SynAccess option) = 
      NavigationItem("", name, kind, glyph, range, bodyRange, singleTopLevel, enclosingEntityKind, isAbstract, access)

/// Represents top-level declarations (that should be in the type drop-down)
/// with nested declarations (that can be shown in the member drop-down)
[<NoEquality; NoComparison>]
type NavigationTopLevelDeclaration = 
    { Declaration: NavigationItem
      Nested: NavigationItem[] }
      
/// Represents result of 'GetNavigationItems' operation - this contains
/// all the members and currently selected indices. First level correspond to
/// types & modules and second level are methods etc.
[<Sealed>]
type NavigationItems(declarations:NavigationTopLevelDeclaration[]) =
    member _.Declarations = declarations

module NavigationImpl =
    let unionRangesChecked r1 r2 =
        if Range.equals r1 range.Zero then r2
        elif Range.equals r2 range.Zero then r1
        else unionRanges r1 r2
    
    let rangeOfDecls2 f decls = 
        match decls |> List.map (f >> (fun (d:NavigationItem) -> d.bodyRange)) with 
        | hd :: tl -> tl |> List.fold unionRangesChecked hd
        | [] -> range.Zero
    
    let rangeOfDecls = rangeOfDecls2 fst

    let moduleRange (idm:range) others = 
      unionRangesChecked idm.EndRange (rangeOfDecls2 (fun (a, _, _) -> a) others)
    
    let fldspecRange fldspec =
      match fldspec with
      | SynUnionCaseKind.Fields(flds) -> flds |> List.fold (fun st (SynField(_, _, _, _, _, _, _, m)) -> unionRangesChecked m st) range.Zero
      | SynUnionCaseKind.FullType(ty, _) -> ty.Range
      
    let bodyRange mb decls =
      unionRangesChecked (rangeOfDecls decls) mb
          
    /// Get information for implementation file      
    let getNavigationFromImplFile (modules: SynModuleOrNamespace list) =
        // Map for dealing with name conflicts
        let names = Dictionary()

        let addItemName name = 
            let count =
                match names.TryGetValue name with
                | true, count -> count + 1
                | _ -> 1
            names.[name] <- count
            count
        
        let uniqueName name idx = 
            let total = names.[name]
            sprintf "%s_%d_of_%d" name idx total

        // Create declaration (for the left dropdown)                
        let createDeclLid(baseName, lid, kind, baseGlyph, m, bodym, nested, enclosingEntityKind, isAbstract, access) =
            let name = (if baseName <> "" then baseName + "." else "") + (textOfLid lid)
            NavigationItem.Create
              (name, kind, baseGlyph, m, bodym, false, enclosingEntityKind, isAbstract, access), (addItemName name), nested
            
        let createDecl(baseName, id:Ident, kind, baseGlyph, m, bodym, nested, enclosingEntityKind, isAbstract, access) =
            let name = (if baseName <> "" then baseName + "." else "") + (id.idText)
            NavigationItem.Create
              (name, kind, baseGlyph, m, bodym, false, enclosingEntityKind, isAbstract, access), (addItemName name), nested
         
        // Create member-kind-of-thing for the right dropdown
        let createMemberLid(lid, kind, baseGlyph, m, enclosingEntityKind, isAbstract, access) =
            NavigationItem.Create(textOfLid lid, kind, baseGlyph, m, m, false, enclosingEntityKind, isAbstract, access), (addItemName(textOfLid lid))

        let createMember(id:Ident, kind, baseGlyph, m, enclosingEntityKind, isAbstract, access) =
            NavigationItem.Create(id.idText, kind, baseGlyph, m, m, false, enclosingEntityKind, isAbstract, access), (addItemName(id.idText))

        // Process let-binding
        let processBinding isMember enclosingEntityKind isAbstract (SynBinding(_, _, _, _, _, _, SynValData(memberOpt, _, _), synPat, _, synExpr, _, _)) =
            let m = 
                match synExpr with 
                | SynExpr.Typed (e, _, _) -> e.Range // fix range for properties with type annotations
                | _ -> synExpr.Range

            match synPat, memberOpt with
            | SynPat.LongIdent(longDotId=LongIdentWithDots(lid,_); accessibility=access), Some(flags) when isMember -> 
                let icon, kind =
                  match flags.MemberKind with
                  | SynMemberKind.ClassConstructor
                  | SynMemberKind.Constructor
                  | SynMemberKind.Member -> 
                        (if flags.IsOverrideOrExplicitImpl then FSharpGlyph.OverridenMethod else FSharpGlyph.Method), NavigationItemKind.Method
                  | SynMemberKind.PropertyGetSet
                  | SynMemberKind.PropertySet
                  | SynMemberKind.PropertyGet -> FSharpGlyph.Property, NavigationItemKind.Property
                let lidShow, rangeMerge = 
                  match lid with 
                  | _thisVar :: nm :: _ -> (List.tail lid, nm.idRange) 
                  | hd :: _ -> (lid, hd.idRange) 
                  | _ -> (lid, m)
                [ createMemberLid(lidShow, kind, icon, unionRanges rangeMerge m, enclosingEntityKind, isAbstract, access) ]
            | SynPat.LongIdent(LongIdentWithDots(lid,_), _, _, _, access, _), _ -> 
                [ createMemberLid(lid, NavigationItemKind.Field, FSharpGlyph.Field, unionRanges (List.head lid).idRange m, enclosingEntityKind, isAbstract, access) ]
            | SynPat.Named (id, _, access, _), _ | SynPat.As(_, SynPat.Named (id, _, access, _), _), _ -> 
                let glyph = if isMember then FSharpGlyph.Method else FSharpGlyph.Field
                [ createMember(id, NavigationItemKind.Field, glyph, unionRanges id.idRange m, enclosingEntityKind, isAbstract, access) ]
            | _ -> []
        
        // Process a class declaration or F# type declaration
        let rec processExnDefnRepr baseName nested (SynExceptionDefnRepr(_, (SynUnionCase(_, id, fldspec, _, _, _)), _, _, access, m)) =
            // Exception declaration
            [ createDecl(baseName, id, NavigationItemKind.Exception, FSharpGlyph.Exception, m, fldspecRange fldspec, nested, NavigationEntityKind.Exception, false, access) ] 

        // Process a class declaration or F# type declaration
        and processExnDefn baseName (SynExceptionDefn(repr, membDefns, _)) =  
            let nested = processMembers membDefns NavigationEntityKind.Exception |> snd
            processExnDefnRepr baseName nested repr

        and processTycon baseName (SynTypeDefn(SynComponentInfo(_, _, _, lid, _, _, access, _), repr, membDefns, _, m)) =
            let topMembers = processMembers membDefns NavigationEntityKind.Class |> snd
            match repr with
            | SynTypeDefnRepr.Exception repr -> processExnDefnRepr baseName [] repr
            | SynTypeDefnRepr.ObjectModel(_, membDefns, mb) ->
                // F# class declaration
                let members = processMembers membDefns NavigationEntityKind.Class |> snd
                let nested = members@topMembers
                ([ createDeclLid(baseName, lid, NavigationItemKind.Type, FSharpGlyph.Class, m, bodyRange mb nested, nested, NavigationEntityKind.Class, false, access) ]: ((NavigationItem * int * _) list))
            | SynTypeDefnRepr.Simple(simple, _) ->
                // F# type declaration
                match simple with
                | SynTypeDefnSimpleRepr.Union(_, cases, mb) ->
                    let cases = 
                        [ for (SynUnionCase(_, id, fldspec, _, _, _)) in cases -> 
                            createMember(id, NavigationItemKind.Other, FSharpGlyph.Struct, unionRanges (fldspecRange fldspec) id.idRange, NavigationEntityKind.Union, false, access) ]
                    let nested = cases@topMembers              
                    [ createDeclLid(baseName, lid, NavigationItemKind.Type, FSharpGlyph.Union, m, bodyRange mb nested, nested, NavigationEntityKind.Union, false, access) ]
                | SynTypeDefnSimpleRepr.Enum(cases, mb) -> 
                    let cases = 
                        [ for (SynEnumCase(_, id, _, _, _, m)) in cases ->
                            createMember(id, NavigationItemKind.Field, FSharpGlyph.EnumMember, m, NavigationEntityKind.Enum, false, access) ]
                    let nested = cases@topMembers
                    [ createDeclLid(baseName, lid, NavigationItemKind.Type, FSharpGlyph.Enum, m, bodyRange mb nested, nested, NavigationEntityKind.Enum, false, access) ]
                | SynTypeDefnSimpleRepr.Record(_, fields, mb) ->
                    let fields = 
                        [ for (SynField(_, _, id, _, _, _, _, m)) in fields do
                            match id with
                            | Some ident -> 
                                yield createMember(ident, NavigationItemKind.Field, FSharpGlyph.Field, m, NavigationEntityKind.Record, false, access)
                            | _ -> 
                                () ]
                    let nested = fields@topMembers
                    [ createDeclLid(baseName, lid, NavigationItemKind.Type, FSharpGlyph.Type, m, bodyRange mb nested, nested, NavigationEntityKind.Record, false, access) ]
                | SynTypeDefnSimpleRepr.TypeAbbrev(_, _, mb) ->
                    [ createDeclLid(baseName, lid, NavigationItemKind.Type, FSharpGlyph.Typedef, m, bodyRange mb topMembers, topMembers, NavigationEntityKind.Class, false, access) ]
                          
                //| SynTypeDefnSimpleRepr.General of TyconKind * (SynType * Range * ident option) list * (valSpfn * MemberFlags) list * fieldDecls * bool * bool * Range 
                //| SynTypeDefnSimpleRepr.LibraryOnlyILAssembly of ILType * Range
                //| TyconCore_repr_hidden of Range
                | _ -> [] 
                  
        // Returns class-members for the right dropdown                  
        and processMembers members enclosingEntityKind : (range * list<NavigationItem * int>) = 
            let members = 
                members 
                |> List.groupBy (fun x -> x.Range)
                |> List.map (fun (range, members) ->
                    range,
                    (match members with
                     | [memb] ->
                         match memb with
                         | SynMemberDefn.LetBindings(binds, _, _, _) -> List.collect (processBinding false enclosingEntityKind false) binds
                         | SynMemberDefn.Member(bind, _) -> processBinding true enclosingEntityKind false bind
                         | SynMemberDefn.ValField(SynField(_, _, Some(rcid), _, _, _, access, range), _) ->
                             [ createMember(rcid, NavigationItemKind.Field, FSharpGlyph.Field, range, enclosingEntityKind, false, access) ]
                         | SynMemberDefn.AutoProperty(_attribs,_isStatic,id,_tyOpt,_propKind,_,_xmlDoc, access,_synExpr, _, _) -> 
                             [ createMember(id, NavigationItemKind.Field, FSharpGlyph.Field, id.idRange, enclosingEntityKind, false, access) ]
                         | SynMemberDefn.AbstractSlot(SynValSig(_, id, _, ty, _, _, _, _, access, _, _), _, _) ->
                             [ createMember(id, NavigationItemKind.Method, FSharpGlyph.OverridenMethod, ty.Range, enclosingEntityKind, true, access) ]
                         | SynMemberDefn.NestedType _ -> failwith "tycon as member????" //processTycon tycon                
                         | SynMemberDefn.Interface(_, Some(membs), _) ->
                             processMembers membs enclosingEntityKind |> snd
                         | _ -> [] 
                     // can happen if one is a getter and one is a setter
                     | [SynMemberDefn.Member(memberDefn=SynBinding(headPat=SynPat.LongIdent(lid1, Some(info1),_,_,_,_)) as binding1)
                        SynMemberDefn.Member(memberDefn=SynBinding(headPat=SynPat.LongIdent(lid2, Some(info2),_,_,_,_)) as binding2)] ->
                         // ensure same long id
                         assert((lid1.Lid,lid2.Lid) ||> List.forall2 (fun x y -> x.idText = y.idText))
                         // ensure one is getter, other is setter
                         assert((info1.idText = "set" && info2.idText = "get") ||
                                (info2.idText = "set" && info1.idText = "get"))
                         // both binding1 and binding2 have same range, so just try the first one, else try the second one
                         match processBinding true enclosingEntityKind false binding1 with
                         | [] -> processBinding true enclosingEntityKind false binding2
                         | x -> x
                     | _ -> [])) 
            
            (members |> Seq.map fst |> Seq.fold unionRangesChecked range.Zero),
            (members |> List.collect snd)

        // Process declarations in a module that belong to the right drop-down (let bindings)
        let processNestedDeclarations decls = decls |> List.collect (function
            | SynModuleDecl.Let(_, binds, _) -> List.collect (processBinding false NavigationEntityKind.Module false) binds
            | _ -> [])        

        // Process declarations nested in a module that should be displayed in the left dropdown
        // (such as type declarations, nested modules etc.)                            
        let rec processNavigationTopLevelDeclarations(baseName, decls) = decls |> List.collect (function
            | SynModuleDecl.ModuleAbbrev(id, lid, m) ->
                [ createDecl(baseName, id, NavigationItemKind.Module, FSharpGlyph.Module, m, rangeOfLid lid, [], NavigationEntityKind.Namespace, false, None) ]
                
            | SynModuleDecl.NestedModule(SynComponentInfo(_, _, _, lid, _, _, access, _), _isRec, decls, _, m) ->
                // Find let bindings (for the right dropdown)
                let nested = processNestedDeclarations(decls)
                let newBaseName = (if (baseName = "") then "" else baseName+".") + (textOfLid lid)
                
                // Get nested modules and types (for the left dropdown)
                let other = processNavigationTopLevelDeclarations(newBaseName, decls)
                createDeclLid(baseName, lid, NavigationItemKind.Module, FSharpGlyph.Module, m, unionRangesChecked (rangeOfDecls nested) (moduleRange (rangeOfLid lid) other), nested, NavigationEntityKind.Module, false, access) :: other
                  
            | SynModuleDecl.Types(tydefs, _) -> tydefs |> List.collect (processTycon baseName)                                    
            | SynModuleDecl.Exception (defn,_) -> processExnDefn baseName defn
            | _ -> [])

        // Collect all the items  
        let items = 
            // Show base name for this module only if it's not the root one
            let singleTopLevel = (modules.Length = 1)
            modules |> List.collect (fun (SynModuleOrNamespace(id, _isRec, kind, decls, _, _, access, m)) ->
                let baseName = if (not singleTopLevel) then textOfLid id else ""
                // Find let bindings (for the right dropdown)
                let nested = processNestedDeclarations(decls)
                // Get nested modules and types (for the left dropdown)
                let other = processNavigationTopLevelDeclarations(baseName, decls)

                // Create explicitly - it can be 'single top level' thing that is hidden
                match id with
                | [] -> other
                | _ ->
                    let decl =
                        NavigationItem.Create
                            (textOfLid id, (if kind.IsModule then NavigationItemKind.ModuleFile else NavigationItemKind.Namespace),
                                FSharpGlyph.Module, m, 
                                unionRangesChecked (rangeOfDecls nested) (moduleRange (rangeOfLid id) other), 
                                singleTopLevel, NavigationEntityKind.Module, false, access), (addItemName(textOfLid id)), nested
                    decl :: other)
                  
        let items = 
            items 
            |> Array.ofList 
            |> Array.map (fun (d, idx, nest) -> 
                let nest = nest |> Array.ofList |> Array.map (fun (decl, idx) -> decl.WithUniqueName(uniqueName d.Name idx))
                nest |> Array.sortInPlaceWith (fun a b -> compare a.Name b.Name)
                { Declaration = d.WithUniqueName(uniqueName d.Name idx); Nested = nest } )                  
        items |> Array.sortInPlaceWith (fun a b -> compare a.Declaration.Name b.Declaration.Name)
        new NavigationItems(items)

    /// Get information for signature file      
    let getNavigationFromSigFile (modules: SynModuleOrNamespaceSig list) =
        // Map for dealing with name conflicts
        let mutable nameMap = Map.empty 
        let addItemName name = 
            let count = defaultArg (nameMap |> Map.tryFind name) 0
            nameMap <- (Map.add name (count + 1) (nameMap))
            (count + 1)
        let uniqueName name idx = 
            let total = Map.find name nameMap
            sprintf "%s_%d_of_%d" name idx total

        // Create declaration (for the left dropdown)                
        let createDeclLid(baseName, lid, kind, baseGlyph, m, bodym, nested, enclosingEntityKind, isAbstract, access) =
            let name = (if baseName <> "" then baseName + "." else "") + (textOfLid lid)
            NavigationItem.Create
              (name, kind, baseGlyph, m, bodym, false, enclosingEntityKind, isAbstract, access), (addItemName name), nested
            
        let createDecl(baseName, id:Ident, kind, baseGlyph, m, bodym, nested, enclosingEntityKind, isAbstract, access) =
            let name = (if baseName <> "" then baseName + "." else "") + (id.idText)
            NavigationItem.Create
              (name, kind, baseGlyph, m, bodym, false, enclosingEntityKind, isAbstract, access), (addItemName name), nested
         
        let createMember(id:Ident, kind, baseGlyph, m, enclosingEntityKind, isAbstract, access) =
            NavigationItem.Create(id.idText, kind, baseGlyph, m, m, false, enclosingEntityKind, isAbstract, access), (addItemName(id.idText))

        let rec processExnRepr baseName nested (SynExceptionDefnRepr(_, (SynUnionCase(_, id, fldspec, _, _, _)), _, _, access, m)) =
            // Exception declaration
            [ createDecl(baseName, id, NavigationItemKind.Exception, FSharpGlyph.Exception, m, fldspecRange fldspec, nested, NavigationEntityKind.Exception, false, access) ] 
        
        and processExnSig baseName (SynExceptionSig(repr, memberSigs, _)) =  
            let nested = processSigMembers memberSigs
            processExnRepr baseName nested repr

        and processTycon baseName (SynTypeDefnSig(SynComponentInfo(_, _, _, lid, _, _, access, _), repr, membDefns, m)) =
            let topMembers = processSigMembers membDefns
            match repr with
            | SynTypeDefnSigRepr.Exception repr -> processExnRepr baseName [] repr
            | SynTypeDefnSigRepr.ObjectModel(_, membDefns, mb) ->
                // F# class declaration
                let members = processSigMembers membDefns
                let nested = members @ topMembers
                ([ createDeclLid(baseName, lid, NavigationItemKind.Type, FSharpGlyph.Class, m, bodyRange mb nested, nested, NavigationEntityKind.Class, false, access) ]: ((NavigationItem * int * _) list))
            | SynTypeDefnSigRepr.Simple(simple, _) ->
                // F# type declaration
                match simple with
                | SynTypeDefnSimpleRepr.Union(_, cases, mb) ->
                    let cases = 
                        [ for (SynUnionCase(_, id, fldspec, _, _, _)) in cases -> 
                            createMember(id, NavigationItemKind.Other, FSharpGlyph.Struct, unionRanges (fldspecRange fldspec) id.idRange, NavigationEntityKind.Union, false, access) ]
                    let nested = cases@topMembers              
                    [ createDeclLid(baseName, lid, NavigationItemKind.Type, FSharpGlyph.Union, m, bodyRange mb nested, nested, NavigationEntityKind.Union, false, access) ]
                | SynTypeDefnSimpleRepr.Enum(cases, mb) -> 
                    let cases = 
                        [ for (SynEnumCase(_, id, _, _, _, m)) in cases ->
                            createMember(id, NavigationItemKind.Field, FSharpGlyph.EnumMember, m, NavigationEntityKind.Enum, false, access) ]
                    let nested = cases@topMembers
                    [ createDeclLid(baseName, lid, NavigationItemKind.Type, FSharpGlyph.Enum, m, bodyRange mb nested, nested, NavigationEntityKind.Enum, false, access) ]
                | SynTypeDefnSimpleRepr.Record(_, fields, mb) ->
                    let fields = 
                        [ for (SynField(_, _, id, _, _, _, _, m)) in fields do
                            match id with
                            | Some ident ->
                                yield createMember(ident, NavigationItemKind.Field, FSharpGlyph.Field, m, NavigationEntityKind.Record, false, access)
                            | _ ->
                                () ]
                    let nested = fields@topMembers
                    [ createDeclLid(baseName, lid, NavigationItemKind.Type, FSharpGlyph.Type, m, bodyRange mb nested, nested, NavigationEntityKind.Record, false, access) ]
                | SynTypeDefnSimpleRepr.TypeAbbrev(_, _, mb) ->
                    [ createDeclLid(baseName, lid, NavigationItemKind.Type, FSharpGlyph.Typedef, m, bodyRange mb topMembers, topMembers, NavigationEntityKind.Class, false, access) ]
                          
                //| SynTypeDefnSimpleRepr.General of TyconKind * (SynType * range * ident option) list * (valSpfn * MemberFlags) list * fieldDecls * bool * bool * range 
                //| SynTypeDefnSimpleRepr.LibraryOnlyILAssembly of ILType * range
                //| TyconCore_repr_hidden of range
                | _ -> [] 
                  
        and processSigMembers (members: SynMemberSig list): list<NavigationItem * int> = 
            [ for memb in members do
                 match memb with
                 | SynMemberSig.Member(SynValSig.SynValSig(_, id, _, _, _, _, _, _, access, _, m), _, _) ->
                     yield createMember(id, NavigationItemKind.Method, FSharpGlyph.Method, m, NavigationEntityKind.Class, false, access)
                 | SynMemberSig.ValField(SynField(_, _, Some(rcid), ty, _, _, access, _), _) ->
                     yield createMember(rcid, NavigationItemKind.Field, FSharpGlyph.Field, ty.Range, NavigationEntityKind.Class, false, access)
                 | _ -> () ]

        // Process declarations in a module that belong to the right drop-down (let bindings)
        let processNestedSigDeclarations decls = decls |> List.collect (function
            | SynModuleSigDecl.Val(SynValSig.SynValSig(_, id, _, _, _, _, _, _, access, _, m), _) ->
                [ createMember(id, NavigationItemKind.Method, FSharpGlyph.Method, m, NavigationEntityKind.Module, false, access) ]
            | _ -> [] )        

        // Process declarations nested in a module that should be displayed in the left dropdown
        // (such as type declarations, nested modules etc.)                            
        let rec processNavigationTopLevelSigDeclarations(baseName, decls) = 
            decls 
            |> List.collect (function
            | SynModuleSigDecl.ModuleAbbrev(id, lid, m) ->
                [ createDecl(baseName, id, NavigationItemKind.Module, FSharpGlyph.Module, m, rangeOfLid lid, [], NavigationEntityKind.Module, false, None) ]
                
            | SynModuleSigDecl.NestedModule(SynComponentInfo(_, _, _, lid, _, _, access, _), _, decls, m) ->                
                // Find let bindings (for the right dropdown)
                let nested = processNestedSigDeclarations(decls)
                let newBaseName = (if baseName = "" then "" else baseName + ".") + (textOfLid lid)
                
                // Get nested modules and types (for the left dropdown)
                let other = processNavigationTopLevelSigDeclarations(newBaseName, decls)
                createDeclLid(baseName, lid, NavigationItemKind.Module, FSharpGlyph.Module, m, unionRangesChecked (rangeOfDecls nested) (moduleRange (rangeOfLid lid) other), nested, NavigationEntityKind.Module, false, access) :: other
                  
            | SynModuleSigDecl.Types(tydefs, _) -> tydefs |> List.collect (processTycon baseName)                                    
            | SynModuleSigDecl.Exception (defn,_) -> processExnSig baseName defn
            | _ -> [])
                  
        // Collect all the items  
        let items = 
            // Show base name for this module only if it's not the root one
            let singleTopLevel = (modules.Length = 1)
            modules |> List.collect (fun (SynModuleOrNamespaceSig(id, _isRec, kind, decls, _, _, access, m)) ->
                let baseName = if (not singleTopLevel) then textOfLid id else ""
                // Find let bindings (for the right dropdown)
                let nested = processNestedSigDeclarations(decls)
                // Get nested modules and types (for the left dropdown)
                let other = processNavigationTopLevelSigDeclarations(baseName, decls)
                
                // Create explicitly - it can be 'single top level' thing that is hidden
                let decl =
                    NavigationItem.Create
                        (textOfLid id, (if kind.IsModule then NavigationItemKind.ModuleFile else NavigationItemKind.Namespace),
                            FSharpGlyph.Module, m, 
                            unionRangesChecked (rangeOfDecls nested) (moduleRange (rangeOfLid id) other), 
                            singleTopLevel, NavigationEntityKind.Module, false, access), (addItemName(textOfLid id)), nested
                decl :: other)
        
        let items = 
            items 
            |> Array.ofList 
            |> Array.map (fun (d, idx, nest) -> 
                let nest = nest |> Array.ofList |> Array.map (fun (decl, idx) -> decl.WithUniqueName(uniqueName d.Name idx))
                nest |> Array.sortInPlaceWith (fun a b -> compare a.Name b.Name)
                let nest = nest |> Array.distinctBy (fun x -> x.Range, x.BodyRange, x.Name, x.Kind) 
                
                { Declaration = d.WithUniqueName(uniqueName d.Name idx); Nested = nest } )                  
        items |> Array.sortInPlaceWith (fun a b -> compare a.Declaration.Name b.Declaration.Name)
        new NavigationItems(items)

[<RequireQualifiedAccess>]
module Navigation =
    let getNavigation (parsedInput: ParsedInput) =
        match parsedInput with
        | ParsedInput.SigFile (ParsedSigFileInput (modules = modules)) -> NavigationImpl.getNavigationFromSigFile modules
        | ParsedInput.ImplFile (ParsedImplFileInput (modules = modules)) -> NavigationImpl.getNavigationFromImplFile modules

    let empty = NavigationItems([||])

[<RequireQualifiedAccess>]
type NavigableItemKind =
    | Module
    | ModuleAbbreviation
    | Exception
    | Type
    | ModuleValue
    | Field
    | Property
    | Constructor
    | Member
    | EnumCase
    | UnionCase
    override x.ToString() = sprintf "%+A" x
    
[<RequireQualifiedAccess>]
type NavigableContainerType =
    | File
    | Namespace
    | Module
    | Type
    | Exception

type NavigableContainer =
    { Type: NavigableContainerType
      Name: string }

type NavigableItem = 
    { Name: string
      Range: range
      IsSignature: bool
      Kind: NavigableItemKind
      Container: NavigableContainer }
        
[<RequireQualifiedAccess>]
module NavigateTo =
    let GetNavigableItems (parsedInput: ParsedInput) : NavigableItem [] = 
        let rec lastInLid (lid: LongIdent) = 
            match lid with
            | [x] -> Some x
            | _ :: xs -> lastInLid xs
            | _ -> None // empty lid is possible in case of broken ast

        let formatLongIdent (lid: LongIdent) = lid |> List.map (fun id -> id.idText) |> String.concat "."
        let result = ResizeArray()
        
        let addIdent kind (id: Ident) (isSignature: bool) (container: NavigableContainer) = 
            if not (String.IsNullOrEmpty id.idText) then
                let item = 
                    { Name = id.idText
                      Range = id.idRange
                      IsSignature = isSignature
                      Kind = kind 
                      Container = container }
                result.Add item
    
        let addModule lid isSig container = 
            match lastInLid lid with
            | Some id -> addIdent NavigableItemKind.Module id isSig container
            | _ -> ()
    
        let addModuleAbbreviation (id: Ident) isSig container =
            addIdent NavigableItemKind.ModuleAbbreviation id isSig container
        
        let addExceptionRepr (SynExceptionDefnRepr(_, SynUnionCase(_, id, _, _, _, _), _, _, _, _)) isSig container = 
            addIdent NavigableItemKind.Exception id isSig container
            { Type = NavigableContainerType.Exception; Name = id.idText }
    
        let addComponentInfo containerType kind (SynComponentInfo(_, _, _, lid, _, _, _, _)) isSig container = 
            match lastInLid lid with
            | Some id -> addIdent kind id isSig container
            | _ -> ()
            { Type = containerType; Name = formatLongIdent lid }
    
        let addValSig kind (SynValSig(_, id, _, _, _, _, _, _, _, _, _)) isSig container = 
            addIdent kind id isSig container
        
        let addField(SynField(_, _, id, _, _, _, _, _)) isSig container = 
            match id with
            | Some id -> addIdent NavigableItemKind.Field id isSig container
            | _ -> ()
        
        let addEnumCase(SynEnumCase(_, id, _, _, _, _)) isSig = 
            addIdent NavigableItemKind.EnumCase id isSig
        
        let addUnionCase(SynUnionCase(_, id, _, _, _, _)) isSig container = 
            addIdent NavigableItemKind.UnionCase id isSig container
    
        let mapMemberKind mk = 
            match mk with
            | SynMemberKind.ClassConstructor // ?
            | SynMemberKind.Constructor -> NavigableItemKind.Constructor
            | SynMemberKind.PropertyGet
            | SynMemberKind.PropertySet
            | SynMemberKind.PropertyGetSet -> NavigableItemKind.Property
            | SynMemberKind.Member -> NavigableItemKind.Member
    
        let addBinding (SynBinding(_, _, _, _, _, _, valData, headPat, _, _, _, _)) itemKind container =
            let (SynValData(memberFlagsOpt, _, _)) = valData
            let kind =
                match itemKind with
                | Some x -> x
                | _ ->
                    match memberFlagsOpt with
                    | Some mf -> mapMemberKind mf.MemberKind
                    | _ -> NavigableItemKind.ModuleValue
    
            match headPat with
            | SynPat.LongIdent(LongIdentWithDots([_; id], _), _, _, _, _access, _) ->
                // instance members
                addIdent kind id false container
            | SynPat.LongIdent(LongIdentWithDots([id], _), _, _, _, _, _) ->
                // functions
                addIdent kind id false container
            | SynPat.Named (id, _, _, _) | SynPat.As(_, SynPat.Named (id, _, _, _), _) ->
                // values
                addIdent kind id false container
            | _ -> ()
    
        let addMember valSig (memberFlags: SynMemberFlags) isSig container = 
            let ctor = mapMemberKind memberFlags.MemberKind
            addValSig ctor valSig isSig container
    
        let rec walkSigFileInput (ParsedSigFileInput (fileName, _, _, _, moduleOrNamespaceList)) = 
            for item in moduleOrNamespaceList do
                walkSynModuleOrNamespaceSig item { Type = NavigableContainerType.File; Name = fileName }
    
        and walkSynModuleOrNamespaceSig (SynModuleOrNamespaceSig(lid, _, kind, decls, _, _, _, _)) container =
            let isModule = kind.IsModule
            if isModule then
                addModule lid true container
            let container = 
                { Type = if isModule then NavigableContainerType.Module else NavigableContainerType.Namespace
                  Name = formatLongIdent lid }
            for decl in decls do
                walkSynModuleSigDecl decl container
    
        and walkSynModuleSigDecl(decl: SynModuleSigDecl) container = 
            match decl with
            | SynModuleSigDecl.ModuleAbbrev(lhs, _, _range) ->
                addModuleAbbreviation lhs true container
            | SynModuleSigDecl.Exception(SynExceptionSig(representation, _, _), _) ->
                addExceptionRepr representation true container |> ignore
            | SynModuleSigDecl.NamespaceFragment fragment ->
                walkSynModuleOrNamespaceSig fragment container
            | SynModuleSigDecl.NestedModule(componentInfo, _, nestedDecls, _) ->
                let container = addComponentInfo NavigableContainerType.Module NavigableItemKind.Module componentInfo true container
                for decl in nestedDecls do
                    walkSynModuleSigDecl decl container
            | SynModuleSigDecl.Types(types, _) ->
                for ty in types do
                    walkSynTypeDefnSig ty container
            | SynModuleSigDecl.Val(valSig, _range) ->
                addValSig NavigableItemKind.ModuleValue valSig true container
            | SynModuleSigDecl.HashDirective _
            | SynModuleSigDecl.Open _ -> ()
    
        and walkSynTypeDefnSig (SynTypeDefnSig(componentInfo, repr, members, _)) container = 
            let container = addComponentInfo NavigableContainerType.Type NavigableItemKind.Type componentInfo true container
            for m in members do
                walkSynMemberSig m container
            match repr with
            | SynTypeDefnSigRepr.ObjectModel(_, membersSigs, _) ->
                for m in membersSigs do
                    walkSynMemberSig m container
            | SynTypeDefnSigRepr.Simple(repr, _) ->
                walkSynTypeDefnSimpleRepr repr true container
            | SynTypeDefnSigRepr.Exception _ -> ()
    
        and walkSynMemberSig (synMemberSig: SynMemberSig) container = 
            match synMemberSig with
            | SynMemberSig.Member(valSig, memberFlags, _) ->
                addMember valSig memberFlags true container
            | SynMemberSig.ValField(synField, _) ->
                addField synField true container
            | SynMemberSig.NestedType(synTypeDef, _) ->
                walkSynTypeDefnSig synTypeDef container
            | SynMemberSig.Inherit _
            | SynMemberSig.Interface _ -> ()
    
        and walkImplFileInput (ParsedImplFileInput (fileName = fileName; modules = moduleOrNamespaceList)) = 
            let container = { Type = NavigableContainerType.File; Name = fileName }
            for item in moduleOrNamespaceList do
                walkSynModuleOrNamespace item container
    
        and walkSynModuleOrNamespace(SynModuleOrNamespace(lid, _, kind, decls, _, _, _, _)) container =
            let isModule = kind.IsModule
            if isModule then
                addModule lid false container
            let container = 
                { Type = if isModule then NavigableContainerType.Module else NavigableContainerType.Namespace
                  Name = formatLongIdent lid }
            for decl in decls do
                walkSynModuleDecl decl container
    
        and walkSynModuleDecl(decl: SynModuleDecl) container =
            match decl with
            | SynModuleDecl.Exception(SynExceptionDefn(repr, synMembers, _), _) -> 
                let container = addExceptionRepr repr false container
                for m in synMembers do
                    walkSynMemberDefn m container
            | SynModuleDecl.Let(_, bindings, _) ->
                for binding in bindings do
                    addBinding binding None container
            | SynModuleDecl.ModuleAbbrev(lhs, _, _) ->
                addModuleAbbreviation lhs false container
            | SynModuleDecl.NamespaceFragment(fragment) ->
                walkSynModuleOrNamespace fragment container
            | SynModuleDecl.NestedModule(componentInfo, _, modules, _, _) ->
                let container = addComponentInfo NavigableContainerType.Module NavigableItemKind.Module componentInfo false container
                for m in modules do
                    walkSynModuleDecl m container
            | SynModuleDecl.Types(typeDefs, _range) ->
                for t in typeDefs do
                    walkSynTypeDefn t container
            | SynModuleDecl.Attributes _
            | SynModuleDecl.DoExpr _
            | SynModuleDecl.HashDirective _
            | SynModuleDecl.Open _ -> ()
    
        and walkSynTypeDefn(SynTypeDefn(componentInfo, representation, members, _, _)) container = 
            let container = addComponentInfo NavigableContainerType.Type NavigableItemKind.Type componentInfo false container
            walkSynTypeDefnRepr representation container
            for m in members do
                walkSynMemberDefn m container
    
        and walkSynTypeDefnRepr(typeDefnRepr: SynTypeDefnRepr) container = 
            match typeDefnRepr with
            | SynTypeDefnRepr.ObjectModel(_, members, _) ->
                for m in members do
                    walkSynMemberDefn m container
            | SynTypeDefnRepr.Simple(repr, _) -> 
                walkSynTypeDefnSimpleRepr repr false container
            | SynTypeDefnRepr.Exception _ -> ()
    
        and walkSynTypeDefnSimpleRepr(repr: SynTypeDefnSimpleRepr) isSig container = 
            match repr with
            | SynTypeDefnSimpleRepr.Enum(enumCases, _) ->
                for c in enumCases do
                    addEnumCase c isSig container
            | SynTypeDefnSimpleRepr.Record(_, fields, _) ->
                for f in fields do
                    // TODO: add specific case for record field?
                    addField f isSig container
            | SynTypeDefnSimpleRepr.Union(_, unionCases, _) ->
                for uc in unionCases do
                    addUnionCase uc isSig container
            | SynTypeDefnSimpleRepr.General _
            | SynTypeDefnSimpleRepr.LibraryOnlyILAssembly _
            | SynTypeDefnSimpleRepr.None _
            | SynTypeDefnSimpleRepr.TypeAbbrev _
            | SynTypeDefnSimpleRepr.Exception _ -> ()
    
        and walkSynMemberDefn (memberDefn: SynMemberDefn) container =
            match memberDefn with
            | SynMemberDefn.AbstractSlot(synValSig, memberFlags, _) ->
                addMember synValSig memberFlags false container
            | SynMemberDefn.AutoProperty(_, _, id, _, _, _, _, _, _, _, _) ->
                addIdent NavigableItemKind.Property id false container
            | SynMemberDefn.Interface(_, members, _) ->
                match members with
                | Some members ->
                    for m in members do
                        walkSynMemberDefn m container
                | None -> ()
            | SynMemberDefn.Member(binding, _) ->
                addBinding binding None container
            | SynMemberDefn.NestedType(typeDef, _, _) -> 
                walkSynTypeDefn typeDef container
            | SynMemberDefn.ValField(field, _) ->
                addField field false container
            | SynMemberDefn.LetBindings (bindings, _, _, _) -> 
                bindings |> List.iter (fun binding -> addBinding binding (Some NavigableItemKind.Field) container)
            | SynMemberDefn.Open _
            | SynMemberDefn.ImplicitInherit _
            | SynMemberDefn.Inherit _
            | SynMemberDefn.ImplicitCtor _ -> ()

        match parsedInput with
        | ParsedInput.SigFile input -> walkSigFileInput input
        | ParsedInput.ImplFile input -> walkImplFileInput input
    
        result.ToArray()

