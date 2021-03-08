Copyright (c) Microsoft Corporation.  All Rights Reserved.
See License.txt in the project root for license information.

## About the release notes

This document contains current and historical release notes information. They are preserved in their original form.

* [Current Release notes](#Current-release-notes)
* [Visual Studio 2017-2019 release notes](#visual-studio-2017-2019-update-167-release-notes)
* [FSharp.Compiler.Service release notes (version 37 and lower)](#FSharp-Compiler-Service-Versions-Release-notes)
* [Older release notes (pre-F# 4.0)](#older-visual-f-releases)

## Current release notes

These release notes track our current efforts to document changes to the F# project over time. They are split into the language, core library, compiler/tools, and compiler service.

### F# 5 / Visual Studio 16.9

### FSharp.Core 5.0.1

* [Performance improvement](https://github.com/dotnet/fsharp/pull/10188) to core collections Map by [Victor Baybekov](https://github.com/buybackoff)

### FSharp tools 11.0.1

* Significant improvements to the performance of code making heavy use of closures at runtime
* Warnings for mismatched parameter names between signature and implementation files
* Warnings for incorrect XML documentation files are turned on by default
* Big performance gains in tools for codebases with F# signature files
* Improved responsiveness for most IDE features
* Signature Help for F# function calls
* .NET 5 scripting for Visual Studio
* Add ConvertToAnonymousRecord quick fix [#10493](https://github.com/dotnet/fsharp/pull/10493)
* Add UseMutationWhenValueIsMutable code fix [#10488](https://github.com/dotnet/fsharp/pull/10488)
* Add MakeDeclarationMutable code fix [#10480](https://github.com/dotnet/fsharp/pull/10480)
* Add ChangeToUpcast code fix [#10463](https://github.com/dotnet/fsharp/pull/10463)
* Add AddMissingEqualsToTypeDefinition code fixer [#10470](https://github.com/dotnet/fsharp/pull/10470)
* Tag items in tooltips consistenly with respect to document classification. [#9563](https://github.com/dotnet/fsharp/pull/9563)
* Add ConvertToSingleEqualsEqualityExpression code fix [#10462](https://github.com/dotnet/fsharp/pull/10462)
* Turn XML doc and Sig<->Impl mismatch warnings on by default [#10457](https://github.com/dotnet/fsharp/pull/10457)
* Add ChangeRefCellDerefToNotExpression code fixer [#10469](https://github.com/dotnet/fsharp/pull/10469)
* Add WrapExpressionInParentheses code fix [#10460](https://github.com/dotnet/fsharp/pull/10460)
* Add ChangePrefixNegationToInfixSubtraction code fixeroo [#10471](https://github.com/dotnet/fsharp/pull/10471)
* Fix generic overloads with nullable [#10582](https://github.com/dotnet/fsharp/pull/10582)
* Resolve issue with implicit yields requiring Zero [#10556](https://github.com/dotnet/fsharp/pull/10556), by [Ryan Coy](https://github.com/laenas)
* Fix issue10550 FSI accessing System.Configuration. [#10572](https://github.com/dotnet/fsharp/pull/10572)
* Add field names to ILNativeType.Custom. [#10567](https://github.com/dotnet/fsharp/pull/10567), by [Scott Hutchinson](https://github.com/ScottHutchinson)
* Add field names to the ILExceptionClause.FilterCatch constructor. [#10559](https://github.com/dotnet/fsharp/pull/10559), by [Scott Hutchinson](https://github.com/ScottHutchinson)
* Fix completion with backticks, underscores, numbers [#10500](https://github.com/dotnet/fsharp/pull/10500), by [zanaptak](https://github.com/zanaptak)
* Emitting IsReadOnly/In attributes on abstract properties [#10542](https://github.com/dotnet/fsharp/pull/10542)
* Disable partial type checking when getting full results for a file [#10448](https://github.com/dotnet/fsharp/pull/10448)
* Fix unused open type declaration detection [#10510](https://github.com/dotnet/fsharp/pull/10510), by [André Slupik](https://github.com/asik)

### FSharp Compiler Service 40.0.0

This is a second big update to FCS. There are significant trimmings and renamings of the API and gets
it close to a more permanent form:

The primary namespaces are now:

```
FSharp.Compiler.IO                // FileSystem
FSharp.Compiler.CodeAnalysis      // FSharpChecker, FSharpCheckFileResults, FSharpChecProjectResults and friends
FSharp.Compiler.Diagnostics       // FSharpDiagnostic and friends
FSharp.Compiler.EditorServices    // Misc functionality for editors, e.g. interface stub generation
FSharp.Compiler.Interactive.Shell // F# Interactive
FSharp.Compiler.Symbols           // FSharpEntity etc
FSharp.Compiler.Syntax            // SyntaxTree, XmlDoc, PrettyNaming
FSharp.Compiler.Text              // ISourceFile, Range, TaggedText and other things
FSharp.Compiler.Tokenization      // FSharpLineTokenizer etc.
```

##### Changes in `FSharp.Compiler.Diagnostics`

* `ErrorHelpers` --> `DiagnosticHelpers`

* `ErrorResolutionHints.getSuggestedNames` --> `ErrorResolutionHints.GetSuggestedNames`

##### Changes in `FSharp.Compiler.SourceCodeServices`

* Everything is moved to one of the other namespaces

* The `FSharp` prefix is used for things in `FSharp.Compiler.Symbols` but not `FSharp.Compiler.EditorServices`

* `AssemblyContentProvider` --> `AssemblyContent` in EditorServices

* `AstVisitorBase` --> moved to `SyntaxVisitorBase` in FSharp.Compiler.Syntax

* `AstVisitorBase.*` --> `SyntaxVisitorBase.*` and methods now all take `path` parameters consistently

* `AstTraversal.Traverse` --> `SyntaxTraversal.Traverse`

* `BasicPatterns` --> `FSharpExprPatterns` in FSharp.Compiler.Symbols

* `DebuggerEnvironment.*` merged into `CompilerEnvironment` in FSharp.Compiler

* `ExternalType` --> `FindDeclExternalType` in EditorServices

* `FSharpChecker.ParseFileNoCache` --> removed in favour of optional `cache=false` argument on `FSharpChecker.ParseFile`

* `FSharpChecker.BeforeBackgroundFileCheck` event now takes FSharpProjectOptions arg

* `FSharpChecker.FileChecked` event now takes FSharpProjectOptions arg

* `FSharpChecker.FileParsed` event now takes FSharpProjectOptions arg

* `FSharpChecker.ProjectChecked` event now takes FSharpProjectOptions arg

* `FSharpCheckFileResults.Errors` --> `FSharpCheckFileResults.Diagnostics`

* `FSharpCheckProjectResults.Errors` --> `FSharpCheckProjectResults.Diagnostics`

* `FSharpDeclarationListInfo` --> `DeclarationListInfo` in EditorServices

* `FSharpEnclosingEntityKind` --> `NavigationEntityKind` in EditorServices

* `FSharpExternalSymbol` --> `FindDeclExternalSymbol` in EditorServices

* `FSharpFileUtilities.*` merged into `CompilerEnvironment` in FSharp.Compiler

* `FSharpFindDeclResult` --> `FindDeclResult`  in EditorServices

* `FSharpLexer.Lex` --> `FSharpLexer.Tokenize` in FSharp.Compiler.Tokenization

* `FSharpMethodGroup` --> `MethodGroup` in EditorServices

* `FSharpMethodGroupItem` --> `MethodGroupItem` in EditorServices

* `FSharpMethodGroupItemParameter` --> `MethodGroupItemParameter` in EditorServices

* `FSharpMethodGroupItemParameter.StructuredDisplay` ---> `MethodGroupItemParameter.Display`

* `FSharpMethodGroupItemParameter.StructuredReturnTypeText` ---> `MethodGroupItemParameter.ReturnTypeText`

* `FSharpNavigationDeclarationItem` --> `NavigationItem` in EditorServices

* `FSharpNavigationTopLevelDeclaration` --> `NavigationTopLevelDeclaration` in EditorServices

* `FSharpNavigationItems` --> `NavigationItems` in EditorServices

* `FSharpNavigation` --> `Navigation` in EditorServices

* `FSharpNavigationDeclarationItemKind` --> `NavigationItemKind` in EditorServices

* `FSharpNoteworthyParamInfoLocations` --> `ParameterLocations`

* `FSharpProjectOptions.ExtraProjectInfo` is removed

* `FSharpSemanticClassificationItem` --> `SemanticClassificationItem` in EditorServices

* `FSharpSemanticClassificationView ` --> `SemanticClassificationView` in EditorServices

* `FSharpStructuredToolTipText` --> `ToolTipText` in EditorServices

* `FSharpStructuredToolTipElementData` --> `ToolTipElementData` in EditorServices

* `FSharpStructuredToolTipElement` --> `ToolTipElement` in EditorServices

* `FSharpSymbol.Accessibility` is now non-optional.  Will return "public" in cases it previously returned "None"

* `FSharpSymbol.XmlDoc` now returns `FSharpXmlDoc` with cases for internal and external XML doc. Use `match symbol.XmlDoc with FSharpXmlDoc.FromText doc -> doc.UnprocessedLines` to get unprocessed XML lines

* `FSharpSymbol.ElaboratedXmlDoc` now removed. Use `match symbol.XmlDoc with FSharpXmlDoc.FromText doc -> doc.GetElaboratedLine()`

* `FSharpToolTipText` --> `ToolTipText` in EditorServices

* `FSharpToolTipElementData` --> `ToolTipElementData` in EditorServices

* `FSharpToolTipElement` --> `ToolTipElement` in EditorServices

* `FSharpType.FormatLayout` now returns `TaggedText[]`

* `FSharpType.FormatLayout` now returns `TaggedText[]`

* `FSharpUnresolvedSymbol` --> `UnresolvedSymbol` in EditorServices

* `InterfaceStubGenerator.*` are all capitalized

* `Idents` --> `ShortIdents` in EditorServices

* `LongIdent` abbreviation removed and replaced by `string`

* `NavigateTo.NavigableItemKind ` --> now directly in EditorServices

* `NavigateTo.ContainerType` --> `NavigableContainerType` now directly in EditorServices

* `NavigateTo.NavigableItem` --> `NavigableItem` now directly in EditorServices

* `NavigateTo.getNavigableItems` --> NavigateTo.GetNavigableItems in EditorServices

* `ParamTypeSymbol` --> `FindDeclExternalParam in EditorServices`

* `ParsedInput.tryFindInsertionContext` --> `ParsedInput.TryFindInsertionContext`

* `ParsedInput.findNearestPointToInsertOpenDeclaration ` --> `ParsedInput.FindNearestPointToInsertOpenDeclaration `

* `ParsedInput.getLongIdentAt` --> `ParsedInput.GetLongIdentAt`

* `ParsedInput.adjustInsertionPoint` --> `ParsedInput.AdjustInsertionPoint`

* `Symbol` --> `FSharpSymbolPatterns` in FSharp.Compiler.Symbols and marked experimental

* `Symbol.isAttribute` --> now `attrib.IsAttribute<'T>()`

* `Symbol.getAbbreviatedType` --> now `symbol.StripAbbreviations()`

* `Symbol.hasAttribute` --> now `symbol.HasAttribute<'T>()`

* `Symbol.tryGetAttribute` --> now `symbol.TryGetAttribute<'T>()`

* `TraverseStep` --> `SyntaxNode` in FSharp.Compiler.Syntax

* ToolTips now only return the structured (TaggedText) tooltips. You can iterate to get the text tooltips

##### Changes in `FSharp.Compiler.Text`

* `Pos` --> `Position`

##### Changes in `FSharp.Compiler.TextLayout` 

* Everything moves to `FSharp.Compiler.Text` 

* `LayoutTag` --> `TextTag`

* `Layout` is now internal and replaced by `TaggedText[]`

* `LayoutOps` is now internal

##### Changes in `FSharp.Compiler.SyntaxTree`

* Everything moves to namespace `FSharp.Compiler.Syntax`

* `DebugPointAtBinding` is now RequireQualifiedAccess

* `NoDebugPointAtStickyBinding` --> `DebugPointAtBinding.NoneAtSticky`

* `Clause` --> `SynMatchClause`

* `NormalBinding` -> `SynBindingKind.Normal`

* `Binding` --> `SynBinding`

* `Field` --> `SynField`

* `UnionCase` --> `SynUnionCase`

* `UnionCaseFullType` --> `SynUnionCaseKind.FullType`

* `UnionCaseFields` --> `SynUnionCaseKind.Fields`

* `MemberKind` --> `SynMemberKind`

* `MemberFlags` --> `SynMemberFlags`

* `TyconUnspecified` --> `SynTypeDefnKind.Unspecified`

* `TyconClass` --> `SynTypeDefnKind.Class`

* `TyconInterface` --> `SynTypeDefnKind.Interface`

* `TyconStruct` --> `SynTypeDefnKind.Struct`

* `TyconHiddenRepr` --> `SynTypeDefnKind.Opaque`

* `TyconUnion` --> `SynTypeDefnKind.Union`

* `TyconDelegate ` --> `SynTypeDefnKind.Delegate`

* `ComponentInfo` --> `SynComponentInfo`

* `TyconAugmentation` --> `SynTypeDefnKind.Augmentation`

* `TypeDefnSig` --> `SynTypeDefnSig`

* `HeadTypeStaticReq` --> `TyparStaticReq.HeadType`

* `NoStaticReq` --> `TyparStaticReq.None`

* `SynTypeConstraint` is now RequiresQualifiedAccess

* `ValSpfn` --> `SynValSpfn`

* `TyparDecl` --> `SynTyparDecl`

* `Typar` --> `SynTypar`

* `SynSimplePatAlternativeIdInfo` is now RequiresQualifiedAccess

* `InterfaceImpl` --> `SynInterfaceImpl`

* `SynBindingKind` is now RequiresQualifiedAccess

* `DoBinding` --> `SynBindingKind.Do`

* `StandaloneExpression` --> `SynBindingKind.StandaloneExpression`

* `SynModuleOrNamespaceKind` is now RequiresQualifiedAccess

* `IDefns` --> `ParsedScriptInteraction.Definitions`

* `IHash ` --> `ParsedScriptInteraction.HashDirective`

##### Other changes

* `LegacyReferenceResolver` now marked obsolete

### FSharp Compiler Service 39.0.0

This is a big update to FCS. There are significant trimmings and renamings of the API as a first step towards getting it under control with aims to eventually have a stable, sane public API surface area.

Renamings:

```diff
-type FSharp.Compiler.AbstractIL.Internal.Library.IFileSystem
+type FSharp.Compiler.SourceCodeServices.IFileSystem

-module FSharp.Compiler.AbstractIL.Internal.Library.Shim
+FSharp.Compiler.SourceCodeServices.FileSystemAutoOpens

-type FSharp.Compiler.AbstractIL.Layout
+type FSharp.Compiler.TextLayout.Layout

-type FSharp.Compiler.AbstractIL.Internal.TaggedText
+type FSharp.Compiler.TextLayout.TaggedText

-type FSharp.Compiler.Layout.layout
+type FSharp.Compiler.TextLayout.layout

-type FSharp.Compiler.Layout.Layout
+FSharp.Compiler.TextLayout.Layout

-module FSharp.Compiler.Layout
+module FSharp.Compiler.TextLayout.LayoutRender

-module FSharp.Compiler.LayoutOps
+module FSharp.Compiler.TextLayout.Layout

-module FSharp.Compiler.Layout.TaggedText
+module FSharp.Compiler.TextLayout.TaggedText

-module FSharp.Compiler.Layout.TaggedTextOps
+FSharp.Compiler.TextLayout.TaggedText

-module FSharp.Compiler.Layout.TaggedTextOps.Literals
+FSharp.Compiler.TextLayout.TaggedText

-type FSharp.Compiler.Range.range
+FSharp.Compiler.Text.Range

-type FSharp.Compiler.Range.pos
+FSharp.Compiler.Text.Pos

-module FSharp.Compiler.Range.Range
+module FSharp.Compiler.Text.Pos
+module FSharp.Compiler.Text.Range

-module FSharp.Compiler.QuickParse
+module FSharp.Compiler.SourceCodeServices.QuickParse

-module FSharp.Compiler.PrettyNaming
+FSharp.Compiler.SourceCodeServices.PrettyNaming

-val FSharpKeywords.PrettyNaming.KeywordNames
+FSharp.Compiler.SourceCodeServices.FSharpKeywords.KeywordNames

-val FSharpKeywords.PrettyNaming.QuoteIdentifierIfNeeded
+FSharp.Compiler.SourceCodeServices.FSharpKeywords.QuoteIdentifierIfNeeded

-val FSharpKeywords.PrettyNaming.FormatAndOtherOverloadsString
+FSharp.Compiler.SourceCodeServices.FSharpKeywords.FormatAndOtherOverloadsString
```

Renamings in `FSharp.Compiler.SourceCodeServices`:

```diff
-Lexer.*
+FSharp.Compiler.SourceCodeServices.*

-FSharpSyntaxToken*
+FSharpToken*

-FSharpErrorInfo
+FSharpDiagnostic

-FSharpErrorSeverity
+FSharpDiagnosticSeverity

-ExternalSymbol
+FSharpExternalSymbol

-UnresolvedSymbol
+FSharpUnresolvedSymbol

-CompletionKind
+FSharpCompletionKind

-module Keywords 
+module FSharpKeywords

```

* Extension methods in `ServiceAssemblyContent.fsi` are now now intrinsic methods on the symbol types themselves.
* `SemanticClassificationType` is now an enum instead of a discriminated union.
* `GetBackgroundSemanticClassificationForFile` now returns `Async<SemanticClassificationView option>` instead of `Async<struct(range * SemanticClassificationType) []>`. The `SemanticClassificationView` provides a read-only view over the semantic classification contents via the `ForEach (FSharpSemanticClassificationItem -> unit) -> unit` function.

The following namespaces have been made internal

* `FSharp.Compiler.AbstractIL.*`, aside from a small hook for JetBrains Rider
* `FSharp.Compiler.ErrorLogger.*`

New functions in the `SourceCodeServices` API:

* `FSharpDiagnostic.NewlineifyErrorString`
* `FSharpDiagnostic.NormalizeErrorString`

### F# 5 / Visual Studio 16.8 / .NET 5

This release covers three important milestones: F# 5, Visual Studio 16.8, and .NET 5.

### FSharpLanguage 5.0.0

* Package references in scripts via `#r "nuget:..."`
* String interpolation
* Support for `nameof` and the `nameof` pattern by Microsoft and [Loïc Denuzière](https://github.com/Tarmil)
* `open type` declarations
* Applicative computation expressions via `let! ... and!`
* Overloads for custom operations in computation expressions (in preview), by [Ryan Riley](https://github.com/panesofglass) and [Diego Esmerio](https://github.com/Nhowka)
* Interfaces can now be implemented at different generic instantiations
* Default interface member consumption
* Better interop with nullable value types

### FSharp Core 5.0.0


* Consistent behavior for empty/non-existent slices for lists, strings, arrays, 2D arrays, 3D arrays, and 4D arrays
* Support for fixed-index slices in 3D and 4D arrays
* Support for negative indexes (in preview)
* Reverted a change where the %A and %O and `ToString`/`string` result of a union with `RequireQualifiedAccess` gave the fully-qualified union case name in response to user feedback
* Significantly improved XML documentation for all APIs
* Optimized reflection for F# types by [kerams](https://github.com/kerams)

### FSharp Tools 11.0.0

* Improved batch compilation performance (up to 30% faster depending on the project type)
* Support for editing `#r "nuget:..."` scripts in Visual Studio
* Various fixes for F# script editing performance, especially for scripts with significant dependencies getting loaded
* Support for compiling against .NET Core on Windows when no STAThread is availble
* Support for validating signatures against XML doc comments when compiling via `/warnon:3390`
* Fixed a bug where FSharp.Core XML doc contents were not displayed in F# scripts in Visual Studio
* Support for strong name signing against F# projects when compiling using the .NET SDK
* Fixed a bug where manually referencing `Akkling.Cluster.Sharding.0.9.3` in FSI would not work
* Bring back the custom colorization options that were missed in the 16.7 update
* Support coloring disposable values independently from disposable types
* Fix a bug where emitting an `inref<_>` could cause a value to no longer be consumable by C#
* Support displaying `Some x` when debugging F# options in Visual Studio, by [Friedrich von Never](https://github.com/ForNeVeR)
* Fix a bug where compiling large anonymous records could fail in release mode
* Support for considering struct tuples and struct anonymous records as subtypes of `System.ValueTuple`
* Fix several internal errors in the compiler when compiling various niche scenarios
* Various performance enhancements when editing large files by Microsoft and [Daniel Svensson](https://github.com/Daniel-Svensson)
* Optimized output for the `string` function, by [Abel Braaksma](https://github.com/abelbraaksma)
* Various fixes for some display regressions for Find all References in Visual Studio
* Fix duplicate symbol output when renaming constructs in a project with multiple TFMs
* Support for `Int64.MinValue` as a `nativeint` literal, by [Abel Braaksma](https://github.com/abelbraaksma)
* Prevent assignment to `const` fields, by [Chet Husk](https://github.com/baronfel)
* Compiler message improvements (especially for overload resolution) by [Gauthier Segay](https://github.com/smoothdeveloper), [Vladimir Shchur](https://github.com/Lanayx), and Microsoft

### FSharp Compiler Service 38.0.2

* Add FSharp.DependencyManager.Nuget as a project reference and ensure it is in the package, allowing other editors to consume `#r "nuget:..."` references at design-time [#10784](https://github.com/dotnet/fsharp/pull/10784)

### FSharp Compiler Service 38.0.1
* Add check for system assemblies completion [#10575](https://github.com/dotnet/fsharp/pull/10575)
* Fix net sdk references discovery [#10569](https://github.com/dotnet/fsharp/pull/10569)
* Fix FSC nuget package dependencies [#10588](https://github.com/dotnet/fsharp/pull/10588)

### FSharp Compiler Service 38.0.0

The most notable change for FSharp.Compiler.Service is that it is now built and deployed as a part of the dotnet/fsharp codebase. Builds are produced nightly, matching exactly the nightly builds of the F# compiler, FSharp.Core, and F# tools.

* Support for Witness information [#9510](https://github.com/dotnet/fsharp/pull/95100) in `FSharpExpr` and `FSharpMemberOrFunctionOrValue`
* Support for Jupyter Notebooks and VSCode notebooks via `FSharp.Compiler.Private.Scripting` and .NET Interactive
* Improvements to the F# syntax tree represtation by [Eugene Auduchinok](https://github.com/auduchinok)
* Support for `const` in keyword completion info by [Alex Berezhnykh](https://github.com/DedSec256)
* Support for passing in a `PrimaryAssembly` for AST compilation routines by [Eirik Tsarpalis](https://github.com/eiriktsarpalis)
* Support for `ToString` in `FSharp.Compiler.SourceCodeServices.StringText` by [Asti](https://github.com/deviousasti)
* Fix an issue with equality comparisons for `StringText` by [Asti](https://github.com/deviousasti)

Significant changes for consumers:

* Several APIs no longer require the Reactor thread
* `GetAllUsesOfAllSymbolsInFile` now returns `seq<FSharpSymbol>` instead of `FSharpSymbol[]`
* All four `Get*UsesOfAllSymbolsIn*` APIs are no longer asynchronous
* `StructuredDescriptionTextAsync` and `DescriptionTextAsync` are now deprecated, please use `StructuredDescriptionText` and `DescriptionText` instead
* The `hasTextChangedSinceLastTypecheck` optional argument has been removed (it has been unused in Visual Studio since VS 2017)
* The `textSnapshotInfo` optional argument is removed as it only made sense in tandem with `hasTextChangedSinceLastTypecheck`
* `userOpName` is removed from all APIs that are no longer asynchronous

## Visual Studio 2017-2019 Update 16.7 release notes

These release notes contain release notes for all F# product release delivered alongside Visual Studio since Visual Studio 2017 and up until Visual Studio 2019 update 16.7.

### Visual Studio 16.7

This release is focused on closing out F# 5 feature development and making Visual Studio tooling improvements.

#### F# language

The following features were added to F# 5 preview:

* String interpolation
* Complete `nameof` design implementation
* `nameof` support for patterns
* `open type` declarations
* Overloads of custom keywords in computation expressions
* Interfaces can be implemented at different generic instantiation

#### F# compiler and tools

* Various bug fixes
* Updated semantic classification to better distinguish various F# constructs from one another
* Improvements to performance of various array-based operations in FSharp.Core by [Abel Braaksma](https://github.com/abelbraaksma)
* Various improvements to the F# compiler service by [Eugene Auduchinok](https://github.com/auduchinok)
* Improvements to SourceLink support by [Chet Husk](https://github.com/baronfel)
* Support for `Map.change` by [Fraser Waters](https://github.com/Frassle)

### Visual Studio 16.6

This release is focused on more F# 5 preview features and Visual Studio tooling improvements.

#### F# language

The following features were added to F# 5 preview:

* Support for interop with Default Interface Methods
* Improvements for `#r "nuget"` - allow packages with native dependencies
* Improvements for `#r "nuget"` - allow packages like FParsec that require `.dll`s to be referenced in a particular order
* Improved interop with Nullable Value Types

#### F# compiler and tools

* Various bug fixes
* Significant improvements to memory usage for symbol-based operations such as Find All References and Rename, especially for large codebases
* 15% or higher reduction in compile times (as measured by compiling [`FSharpPlus.dll`](https://github.com/fsprojects/FSharpPlus/))
* Performance improvements for design-time builds by [Saul Rennison](https://github.com/saul)
* Improved stack traces in F# async and other computations, thanks to [Nino Floris](https://github.com/NinoFloris)
* All new F# project types now use the .NET SDK
* Descriptions in completion lists for tooltips
* Improved UI for completion for unimported types
* Bug fix for adding a needed `open` declaration when at the top of a file (such as a script)
* Various improvements to error recovery and data shown in tooltips have been contributed by [Eugene Auduchinok](https://github.com/auduchinok) and [Matt Constable](https://github.com/mcon).
* Various compiler performance improvements by [Steffen Forkmann](https://github.com/forki/)
* Improvements to F# CodeLenses by [Victor Peter Rouven Muller](https://github.com/realvictorprm)
* Better emitted IL for `uint64` literals by [Teo Tsirpanis](https://github.com/teo-tsirpanis)

### Visual Studio 16.5

The primary focus of this release has been improving the [performance and scalability of large F# codebases in Visual Studio](https://github.com/dotnet/fsharp/issues/8255). This work was influenced by working directly with customers who have very large codebases. The performance work is [still ongoing](https://github.com/dotnet/fsharp/issues/6866), but if you have a medium-to-large sized codebase, you should see reduced memory usage.

Beyond performance enhancements, this release includes a variety of other fixes, many of which were contributed by our wonderful F# OSS community.

#### F# language

Several F# preview language features have been merged. You can try them out by setting your `LangVersion` to `preview` in your project file.

* [F# RFC FS-1076 - From the end slicing and indexing for collections](https://github.com/fsharp/fslang-design/blob/master/preview/FS-1076-from-the-end-slicing.md) has been completed for F# preview
* [F# RFC FS-1077 - Tolerant Slicing](https://github.com/fsharp/fslang-design/blob/master/preview/FS-1077-tolerant-slicing.md) has been completed for F# preview
* [F# RFC FS-1077 - Slicing for 3D/4D arrays with fixed index](https://github.com/fsharp/fslang-design/blob/master/preview/FS-1077-3d-4d-fixed-index-slicing.md) has been completed for F# preview
* [F# RFC FS-1080 - Float32 without dot](https://github.com/fsharp/fslang-design/blob/master/preview/FS-1080-float32-without-dot.md) has been completed for F# preview, contributed by [Grzegorz Dziadkiewicz](https://github.com/gdziadkiewicz)

#### F# compiler

* [Support for `--standalone`](https://github.com/dotnet/fsharp/issues/3924) has been added for .NET Core
* Various improvements to error recovery have been contributed by [Eugene Auduchinok](https://github.com/auduchinok)
* [Support for generating an AssemblyInfo from a project file](https://github.com/dotnet/fsharp/issues/3113) has been added
* [Better error reporting for mismatched Anonymous Records](https://github.com/dotnet/fsharp/issues/8091) was contributed by [Isaac Abraham](https://github.com/isaacabraham)
* A bug where [use of type abbreviations could bypass `byref` analysis](https://github.com/dotnet/fsharp/issues/7934) in the compiler has been resolved
* It is now possible to [specify the `[<Literal>]` attribute](https://github.com/dotnet/fsharp/issues/7897) in F# signature files
* A bug where the [`LangVersion` flag was culture-dependent](https://github.com/dotnet/fsharp/issues/7757) has been resolved
* A bug where [large types and expressions defined in source would lead to a stack overflow](https://github.com/dotnet/fsharp/issues/7673) has been resolved
* A bug where [arbitrary, nonsense attributes could be defined on F# type extesions](https://github.com/dotnet/fsharp/issues/7394) was resolved
* A bug where [exhaustive matches on SByte and Byte literal values emitted a warning](https://github.com/dotnet/fsharp/issues/6928) was resolved
* A bug where [invalid type abbreviations with `byref`s and `byref`-like values](https://github.com/dotnet/fsharp/issues/6133) could be defined was resolved
* A bug where [invalid binary and octal literals would be accepted by the compiler](https://github.com/dotnet/fsharp/issues/5729) was resolved, contributed by [Grzegorz Dziadkiewicz](https://github.com/gdziadkiewicz)
* A bug where [`P/Invoke to "kernel32.dll"` was getting called in a FreeBSD source build of .NET Core](https://github.com/dotnet/fsharp/pull/7858) has been resolved by [Adeel Mujahid](https://github.com/am11)
* Various smaller performance improvements have been added by [Eugene Auduchinok](https://github.com/auduchinok) and [Steffen Formann](https://github.com/forki/)

#### F# core library

* A bug where [calling `string` or `.ToString` on `ValueNone` would throw an exception](https://github.com/dotnet/fsharp/issues/7693) has been resolved
* A bug where calling `Async.Sleep` within a sequentially processed set of async expressions wouldn't process sequentially has been resolved, contributed by [Fraser Waters](https://github.com/Frassle)
* An issue in [`Async.Choice` that could lead to memory leaks](https://github.com/dotnet/fsharp/pull/7892) has been resolved, contributed by [Fraser Waters](https://github.com/Frassle)

#### F# tools for Visual Studio

* A bug where the Product Version in the About Visual Studio window [mistakenly displayed F# 4.6](https://github.com/dotnet/fsharp/issues/8018) has been resolved
* A bug where the `fsi` type in F# scripts was [incorrectly treated as not defined](https://github.com/dotnet/fsharp/issues/7950) has been resolved

#### F# open source development experience

* The FSharp.Compiler.Service build in the F# repository has been moved to use the .NET SDK, contributed by [Chet Husk](https://github.com/baronfel)

### Visual Studio 16.3

This release includes support for F# 4.7, the newest version of the F# language!

Much of F# 4.7 was dedicated to underlying infrastructural changes that allow us to deliver preview of F# language functionality more effectively. That said, there are still some nice new features delivered as well.

#### F# language and core library

We added support for F# 4.7, a minor language release that comes with compiler infrastructure to enable preview features so that we can get feedback on feature designs earlier in the development process.

The full F# 4.7 feature set is:

* Support for the `LangVersion` flag, which allows for configuring the F# language version used by the compiler to be F# 4.6 or higher
* Support for [implicit yields](https://github.com/fsharp/fslang-design/blob/master/FSharp-4.7/FS-1069-implicit-yields.md) in array, list, and sequence expressions
* [Indentation requirement relaxations](https://github.com/fsharp/fslang-design/blob/master/FSharp-4.7/FS-1070-offside-relaxations.md) for static members and constructors
* [Relaxing the need for a double-underscore (`__`)](https://github.com/fsharp/fslang-design/blob/master/FSharp-4.7/FS-1046-wildcard-self-identifiers.md) in [member declarations](https://github.com/dotnet/fsharp/pull/6829) and [`for` loops](https://github.com/dotnet/fsharp/pull/6867), contributed by [Gustavo Leon](https://github.com/gusty)
* FSharp.Core now targets `netstandard2.0` instead of `netstandard1.6`, following the deprecation of support for .NET Core 1.x
* FSharp.Core on .NET Core now supports `FSharpFunc.FromConverter`, `FSharpFunc.ToConverter`, and `FuncConvert.ToFSharpFunc`
* FSharp.Core now supports [`Async.Sequential` and an optional `maxDegreeOfParallelism` parameter for `Async.Parallel`](https://github.com/fsharp/fslang-design/blob/master/FSharp.Core-4.7.0/FS-1067-Async-Sequential.md), contributed by [Fraser Waters](https://github.com/Frassle)

In addition to the F# 4.7 feature set, this release includes support for the following preview F# language features:

* Support for [`nameof` expressions](https://github.com/fsharp/fslang-design/blob/master/preview/FS-1003-nameof-operator.md)
* Support for opening of static classes

You can enable this by seeting `<LangVersion>preview</LangVersion>` in your project file.

This release also contains the following bug fixes and improvements to the F# compiler:

* A longstanding issue where the F# compiler could stack overflow with massive records, structs, or other types has been resolved ([#7070](https://github.com/dotnet/fsharp/issues/7070))
* An issue where specifying invalid inline IL could crash Visual Studio has been resolved ([#7164](https://github.com/dotnet/fsharp/issues/7164)
* Resolution of an issue where copying of a struct would not occur if it was defined in C# and mutated in a member call ([#7406](https://github.com/dotnet/fsharp/issues/7406))
* A crypto hash of the portable PDB content created by the compiler is not included in the PE debug directory, with a configurable hash set to SHA-256 by default ([#4259](https://github.com/dotnet/fsharp/issues/4259), [#1223](https://github.com/dotnet/fsharp/issues/1223))
* A bug where `LeafExpressionConverter` ignored `ValueType` and assumed `System.Tuple` has been fixed ([#6515](https://github.com/dotnet/fsharp/issues/6515)) by [Kevin Malenfant](https://github.com/kevmal)
* A bug where `List.transpose` discaded data instead of throwing an exception has been resolved ([#6908](https://github.com/dotnet/fsharp/issues/6908)) by [Patrick McDonald](https://github.com/PatrickMcDonald)
* A bug where `List.map3` gave a misleading error when used on lists of different lengths has been resolved ([#6897](https://github.com/dotnet/fsharp/issues/6897)) by [reacheight](https://github.com/reacheight)

#### F# tools

This release also includes a few improvements to the F# tools for Visual Studio:

* Records are formatted more to look more like canonical declarations and values in tooltips and F# interactive ([#7163](https://github.com/dotnet/fsharp/issues/7163))
* Properties in tooltips now specify whether or not that are `get`-only, `set`-only, or `get` and `set` ([#7007](https://github.com/dotnet/fsharp/issues/7007))
* An issue where Go to Definition and other features could not always work across projects when files use forward slashes ([#4446](https://github.com/dotnet/fsharp/issues/4446), [#5521](https://github.com/dotnet/fsharp/issues/5521), [#4016](https://github.com/dotnet/fsharp/issues/4016)) has been fixed, with help from [chadunit](https://github.com/chadunit)
* Issues with anonymous records and debugging have been resolved ([#6728](https://github.com/dotnet/fsharp/issues/6728), [#6512](https://github.com/dotnet/fsharp/issues/6512))
* A bug where empty hash directives in source could make source text coloring seem random has been resolved ([#6400](https://github.com/dotnet/fsharp/issues/6400), [#7000](https://github.com/dotnet/fsharp/issues/7000))

### Visual Studio 16.1

This is a relatively minor release for the F# language and tools, but it's not without some goodies! As with the VS 16.0 update, this release also focused on performance of editor tooling.

#### F# compiler and F# interactive

* Added `P/Invoke` support to F# interactive on .NET Core ([#6544](https://github.com/Microsoft/visualfsharp/issues/6544))
* Added a compiler optimization for `Span<'T>` when used in a `for` loop ([#6195](https://github.com/Microsoft/visualfsharp/issues/6195))
* Added an optimization to avoid an extraneous `Some` allocations for F# options in various scenarios ([#6532](https://github.com/Microsoft/visualfsharp/issues/6532))
* Changed the execution order of expressions used in the instantiation of Anonymous Records to be top-to-bottom, rather than alphabetical, to match the current experience with normal Records ([#6487](https://github.com/Microsoft/visualfsharp/issues/6487))
* A bug where very large literal expressions or very large struct declarations could cause the compiler to stack overflow on build has been resolved ([#6258](https://github.com/Microsoft/visualfsharp/issues/6258))
* A bug where breakpoints would no longer trigger when debugging a function with an Anonymous Records has been fixed ([#6512](https://github.com/Microsoft/visualfsharp/issues/6512))
* A bug where Anonymous Records passed to constructs expecting an `obj` parameter caused a compiler crash has been fixed ([#6434](https://github.com/Microsoft/visualfsharp/issues/6434))
* A bug where `for var expr1 to expr2 do ...` loops could result in bizarrely valid (and discarded) syntax has been fixed ([#6586](https://github.com/Microsoft/visualfsharp/issues/6586))
* A bug where Anonymous Records could not be used properly with events has been fixed ([#6572](https://github.com/Microsoft/visualfsharp/issues/6572))
* A longstanding bug where extremely large generated parsers in FsLexYacc (over 100 million lines) has been resolved ([#5967](https://github.com/Microsoft/visualfsharp/issues/5976])
* A longstanding issue in the Type Provider plugin component of the compiler that could leave the door open for a memory leak caused by a type provider has been resolved ([#6409](https://github.com/Microsoft/visualfsharp/issues/6409))
* Support for `--pathmap` was added to the F# compiler by [Saul Rennison](https://github.com/saul), which resolves an issue where the resulting executable from a compilation would include absolute paths to source files in the embedded F# signature file resource ([#5213](https://github.com/Microsoft/visualfsharp/issues/5213))
* An optimization to the F# AST that improves its consumption via other tools and enviroments (e.g., [Fable](https://fable.io/)) has been added by [ncave](https://github.com/ncave) ([#6333](https://github.com/Microsoft/visualfsharp/pull/6333))
* An optimization around traversing information when resolving members has been added by [Steffen Forkmann](https://github.com/forki) ([#4457](https://github.com/Microsoft/visualfsharp/pull/4457))
* An improvement to error messages such that when a type implementation is missing necessary overrides a list of those missing overrides is reported has been added by [Gauthier Segay](https://github.com/smoothdeveloper) ([#4982](https://github.com/Microsoft/visualfsharp/issues/4982))

#### F# tools

* The Target Framework dropdown for F# projects in the .NET SDK will now include values for all available .NET Core, .NET Standard, and .NET Framework values to ease migrating to .NET Core from .NET Framework on the .NET SDK
* A bug where renaming generic type parameters produces double backtick-escaped names has been fixed ([#5389](https://github.com/Microsoft/visualfsharp/issues/5389))
* A longstanding problem where Type Providers were redundantly reinstantiated, causing massive allocations over time, has been resolved ([#5929](https://github.com/Microsoft/visualfsharp/issues/5929))
* A longstanding issue where reading IL needlessly allocated 20MB over a short period of time was been resolved ([#6403](https://github.com/Microsoft/visualfsharp/issues/6403))
* A bug where the method `GetToolTipText` in the F# compiler service could show the same XML signature for several member overloads has been resolved by [Vasily Kirichenko](https://github.com/vasily-kirichenko) ([#6244](https://github.com/Microsoft/visualfsharp/issues/6244))

#### F# open source infrastructure

Finally, we improved the contribution experience by doing the following:

* Completed our build from source process so that the F# compiler and core library can be built with the .NET Core source build repository
* Removed our dependency on `netstandard1.6` so that the entire codebase uniformly targets `netstandard2.0` and `net472`
* Added a `.vsconfig` file to the root of the repository so that contributors using Visual Studio don't have to know everything that needs installing up front
* Rewrote our project's [README](https://github.com/microsoft/visualfsharp) to remove potentially confusing information, include a quickstart for getting started with contributing, and attempting to be more inclusive in our messaging about the kinds of changes we'd love to take

### Visual Studio 16.0

F# improvements in Visual Studio 2019 are in three major areas:

* F# 4.6
* Major performance improvements for medium and larger solutions
* Lots of open source work by our excellent open source community

#### F# 4.6

This release contains the F# 4.6 language:

* [Anonymous Record types](https://github.com/fsharp/fslang-design/blob/master/FSharp-4.6/FS-1030-anonymous-records.md) have been added to the language, including full tooling support and the ability to emit them types into JavaScript objects via the [Fable](https://fable.io/) compiler.
* [ValueOption type and ValueOption module function parity with Option type](https://github.com/fsharp/fslang-design/blob/master/FSharp.Core-4.6.0/FS-1065-valueoption-parity.md).
* [tryExactlyOne function for arrays, lists, and sequences](https://github.com/fsharp/fslang-design/blob/master/FSharp.Core-4.6.0/FS-1065-valueoption-parity.md), contributed by [Grzegorz Dziadkiewicz](https://github.com/gdziadkiewicz).

#### F# compiler and FSharp.Core improvements

The F# and compiler and FSharp.Core have seen numerous improvements, especially from open source contributors:

* **fsi.exe** and **fsc.exe** now defaults to .NET Framework 4.7.2, allowing the loading of components targeting this framework or lower ([#4946](https://github.com/Microsoft/visualfsharp/issues/4946)).
* We optimized methods on structs and struct records to perform as well as methods on classes and class-based records (<a target="blank" href="https://github.com/Microsoft/visualfsharp/issues/3057">#3057</a>).
* We optimized the emitted IL for combined Boolean logic in F# code (<a target="blank" href="https://github.com/Microsoft/visualfsharp/issues/635">#635</a>).
* We've optimized the use of `+` with strings in F# to call the minimal amount of `String.Concat` calls as possible ([#5560](https://github.com/Microsoft/visualfsharp/issues/5560)).
* We fixed an issue in the FSharp.Core package where some extra directories with test assets were included. FSharp.Core 4.5.5 and 4.6.1 should have the fix ([#5814](https://github.com/Microsoft/visualfsharp/issues/5814)).
* When a user-defined attribute does not inherit from the `Attribute` class, you will now receive a warning, by [Vasily Kirichenko](https://github.com/vasily-kirichenko).
* The `AssemblyInformationVersionAttribute` value in a project file now supports arbitrary values to support scenarios such as SourceLink (<a target="blank" href="https://github.com/Microsoft/visualfsharp/issues/4822">#4822</a>).
* A bug where illegal syntax with Active Patterns would cause an internal compiler error has been fixed by <a target="blank" href="https://github.com/forki">Steffen Forkmann</a> (<a target="blank" href="https://github.com/Microsoft/visualfsharp/issues/5745">#5745</a>).
* A bug where the `Module` suffix was erroneously added to a module in a recursive module to match a type where the only difference is a generic parameter was fixed by <a target="blank" href="https://github.com/Booksbaum">BooksBaum</a> (<a target="blank" href="https://github.com/Microsoft/visualfsharp/issues/5794">#5794</a>).
* An improvement to the error message when type parameters are not adjacent to a type name has been improved by <a target="blank" href="https://github.com/voronoipotato">Alan Ball</a> (<a target="blank" href="https://github.com/Microsoft/visualfsharp/pull/4183">#4183</a>).
* The `uint16` literal suffix is listed correctly in the error messages for invalid numeric literals, by <a target="blank" href="https://github.com/teo-tsirpanis">Teo Tsirpanis</a> (<a target="blank" href="https://github.com/Microsoft/visualfsharp/pull/5712">#5712</a>).
* Error messages for computation expressions no longer state `async` in the message and instead refer to "computation expression(s)", by <a target="blank" href="https://github.com/jwosty">John Wostenberg</a> (<a target="blank" href="https://github.com/Microsoft/visualfsharp/issues/5343">#5343</a>).
* An error message when incorrectly referencing `.dll`s in F# interactive was fixed by <a target="blank" href="https://github.com/Horusiath">Bartoz Sypytkowski</a> (<a target="blank" href="https://github.com/Microsoft/visualfsharp/pull/5416">#5416</a>).
* A bug where Statically Resolved Type Parameters couldn't handle calling a member that hides an inherited member was fixed by [Victor Peter Rouven Müller](https://github.com/realvictorprm) ([#5531](https://github.com/Microsoft/visualfsharp/issues/5531)).
* Various smaller performance improvements to the compiler have been added by <a target="blank" href="https://github.com/forki">Steffen Forkmann</a> and <a target="blank" href="https://github.com/rojepp">Robert Jeppesen</a>.

#### F# performance improvements

Another big focus area for F# in Visual Studio 2019 has been performance for medium and large solutions. We addressed some very long-standing issues, some of which dating back to the very first edition of F# tools for Visual Studio. We also got some help from the excellent F# open source community.

* We've revamped how the F# language service is initialized by Roslyn. Type colorization for larger solutions should generally appear sooner.
* We changed our representation of source text to avoid large allocations over time, especially with bigger files ([#5935](https://github.com/Microsoft/visualfsharp/issues/5935), [#5936](https://github.com/Microsoft/visualfsharp/issues/5936), [#5937](https://github.com/Microsoft/visualfsharp/issues/5937), [#4881](https://github.com/Microsoft/visualfsharp/issues/4881)).
* We changed our build caches for small edits to files to use significantly less memory ([#6028](https://github.com/Microsoft/visualfsharp/issues/6028)).
* We modified a compiler feature that suggests names when unrecognized tokens are typed to only compute these suggestions on-demand, resulting in significant CPU and memory reductions when typing slowly in larger solutions ([#6044](https://github.com/Microsoft/visualfsharp/issues/6044)).
* We changed IntelliSense so that it will no longer show symbols from unopened namespaces by default. This notably improves performance for IntelliSense in projects with many references. This feature can be turned back on in the settings via **Tools > Options > Text Editor > F# > IntelliSense**.
* We improved memory usage when using Type Providers to generate very large amounts of provided types in a completion list (<a target="blank" href="https://github.com/Microsoft/visualfsharp/pull/5599">#5599</a>).
* A reduction to CPU and memory usage to an internal string comparison algorithm for suggesting unrecognized names has been fixed by [Avi Avni](https://github.com/aviavni) ([#6050](https://github.com/Microsoft/visualfsharp/pull/6050)).
* A notable source of large string allocations, particularly for IDE tooling, was fixed by [Avi Avni](https://github.com/aviavni) ([#5922](https://github.com/Microsoft/visualfsharp/issues/5922)).
* A notable source of Large Object Heap allocations coming from computing IntelliSense has been fixed by [Chet Husk](https://github.com/baronfel) ([#6084](https://github.com/Microsoft/visualfsharp/issues/6084))

#### F# tooling improvements

In addition to performance improvements, various other improvements to F# tooling for Visual Studio 2019 have been made:

* The Add `open` statement code fix will now default to adding the `open` statement at the top of the file.
* We fixed a bug where `match!` in user code invalidated structure guidelines and code outlining nodes for subsequent scopes (<a target="blank" href="https://github.com/Microsoft/visualfsharp/issues/5456">#5456</a>).
* The editor will now correctly color `byref`, `outref`, and `ref` values as record fields with the mutable value colorization (<a target="blank" href="https://github.com/Microsoft/visualfsharp/issues/5579">#5579</a>).
* We fixed a bug where the rename refactoring did not recognize the `'` character in symbol names (<a target="blank" href="https://github.com/Microsoft/visualfsharp/issues/5604">#5604</a>).
* We've fixed a longstanding bug where renaming F# script files resulted in a loss of colorization data ([#1944](https://github.com/Microsoft/visualfsharp/issues/1944)).
* We cleaned up IntelliSense so that it doesn't show unrelated items in the list when pressing backspace.
* With "Smart" indentation on, pasting F# code into the editor will now format it to match an appropriate scope based on the current cursor position, implemented by <a target="blank" href="https://github.com/saul">Saul Rennison</a> (<a target="blank" href="https://github.com/Microsoft/visualfsharp/pull/4702">#4702</a>).
* An issue where F# editor options weren't syncing has been fixed by [Jakob Majocha](https://github.com/majocha) ([#5997](https://github.com/Microsoft/visualfsharp/issues/5997), [#5998](https://github.com/Microsoft/visualfsharp/issues/5998)).
* A bug where IntelliSense in a constructor within an `inherit` clause wouldn't show the primary constructor has been fixed by [Eugene Auduchinok](https://github.com/auduchinok) ([#3699](https://github.com/Microsoft/visualfsharp/issues/3699))
* Various smaller improvements to the F# language service have been made by [Eugene Auduchinok](https://github.com/auduchinok)

##### F# open source infrastructure

We've fully migrated the F# and F# tools codebase to use the .NET SDK. This dramatically simplifies the contribution process for developers, especially if they are not using Windows. Additionally, [Jakob Majocha](https://github.com/majocha) has helped in cleaning up documents for new contributors in light of the changes to the codebase.

### Visual Studio 15.9

You can find all tracked VS 15.9 items in the [15.9 milestone](https://github.com/Microsoft/visualfsharp/milestone/24).

#### F# Compiler

* Fix (#4637) - Can't debug FCS when compiled with portable pdb debug symbols, by [Jason Imison](https://github.com/nosami).
* Fix (#5355) - We fixed a bug where extension methods that take `byref` values could mutate an immutable value.
* Fix (#5446) - We improved the compile error information for overloads on `byref`/`inref`/`outref`, rather than displaying the previously obscure error.
* Fix (#5354) - Optional Type Extensions on `byref`s are now disallowed entirely. They could be declared previously, but were unusable, resulting in a confusing user experience.
* Fix (#5294) - We fixed a bug where `CompareTo` on a struct tuple and causing a type equivalence with an aliased struct tuple would result in a runtime exception.
* Fix (#5621) - We fixed a bug where use of `System.Void` in the context of authoring a Type Provider for .NET Standard could fail to find the `System.Void` type at design-time.
* Fix (#5468) - We fixed a bug where an internal error could occur when a partially applied Discriminated Union constructor is mismatched with an annotated or inferred type for the Discriminated Union.
* Fix (#5540) - We modified the compiler error message when attempting to take an address of an expression (such as accessing a property) to make it more clear that it violates scoping rules for `byref` types.
* Fix (#5536) - We fixed a bug where your program could crash at runtime when partially applying a `byref` type to a method or function. An error message will now display.
* Fix (#5459) - We fixed an issue where an invalid combination of a `byref` and a reference type (such as `byref<int> option`) would fail at runtime and not emit an error message. We now emit an error message.

#### F# Tools for Visual Studio

* Fix (#5657) - We resolved an issue where metadata for F# assemblies built with the .NET Core SDK was not shown in file properties on Windows. You can now see this metadata by right-clicking an assembly on Windows and selecting **Properties**.
* Fix (#5615) - We fixed a bug where use of `module global` in F# source could cause Visual Studio to become unresponsive.
* Fix (#5515) - We fixed a bug where extension methods using `inref<'T>` would not show in completion lists.
* Fix (#5514) - We fixed a bug where the TargetFramework dropdown in Project Properties for .NET Framework F# projects was empty.
* Fix (#5507) - We fixed a bug where File | New Project on a .NET Framework 4.0 project would fail.

#### F# OSS Build

* Feature (#5027) - Set VisualFSharpFull as the default startup project, by [Robert Jeppesen](https://github.com/rojepp).

### Visual Studio 15.8.5

* Fix (#5504) - Internal MSBuild Error when building non-.NET SDK projects with MSBuild parallelism
* Fix (#5518) - Visual Studio-deployed components are not NGEN'd
* Fix ([Devcom 322883](https://developercommunity.visualstudio.com/content/problem/322883/all-net-framework-f-projects-build-to-4500-regardl.html)) - FSharp.Core 4.5.0.0 binary is deployed to FSharp.Core 4.4.3.0 location

All other closed issues for the VS 15.8 release can be found [here](https://github.com/Microsoft/visualfsharp/milestone/14).

### F# 4.5

We introduced the F# language version 4.5 with this release. This also corresponds with the new 4.5.x family of FSharp.Core (the F# core library). You can read the specs for each of these changes in the [F# RFC repository](https://github.com/fsharp/fslang-design). There are also many improvements to F# tools for Visual Studio with this release.

#### Releases

* Visual Studio 2017 update 15.8
* .NET Core SDK version 2.1.400 or higher

#### Language features

* Support for `voidptr`
* `NativePtr.ofVoidPtr` and `NativePtr.toVoidPtr` support
* New types: `inref<'T>`, `outref<'T>` to represent read-only and write-only `byref`s
* Support for `IsByRefLike` structs
* Support for `IsReadOnly` structs
* Full support for production and consumption of `byref` returns
* Support for extension methods for `byref`/`inref`/`outref`
* Support for `match!` in computation expressions
* Relaxed upcast requirements for `yield` in sequence, list, and array expressions
* Relaxed indentation requirements for list and array expressions
* Enumeration cases emitted as public
* Various bug fixes with `byref` programming

#### FSharp.Core features

* Version aligned to 4.5.x for the NuGet package and 4.5.0.0 for the binary
* Improved strack traces for `async { }` so that user code can now be seen
* Support for `ValueOption<'T>`
* Support for `TryGetValue` on Map

#### Compiler improvements

Improvements to the F# compiler in addition to the previously-mentioned language features are in F# 4.5. These include:

* Restored ability to inherit from `FSharpFunc`
* Removed ~2.2% of all allocations in the F# compiler
* F# reference normalization support for user control of transitive assembly references written to an output file
* Respecting `WarningsNotAsErrors`
* Error message improvement when branches of a pattern match do not return the same type
* Respecting `#nowarn "2003"`
* Other smaller performance improvements and many bug fixes

#### Tooling improvements

Significant improvements in the F# tools, such as performance enhancements and some new editor features are included this release. As always, with a large number of contributions from the F# open source community. Here are the highlights:

* We improved IntelliSense performance for .NET SDK-style projects of all forms, including those that use multi-targeting.
* A community-driven effort to analyze and improve IntelliSense performance for very large files was contributed by [Vasily Kirichenko](https://github.com/vasily-kirichenko),[ Steffen Forkmann](https://github.com/forki), and [Gauthier Segay](https://github.com/smoothdeveloper). IntelliSense in very large files (10k+ lines of code) is roughly twice as fast now.
* The warning for an outdated FSharp.Core (despite the package being installed) is no longer present in .NET SDK-style projects.
* The description tooltip that displays XML documentation for a member after . in IntelliSense no longer times out after 10 seconds.
* A bug where you could not set breakpoints in object constructor arguments has been fixed.
* A bug where a renamed symbol would be duplicated when it is a generic parameter has been fixed.
* Templates for .NET Framework (classic F# templates) now consume FSharp.Core from a NuGet package, to align with .NET SDK F# templates.
* Automatic, transactional brace completion is now available for `()`, `[]`, `{}`, `[||]`, and `[<>]` brace pairs. We did this work in collaboration with [Gibran Rosa](https://github.com/gibranrosa).
* You can now go to definition with **Ctrl + Click** on an F# symbol. The settings for this gesture are also respected in the **Tools > Options** window.
* The IntelliSense performance UI has been modified to allow configuration of stale typecheck information for various IDE features. Explanations for each option are now present in tooltips for the settings.
* Brace match highlighting now correctly highlights braces, completed in collaboration with [Vasily Kirichenko](https://github.com/vasily-kirichenko).
* Go to definition now navigates correctly when a type is defined recursively, contributed by [Vasily Kirichenko](https://github.com/vasily-kirichenko).
* A bug where an auto-imported namespace wasn't opened when the top of a file was empty has been fixed by [Vasily Kirichenko](https://github.com/vasily-kirichenko).
* A bug where `printf` specifiers that contained dots were miscolored has been fixed by [Vasily Kirichenko](https://github.com/vasily-kirichenko).
* A bug where all opens were considered unused inside of a recursive module has been fixed by [Vasily Kirichenko](https://github.com/vasily-kirichenko).
* Autocompletion for attributes now only suggests options that are actually attributes, contributed by [Vasily Kirichenko](https://github.com/vasily-kirichenko).
* Signature Help tooltips are now generated for Type Provider static parameters at the constructor call site, contributed by[Vasily Kirichenko](https://github.com/vasily-kirichenko).
* A bug where value types used as units of measure were colored as reference types has been fixed by [Vasily Kirichenko](https://github.com/vasily-kirichenko).
* A bug where semantic colorization could disappear for some files while scrolling has been fixed by  [Vasily Kirichenko](https://github.com/vasily-kirichenko).
There is now an experimental CodeLens implementation, contributed by [Victor Peter Rouven Müller](https://github.com/realvictorprm). You can turn it on in **Options > Text Editor > F# > Code Lens**.
* A bug where the F# compiler service would incorrectly elide the module names in XML documentation has been fixed by [Sebastian Urban](https://github.com/surban).
* Code that uses `Dictionary` with `ContainsKey` and subsequent `Item` calls has been changed to use `TryGetValue`, by [Eugene Auduchinok](https://github.com/auduchinok).
* [Jakob Majoka](https://github.com/majocha) also contributed in the process of consuming a different API for FSharpToolTip.

#### Infrastructure, Packaging, and Open Source Improvements

We made the following enhancements to infrastructure, packaging, and our open source contribution experience:

* The F# compiler distributed with Visual Studio no longer installs as a singleton in the F# Compiler SDK location. It is now fully side-by-side with Visual Studio, meaning that side-by-side installations of Visual Studio wil finally have truly side-by-side F# tooling and language experiences.
* The FSharp.Core NuGet package is now signed.
* ETW logging has been added to the F# tools and compiler.
* The very large `control.fs`/`control.fsi` files in FSharp.Core have been split into `async.fs`/`async.fsi`, `event.fs`/`event.fsi`, `eventmodule.fs`/`eventmodule.fsi`, `mailbox.fs`/`mailbox.fsi`, and `observable.fs`/`observable.fsi`.
* We added .NET SDK-style versions of our project performance stress test artifacts.
* We removed Newtonsoft.json from our codebase, and you now have one less package downloaded for OSS contributors.
* We now use the latest versions of System.Collections.Immutable and System.Reflection.Metadata.

### F# 4.1

#### Releases

* Visual Studio 2017 updates 15.0 to 15.8 (exclusive)
* .NET Core SDK versions 1.0 to 2.1.400 (exclusive)

#### Language and Core Library features

* Struct tuples
* Initial support for consuming C#-style `ref` returns
* Struct record support with the `[<Struct>]` attribute
* Struct Discriminated Union support with the `[<Struct>]` attribute
* `Result<'TSuccess, 'TFailure>` type, with supporting functions in FSharp.Core
* Support for the `fixed` keyword to pin a pointer-tyle local to the stack
* Underscores in numeric literals
* Caller Info Attribute Argument support
* `namespace rec` and `module rec` to support mutually referential types and functions within the same file
* Implicit "Module" suffix added to modules that share the same name as a type
* Tuple equality for F# tuples and `System.Tuple`

#### Compiler improvements

* Support for generating Portable PDBs
* Significant improvements to error messages, particularly to aid with suggestions
* Performance improvements
* Interoperability improvements
* Support for geenerating F# AssymblyInfo from properties for .NET SDK projects
* `--debug:full` support for F# on .NET Core on Windows
* `MakeTuple` support for struct tuples
* Warnings are forwarded when searching for method overloads
* Support for emitting an enum-specific warning when pattern matching over one
* Many smaller bug fixes

#### FSharp.Core features

* Support for `NativePtr.ByRef`
* Support for `Async.StartImmediateAsTask`
* Support for `Seq.transpose`/`Array.transpose`/`List.transpose`
* `IsSerializable` support for `Option` and `Async<'T>`
* Many smaller bug fixes

#### IDE features for F# tools in Visual Studio

Most items here contributed by community members.

* Default installation of F# wherever .NET Core is installed
* Significant memory reductions in F# tooling
* IntelliSense Filters and Glyphs
* Support for Go to All
* Find all Reference support
* In-memory cross-project references support
* QuickInfo supports type colorization
* QuickInfo supports navigable links that will invoke Go to Definition
* Inline Rename support
* Go to Definition from F# to C# support
* Semantic document highlighting for selected symbols
* Support for Structured Guidelines and code outlining, which is toggleable
* Support for `EditorBrowsable(EditorBrowsableState.Never)`
* Code fix for making Record and Discriminated Union case lables upper-case
* Code fix to make suggestions for an unkown identifier
* Code fix for prefixing or replacing an unused value with an underscore
* Code fix to add the `new` keyword to a disposable type
* Code fix to add an `open` statement at the top for a symbol coming from an unopened namespace or module
* Code fix to simplify a name by removing unnecessary namespace qualifiers
* Graying out unused values in the editor
* Colorized `fsi.exe` when ran as a standalone console application
* Autocompletion support, including symbols from unopened namespaces
* Colorization for mutable values to distinguish them from immutable values
* Support for move up/down on solution folder nodes
* Support for Blue (High Contrast) theming
* Full support for .NET Core and .NET Standard projects, with the ability to create new ASP.NET Core projects in F#
* Full support for ASP.NET Web SDK tooling, such as Azure publish UI, for F# projects
* Property page auto-sizing support for different monitors
* Smart indentation support which auto-indents based on scope and auto-deindents for bracket-like characters
* XML documentation comment width scaling to prevent it running horizontally off the screen
* Multiple settings pages to modify tooling settings
* Support for Drag and Drop across folders
* Support for nightly builds of the tools
* Expanded debugger view for lists from 50 items to 5000 items
* Support for Optimization APIs in the compiler service
* Support for `IsNameGenerated` in the F# symbols API

## FSharp Compiler Service Versions Release notes

## 37.0.0

This release bring a number of new changes, including a massive enhancement to SemanticClassification types thanks to @cartermp.

From dotnet/fsharp:333eb82c8..d9e070a9d:

* now allows for multiple implementations of generic interfaces (preview feature) (thanks @0x53A!)
* the default assembly set for script resolution now includes System.Numerics (thanks @KevinRansom)
* #r nuget package resolution is now committed eagerly, instead of delayed (thanks @KevinRansom)
* reduced allocations for some strings in the compiler (thanks @kerams)
* better printing for SRTP constraints (thanks @abelbraaksma/@realvictorprm)
* more expressive DUs for semantic classification (thanks @cartermp)
* remove legacymsbuildreferenceresolver (thanks @KevinRandom)
* supply witnesses for op_UnaryPlus (thanks @TIHan)
* clean up CE classifications (thanks @cartermp)
* Fixed tostring/equality for StringText (thanks @deviousasti)
* Fixed error text for FS0874
* Disallow assignment to C# Const/Readonly properties (thanks @baronfel)
* Allow Int64.MinValue as a valid nativeint literal (thanks @abelbraaksma)
* Enhancements to the nameof feature (preview feature)
* String interpolation (preview feature)

### 36.0.3

This is a small bugfix release that fixes a nuspec package dependency issue with Sourcelink

### 36.0.2

This is a small bugfix release that I'm making primarily to publish a version
of FCS with sourcelink enabled, so that tooling users can make use of that information.

From dotnet/fsharp:079276b4b..37d0cccec:

* Fixes for `do!` handling in computation expressions (thanks @baronfel)
* Add missing versions in FCS' Interactive header (thanks @nightroman)
* Support `Source`-translation in `match!` expressions (thanks @baronfel)
* Ensure stack traces from uncaught exceptions in CEs are maintained (thanks @NinoFloris)
* Better handling of `inline` in witness-passing codepaths (thanks @dsyme)
* Enable publishing of FCS with sourcelink (thanks @baronfel)
* Extend `nameof` to support naming generic parameters (`nameof<'t>`) and instance members (`nameof(Unchecked.defaultof<C>.Property)`) (thanks @dsyme)

### 36.0.1

From dotnet/fsharp:522dd906c..16bca5aef:

* Fixes to attribute range parsing (thanks @auduchinok)
* Added nested exception details to Type Provider load errors (thanks @dsyme)
* Improved error messages around unknown identifiers in patterns (thanks @jbeeko)
* Better dependency probing behavior with Facade assemblies (thanks @KevinRansom)
* APIs for programmatically adding and retrieving bindings in an FSI session (thanks @TIHan)
* Optional parameter on `FSharpChecker.FindBackgroundReferencesInFile` API to allow for stale results (thanks @TIHan)
* Better tooltips for function arguments (thanks @mcon)
* Many performance tweaks to various compiler function implementations (thanks @forki)
* Typo fixes in the AST (thanks @auduchinok)
* Better IL emitted for usages of static members as System.Action (thanks @MoFtZ)
* Allow for indexers to specify a type annotation (thanks @TIHan)
* Allow languages/scripts that have no notion of upper-case to use their characters for DU identifiers (thanks @KevinRansom) 
* more optimized comparison/equality for DateTime (thanks @cartermp)
* add support for `char` for the `GenericZero/GenericOne` mechanisms (thanks @Happypig375)
* enhancements for the dependencymanager's resolution for native scripts (thanks @KevinRansom)
* more consistent support for type-directed nullable parameters (thanks @dsyme)
* fix FSI's ordering of out-of-order dlls in nuget packages (thanks @KevinRansom)
* New SyntaxTree.Paren syntax node (thanks @auduchinok)
* add SRTP witness solutions (via the new `CallWithWitnesses` pattern) (thanks @dsyme)

### 35.0.0

This version bumps the major due to API surface area changes in the AST and TAST. In addition, there's a new package for the
built-in Nuget dependency manager for FSI: FSharp.DependencyManager.Nuget

Members are now in SyntaxTree/SyntaxTreeOps and TypedTree/TypedTreeBasics/TypedTreeOps/TypedTreePickle.

From dotnet/fsharp:d1a3d0705..522dd906c:

* Improved error recovery from patterns (thanks @auduchinok)
* Smaller IL Emit for unsigned 64-bit constants (thanks @teo-tsirpanis)
* Improved ProvidedTypes Type generation (thanks @DedSec256)
* Improved CodeLenses provided (thanks @realvictorprm)
* Optimize internal member calculations in PrettyNaming and items lookup (thanks @auduchinok)
* More fixes to compiler internals, ranges, etc (thanks @auduchinok)
* Support for consumption of C# Default Interfaces
* Better encapsulation of ProvidedExpr members (thanks @DedSec256)

### 34.1.1

From dotnet/fsharp:3777cd4d8..836da28c0:

* Slight tweaks to error messages around numeric literals (Thanks @Happypig375)
* Deny taking native address of an immutable local value (Thanks @TIHan)
* Fixes to reported ranges for wildcard self-identifiers, module abbreviations, nested modules, attributes, nested types, and fields (Thanks @auduchinok)
* Better compiler error recovery for errors in constructor expressions (Thanks @auduchinok)
* Fix handling of F# Options in C# members with regards to nullable type interop (Thanks @TIHan)
* Move dependency handling of native dlls to the DependencyManager (Thanks @KevinRansom)

### 34.1.0

From dotnet/fsharp:3af8959b6..9d69b49b7:

* set default script TFM to netcoreapp3.1 if none found
* improve C#-nullable and optional interop (RFC FS-1075)
* Add type name to `undefined name error` if known
* improve printing via %A/fsi
* misc. improvements to DependencyManager
* improve type provider support for .netcoreapp3.1 target frameworks.
* New, optimized find-all-references API with reduced memory usage.
* better error messages for failed overload resolution

### 34.0.1

Contains commits from 32b124966 to d7018737c from dotnet/fsharp. Notable changes include:

* lowered allocations for large strings and char arrays (notably source file texts)
* improved support for byref-like rules with regards to type abbreviations
* better support for scopes in recursive modules
* better location of .net core reference assemblies
* lowered allocations for several internal compiler structures
* better error messages for anonymous record mismatches
* FSharpChecker learned how to keep background symbol uses
* Project cracker/project cracker tool were removed
* Better support for consuming C# in-ref parameters
* new services around simplifying names and finding unused declarations
* package management in scripts (in preview)
* and-bang syntax support (in preview)

### 33.0.1

Integrate dotnet/fsharp from 4f5f08320 to 7b25d7f82. Notable changes include:

* Addition of the FsharpUnionCase.HasFields property
* FCS builds entirely on .Net Core now
* Better debug information for ranges
* Support for Literal values in signature files
* Using memory-mapped files cross-platform to read IL.

### 33.0.0

Integrate dotnet/fsharp from 48f932cf8 to 085985140. Notable changes include:

    allowing '_' as a self-identifier
    events for FSI evaluation lifecycle events
    enhancements to FSI return-values
    fixed parsing for langversion CLI arguments
    allow cancellation of FSI interactions
    ToString fixes for value options
    Fixes for code generation in autogenerated members for very large records
    make ranges of member declarations capture the entire member
    improve error recovery in the parser
    improve logic for auto-detecting framework assemblies for FSI

### 32.0.0

* integrate dotnet/fsharp from e1b8537ee to 48f932cf8
* notable changes include:
* (preview) nameof
* (preview) open static classes
* Fixed 64-bit integer literal parsing
* Better exhaustiveness checking for byte and sbyte pattern matches
* Better native resource handling
* Script-host assembly load events

### 31.0.0

* Integrate dotnet/fsharp from 5a8f454a1 to 05c558a61
* Notable changes include:
  * Removal of the `Microsoft.FSharp.Compiler.SourceCodeServices` namespace
  * A new API for determining if an identifier needs to be quoted is available: `FSharp.Compiler.LexHelp.FSharpKeywords.DoesIdentifierNeedQuotation`
  * Enhancements to the correctness of PDBs
  * Better string formatting of records and values
  * More stack overflow fixes in the compiler
  * Inline IL parsing error handling
  * `LeafExpressionConverter` handles struct tuples
  * `FSharpChecker` now can parse a file without caching: `ParseFileNoCache`

### 30.0.0

* Integrate dotnet/fsharp from 25560f477 to 5a8f454a1
* Notable improvements include:
  * performance improvements
  * FCS APIs for FSComp and Implement Interface
  * line directive handling
  * improved performance when computing quick fix suggestions

### 29.0.1

* Fix versioning of the assembly

### 29.0.0

* Integrate visualfsharp master from 165b736b9 (2019-03-29) to 25560f477 (2019-05-24)
* Notable improvements include:
  * Improved Portable PDB debugging
  * Misc IL generation fixes
  * Representing inlined mutable variables in the AST
  * Moving on from net46 targeting
  * Fixes for anonymous record generation
  * Dependency updates
  * Checking for constructors in FSharpMemberOrFunctionOrValue
  * Fix unused opens false positive for record fields

### 28.0.0

* Integrate visualfsharp master from efb57cf56 to 8dfc02feb
* Notable improvements include:
  * XmlDoc fixes for overloads
  * Fixes for deterministic compilation
  * Improved tail-recursion when processing large expressions
  * Better tooltip detection for operators with constraints
  * FSharp.Compiler.Service nuget now uses net461, netstandard2.0 and FSharp.Core 4.6.2
  * updated lexer and parser implementations to reduce stackoverflow likelihood on .net core

### 27.0.1

* Integrate visualfsharp master from 5a5ca976ec296d02551e79c3eb8e8db809e4304d to 2c8497bb097d5c5d3ef12f355594873838a48494
* Notable improvements include:
  * Anonymous Record support for expressions
  * Union Case Naming fixes
  * Trimming of the nuget package dependencies from 26.0.1

### 26.0.1

* Integrate visualfsharp master to 99e307f3a3ef2109ba6542ffc58affe76fc0e2a0

### 25.0.1

* Integrate visualfsharp master to 15d9391e78c554f91824d2be2e69938cd811df68

### 24.0.1

* Integrate visualfsharp master to 59156db2d0a744233d1baffee7088ca2d9f959c7

### 23.0.3

* Clarify package authors

### 23.0.1

* Integrate visualfsharp master to ee938a7a5cfdf4849b091087efbf64605110541f

### 22.0.3

* [Add entity.DeclaringEntity](https://github.com/Microsoft/visualfsharp/pull/4633), [FCS feature request](https://github.com/fsharp/FSharp.Compiler.Service/issues/830)

### 22.0.2

* Use correct version number in DLLs (needed until https://github.com/Microsoft/visualfsharp/issues/3113 is fixed)

### 22.0.1

* Integrate visualfsharp master
* Includes recent memory usage reduction work for ByteFile and ILAttributes

### 21.0.1

* Use new .NET SDK project files
* FSharp.Compiler.Service nuget now uses net461 and netstandard2.0
* FSharp.Compiler.Service netstandard2.0 now supports type providers
  
### 20.0.1

* Integrate visualfsharp master

### 19.0.1

* Rename ``LogicalEnclosingEntity`` to ``ApparentEnclosingEntity`` for consistency int he F# codebase terminology.
* Rename ``EnclosingEntity`` to ``DeclaringEntity``.  In the case of extension properties, ``EnclosingEntity`` was incorrectly returning the logical enclosing entity (i.e. the type the property appears to extend), and in this case ``ApparentEnclosingEntity`` should be used instead.

### 18.0.1

* Integrate visualfsharp master

### 17.0.2

* Integrate visualfsharp master

### 16.0.3

* [File name deduplication not working with ParseAndCheckFileInProject](https://github.com/fsharp/FSharp.Compiler.Service/issues/819)

### 16.0.2

* [ProjectCracker returns *.fsi files in FSharpProjectOptions.SourceFiles array](https://github.com/fsharp/FSharp.Compiler.Service/pull/812)
* [Fix line endings in the Nuget packages descriptions](https://github.com/fsharp/FSharp.Compiler.Service/pull/811)

### 16.0.1

* FSharpChecker provides non-reactor ParseFile instead of ParseFileInProject
* Add FSharpParsingOptions, GetParsingOptionsFromProjectOptions, GetParsingOptionsFromCommandLine

### 15.0.1

* Integrate latest changes from visualfsharp
* Add implementation file contents to CheckFileResults
* Fix non-public API in .NET Standard 1.6 version

### 14.0.1

* Integrate latest changes from visualfsharp
* Trial release for new build in fcs\...

### 13.0.1

* Move docs --> docssrc

### 13.0.0

* Move FSharp.Compiler.Service.MSBuild.v12.dll to a separate nuget package

### 12.0.8

* Set bit on output executables correctly

### 12.0.7

* Integrate visualfsharp master

### 12.0.6

* [758: Fix project cracker when invalid path given](https://github.com/fsharp/FSharp.Compiler.Service/pull/758)

### 12.0.5

* Remove dependency on System.ValueTuple

### 12.0.3

* [De-duplicate module names again](https://github.com/fsharp/FSharp.Compiler.Service/pull/749)

### 12.0.2

* De-duplicate module names

### 12.0.1

* [Integrate visualfsharp and fsharp](https://github.com/fsharp/fsharp/pull/696)

### 11.0.10

* [Fix F# Interactive on Mono 4.0.9+](https://github.com/fsharp/fsharp/pull/696)

### 11.0.9

* [Make incremental builder counter atomic](https://github.com/fsharp/FSharp.Compiler.Service/pull/724)
* [Add IsValCompiledAsMethod to FSharpMemberOrFunctionOrValue](https://github.com/fsharp/FSharp.Compiler.Service/pull/727)
* [Check before ILTypeInfo.FromType](https://github.com/fsharp/FSharp.Compiler.Service/issues/734)
* [Transition over to dotnet cli Fsproj](https://github.com/fsharp/FSharp.Compiler.Service/issues/700)

### 11.0.8

* Depend on FSharp.Core package

### 11.0.6

* Fix [stack overflow exception](https://github.com/fsharp/FSharp.Compiler.Service/issues/672)

### 11.0.4

* Fix [out of range exception](https://github.com/fsharp/FSharp.Compiler.Service/issues/709)

### 11.0.2

* Integrate fsharp\fsharp and Microsoft\visualfsharp to 262deb017cfcd0f0d4138779ff42ede7dbf44c46

### 11.0.1

* Integrate fsharp\fsharp and Microsoft\visualfsharp to d0cc249b951374257d5a806939e42714d8a2f4c6

### 10.0.3

* [Expose assumeDotNetFramework in FSharpChecker.GetProjectOptionsFromScript](https://github.com/fsharp/FSharp.Compiler.Service/pull/699)
* [SemanticClassificationType should not be internal](https://github.com/fsharp/FSharp.Compiler.Service/pull/696)

### 10.0.1

* [Adds FormatValue to FsiEvaluationSession, using the fsi object values for formatting](https://github.com/fsharp/FSharp.Compiler.Service/pull/686)

### 10.0.0

* Integrate fsharp\fsharp and Microsoft\visualfsharp to c3e55bf0b10bf08790235dc585b8cdc75f71618e
* Integrate fsharp\fsharp and Microsoft\visualfsharp to 11c0a085c96a91102cc881145ce281271ac159fe
* Some API changes for structured text provision for tagged structured text

### 9.0.0

* Update names of union fields in AST API
* Fix load closure for ParseAndCheckInteraction
* [Fix #631 compiler dependency on MSBuild](https://github.com/fsharp/FSharp.Compiler.Service/pull/657)
* Fixed netcore codegen on Linux
* Explicit error when cracker exe is missing

### 8.0.0

* Integrate fsharp\fsharp and Microsoft\visualfsharp to c494a9cab525dbd89585f7b733ea5310471a8001
* Then integrate to 	2002675f8aba5b3576a924a2e1e47b18e4e9a83d
* [Add module values to navigable items](https://github.com/fsharp/FSharp.Compiler.Service/pull/650)
* Optionally remove dependency on MSBuild reference resolution https://github.com/fsharp/FSharp.Compiler.Service/pull/649
* [Compiler api harmonise](https://github.com/fsharp/FSharp.Compiler.Service/pull/639)
* Various bits of work on .NET Core version (buildable from source but not in nuget package)

### 7.0.0

* Integrate fsharp\fsharp and Microsoft\visualfsharp to 835b79c041f9032fceeceb39f680e0662cba92ec

### 6.0.2

* [Fix #568: recognize provided expressions](https://github.com/fsharp/FSharp.Compiler.Service/pull/568)

### 6.0.1

* [Fix ProjectFileNames order when getting project options from script](https://github.com/fsharp/FSharp.Compiler.Service/pull/594)

### 6.0.0

* Switch to new major version on assumption integrated F# compiler changes induce API change

### 5.0.2

* Integrate Microsoft\visualfsharp to 688c26bdbbfc766326fc45e4d918f87fcba1e7ba. F# 4.1 work

### 5.0.1

* [Fixed dependencies in nuget package](https://github.com/fsharp/FSharp.Compiler.Service/pull/608)

### 5.0.0

* Fixed empty symbol declared pdb #564 from kekyo/fix-empty-pdb
* .NET Core ProjectCracker - updated version and dependencies
* Properly embed 'FSIstrings' resource, fixes #591
* make build.sh work on windows (git bash).
* Added default script references for .NET Core
* Store useMonoResolution flag
* Updated MSBuild version
* Assume FSharp.Core 4.4.0.0

### 4.0.1

* Integrate Microsoft\visualfsharp and fsharp\fsharp to master (including portable PDB)
* Remove .NET Framework 4.0 support (now needs .NET Framework 4.5)

### 4.0.0

* Integrate Microsoft\visualfsharp and fsharp\fsharp to master

### 3.0.0.0

* #538 - BackgroundCompiler takes a very long time on a big solution with a very connected project dependency graph
* #544 - Losing operator call when one of operands is application of a partially applied function
* #534 - Function valued property erasing calls
* #495 - Detupling missing when calling a module function value
* #543 - Tuple not being destructured in AST
* #541 - Results of multiple calls to active pattern are always bound to variable with same name
* #539 - BasicPatterns.NewDelegate shows same value for different arguments

### 2.0.0.6

* #530 - Adjust ProjectCracker NuGet for VS/NuGet

### 2.0.0.5

* #527 - Provide API that includes printf specifier arities along with ranges

### 2.0.0.4

* #519 - Change nuget layout for ProjectCracker package
* #523 - Project cracking: spaces in file paths

### 2.0.0.3

* #508 - Integrate visualfsharp/master removal of Silverlight #if
* #513 - Make CrackerTool `internal` to prevent accidental usage
* #515 - Add simple Visual Studio version detection for project cracker

### 2.0.0.2

* Integrate visualfsharp/master and fsharp/master --> master
* Expose QualifiedName and FileName of FSharpImplementationFileContents
* Add FSharpDiagnostic.ErrorNumber

### 2.0.0.1-beta

* Fix 452 - FSharpField.IsMutable = true for BCL enum cases
* Fix 414 - Add IsInstanceMemberInCompiledCode

### 2.0.0.0-beta

* Feature #470, #478, #479 - Move ProjectCracker to separate nuget package and DLL, used ProjectCrackerTool.exe to run
* Feature #463 - Expose slot signatures of members in object expressions
* Feature #469, #475 - Add EvalExpressionNonThrowing, EvalInteractionNonThrowing, EvalScriptNonThrowing
* Fix #456 - FCS makes calls to kernel32.dll when running on OSX
* Fix #473 - stack overflow in resolution logic
* Fix #460 - Failure getting expression for a provided method call

### 1.4.2.3 -

* Fix bug in loop optimization, apply https://github.com/Microsoft/visualfsharp/pull/756/

### 1.4.2.2 -

* #488 - Performance problems with project references

### 1.4.2.1 -

* #450 - Correct generation of ReferencedProjects

### 1.4.2.0 -

* Fix bug in double lookup of cache, see https://github.com/fsharp/FSharp.Compiler.Service/pull/447

### 1.4.1 -

* Add pause before backgrounnd work starts. The FCS request queue must be empty for 1 second before work will start
* Write trace information about the reactor queue to the event log
* Rewrite reactor to consistently prioritize queued work
* Implement cancellation for queued work if it is cancelled prior to being executed
* Adjust caching to check cache correctly if there is a gap before the request is executed

### 1.4.0.9 -

* FSharpType.Format fix
* Disable maximum-memory trigger by default until use case ironed out

### 1.4.0.8 -

* FSharpType.Format now prettifies type variables.  If necessary, FSharpType.Prettify can also be called
* Add maximum-memory trigger to downsize FCS caches. Defaults to 1.7GB of allocaed memory in the system
  process for a 32-bit process, and 2x this for a 64-bit process

### 1.4.0.7 -

* fix 427 - Make event information available for properties which represent first-class uses of F#-declared events
* fix 410 - Symbols for C# fields (and especially enum fields)
* Expose implemented abstract slots
* Fix problem with obscure filenames caught by Microsoft\visualfsharp tests
* Integrate with visualfsharp master

### 1.4.0.6 -

* fix 423 - Symbols for non-standard C# events
* fix 235 - XmlDocSigs for references assemblies
* fix 177 - GetAllUsesOfAllSymbolsInFile returns nothing for C# nested enum
* make Internal.Utilities.Text.Lexing.Position a struct
* Exposing assembly attributes on FSharpAssemblySignature
* clean up IncrementalFSharpBuild.frameworkTcImportsCache

### 1.4.0.5 -

* add more entries to FSharpTokenTag

### 1.4.0.4 -

* add more entries to FSharpTokenTag
* add PrettyNaming.QuoteIdentifierIfNeeded and PrettyNaming.KeywordNames

### 1.4.0.3 -

* integrate Microsoft/visualfsharp OOB cleanup via fsharp/fsharp
* Make Parser and Lexer private

### 1.4.0.2 -

* #387 - types and arrays in F# attribute contructor arguments

### 1.4.0.1 - F# 4.0 support

* Use FSharp.Core 4.4.0.0 by default for scripting scenarios if not FSharp.Core referenced by host process

### 1.4.0.0-beta - F# 4.0 support

* Integrate F# 4.0 support into FSharp.Compiler.Service

### 1.3.1.0 -

* simplified source indexing with new SourceLink
* Add noframework option in AST compiler methods

### 0.0.90 -

* Add fix for #343 Use ResolveReferences task
* Expose BinFolderOfDefaultFSharpCompiler to editors
* Fix the registry checking on mono to avoid unnecessary exceptions being thrown

### 0.0.89 -

* Fix output location of referenced projects

### 0.0.88 -
* Added Fix to al
* low implicit PCL references to be retrieved

### 0.0.87 -

* Don't report fake symbols in indexing #325
* Add EnclosingEntity for an active pattern group #327
* Add ImmediateSubExpressions #284
* integrate fsharp/fsharp master into master

### 0.0.85 -

* Fix for FSharpSymbolUse for single case union type #301
* Added supprt for ReturnParameter in nested functions

### 0.0.84 -

* Added curried parameter groups for nested functions

### 0.0.83 -

* Add Overloads to the symbols signature so it is publicly visible
* Update OnEvaluation event to have FSharpSymbolUse information available

### 0.0.82 -

* Better support for Metadata of C# (and other) Assemblies.
* Expose the DefaultFileSystem as a type instead of anonymous

### 0.0.81 -

* Update GetDeclarationListSymbols to expose FSharpSymbolUse
* Improve reporting of format specifiers

### 0.0.80 -

* Update to latest F# 3.1.3 (inclunding updated FsLex/FsYacc used in build of FCS)
* Report printf specifiers from Service API
* Improve Accessibility of non-F# symbols

### 0.0.79 -

* Do not use memory mapped files when cracking a DLL to get an assembly reference
* Fix for multilanguage projects in project cracker

### 0.0.78 -

* Reduce background checker memory usage
* add docs on FSharp.Core
* docs on caches and queues

### 0.0.77 -

* Update to github.com/fsharp/fsharp 05f426cee85609f2fe51b71473b07d7928bb01c8

### 0.0.76 -

* Fix #249 - Fix TryFullName when used on namespaces of provided erased type definitions
* Add OnEvaluation event to FCS to allow detailed information to be exposed

### 0.0.75 -

* Do not use shared cursor for IL binaries (https://github.com/fsprojects/VisualFSharpPowerTools/issues/822)

### 0.0.74 -

* Extension members are returned as members of current modules
* Fix exceptions while cross-reference a type provider project

### 0.0.73 -

* Add AssemblyContents and FSharpExpr to allow access to resolved, checked expression trees
* Populate ReferencedProjects using ProjectFileInfo
* Fix finding symbols declared in signature files
* Add logging to project cracking facility

### 0.0.72 -

* Allow project parser to be used on project file with relative paths
* Expose attributes for non-F# symbols

### 0.0.71 -

* More renamings in SourceCodeServices API for more consistent use of 'FSharp' prefix

### 0.0.70 -

* Make FSharpProjectFileParser public
* Fixes to project parser for Mono (.NET 4.0 component)
* Renamings in SourceCodeServices API for more consistent use of 'FSharp' prefix

### 0.0.67 -

* Fixes to project parser for Mono

### 0.0.66 -

* Fixes to project parser for Mono
* Use MSBuild v12.0 for reference resolution on .NET 4.5+

### 0.0.65 -

* Fixes to project parser

### 0.0.64 -

* Add project parser, particularly GetProjectOptionsFromProjectFile

### 0.0.63 -

* #221 - Normalize return types of .NET events

### 0.0.62 -

* Integrate to latest https://github.com/fsharp/fsharp (#80f9221f811217bd890b3a670d717ebc510aeeaf)

### 0.0.61 -

* #216 - Return associated getters/setters from F# properties
* #214 - Added missing XmlDocSig for FSharpMemberOrFunctionOrValue's Events, Methods and Properties
* #213 - Retrieve information for all active pattern cases
* #188 - Fix leak in file handles when using multiple instances of FsiEvaluationSession, and add optionally collectible assemblies

### 0.0.60 -

* #207 - Add IsLiteral/LiteralValue to FSharpField
* #205 - Add IsOptionalArg and related properties to FSharpParameter
* #210 - Check default/override members via 'IsOverrideOrExplicitMember'
* #209 - Add TryFullName to FSharpEntity

### 0.0.59 -

* Fix for #184 - Fix EvalScript by using verbatim string for #Load
* Fix for #183 - The line no. reporting is still using 0-based indexes in errors. This is confusing.

### 0.0.58 -

* Fix for #156 - The FSharp.Core should be retrieved from the hosting environment

### 0.0.57 -

* Second fix for #160 - Nuget package now contains .NET 4.0 and 4.5

### 0.0.56 -

* Fix for #160 - Nuget package contains .NET 4.0 and 4.5

### 0.0.55 -

* Integrate changes for F# 3.1.x, Fix #166

### 0.0.54 -

* Fix for #159 - Unsubscribe from TP Invalidate events when disposing builders

### 0.0.53 -

* Add queue length to InteractiveChecker

### 0.0.52 -

* Fix caches keeping hold of stale entries

### 0.0.51 -

* Add IsAccessible to FSharpSymbol, and ProjectContext.AccessibilityRights to give the context of an access

### 0.0.50 -

* Fix #79 - FindUsesOfSymbol returns None at definition of properties with explicit getters and setters

### 0.0.49 -

* Fix #138 - Fix symbol equality for provided type members
* Fix #150 - Return IsGetterMethod = true for declarations of F# properties (no separate 'property' symbol is yet returned, see #79)
* Fix #132 - Add IsStaticInstantiation on FSharpEntity to allow clients to detect fake symbols arising from application of static parameters
* Fix #154 - Add IsArrayType on FSharpEntity to allow clients to detect the symbols for array types
* Fix #96 - Return resolutions of 'Module' and 'Type' in "Module.field" and "Type.field"

### 0.0.48 -

* Allow own fsi object without referencing FSharp.Compiler.Interactive.Settings.dll (#127)

### 0.0.47 -

* Adjust fix for #143 for F# types with abstract+default events

### 0.0.46 -

* Fix multi-project analysis when referenced projects have changed (#141)
* Fix process exit on bad arguments to FsiEvaluationSession (#126)
* Deprecate FsiEvaluationSession constructor and add FsiEvaluationSession.Create static method to allow for future API that can return errors
* Return additional 'property' and 'event' methods for F#-defined types to regularize symbols (#108, #143)
* Add IsPropertySetterMethod and IsPropertyGetterMethod which only return true for getter/setter methods, not properties. Deprecate IsSetterMethod and IsGetterMethod in favour of these.
* Add IsEventAddMethod and IsEventRemoveMethod which return true for add/remove methods with an associated event
* Change IsProperty and IsEvent to only return true for the symbols for properties and events, rather than the methods associated with these
* Fix value of Assembly for some symbols (e.g. property symbols)

### 0.0.45 -

* Add optional project cache size parameter to InteractiveChecker
* Switch to openBinariesInMemory for SimpleSourceCodeServices
* Cleanup SimpleSourceCodeServices to avoid code duplication

### 0.0.44 -

* Integrate latest changes from visualfsharp.codeplex.com via github.com/fsharp/fsharp
* Fix problem with task that generates description text of declaration
* Add AllInterfaceTypes to FSharpEntity and FSharpType
* Add BaseType to FSharpType to propagate instantiation
* Add Instantiate to FSharpType

### 0.0.43 -

* Fix #109 - Duplicates in GetUsesOfSymbolInFile

### 0.0.42 -

* Fix #105 - Register enum symbols in patterns
* Fix #107 - Return correct results for inheritance chain of .NET types
* Fix #101 - Add DeclaringEntity property

### 0.0.41 -

* Fixed #104 - Make all operations that may utilize the FCS reactor async
* Add FSharpDisplayContext and FSharpType.Format
* Replace GetSymbolAtLocationAlternate by GetSymbolUseAtLocation

### 0.0.40 -

* Fixed #86 - Expose Microsoft.FSharp.Compiler.Interactive.Shell.Settings.fsi
* Fixed #99 - Add IsNamespace property to FSharpEntity

### 0.0.39 -

* Fixed #79 - Usage points for symbols in union patterns

### 0.0.38 -

* Fixed #94 and #89 by addition of new properties to the FSharpSymbolUse type
* Fixed #93 by addition of IsOpaque to FSharpEntity type
* Fixed #92 - Issue with nested classes
* Fixed #87 - Allow analysis of members from external assemblies

### 0.0.37 -

* Obsolete HasDefaultValue - see https://github.com/fsharp/FSharp.Compiler.Service/issues/77

### 0.0.36 -

* Fix #71 - Expose static parameters and xml docs of type providers
* Fix #63 - SourceCodeServices: #r ignores include paths passed as command-line flags

### 0.0.35 -

* Fix #38 - FSharp.Compiler.Services should tolerate an FSharp.Core without siginfo/optdata in the search path


### 0.0.34 -

* Add StaticParameters property to entities, plus FSharpStaticParameter symbol
* Fix #65

### 0.0.33 -

* Add FullName and Assembly properties for symbols
* Fix #76
* Add Japanese documentation

### 0.0.32 -

* Make ParseFileInProject asynchronous
* Add ParseAndCheckFileInProject
* Use cached results in ParseAndCheckFileInProject if available

### 0.0.31 -

* Fix performance problem with CheckFileInProject

### 0.0.30 -

* Add initial prototype version of multi-project support, through optional ProjectReferences in ProjectOptions. Leave this empty
  to use DLL/file-based references to results from other projects.

### 0.0.29 -

* Fix symbols for named union fields in patterns

### 0.0.28 -

* Fix symbols for named union fields
* Add FSharpActivePatternCase to refine FSharpSymbol

### 0.0.27 -

* Fix exception tag symbol reporting

### 0.0.26 -

* Fix off-by-one in reporting of range for active pattern name

### 0.0.25 -

* Add optional source argument to TryGetRecentTypeCheckResultsForFile to specify that source must match exactly

### 0.0.24 -

* Update version number as nuget package may not have published properly

### 0.0.23 -

* Move to one-based line numbering everywhere
* Provide better symbol information for active patterns

### 0.0.22 -

* Provide symbol location for type parameters

### 0.0.21 -

* Add GetUsesOfSymbolInFile
* Better symbol resolution results for type parameter symbols

### 0.0.20 -

* Update version number as nuget package may not have published properly

### 0.0.19 -

* Change return type of GetAllUsesOfSymbol, GetAllUsesOfAllSymbols and GetAllUsesOfAllSymbolsInFile to FSharpSymbolUse
* Add symbol uses when an abstract member is implemented.

### 0.0.18 -

* Add GetAllUsesOfAllSymbols and GetAllUsesOfAllSymbolsInFile

### 0.0.17 -

* Improvements to symbol accuracy w.r.t. type abbreviations

### 0.0.16 -

* Make FSharpEntity.BaseType return an option
* FsiSesion got a new "EvalScript" method which allows to evaluate .fsx files

### 0.0.15 -

* Update version number as nuget package may not have published properly

### 0.0.14 -

* Update version number as nuget package may not have published properly

### 0.0.13-alpha -

* Fix #39 - Constructor parameters are mistaken for record fields in classes

### 0.0.12-alpha -

* Make the parts of the lexer/parser used by 'XmlDoc' tools in F# VS Power tools public

### 0.0.11-alpha -

* Add 'IsUnresolved'

### 0.0.10-alpha -

* Fix bug where 'multiple references to FSharp.Core' was given as error for scripts

### 0.0.9-alpha -

* Fix fsc corrupting assemblies when generating pdb files (really)
* Give better error messages for missing assemblies
* Report more information about symbols returned by GetSymbolAtLocation (through subtypes)
* Fix typos in docs
* Return full project results from ParseAndCheckInteraction
* Be more robust to missing assembly references by default.

### 0.0.8-alpha -

* Fix fsc corrupting assemblies when generating pdb files

### 0.0.7-alpha -

* Fix docs
* Make symbols more robust to missing assemblies
* Be robust to failures on IncrementalBuilder creation
* Allow use of MSBuild resolution by IncrementalBuilder

### 0.0.6-alpha -

* Fix version number

### 0.0.5-alpha -

* Added GetUsesOfSymbol(), FSharpSymbol type, GetSymbolAtLocation(...)

### 0.0.4-alpha -

* Added documentation of file system API
* Reporte errors correctly from ParseAndCheckProject

### 0.0.3-alpha -

* Integrate FSharp.PowerPack.Metadata as the FSharp* symbol API
* Renamed Param --> MethodGroupItemParameter and hid record from view, made into an object
* Renamed Method --> MethodGroupItem and hid record from view, made into an object
* Renamed Methods --> MethodGroup and hid record from view, made into an object
* Renamed MethodGroup.Name --> MethodGroup.MethodName
* Renamed DataTip --> ToolTip consistently across all text
* Renamed CheckOptions --> ProjectOptions
* Renamed TypeCheckAnswer --> CheckFileAnswer
* Renamed UntypedParseInfo --> ParseFileResults
* Removed GetCheckOptionsFromScriptRoot member overload in favour of optional argument
* Renamed GetCheckOptionsFromScriptRoot --> GetProjectOptionsFromScript
* Renamed UntypedParse --> ParseFileInProject
* Renamed TypeCheckSource --> CheckFileInProjectIfReady
* Added numerous methods to API including CheckFileInProject
* Added experimental GetBackgroundCheckResultsForFileInProject, GetBackgroundParseResultsForFileInProject
* Added PartialAssemblySignature to TypeCheckResults/CheckFileResults
* Added CurrentPartialAssemblySignature to FsiEvaluationSession
* Added ParseAndCheckInteraction to FsiEvaluationSession to support intellisense implementation against a script fragment
* Added initial testing in tests/service
* Added ParseAndCheckProject to SourceCodeServices API. This will eventually return "whole project" information such as symbol tables.
* Added GetDefaultConfiguration to simplify process of configuring FsiEvaluationSession
* Added PartialAssemblySignatureUpdated event to FsiEvaluationSession
* Added travis build

### 0.0.2-alpha -

* Integrate hosted FSI configuration, SimpleSourceCodeServices, cleanup to SourceCodeServices API

## Older Visual F# releases

### [4.0.0] - Visual Studio 2015 Update 1 - 30 November 2015

#### Enhancements

* Perf: `for i in expr do body` optimization [#219](https://github.com/Microsoft/visualfsharp/pull/219)
* Remove type provider security dialog and use custom icon for type provider assembly reference [#448](https://github.com/Microsoft/visualfsharp/pull/448)
* Perf: Enable parallel build inside Visual Studio [#487](https://github.com/Microsoft/visualfsharp/pull/487)
* Perf: Remove StructBox for Value Types [#549](https://github.com/Microsoft/visualfsharp/pull/549)
* Add compiler warnings for redundant arguments in raise/failwith/failwithf/nullArg/invalidOp/invalidArg [#630](https://github.com/Microsoft/visualfsharp/pull/630)
* Add a compiler warning for lower case literals in patterns [#666](https://github.com/Microsoft/visualfsharp/pull/666)

#### Bug fixes

* Fix scope of types for named values in attributes improperly set [#437](https://github.com/Microsoft/visualfsharp/pull/437)
* Add general check for escaping typars to check phase [#442](https://github.com/Microsoft/visualfsharp/pull/442)
* Fix AccessViolationException on obfuscated assemblies [#519](https://github.com/Microsoft/visualfsharp/pull/519)
* Fix memory leaks while reloading solutions in Visual Studio [#591](https://github.com/Microsoft/visualfsharp/pull/591)
* Enable breakpoints in `with` augmentations for class types [#608](https://github.com/Microsoft/visualfsharp/pull/608)
* Fix false escaping type parameter check error [#613](https://github.com/Microsoft/visualfsharp/pull/613)
* Fix quotation of readonly fields [#622](https://github.com/Microsoft/visualfsharp/pull/622)
* Keep the reference icons when opening references [#623](https://github.com/Microsoft/visualfsharp/pull/623)
* Don't suppress missing FSI transitive references [#626](https://github.com/Microsoft/visualfsharp/pull/626)
* Make Seq.cast's non-generic and generic IEnumerable implementations equivalent [#651](https://github.com/Microsoft/visualfsharp/pull/651)

### [4.0.0] - 20 July 2015

Includes commits up to `dd8252eb8d20aaedf7b1c7576cd2a8a82d24f587`

#### Language, compiler, runtime, interactive

* Normalization and expansion of `Array`, `List`, and `Seq` modules
* New APIs for 4.0: `chunkBySize`, `contains`, `except`, `findBack`, `findInstanceBack`, `indexed`, `item`, `mapFold`, `mapFoldBack`, `sortByDescending`, `sortDescending`, `splitInto`, `tryFindBack`, `tryFindIndexBack`, `tryHead`, `tryItem`, `tryLast`
  ![Collection API additions](http://i.imgur.com/SdJ7Doh.png)
* Other new APIs
* `Option.filter`, `Option.toObj`, `Option.ofObj`, `Option.toNullable`, `Option.ofNullable`
* `String.filter`
* `Checked.int8`, `Checked.uint8`
* `Async.AwaitTask` (non-generic)
* `WebClient.AsyncDownloadFile`, `WebClient.AsyncDownloadData`
* `tryUnbox`, `isNull`
* New active pattern to match constant `Decimal` in quotations
* Slicing support for lists
* Support for consuming high-rank (> 4) arrays
* Support for units of measure in `printf`-family functions
* Support for constructors/class names as first-class functions
* Improved exception stack traces in async code
* Automatic `mutable`/`ref` conversion
* Support for static arguments to provided methods
* Support for non-nullable provided types
* Added `NonStructuralComparison` module containing non-structural comparison operators
* Support for rational exponents in units of measure
* Give fsi.exe, fsiAnyCpi.exe nice icons
* `Microsoft.` optional in namepsace paths from FSharp.Core
* Support for extension properties in object initializers
* Pre-support (not yet used) for additional nativeptr intrinsics
* Simplified, more robust resolution of type references in quotations
* Support for inheritance of types that have multiple interface instantiations
* Extended preprocessor grammar
* Support for implicit quotation of expressions used as method arguments
* Support for multiple properties in `[<StructuredFormatDisplay>]`
* Eliminate tuple allocation for implicitly returned formal arguments
* Perf: fsc.exe now uses `GCLatencyMode.Batch`
* Perf: Improved `hash`/`compare`/`distinctBy`/`groupBy` performance
* Perf: `Seq.toArray` perf improvement
* Perf: Use `OptimizedClosures.FSharpFunc` in seq.fs where applicable
* Perf: Use literals and mutable variables instead of ref cells for better performance in SHA1 calc
* Perf: Use smart blend of `System.Array.Copy` and iterative copy for array copies
* Perf: Change `Seq.toList` to mutation-based to remove reliance on `List.rev`
* Perf: Change `pdbClose` to test if files are locked before inducing GCs
* Perf: Use server GC mode for compiler
* Bugfix: Changed an error message within the Set module to use the correct module name.
* Bugfix: Fix assembly name of warning FS2003
* Bugfix [#132](http://visualfsharp.codeplex.com/workitem/132): FSI Shadowcopy causes a significant degrade in the fsi first execute time
* Bugfix [#131](https://visualfsharp.codeplex.com/workitem/131): Fix getentryassembly return value when shadowcopy is enabled in FSI
* Bugfix [#61](https://visualfsharp.codeplex.com/workitem/61) Nonverifiable code generated with units of measure conversion
* Bugfix [#68](https://visualfsharp.codeplex.com/workitem/68) BadImageFormatException with Units of Measure
* Bugfix [#146](https://visualfsharp.codeplex.com/workitem/146) BadImageFormatException in both Release and Debug build with units of measure
* Bugfix: Incorrent cross-module inlining between different .NET profiles
* Bugfix: Properly document exceptions in `Array` module
* Bugfix [#24](https://visualfsharp.codeplex.com/workitem/24): Error reporting of exceptions in type providers `AddMemberDelayed`
* Bugfix [#13](https://github.com/fsharp/fsharp/issues/13): Error on FSI terminal resize
* Bugfix [#29](https://github.com/fsharp/fsharp/issues/29): Module access modifier `internal` does not give internal access if no namespaces are used
* Bugfix: Fix typo in error message for invalid attribute combination
* Bugfix [#27](https://github.com/microsoft/visualfsharp/issues/27): Private module values can be mutated by other modules
* Bugfix [#38](https://github.com/microsoft/visualfsharp/issues/38): ICE - System.ArgumentException: not a measure abbreviation, or incorrect kind
* Bugfix [#44](https://github.com/microsoft/visualfsharp/issues/44): Problems using FSI to `#load` multiple files contributing to the same namespace
* Bugfix [#95](https://github.com/microsoft/visualfsharp/issues/95): `[<RequireQualifiedAccess>]` allows access to DU member if qualified only by module name
* Bugfix [#89](https://github.com/microsoft/visualfsharp/issues/89): Embedding an untyped quotation in a typed quotation results in ArgumentException
* Bugfix: Show warning when Record is accessed without type but `[<RequireQualifiedAccess>]` was set
* Bugfix [#139](https://visualfsharp.codeplex.com/workitem/139): Memory leak in `Async.AwaitWaitHandle`
* Bugfix [#122](https://github.com/microsoft/visualfsharp/issues/122): `stfld` does not give `.volatile` annotation
* Bugfix [#30](https://github.com/microsoft/visualfsharp/issues/30): Compilation error "Incorrect number of type arguments to local call"
* Bugfix [#163](https://github.com/microsoft/visualfsharp/issues/163): Array slicing does not work properly with non 0-based arrays
* Bugfix [#148](https://github.com/microsoft/visualfsharp/issues/148): XML doc comment generation adding empty garbage
* Bugfix [#98](https://github.com/Microsoft/visualfsharp/issues/98): Using a single, optional, static parameter to a type provider causes failure
* Bugfix [#109](https://github.com/Microsoft/visualfsharp/issues/109): Invalid interface generated by --sig
* Bugfix [#123](https://github.com/Microsoft/visualfsharp/issues/123): Union types without sub-classes should be sealed
* Bugfix [#68](https://github.com/Microsoft/visualfsharp/issues/68): F# 3.1 / Profile 259: `<@ System.Exception() @>` causes AmbiguousMatchException at runtime
* Bugfix [#9](https://github.com/Microsoft/visualfsharp/issues/9): Internal error in FSI: FS0192: binding null type in envBindTypeRef
* Bugfix [#10](https://github.com/Microsoft/visualfsharp/issues/10): Internal error: binding null type in envBindTypeRef
* Bugfix [#266](https://github.com/Microsoft/visualfsharp/issues/266): `windowed` error message incorrectly flags "non-negative" input when "positive" is what's needed
* Bugfix [#270](https://github.com/Microsoft/visualfsharp/issues/270): "internal error: null: convTypeRefAux" in interactive when consuming quotation containing type name with commas or spaces
* Bugfix [#276](https://github.com/Microsoft/visualfsharp/issues/276): Combining struct field with units of measure will result managed type instead of unmanaged type
* Bugfix [#269](https://github.com/Microsoft/visualfsharp/issues/269): Accidentally `#load`ing a DLL in script causes internal error
* Bugfix [#293](https://github.com/Microsoft/visualfsharp/issues/293): `#r` references without relative path are not loaded when file is local
* Bugfix [#237](https://github.com/Microsoft/visualfsharp/issues/237): Problems using FSI on multiple namespaces in a single file
* Bugfix [#338](https://github.com/Microsoft/visualfsharp/issues/338): Escaped unicode characters are encoded incorrectly
* Bugfix [#370](https://github.com/Microsoft/visualfsharp/issues/370): `Seq.sortBy` cannot handle sequences of floats containing NaN
* Bugfix [#368](https://github.com/Microsoft/visualfsharp/issues/368): Optimizer incorrectly assumes immutable field accesses are side-effect free
* Bugfix [#337](https://github.com/Microsoft/visualfsharp/issues/337): Skip interfaces that lie outside the set of referenced assemblies
* Bugfix [#383](https://github.com/Microsoft/visualfsharp/issues/383): Class with `[<AllowNullLiteral(false)>]` barred from inheriting from normal non-nullable class
* Bugfix [#420](https://github.com/Microsoft/visualfsharp/issues/420): Compiler emits incorrect visibility modifier for internal constructors of abstract class
* Bugfix [#362](https://github.com/Microsoft/visualfsharp/issues/362): Depickling assertion followed by nullref internal errors in units-of-measure case
* Bugfix [#342](https://github.com/Microsoft/visualfsharp/issues/342): FS0193 error when specifying sequential struct layout of a type
* Bugfix [#299](https://github.com/Microsoft/visualfsharp/issues/299): AmbiguousMatchException with `[<ReflectedDefinition>]` on overloaded extension methods
* Bugfix [#316](https://github.com/Microsoft/visualfsharp/issues/316): Null array-valued attribute causes internal compiler error
* Bugfix [#147](https://github.com/Microsoft/visualfsharp/issues/147): FS0073: internal error: Undefined or unsolved type variable: 'a
* Bugfix [#34](https://github.com/Microsoft/visualfsharp/issues/34): Error in pass2 for type FSharp.DataFrame.FSharpFrameExtensions, error: duplicate entry 'Frame2.GroupRowsBy' in method table
* Bugfix [#212](https://github.com/Microsoft/visualfsharp/issues/212): Record fields initialized in wrong order
* Bugfix [#445](https://github.com/Microsoft/visualfsharp/issues/445): Inconsistent compiler prompt message when using `--pause` switch
* Bugfix [#238](https://github.com/Microsoft/visualfsharp/issues/238): Generic use of member constraint solved to record field causes crash

#### Visual Studio

* Updated all templates (except tutorial) to include AssemblyInfo.fs setup in the same manner as default C# project templates
* Add keyboard shortcuts for FSI reset and clear all
* Improved debugger view for Map values
* Improved performance reading stdout/stderr from fsi.exe to F# Interactive window
* Support for VS project up-to-date check
* Improved project template descriptions, make it clearer how to target Xamarin platforms
* Intellisense completion in object initializers
* Add menu entry "Open folder in File Explorer" on folder nodes
* Intellisense completion for named arguments
* `Alt+Enter` sends current line of code to interactive if there is no selection
* Support for debugging F# scripts with the VS debugger
* Add support for hexadecimal values (like 0xFF) ??to MSBuild property BaseAddress
* Updated menu icons used for F# interactive to align with other VS interactive windows
* Bugfix: Fix url of fsharp.org website in vs templates
* Bugfix [#141](https://visualfsharp.codeplex.com/workitem/141): The "Error List" window does not parse MSBuild messages correctly
* Bugfix [#147](https://visualfsharp.codeplex.com/workitem/147): Go to definition doesn't work for default struct ctors
* Bugfix [#50](https://github.com/microsoft/visualfsharp/issues/50): Members hidden from IntelliSense still show up in tooltips
* Bugfix [#57](https://github.com/microsoft/visualfsharp/issues/57) (partial): Visual Studio locking access to XML doc files
* Bugfix [#157](https://github.com/Microsoft/visualfsharp/issues/157): Should not allow Framework 4 / F# 3.1 combination in project properties
* Bugfix [#114](https://github.com/Microsoft/visualfsharp/issues/114): Portable Library (legacy) template displays wrong target framework version
* Bugfix [#273](https://github.com/Microsoft/visualfsharp/issues/273): VS editor shows bogus errors when scripts use multi-hop `#r` and `#load` with relative paths
* Bugfix [#312](https://github.com/Microsoft/visualfsharp/issues/312): F# library project templates and portable library templates do not have `AutoGenerateBindingRedirects` set to true
* Bugfix [#321](https://github.com/Microsoft/visualfsharp/issues/321): Provided type quickinfo shouldn't show hidden and obsolete members from base class
* Bugfix [#319](https://github.com/Microsoft/visualfsharp/issues/319): Projects with target runtime 3.0 don't show up correctly on the VS project dialog
* Bugfix [#283](https://github.com/Microsoft/visualfsharp/issues/283): Changing target framework causes incorrect binding redirects to be added to app.config
* Bugfix [#278](https://github.com/Microsoft/visualfsharp/issues/278): NullReferenceException when trying to add some COM references
* Bugfix [#259](https://github.com/Microsoft/visualfsharp/issues/259): Renaming files in folders causes strange UI display
* Bugfix [#350](https://github.com/Microsoft/visualfsharp/issues/350): Renaming linked file results in error dialog
* Bugfix [#381](https://github.com/Microsoft/visualfsharp/issues/381): Intellisense stops working when referencing PCL component from script (requires `#r "System.Runtime"`)
* Bugfix [#104](https://github.com/Microsoft/visualfsharp/issues/104): Using paste to add files to an F# project causes the order of files in the project and on the UI to get out of sync
* Bugfix [#417](https://github.com/Microsoft/visualfsharp/issues/417): 'Move file up/down' keybindings should be scoped to solution explorer
* Bugfix [#246](https://github.com/Microsoft/visualfsharp/issues/246): Fix invalid already rendered folder error
* Bugfix [#106](https://github.com/Microsoft/visualfsharp/issues/106) (partial): Visual F# Tools leak memory while reloading solutions

### [3.1.2] - 20 August 2014

Includes commits up to `3385e58aabc91368c8e1f551650ba48705aaa285`

#### Language, compiler, runtime, interactive

* Allow arbitrary-dimensional slicing
* Ship versions FSharp.Core.dll built on portable profiles 78 and 259
* Support "shebang" (`#!`) in F# source files
* Vertical pipes disallowed in active pattern case identifiers
* Enable non-locking shadow copy of reference assemblies in fsi/fsianycpu
* Inline codegen optimization using structs
* Perf improvement for `Seq.windowed`
* exe.config files for fsc, fsi, fsianycpu now use simple version range instead of long set of explicit version redirects
* Bugfix [#72](https://visualfsharp.codeplex.com/workitem/72): Indexer properties with more than 4 arguments cannot be accessed
* Bugfix [#113](https://visualfsharp.codeplex.com/workitem/113): `Async.Sleep` in .NETCore profiles does not invoke error continuation
* Bugfix [#91](https://visualfsharp.codeplex.com/workitem/91): String module documentation is false
* Bugfix [#78](https://visualfsharp.codeplex.com/workitem/78): Allow space characters in active pattern case identifiers
* Bugfix: Invalid code generated when calling VB methods with optional byref args
* Bugfix [#69](https://visualfsharp.codeplex.com/workitem/69): Invalid code generated when calling C# method with optional nullable args
* Bugfix [#9](https://visualfsharp.codeplex.com/workitem/9): XML doc comments on F# record type fields do not appear when accessing in C#
* Bugfix [#59](https://visualfsharp.codeplex.com/workitem/59): Compiler always requires System.Runtime.InteropServices, this is not present in all portable profiles
* Bugfix [#17](https://visualfsharp.codeplex.com/workitem/17): Incorrect generation of XML from doc comments for Record fields
* Bugfix [#7](https://visualfsharp.codeplex.com/workitem/17): NullRef in list comprehension, when for loop works
* Bugfix [#1](https://visualfsharp.codeplex.com/workitem/1): Type inference involving generic param arrays
* Bugfix [#37](https://visualfsharp.codeplex.com/workitem/37): Perf regression in 3.1.0 related to resolving extension methods
* Bugfix: Can't run F# console application with 'update' in name
* Bugfix: Slicing and range expression inconsistent
* Bugfix: Invalid code is generated when using field initializers in struct constructor

#### Visual Studio

* Project templates for F# portable libraries targeting profiles 78 and 259
* Enable non-locking shadow copy of reference assemblies in fsi/fsianycpu (VS options added)
* Allow breakpoints to be set inside of quotations
* Support "Publish" action in project system for web, Azure
* Bugfix [#126](https://visualfsharp.codeplex.com/workitem/126): F# package installer does not honor custom install paths for express SKUs
* Bugfix [#75](https://visualfsharp.codeplex.com/workitem/75): Microsoft.FSharp.Targets shim not deployed with F# SDK
* Bugfix: Fix crash in smart indent provider
* Bugfix [#55](https://visualfsharp.codeplex.com/workitem/55): Cannot add reference to F# PCL project
* Bugfix: Typos in tutorial project script
* Bugfix: Required C# event members do not appear in intellisense when signature is (object, byref)

### [3.1.1] - 24 January 2014

#### Language, compiler, runtime, interactive

* Improve F# compiler telemetry
* Bugfix: Improper treatment of * in AssemblyVersion attribute
* Bugfix: ``sprintf "%%"`` returns `"%%"` in F# 3.1.0, previously returned `"%"` in F# 3.0 and earlier
* Bugfix: F# 3.0 1D slice setter does not compile in F# 3.1.0

#### Visual Studio

* Enable installation of Visual F# on VS Desktop Express
* Added support for showing xml doc comments for named arguments
* Visual F# package deployable on non-VS machines. Deploys compiler and runtime toolchain plus msbuild targets
* Bugfix: Errors when attempting to add reference to .NET core library
* Bugfix: Crash in `FSComp.SR.RunStartupValidation()`

[4.0.0]: https://fsharp.org
[3.1.2]: http://blogs.msdn.com/b/fsharpteam/archive/2014/08/20/announcing-the-release-of-visual-f-tools-3-1-2.aspx
[3.1.1]: http://blogs.msdn.com/b/fsharpteam/archive/2014/01/22/announcing-visual-f-3-1-1-and-support-for-desktop-express.aspx


Features Added in F# Language Versions
======================================

# [F# 4.7](https://docs.microsoft.com/en-us/dotnet/fsharp/whats-new/fsharp-47)

- Compiler support for `LangVersion`
- Implicit `yield`s
- No more required double underscore (wildcard identifier)
- Indentation relaxations for parameters passed to constructors and static methods

# [F# 4.6](https://docs.microsoft.com/en-us/dotnet/fsharp/whats-new/fsharp-46)

- Anonymous records
- `ValueOption` module functions

# [F# 4.5](https://docs.microsoft.com/en-us/dotnet/fsharp/whats-new/fsharp-45)

- Versioning alignment of binary, package, and language
- Support for `Span<'T>` and related types
- Ability to produce `byref` returns
- The `voidptr` type
- The `inref<'T>` and `outref<'T>` types to represent readonly and write-only `byref`s
- `IsByRefLike` structs
- `IsReadOnly` structs
- Extension method support for `byref<'T>`/`inref<'T>`/`outref<'T>`
- `match!` keyword in computation expressions
- Relaxed upcast with `yield` in F# sequence/list/array expressions
- Relaxed indentation with list and array expressions
- Enumeration cases emitted as public

# [F# 4.1](https://fsharp.org/specs/language-spec/4.1/FSharpSpec-4.1-latest.pdf)

- Struct tuples which inter-operate with C# tuples
- Struct annotations for Records
- Struct annotations for Single-case Discriminated Unions
- Underscores in numeric literals
- Caller info argument attributes
- Result type and some basic Result functions
- Mutually referential types and modules within the same file
- Implicit `Module` syntax on modules with shared name as type
- Byref returns, supporting consuming C# `ref`-returning methods
- Error message improvements
- Support for `fixed`

# [F# 4.0](https://fsharp.org/specs/language-spec/4.0/FSharpSpec-4.0-final.pdf)

- `printf` on unitized values
- Extension property initializers
- Non-null provided types
- Primary constructors as functions
- Static parameters for provided methods
- `printf` interpolation
- Extended `#if` grammar
- Multiple interface instantiations
- Optional type args
- Params dictionaries

# [F# 3.1](https://fsharp.org/specs/language-spec/3.1/FSharpSpec-3.1-final.pdf)

- Named union type fields
- Extensions to array slicing
- Type inference enhancements

# [F# 3.0](https://fsharp.org/specs/language-spec/3.0/FSharpSpec-3.0-final.pdf)

- Type providers
- LINQ query expressions
- CLIMutable attribute
- Triple-quoted strings
- Auto-properties
- Provided units-of-measure

# [F# 2.0](https://fsharp.org/specs/language-spec/2.0/FSharpSpec-2.0-April-2012.pdf)

- Active patterns
- Units of measure
- Sequence expressions
- Asynchronous programming
- Agent programming
- Extension members
- Named arguments
- Optional arguments
- Array slicing
- Quotations
- Native interoperability
- Computation expressions

# [F# 1.1](https://docs.microsoft.com/en-us/archive/blogs/dsyme/a-taste-of-whats-new-in-f-1-1)

- Interactive environment
- Object programming
- Encapsulation Extensions

# [F# 1.0](https://docs.microsoft.com/en-us/archive/blogs/dsyme/welcome-to-dons-f-blog)

- Discriminated unions
- Records
- Tuples
- Pattern matching
- Type abbreviations
- Object expressions
- Structs
- Signature files
- Imperative programming
- Modules (no functors)
- Nested modules
- .NET Interoperability
