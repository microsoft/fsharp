namespace NUnit.Framework

module Assert =

    [<assembly: Parallelizable(ParallelScope.All)>]
    do()

    let inline fail message = Assert.Fail message

    let inline failf fmt = Printf.kprintf fail fmt

    let inline areEqual (expected: ^T) (actual: ^T) =
        Assert.AreEqual(expected, actual)

module StringAssert =

    let inline contains expected actual = StringAssert.Contains(expected, actual)
