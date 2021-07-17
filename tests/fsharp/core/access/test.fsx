// #Regression #Conformance #Accessibility #SignatureFiles #Regression #Records #Unions 
#if TESTS_AS_APP
module Core_access
#endif

#light
let failures = ref []

let report_failure (s : string) = 
    stderr.Write" NO: "
    stderr.WriteLine s
    failures := !failures @ [s]

let test (s : string) b = 
    stderr.Write(s)
    if b then stderr.WriteLine " OK"
    else report_failure (s)

(*--------------------*)

// Test cases for bug://1562
// Checking that generated signature can be compiled against the file.
  
type internal typInternal = | AAA1
type private  typPrivate  = | AAA2
type public   typPublic   = | AAA3
type          typDefault  = | AAA4
type internal rrr = | AAA

let  internal  ValInternal = 1212
let  private   ValPrivate  = 1212
let  public    ValPublic   = 1212
let            ValDefault  = 1212

type MyClassFields = 
    val internal fieldInternal : int
    val private  fieldPrivate  : int
    val public   fieldPublic   : int    
    
type MyClassMutableFields = 
    val mutable internal mfieldInternal : int
    val mutable private  mfieldPrivate  : int
    val mutable public   mfieldPublic   : int
    
type MyClassStaticMembers = 
    static member internal SInternal = 12
    static member private  SPrivate  = 12
    static member public   SPublic   = 12
    static member          SDefault  = 12
    static member internal SMInternal() = 12
    static member private  SMPrivate()  = 12
    static member public   SMPublic()   = 12
    static member          SMDefault()  = 12
    
type MyClassPropertiyGetters =     
    member internal x.InstInternal = 12
    member private  x.InstPrivate  = 12
    member public   x.InstPublic   = 12
    member          x.InstDefault  = 12
    
type MyClassExplicitCtors =  
    val v : int   
    internal new(x)     = { v = x }
    private  new(x,y)   = { v = x + y}
    public   new(x,y,z) = { v = x + y + z}

type MyClassExplicitCtors2 =  
    new() = {}
    internal new(x)     = let v : int = x in {}
    private  new(x,y)   = let v : int = x + y in {}
    public   new(x,y,z) = let v : int = x + y + z in {}
    
type MyClassPropertyGetSetterMatrix =      
    //--
    member obj.PropGetSetInternalInternal
        with internal get() = 1 
        and  internal set(x:int) = ()
    member obj.PropGetSetInternalPrivate
        with internal get() = 1 
        and  private  set(x:int) = ()
    member obj.PropGetSetInternalPublic
        with internal get() = 1 
        and  public   set(x:int) = ()
    //--
    member obj.PropGetSetPrivateInternal
        with private  get() = 1 
        and  internal set(x:int) = ()
    member obj.PropGetSetPrivatePrivate
        with private  get() = 1 
        and  private  set(x:int) = ()
    member obj.PropGetSetPrivatePublic
        with private  get() = 1 
        and  public   set(x:int) = ()
    //--
    member obj.PropGetSetPublicInternal    
        with public   get() = 1 
        and  internal set(x:int) = ()
    member obj.PropGetSetPublicPrivate
        with public   get() = 1 
        and  private  set(x:int) = ()
    member obj.PropGetSetPublicPublic
        with public   get() = 1 
        and  public   set(x:int) = ()    

type MyClassImplicitCtorInternal internal() =
    member obj.Res = 12    
    
type MyClassImplicitCtorPrivate private() =
    member obj.Res = 12    
    
module internal ModInternal = begin end
module private  ModPrivate  = begin end
module public   ModPublic   = begin end

type recordRepInternal = internal { rfA1 : int }
type recordRepPrivate  = private  { rfA2 : int }
type recordRepPublic   = public   { rfA3 : int }

type dtypeRepInternal = internal | AA1 | BB1
type dtypeRepPrivate  = private  | AA2 | BB2
type dtypeRepPublic   = public   | AA3 | BB3

type internal dtypeRepPublic2   =  private  | AA3 | BB3
type private  dtypeRepPublic3   =  internal | AA3 | BB3

type internal dtypeRepPublic4 =
      private 
              | AA3
              | BB3

module internal M = 
  module private PP = 
    type dtypeRepPublic5 =
            | AA3
            | BB3

module private M2 = 
    module internal P =
        let vv = 12
        

module RestrictedRecordsAndUnionsUsingPrivateAndInternalTypes = 

    module public Test1 =
        
        type internal Data = 
                {
                    Datum: int
                }

        type public Datum = 
            internal
                {
                    Thing: Data
                }

        type public Datum2 = 
            internal | A of Data * Data | B of Data
                
    module public Test2 =
        
        type internal Data = 
                {
                    Datum: int
                }

        type internal Datum = 
                {
                    Thing: Data
                }

        type internal Datum2 = 
             | A of Data * Data | B of Data
                
    module public Test3 =
        
        type public Data = 
              internal
                {
                    Datum: int
                }

        type internal Datum = 
                {
                    Thing: Data
                }

        type internal Datum2 = 
            internal | A of Data * Data | B of Data
                

    module public Test4 =
        
        type internal Data = 
                {
                    Datum: int
                }

        type public Datum = 
             internal
                {
                    Thing: Data
                }

        type public Datum2 = 
            internal | A of Data * Data | B of Data
                

    module public Test5 =
        
        type private Data = 
                {
                    Datum: int
                }

        type public Datum = 
            private
                {
                    Thing: Data
                }

        type public Datum2 = 
            private | A of Data * Data | B of Data


    module Test6 = 
        module internal HelperModule = 
            
            type public Data = 
                private
                    {
                        Datum: int
                    }
                    
            let internal handle (data:Data): int = data.Datum
            
        module public Module =
            
            type public Data = 
                private
                    {
                        Thing: HelperModule.Data
                    }
                    
            let public getInt (data:Data): int = HelperModule.handle data.Thing               

    module Test7 = 
        module internal HelperModule = 
            
            type Data = 
                    {
                        Datum: int
                    }
                    
            let handle (data:Data): int = data.Datum
            
        module Module =
            
            type Data = 
                internal
                    {
                        Thing: HelperModule.Data
                    }
                    
            let getInt (data:Data): int = HelperModule.handle data.Thing               


    (*--------------------*)  

#if TESTS_AS_APP
let RUN() = !failures
#else
let aa =
  match !failures with 
  | [] -> 
      stdout.WriteLine "Test Passed"
      System.IO.File.WriteAllText("test.ok","ok")
      exit 0
  | _ -> 
      stdout.WriteLine "Test Failed"
      exit 1
#endif

