// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System
open System.ComponentModel.Composition
open System.Windows.Media

open Microsoft.VisualStudio
open Microsoft.VisualStudio.Editor
open Microsoft.VisualStudio.PlatformUI
open Microsoft.VisualStudio.Shell
open Microsoft.VisualStudio.Shell.Interop
open Microsoft.Internal.VisualStudio.Shell.Interop
open Microsoft.VisualStudio.Language.StandardClassification
open Microsoft.VisualStudio.Text.Classification
open Microsoft.VisualStudio.Utilities
open Microsoft.CodeAnalysis.Classification

open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.EditorServices

[<RequireQualifiedAccess>]
module internal FSharpClassificationTypes =
    let [<Literal>] Function = "FSharp.Function"
    let [<Literal>] MutableVar = "FSharp.MutableVar"
    let [<Literal>] DisposableType = "FSharp.DisposableType"
    let [<Literal>] Printf = "FSharp.Printf"
    let [<Literal>] DisposableLocalValue = "FSharp.DisposableLocalValue"
    let [<Literal>] DisposableTopLevelValue = "FSharp.DisposableTopLevelValue"

    let getClassificationTypeName = function
        | SemanticClassificationType.MutableRecordField
        | SemanticClassificationType.MutableVar -> MutableVar
        | SemanticClassificationType.DisposableType -> DisposableType
        | SemanticClassificationType.Namespace -> ClassificationTypeNames.NamespaceName
        | SemanticClassificationType.Printf -> Printf
        | SemanticClassificationType.Exception
        | SemanticClassificationType.Module
        | SemanticClassificationType.Type
        | SemanticClassificationType.TypeDef
        | SemanticClassificationType.ConstructorForReferenceType
        | SemanticClassificationType.ReferenceType -> ClassificationTypeNames.ClassName
        | SemanticClassificationType.ConstructorForValueType
        | SemanticClassificationType.ValueType -> ClassificationTypeNames.StructName
        | SemanticClassificationType.ComputationExpression
        | SemanticClassificationType.IntrinsicFunction -> ClassificationTypeNames.Keyword
        | SemanticClassificationType.UnionCase
        | SemanticClassificationType.Enumeration -> ClassificationTypeNames.EnumName
        | SemanticClassificationType.Field
        | SemanticClassificationType.UnionCaseField -> ClassificationTypeNames.FieldName
        | SemanticClassificationType.Interface -> ClassificationTypeNames.InterfaceName
        | SemanticClassificationType.TypeArgument -> ClassificationTypeNames.TypeParameterName
        | SemanticClassificationType.Operator -> ClassificationTypeNames.Operator
        | SemanticClassificationType.Function -> Function
        | SemanticClassificationType.Method -> ClassificationTypeNames.MethodName
        | SemanticClassificationType.ExtensionMethod -> ClassificationTypeNames.ExtensionMethodName
        | SemanticClassificationType.Literal -> ClassificationTypeNames.ConstantName
        | SemanticClassificationType.Property
        | SemanticClassificationType.RecordFieldAsFunction
        | SemanticClassificationType.RecordField -> ClassificationTypeNames.PropertyName // TODO - maybe pick something that isn't white by default like Property?
        | SemanticClassificationType.NamedArgument -> ClassificationTypeNames.LabelName
        | SemanticClassificationType.Event -> ClassificationTypeNames.EventName
        | SemanticClassificationType.Delegate -> ClassificationTypeNames.DelegateName
        | SemanticClassificationType.DisposableTopLevelValue -> DisposableTopLevelValue
        | SemanticClassificationType.Value -> ClassificationTypeNames.Identifier
        | SemanticClassificationType.DisposableLocalValue -> DisposableLocalValue
        | SemanticClassificationType.LocalValue -> ClassificationTypeNames.LocalName
        | SemanticClassificationType.Plaintext -> ClassificationTypeNames.Text
        | _ -> failwith "Compiler Bug: Unknown classification type"

module internal ClassificationDefinitions =

    [<Export>]
    [<Export(typeof<ISetThemeColors>)>]
    type internal ThemeColors
        [<ImportingConstructor>]
        (
            classificationformatMapService: IClassificationFormatMapService,
            classificationTypeRegistry: IClassificationTypeRegistryService,
            [<Import(typeof<SVsServiceProvider>)>] serviceProvider: IServiceProvider
        ) =

        let (| LightTheme | DarkTheme | UnknownTheme |) id =
            if id = KnownColorThemes.Light || id = KnownColorThemes.Blue || id = Guids.blueHighContrastThemeId then LightTheme 
            elif id = KnownColorThemes.Dark then DarkTheme
            else UnknownTheme
    
        let getCurrentThemeId() =
            let themeService = serviceProvider.GetService(typeof<SVsColorThemeService>) :?> IVsColorThemeService
            themeService.CurrentTheme.ThemeId

        let customColorData =
            [
                {| ClassificationName = FSharpClassificationTypes.MutableVar
                   LightThemeColor = Color.FromRgb(160uy, 128uy, 0uy)
                   DarkThemeColor = Color.FromRgb(255uy, 210uy, 28uy) |}
                {| ClassificationName = FSharpClassificationTypes.DisposableLocalValue
                   LightThemeColor = Color.FromRgb(31uy, 55uy, 127uy)
                   DarkThemeColor = Color.FromRgb(156uy, 220uy, 254uy) |}
                {| ClassificationName = FSharpClassificationTypes.DisposableTopLevelValue
                   LightThemeColor = Colors.Black
                   DarkThemeColor = Color.FromRgb(220uy, 220uy, 220uy) |}
                {| ClassificationName = FSharpClassificationTypes.DisposableType
                   LightThemeColor = Color.FromRgb(43uy, 145uy, 175uy)
                   DarkThemeColor = Color.FromRgb(78uy, 220uy, 176uy) |}
                {| ClassificationName = FSharpClassificationTypes.Function
                   LightThemeColor = Color.FromRgb(116uy, 83uy, 31uy)
                   DarkThemeColor = Color.FromRgb(220uy, 220uy, 170uy) |}
                {| ClassificationName = FSharpClassificationTypes.Printf
                   LightThemeColor = Color.FromRgb(43uy, 145uy, 175uy)
                   DarkThemeColor = Color.FromRgb(78uy, 220uy, 176uy) |}
            ]

        let setColors _ =
            let fontAndColorStorage = serviceProvider.GetService(typeof<SVsFontAndColorStorage>) :?> IVsFontAndColorStorage
            let fontAndColorCacheManager = serviceProvider.GetService(typeof<SVsFontAndColorCacheManager>) :?> IVsFontAndColorCacheManager
            fontAndColorCacheManager.CheckCache(ref DefGuidList.guidTextEditorFontCategory) |> ignore
            fontAndColorStorage.OpenCategory(ref DefGuidList.guidTextEditorFontCategory, uint32 __FCSTORAGEFLAGS.FCSF_READONLY) |> ignore

            let formatMap = classificationformatMapService.GetClassificationFormatMap(category = "text")
            try 
                formatMap.BeginBatchUpdate()
                for item in customColorData do
                    // we don't touch the changes made by the user
                    if fontAndColorStorage.GetItem(item.ClassificationName, Array.zeroCreate 1) <> VSConstants.S_OK  then
                        let ict = classificationTypeRegistry.GetClassificationType(item.ClassificationName)
                        let oldProps = formatMap.GetTextProperties(ict)
                        let newProps =
                            match getCurrentThemeId() with
                            | LightTheme -> oldProps.SetForeground item.LightThemeColor
                            | DarkTheme -> oldProps.SetForeground item.DarkThemeColor
                            | UnknownTheme -> oldProps
                        formatMap.SetTextProperties(ict, newProps)
                fontAndColorStorage.CloseCategory() |> ignore
            finally formatMap.EndBatchUpdate()

        let handler = ThemeChangedEventHandler setColors

        do VSColorTheme.add_ThemeChanged handler

        member _.GetColor(ctype) =
            match customColorData |> List.tryFind (fun item -> item.ClassificationName = ctype) with
            | Some item ->
                let light, dark = item.LightThemeColor, item.DarkThemeColor
                match getCurrentThemeId() with
                | LightTheme -> Nullable light
                | DarkTheme -> Nullable dark
                | UnknownTheme -> Nullable()
            | None ->
                Nullable()

        interface ISetThemeColors with
            member _.SetColors() = setColors()
        
        interface IDisposable with
            member _.Dispose() = VSColorTheme.remove_ThemeChanged handler

    [<Export; Name(FSharpClassificationTypes.MutableVar); BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)>]
    let FSharpMutableVarClassificationType : ClassificationTypeDefinition = null

    [<Export; Name(FSharpClassificationTypes.DisposableType); BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)>]
    let FSharpDisposableClassificationType : ClassificationTypeDefinition = null
    
    [<Export; Name(FSharpClassificationTypes.DisposableLocalValue); BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)>]
    let FSharpDisposableLocalValueClassificationType : ClassificationTypeDefinition = null

    [<Export; Name(FSharpClassificationTypes.DisposableTopLevelValue); BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)>]
    let FSharpDisposableTopLevelValueClassificationType : ClassificationTypeDefinition = null

    [<Export; Name(FSharpClassificationTypes.Function); BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)>]
    let FSharpFunctionClassificationType : ClassificationTypeDefinition = null

    [<Export; Name(FSharpClassificationTypes.Printf); BaseDefinition(PredefinedClassificationTypeNames.FormalLanguage)>]
    let FSharpPrintfClassificationType : ClassificationTypeDefinition = null

    [<Export(typeof<EditorFormatDefinition>)>]
    [<ClassificationType(ClassificationTypeNames = FSharpClassificationTypes.Function)>]
    [<Name(FSharpClassificationTypes.Function)>]
    [<UserVisible(true)>]
    [<Order(After = PredefinedClassificationTypeNames.Keyword)>]
    type internal FSharpFunctionTypeFormat [<ImportingConstructor>](theme: ThemeColors) as self =
        inherit ClassificationFormatDefinition()

        do
            self.DisplayName <- SR.FSharpFunctionsClassificationType()
            self.ForegroundColor <- theme.GetColor FSharpClassificationTypes.Function

    [<Export(typeof<EditorFormatDefinition>)>]
    [<ClassificationType(ClassificationTypeNames = FSharpClassificationTypes.MutableVar)>]
    [<Name(FSharpClassificationTypes.MutableVar)>]
    [<UserVisible(true)>]
    [<Order(After = PredefinedClassificationTypeNames.Keyword)>]
    type internal FSharpMutableVarTypeFormat [<ImportingConstructor>](theme: ThemeColors) as self =
        inherit ClassificationFormatDefinition()

        do
            self.DisplayName <- SR.FSharpMutableVarsClassificationType()
            self.ForegroundColor <- theme.GetColor FSharpClassificationTypes.MutableVar

    [<Export(typeof<EditorFormatDefinition>)>]
    [<ClassificationType(ClassificationTypeNames = FSharpClassificationTypes.Printf)>]
    [<Name(FSharpClassificationTypes.Printf)>]
    [<UserVisible(true)>]
    [<Order(After = PredefinedClassificationTypeNames.String)>]
    type internal FSharpPrintfTypeFormat [<ImportingConstructor>](theme: ThemeColors) as self =
        inherit ClassificationFormatDefinition()
    
        do
            self.DisplayName <- SR.FSharpPrintfFormatClassificationType()
            self.ForegroundColor <- theme.GetColor FSharpClassificationTypes.Printf

    [<Export(typeof<EditorFormatDefinition>)>]
    [<ClassificationType(ClassificationTypeNames = FSharpClassificationTypes.DisposableType)>]
    [<Name(FSharpClassificationTypes.DisposableType)>]
    [<UserVisible(true)>]
    [<Order(After = PredefinedClassificationTypeNames.Keyword)>]
    type internal FSharpDisposableFormat [<ImportingConstructor>](theme: ThemeColors) as self =
        inherit ClassificationFormatDefinition()

        do
            self.DisplayName <- SR.FSharpDisposableTypesClassificationType()
            self.ForegroundColor <- theme.GetColor FSharpClassificationTypes.DisposableType

    [<Export(typeof<EditorFormatDefinition>)>]
    [<ClassificationType(ClassificationTypeNames = FSharpClassificationTypes.DisposableLocalValue)>]
    [<Name(FSharpClassificationTypes.DisposableLocalValue)>]
    [<UserVisible(true)>]
    [<Order(After = PredefinedClassificationTypeNames.Keyword)>]
    type internal FSharpDisposableLocalValueFormat [<ImportingConstructor>](theme: ThemeColors) as self =
        inherit ClassificationFormatDefinition()

        do
            self.DisplayName <- SR.FSharpDisposableLocalValuesClassificationType()
            self.ForegroundColor <- theme.GetColor FSharpClassificationTypes.DisposableLocalValue

    [<Export(typeof<EditorFormatDefinition>)>]
    [<ClassificationType(ClassificationTypeNames = FSharpClassificationTypes.DisposableTopLevelValue)>]
    [<Name(FSharpClassificationTypes.DisposableTopLevelValue)>]
    [<UserVisible(true)>]
    [<Order(After = PredefinedClassificationTypeNames.Keyword)>]
    type internal FSharpDisposableTopLevelValueFormat [<ImportingConstructor>](theme: ThemeColors) as self =
        inherit ClassificationFormatDefinition()

        do
            self.DisplayName <- SR.FSharpDisposableTopLevelValuesClassificationType()
            self.ForegroundColor <- theme.GetColor FSharpClassificationTypes.DisposableTopLevelValue