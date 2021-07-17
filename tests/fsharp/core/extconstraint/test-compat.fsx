
// We compile this and check the inferred signature against a baseline.
// The baseline is manually generated from a previous-generated F# compiler.

module TestCompat

module CheckNewOverloadsDoneConfusePreviousCode = 
    open System

    type System.DateTime with
        static member (+)(a: DateTime, b: TimeSpan) = a

    let x = DateTime.Now + TimeSpan.Zero
    let f1 (x: DateTime) = x + TimeSpan.Zero
    let f2 (x: TimeSpan) y = y + x
    let f3 x y = DateTime.op_Addition (x, y)
    let f4 x y = TimeSpan.op_Addition (x, y)


module CheckNewOverloadsDoneConfusePreviousCode2 = 
    open System
    open System.Numerics

    // This adds one op_Addition overload to a type that currently only has one op_Addition overload
    type System.Numerics.Complex with
        static member (+)(a: Complex, b: TimeSpan) = a

    type CheckNominal() = 
        static member CanResolveOverload(x: TimeSpan) = ()
        static member CanResolveOverload(x: Complex) = ()

    // Next check we can resolve direct calls to the op_Addition overload both to new and old types.
    // There is nothing new here, no SRTP constraints involved.
    let f1 (x: Complex) (y: Complex) = System.Numerics.Complex.op_Addition (x, y)
    let f2 (x: Complex) (y: TimeSpan) = System.Numerics.Complex.op_Addition (x, y)

    // Next check we can resolve the op_Addition overload with no type information.
    // This in F# overload resolution the original method is preferred to the extension method.
    // There is nothing new here, no SRTP constraints involved.
    let f3 x y = System.Numerics.Complex.op_Addition (x, y)

    // Next check we can resolve the SRTP constraint implied by the use of the
    // '+' operator when given two argument types (no return type)
    let f4 (x: Complex) (y: Complex) = x + y
    
#if LANGVERSION_PREVIEW
    // Next check we can resolve the SRTP constraint implied by the use of the
    // '+' operator when given two argument types (no return type), resolving to the
    // extension member.
    let f5 (x: Complex) (y: TimeSpan) = x + y
#endif

    // Next check we can resolve the SRTP constraint implied by the use of the
    // '+' operator when given only the first argument type.  This must resolve to the original
    // overload.
    //
    // The original overload is preferred to the extension overload.
    //
    // Note the SRTP constraint is resolved based on one type only 
    // because canonicalization (weak resolution) is forced prior to
    // generalizing 'f6', see calls to CanonicalizePartialInferenceProblem in TypeChecker.fs.
    let f6 (x: Complex) y = x + y
    
    // The following does similar to the previous but checks more subtletly about when the resolution
    // is done.
    let f7 (x: Complex) (y: 'B) (z: 'C) : 'C =
        // 1. Commit to first argument Complex
        ((+): 'A -> 'B -> 'C) 
            // Processing the next fragment commits the first type to be Complex
            x 
            // Processing the next fragment checks that we can do dot notation
            // name resolution on the type of 'y', which must force 'y' to be nominal.
            //
            // Just prior to processing this, 'y' still
            // has variable type. When processing it, canonicalization (weak resolution) is
            // forced on the type of 'y' prior to name resolution, see calls
            // to CanonicalizePartialInferenceProblem in TypeChecker.fs TcLookupThen.
            (ignore y.Magnitude; y)

    // Next check that overload resolution eagerly commits to Complex * Complex -> Complex overload
    let f8 (x: Complex) (y: 'B) (z: 'C) : 'C =
        // 1. Commit to first argument Complex
        ((+): 'A -> 'B -> 'C) 
            // Commit first type to be Complex
            x 
            // Check we can do overload resolution based on the type of 'y', 
            // and this is enough to force 'y' to be nominal. Just prior to processing this, 'y' still
            // has variable type, however canonicalization (weak resolution)
            // is forced on the type of 'y' prior to overload resolution, see calls
            // to CanonicalizePartialInferenceProblem in TypeChecker.fs.
            (CheckNominal.CanResolveOverload y; y)

#if LANGVERSION_PREVIEW
    // Next check that overload resolution can commit to Complex * TimeSpan -> Complex overload 
    // as soon as enough information is available.
    //   Check resulting type is known to be Complex (by resolving .Magnitude
    let f9 (x: Complex) (y: 'B) (z: 'C) : 'C =
        // 1. Commit to first argument Complex
        ((+): Complex -> 'B -> 'C)
            // 2. Commit to second argument TimeSpan
            (ignore (y: TimeSpan); x)
            // 3. check we already know the type of 'z'
            (ignore z.Magnitude; y)
#endif
