
test-compat.fsx(11,24): warning FS1215: Extension members cannot provide operator overloads.  Consider defining the operator as part of the type definition instead.

test-compat.fsx(26,24): warning FS1215: Extension members cannot provide operator overloads.  Consider defining the operator as part of the type definition instead.

test-compat.fsx(70,13): warning FS0064: This construct causes code to be less generic than indicated by the type annotations. The type variable 'A has been constrained to be type 'Complex'.

test-compat.fsx(68,11): warning FS0064: This construct causes code to be less generic than indicated by the type annotations. The type variable 'B has been constrained to be type 'Complex'.

test-compat.fsx(68,11): warning FS0064: This construct causes code to be less generic than indicated by the type annotations. The type variable 'C has been constrained to be type 'Complex'.

test-compat.fsx(85,13): warning FS0064: This construct causes code to be less generic than indicated by the type annotations. The type variable 'A has been constrained to be type 'Complex'.

test-compat.fsx(83,11): warning FS0064: This construct causes code to be less generic than indicated by the type annotations. The type variable 'B has been constrained to be type 'Complex'.

test-compat.fsx(83,11): warning FS0064: This construct causes code to be less generic than indicated by the type annotations. The type variable 'C has been constrained to be type 'Complex'.
module TestCompat
module CheckNewOverloadsDoneConfusePreviousCode = begin
  type DateTime with
    static member
      ( + ) : a:System.DateTime * b:System.TimeSpan -> System.DateTime
  val x : System.DateTime
  val f1 : x:System.DateTime -> System.DateTime
  val f2 : x:System.TimeSpan -> y:System.TimeSpan -> System.TimeSpan
  val f3 : x:System.DateTime -> y:System.TimeSpan -> System.DateTime
  val f4 : x:System.TimeSpan -> y:System.TimeSpan -> System.TimeSpan
end
module CheckNewOverloadsDoneConfusePreviousCode2 = begin
  type Complex with
    static member
      ( + ) : a:System.Numerics.Complex * b:System.TimeSpan ->
                System.Numerics.Complex
  type CheckNominal =
    class
      new : unit -> CheckNominal
      static member CanResolveOverload : x:System.Numerics.Complex -> unit
      static member CanResolveOverload : x:System.TimeSpan -> unit
    end
  val f1 :
    x:System.Numerics.Complex ->
      y:System.Numerics.Complex -> System.Numerics.Complex
  val f2 :
    x:System.Numerics.Complex -> y:System.TimeSpan -> System.Numerics.Complex
  val f3 :
    x:System.Numerics.Complex ->
      y:System.Numerics.Complex -> System.Numerics.Complex
  val f4 :
    x:System.Numerics.Complex ->
      y:System.Numerics.Complex -> System.Numerics.Complex
  val f6 :
    x:System.Numerics.Complex ->
      y:System.Numerics.Complex -> System.Numerics.Complex
  val f7 :
    x:System.Numerics.Complex ->
      y:System.Numerics.Complex ->
        z:System.Numerics.Complex -> System.Numerics.Complex
  val f8 :
    x:System.Numerics.Complex ->
      y:System.Numerics.Complex ->
        z:System.Numerics.Complex -> System.Numerics.Complex
end

