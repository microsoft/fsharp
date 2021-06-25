// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace Microsoft.VisualStudio.FSharp.Editor

open System.Collections.Generic
open FSharp.Compiler.Text
open Microsoft.CodeAnalysis.Classification
open Microsoft.VisualStudio.Core.Imaging
open Microsoft.VisualStudio.Language.StandardClassification
open Microsoft.VisualStudio.Text.Adornments

module internal QuickInfoViewProvider =

    let layoutTagToClassificationTag (layoutTag:TextTag) =
        match layoutTag with
        | TextTag.ActivePatternCase
        | TextTag.ActivePatternResult
        | TextTag.UnionCase
        | TextTag.Enum -> ClassificationTypeNames.EnumName
        | TextTag.Struct -> ClassificationTypeNames.StructName
        | TextTag.TypeParameter -> ClassificationTypeNames.TypeParameterName
        | TextTag.Alias
        | TextTag.Class
        | TextTag.Record
        | TextTag.Union
        | TextTag.UnknownType // Default to class until/unless we use classification data
        | TextTag.Module -> ClassificationTypeNames.ClassName
        | TextTag.Interface -> ClassificationTypeNames.InterfaceName
        | TextTag.Keyword -> ClassificationTypeNames.Keyword
        | TextTag.Member
        | TextTag.Function
        | TextTag.Method -> ClassificationTypeNames.MethodName
        | TextTag.Property
        | TextTag.RecordField -> ClassificationTypeNames.PropertyName
        | TextTag.Parameter
        | TextTag.Local -> ClassificationTypeNames.LocalName
        | TextTag.ModuleBinding -> ClassificationTypeNames.Identifier
        | TextTag.Namespace -> ClassificationTypeNames.NamespaceName
        | TextTag.Delegate -> ClassificationTypeNames.DelegateName
        | TextTag.Event -> ClassificationTypeNames.EventName
        | TextTag.Field -> ClassificationTypeNames.FieldName
        | TextTag.LineBreak
        | TextTag.Space -> ClassificationTypeNames.WhiteSpace
        | TextTag.NumericLiteral -> ClassificationTypeNames.NumericLiteral
        | TextTag.Operator -> ClassificationTypeNames.Operator
        | TextTag.StringLiteral -> ClassificationTypeNames.StringLiteral
        | TextTag.Punctuation -> ClassificationTypeNames.Punctuation
        | TextTag.UnknownEntity
        | TextTag.Text -> ClassificationTypeNames.Text

    let provideContent
        (
            imageId:ImageId option,
            description: seq<TaggedText>,
            documentation: seq<TaggedText>,
            navigation:QuickInfoNavigation
        ) =

        let buildContainerElement (itemGroup: seq<TaggedText>) =
            let finalCollection = List<ContainerElement>()
            let currentContainerItems = List<obj>()
            let runsCollection = List<ClassifiedTextRun>()
            let flushRuns() =
                if runsCollection.Count > 0 then
                    let element = ClassifiedTextElement(runsCollection)
                    currentContainerItems.Add(element :> obj)
                    runsCollection.Clear()
            let flushContainer() =
                if currentContainerItems.Count > 0 then
                    let element = ContainerElement(ContainerElementStyle.Wrapped, currentContainerItems)
                    finalCollection.Add(element)
                    currentContainerItems.Clear()
            for item in itemGroup do
                let classificationTag = layoutTagToClassificationTag item.Tag
                match item with
                | :? NavigableTaggedText as nav when navigation.IsTargetValid nav.Range ->
                    flushRuns()
                    let navigableTextRun = NavigableTextRun(classificationTag, item.Text, fun () -> navigation.NavigateTo nav.Range)
                    currentContainerItems.Add(navigableTextRun :> obj)
                | :? WebLinkTaggedText as nav  ->
                    flushRuns()
                    let navigableTextRun = NavigableTextRun(classificationTag, item.Text, fun () -> navigation.NavigateToUri nav.Uri)
                    currentContainerItems.Add(navigableTextRun :> obj)
                | _ when item.Tag = TextTag.LineBreak ->
                    flushRuns()
                    // preserve succesive linebreaks
                    if currentContainerItems.Count = 0 then
                        runsCollection.Add(ClassifiedTextRun(PredefinedClassificationTypeNames.Other, System.String.Empty))
                        flushRuns()
                    flushContainer()
                | _ -> 
                    let newRun = ClassifiedTextRun(classificationTag, item.Text)
                    runsCollection.Add(newRun)   
            flushRuns()
            flushContainer()
            ContainerElement(ContainerElementStyle.Stacked, finalCollection |> Seq.map box)

        let innerElement =
            match imageId with
            | Some imageId ->
                ContainerElement(ContainerElementStyle.Wrapped, ImageElement(imageId), buildContainerElement description)
            | None ->
                ContainerElement(ContainerElementStyle.Wrapped, buildContainerElement description)

        ContainerElement(ContainerElementStyle.Stacked, innerElement, buildContainerElement documentation)
