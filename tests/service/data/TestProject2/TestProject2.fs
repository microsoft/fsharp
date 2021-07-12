namespace TestProject2

// This is code in one project making use of another project that uses a generative type provider

type Class1() = 
    member this.X1 = TestProject.T()
    member this.X2 = TestProject.T2()
