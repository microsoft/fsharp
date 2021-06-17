[<AutoOpen>]
module internal FSharpParseFileResultsExtensions

open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Syntax
open FSharp.Compiler.Text

type FSharpParseFileResults with
    member this.TryRangeOfBindingWithHeadPatternWithPos pos =
        let input = this.ParseTree
        SyntaxTraversal.Traverse(pos, input, { new SyntaxVisitorBase<_>() with 
            member _.VisitExpr(_, _, defaultTraverse, expr) =
                defaultTraverse expr

            override _.VisitBinding(_path, defaultTraverse, binding) =
                match binding with
                | SynBinding(_, SynBindingKind.Normal, _, _, _, _, _, pat, _, _, _, _) as binding ->
                    if Position.posEq binding.RangeOfHeadPattern.Start pos then
                        Some binding.RangeOfBindingWithRhs
                    else
                        // Check if it's an operator
                        match pat with
                        | SynPat.LongIdent(LongIdentWithDots([id], _), _, _, _, _, _) when id.idText.StartsWith("op_") ->
                            if Position.posEq id.idRange.Start pos then
                                Some binding.RangeOfBindingWithRhs
                            else
                                defaultTraverse binding
                        | _ -> defaultTraverse binding

                | _ -> defaultTraverse binding })

    member this.TryRangeOfTypeofWithNameAndTypeExpr pos =
        SyntaxTraversal.Traverse(pos, this.ParseTree, { new SyntaxVisitorBase<_>() with 
            member _.VisitExpr(_path, _, defaultTraverse, expr) =
                match expr with
                | SynExpr.DotGet(expr, _, _, range) ->
                    match expr with
                    | SynExpr.TypeApp(SynExpr.Ident(ident), _, typeArgs, _, _, _, _) ->
                        let onlyOneTypeArg =
                            match typeArgs with
                            | [] -> false
                            | [_] -> true
                            | _ -> false
                        if ident.idText = "typeof" && onlyOneTypeArg then
                            Some {| NamedIdentRange = typeArgs.Head.Range; FullExpressionRange = range |}
                        else
                            defaultTraverse expr
                    | _ -> defaultTraverse expr
                | _ -> defaultTraverse expr })