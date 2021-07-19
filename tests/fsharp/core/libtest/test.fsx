// #Regression #Conformance #Regression #Exceptions #Constants #LetBindings #Lists #Collections #Stress #Sequences #Optimizations #Records #Unions 
#if TESTS_AS_APP
module Core_libtest
#endif


#nowarn "62"
#nowarn "44"

let failures = ref []
let reportFailure s = 
  stdout.WriteLine "\n................TEST FAILED...............\n"; failures := !failures @ [s]

let check s e r = 
  if r = e then  stdout.WriteLine (s^": YES") 
  else (stdout.WriteLine ("\n***** "^s^": FAIL\n"); reportFailure s)

let test s b = 
  if b then ( (* stdout.WriteLine ("passed: " + s) *) ) 
  else (stderr.WriteLine ("failure: " + s); 
        reportFailure s)

  
let format_uint64 outc formatc width left_justify add_zeros num_prefix_if_pos (n:uint64) = 
  let _ = match formatc with 'd' | 'i' | 'u' -> 10UL | 'o' -> 8UL | 'x' | 'X' -> 16UL in
  failwith "hello"


(*---------------------------------------------------------------------------
!* Exceptions
 *--------------------------------------------------------------------------- *)

let myFunc x y = 
  if x > y then stdout.WriteLine "greater";
  if x < y then stdout.WriteLine "less";
  try 
    if x = y then stdout.WriteLine "equal";
    failwith "fail";
    reportFailure "ABCDE"
  with Failure s -> 
    stdout.WriteLine "caught!";

let _ = myFunc 1 4
let _ = myFunc "a" "b"
let _ = myFunc "c" "b"
let _ = myFunc "c" "c"
let _ =
   myFunc 
     begin 
      try 
        failwith "string1";
      with Failure s -> 
        s;
     end
     begin 
      try 
        failwith "string2";
      with Failure s -> 
        s;
     end

let _ =
   myFunc 
     begin 
      try
       begin 
        try 
         failwith "yes";
        with e -> 
         reraise (); failwith "no"
       end
      with Failure "yes" -> "yes"
     end
     begin 
      try
       begin 
        try 
         failwith "yes";
        with e -> 
         reraise (); failwith "no"
       end
      with Failure "yes" -> "yes"
     end


//---------------------------------------------------------------------------
// Basic operations 
//--------------------------------------------------------------------------- 

#if INVARIANT_CULTURE_STRING_COMPARISON
// These check we are using InvariantCulture string comparison, not Ordinal comparison
let _ = check "vknwwer41" (";" >  "0") false
let _ = check "vknwwer42" (";" >= "0") false
let _ = check "vknwwer43" (";" =  "0") false
let _ = check "vknwwer44" (";" <> "0") true
let _ = check "vknwwer53" (";" <= "0") true
let _ = check "vknwwer54" (";" <  "0") true
let _ = check "vknwwer55" (compare ";" "0") -1
let _ = check "vknwwer55" (compare "0" ";") 1

(*
// check consistency with characters
let _ = check "vknwwer41" (';' >  '0') false
let _ = check "vknwwer42" (';' >= '0') false
let _ = check "vknwwer43" (';' =  '0') false
let _ = check "vknwwer44" (';' <> '0') true
let _ = check "vknwwer53" (';' <= '0') true
let _ = check "vknwwer54" (';' <  '0') true
let _ = check "vknwwer55" (compare ';' '0') -1
let _ = check "vknwwer55" (compare '0' ';') 1
*)

// check consistency with lists of strings
let _ = check "vknwwer41" ([";"] >  ["0"]) false
let _ = check "vknwwer42" ([";"] >= ["0"]) false
let _ = check "vknwwer43" ([";"] =  ["0"]) false
let _ = check "vknwwer44" ([";"] <> ["0"]) true
let _ = check "vknwwer53" ([";"] <= ["0"]) true
let _ = check "vknwwer54" ([";"] <  ["0"]) true
let _ = check "vknwwer55" (compare [";"] ["0"]) -1
let _ = check "vknwwer55" (compare ["0"] [";"]) 1

(*
// check consistency with lists of chars
let _ = check "vknwwer41" ([';'] >  ['0']) false
let _ = check "vknwwer42" ([';'] >= ['0']) false
let _ = check "vknwwer43" ([';'] =  ['0']) false
let _ = check "vknwwer44" ([';'] <> ['0']) true
let _ = check "vknwwer53" ([';'] <= ['0']) true
let _ = check "vknwwer54" ([';'] <  ['0']) true
let _ = check "vknwwer55" (compare [';'] ['0']) -1
let _ = check "vknwwer55" (compare ['0'] [';']) 1
*)
#endif

let getObjectHashCode (x:'a) = (box x).GetHashCode()
let (===) (x:'a) (y:'a) = (box x).Equals(box y)

let _ = stdout.WriteLine "90erw9"
let _ = if true && true then stdout.WriteLine "YES" else  reportFailure "intial test"
let _ = if true && false then reportFailure "basic test 1" else  stdout.WriteLine "YES"
let _ = if false && true then reportFailure "basic test 2" else  stdout.WriteLine "YES"
let _ = if false && false then reportFailure "basic test 3" else  stdout.WriteLine "YES"
let _ = if true || true then stdout.WriteLine "YES" else  reportFailure "basic test Q1"
let _ = if true || false then stdout.WriteLine "YES" else  reportFailure "basic test Q2"
let _ = if false || true then stdout.WriteLine "YES" else  reportFailure "basic test Q3"
let _ = if false || false then reportFailure "basic test 4" else  stdout.WriteLine "YES"

let _ = stdout.WriteLine "vwlkew0"
let _ = if true && true then stdout.WriteLine "YES" else  reportFailure "basic test Q4"
let _ = if true && false then reportFailure "basic test 5" else  stdout.WriteLine "YES"
let _ = if false && true then reportFailure "basic test 6" else  stdout.WriteLine "YES"
let _ = if false && false then reportFailure "basic test 7" else  stdout.WriteLine "YES"
let _ = if true || true then stdout.WriteLine "YES" else  reportFailure "basic test Q5"
let _ = if true || false then stdout.WriteLine "YES" else  reportFailure "basic test Q6"
let _ = if false || true then stdout.WriteLine "YES" else  reportFailure "basic test Q7"
let _ = if false || false then reportFailure "basic test 8" else  stdout.WriteLine "YES"

let _ = stdout.WriteLine "vr90vr90"
let truE () = (stdout.WriteLine "."; true)
let falsE () = (stdout.WriteLine "."; false)
let _ = if truE() && truE() then stdout.WriteLine "YES" else  reportFailure "basic test Q8"
let _ = if truE() && falsE() then reportFailure "basic test 9" else  stdout.WriteLine "YES"
let _ = if falsE() && truE() then reportFailure "basic test 10" else  stdout.WriteLine "YES"
let _ = if falsE() && falsE() then reportFailure "basic test 11" else  stdout.WriteLine "YES"
let _ = if truE() || truE() then stdout.WriteLine "YES" else  reportFailure "basic test Q9"
let _ = if truE() || falsE() then stdout.WriteLine "YES" else  reportFailure "basic test Q10"
let _ = if falsE() || truE() then stdout.WriteLine "YES" else  reportFailure "basic test Q11"
let _ = if falsE() || falsE() then reportFailure "basic test 12" else  stdout.WriteLine "YES"

let _ = stdout.WriteLine "tgbri123d: "
let truERR () = (reportFailure "basic test 13" ; true)
let falsERR () = (reportFailure "basic test 14" ; false)
let _ = if false && truERR() then reportFailure "basic test 15" else  stdout.WriteLine "YES"
let _ = if false && falsERR() then reportFailure "basic test 16" else  stdout.WriteLine "YES"
let _ = if true || truERR() then stdout.WriteLine "YES" else  reportFailure "basic test Q12"
let _ = if true || falsERR() then stdout.WriteLine "YES" else  reportFailure "basic test Q13"

let _ = stdout.WriteLine "d298c123d: "
let _ = if falsE() && truERR() then reportFailure "basic test 17" else  stdout.WriteLine "YES"
let _ = if falsE() && falsERR() then reportFailure "basic test 18" else  stdout.WriteLine "YES"
let _ = if truE() || truERR() then stdout.WriteLine "YES" else  reportFailure "basic test Q14"
let _ = if truE() || falsERR() then stdout.WriteLine "YES" else  reportFailure "basic test Q15"

let _ = stdout.WriteLine "ddwqd123d: "
let _ = if falsE() && truERR() then reportFailure "basic test 19" else  stdout.WriteLine "YES"
let _ = if falsE() && falsERR() then reportFailure "basic test 20" else  stdout.WriteLine "YES"
let _ = if truE() || truERR() then stdout.WriteLine "YES" else  reportFailure "basic test Q16"
let _ = if truE() || falsERR() then stdout.WriteLine "YES" else  reportFailure "basic test Q17"


let _ = stdout.WriteLine "d3wq123d: "
let _ = if 1 = 1 then stdout.WriteLine "YES" else  reportFailure "basic test Q18"
let _ = if 1 === 1 then stdout.WriteLine "YES" else  reportFailure "basic test Q19"
let _ = if 1 < 2 then stdout.WriteLine "YES" else  reportFailure "basic test Q20"
let _ = if 2 > 1 then stdout.WriteLine "YES" else  reportFailure "basic test Q21"
let _ = if 2 >= 2 then stdout.WriteLine "YES" else  reportFailure "basic test Q22"
let _ = if 1 <= 2  then stdout.WriteLine "YES" else  reportFailure "basic test Q23"
let _ = if 2 <= 2 then stdout.WriteLine "YES" else  reportFailure "basic test Q24"
let _ = if 'c' < 'd' then stdout.WriteLine "YES" else  reportFailure "basic test Q25"
let _ = if 'c' <= 'c' then stdout.WriteLine "YES" else  reportFailure "basic test Q26"
let _ = if 'c' < 'c' then reportFailure "basic test 21" else  stdout.WriteLine "YES"

let printString (s:string) = System.Console.Write s
let printInt (i:int) = System.Console.Write (string i)
let printNewLine () = System.Console.WriteLine ()

type fpclass = 
  | CaseA
  | CaseB
  | CaseC
  | CaseD

let _ = printString "d3123d: "; if CaseA = CaseA then stdout.WriteLine "YES" else  reportFailure "basic test Q27"
(*let _ = if FP_subnormal = FP_subnormal then stdout.WriteLine "YES" else  reportFailure "basic test Q28" *)
let _ = if CaseB = CaseB then stdout.WriteLine "YES" else  reportFailure "basic test Q29"
let _ = if CaseB === CaseB then stdout.WriteLine "YES" else  reportFailure "basic test Q30"
let _ = if CaseC = CaseC then stdout.WriteLine "YES" else  reportFailure "basic test Q31"
let _ = if CaseD = CaseD  then stdout.WriteLine "YES" else  reportFailure "basic test Q32"
let _ = if CaseD === CaseD  then stdout.WriteLine "YES" else  reportFailure "basic test Q33"
let _ = if CaseA <= CaseA then stdout.WriteLine "YES" else  reportFailure "basic test Q34"
(* let _ = if FP_subnormal <= FP_subnormal then stdout.WriteLine "YES" else  reportFailure "basic test Q35"*)
let _ = if CaseB <= CaseB then stdout.WriteLine "YES" else  reportFailure "basic test Q36"
let _ = if CaseC <= CaseC then stdout.WriteLine "YES" else  reportFailure "basic test Q37"
let _ = if CaseD <= CaseD  then stdout.WriteLine "YES" else  reportFailure "basic test Q38"
let _ = if CaseA >= CaseA then stdout.WriteLine "YES" else  reportFailure "basic test Q39"
(* let _ = if FP_subnormal >= FP_subnormal then stdout.WriteLine "YES" else  reportFailure "basic test Q40" *)
let _ = if CaseB >= CaseB then stdout.WriteLine "YES" else  reportFailure "basic test Q41"
let _ = if CaseC >= CaseC then stdout.WriteLine "YES" else  reportFailure "basic test Q42"
let _ = if CaseD >= CaseD  then stdout.WriteLine "YES" else  reportFailure "basic test Q43"

let _ = printString "d1t43: "; if CaseA < CaseA then reportFailure "basic test 22" else  stdout.WriteLine "YES"
(* let _ = if CaseA < FP_subnormal then stdout.WriteLine "YES" else  reportFailure "basic test Q44"*)
let _ = if CaseA < CaseB then stdout.WriteLine "YES" else  reportFailure "basic test Q45"
let _ = if CaseA < CaseC then stdout.WriteLine "YES" else  reportFailure "basic test Q46"
let _ = if CaseA < CaseD then stdout.WriteLine "YES" else  reportFailure "basic test Q47"

(* let _ =  printString "er321: "; if FP_subnormal < CaseA then reportFailure "basic test Q48" else  stdout.WriteLine "YES" 
let _ = if FP_subnormal < FP_subnormal then reportFailure "basic test Q49" else  stdout.WriteLine "YES"
let _ = if FP_subnormal < CaseB then stdout.WriteLine "YES" else  reportFailure "basic test Q50"
let _ = if FP_subnormal < CaseC then stdout.WriteLine "YES" else  reportFailure "basic test Q51"
let _ = if FP_subnormal < CaseD then stdout.WriteLine "YES" else  reportFailure "basic test Q52" *)

let _ =  printString "ff23f2: ";if CaseB < CaseA then reportFailure "basic test 23" else  stdout.WriteLine "YES"
(* let _ = if CaseB < FP_subnormal then reportFailure "basic test Q53" else  stdout.WriteLine "YES" *)
let _ = if CaseB < CaseB then reportFailure "basic test 24" else  stdout.WriteLine "YES"
let _ = if CaseB < CaseC then stdout.WriteLine "YES" else  reportFailure "basic test Q54"
let _ = if CaseB < CaseD then stdout.WriteLine "YES" else  reportFailure "basic test Q55"

let _ =  printString "f2234g54: ";if CaseC < CaseA then reportFailure "basic test 25" else  stdout.WriteLine "YES"
(* let _ = if CaseC < FP_subnormal then reportFailure "basic test Q56" else  stdout.WriteLine "YES" *)
let _ = if CaseC < CaseB then reportFailure "basic test 26" else  stdout.WriteLine "YES"
let _ = if CaseC < CaseC then reportFailure "basic test 27" else  stdout.WriteLine "YES"
let _ = if CaseC < CaseD then stdout.WriteLine "YES" else  reportFailure "basic test Q57"

let _ = printString "dw432b4: "; if CaseD < CaseA then reportFailure "basic test 28" else  stdout.WriteLine "YES"
(* let _ = if CaseD < FP_subnormal then reportFailure "basic test Q58" else  stdout.WriteLine "YES" *)
let _ = if CaseD < CaseB then reportFailure "basic test 29" else  stdout.WriteLine "YES"
let _ = if CaseD < CaseC then reportFailure "basic test 30" else  stdout.WriteLine "YES"
let _ = if CaseD < CaseD then reportFailure "basic test 31" else  stdout.WriteLine "YES"

let _ = printString "fdew093 "; if 1 < 2 then stdout.WriteLine "YES" else  reportFailure "basic test Q59"
let _ = if 1 < 1 then reportFailure "basic test 32" else  stdout.WriteLine "YES"
let _ = if 1 < 0 then reportFailure "basic test 33" else  stdout.WriteLine "YES"
let _ = if -1 < 0 then stdout.WriteLine "YES" else  reportFailure "basic test Q60"
let _ = if -1 < 1 then stdout.WriteLine "YES" else  reportFailure "basic test Q61"

let _ = stdout.WriteLine "dwqfwe3t:"
let _ = printInt (compare "abc" "def"); printNewLine()
let _ = printInt (compare "def" "abc"); printNewLine()
let _ = printInt (compare "abc" "abc"); printNewLine()
let _ = printInt (compare "aaa" "abc"); printNewLine()
let _ = printInt (compare "abc" "aaa"); printNewLine()
let _ = printInt (compare "longlonglong" "short"); printNewLine() 
let _ = printInt (compare "short" "longlonglong"); printNewLine() 
let _ = printInt (compare "" "a"); printNewLine() 
let _ = printInt (compare "a" ""); printNewLine() 

let _ = printString "grfwe3t "
let _ = if "abc" < "def" then stdout.WriteLine "YES" else  reportFailure "basic test Q62"
let _ = if "def" < "abc" then reportFailure "basic test 34" else  stdout.WriteLine "YES"
let _ = if "abc" < "abc" then reportFailure "basic test 35" else  stdout.WriteLine "YES"
let _ = if "aaa" < "abc" then stdout.WriteLine "YES" else  reportFailure "basic test Q63"
let _ = if "abc" < "aaa" then reportFailure "basic test 36" else  stdout.WriteLine "YES"
let _ = if "longlonglong" < "short" then stdout.WriteLine "YES" else  reportFailure "basic test Q64"
let _ = if "short" < "longlonglong" then reportFailure "basic test 37" else  stdout.WriteLine "YES"
let _ = if "" < "a" then stdout.WriteLine "YES" else  reportFailure "basic test Q65"
let _ = if "a" < "" then reportFailure "basic test 38" else  stdout.WriteLine "YES"

let _ = printString "df32v4 "
let _ = if "abc" = "def" then reportFailure "basic test 39" else  stdout.WriteLine "YES"
let _ = if "abc" === "def" then reportFailure "basic test 40" else  stdout.WriteLine "YES"
let _ = if "def" = "abc" then reportFailure "basic test 41" else  stdout.WriteLine "YES"
let _ = if "def" === "abc" then reportFailure "basic test 42" else  stdout.WriteLine "YES"
let _ = if "abc" = "abc" then stdout.WriteLine "YES" else  reportFailure "basic test Q66"
let _ = if "abc" === "abc" then stdout.WriteLine "YES" else  reportFailure "basic test Q67"
let _ = if "aaa" = "abc" then reportFailure "basic test 43" else  stdout.WriteLine "YES"
let _ = if "aaa" === "abc" then reportFailure "basic test 44" else  stdout.WriteLine "YES"
let _ = if "abc" = "aaa" then reportFailure "basic test 45" else  stdout.WriteLine "YES"
let _ = if "abc" === "aaa" then reportFailure "basic test 46" else  stdout.WriteLine "YES"
let _ = if "longlonglong" = "short" then reportFailure "basic test 47" else  stdout.WriteLine "YES"
let _ = if "longlonglong" === "short" then reportFailure "basic test 48" else  stdout.WriteLine "YES"
let _ = if "short" = "longlonglong" then reportFailure "basic test 49" else  stdout.WriteLine "YES"
let _ = if "short" === "longlonglong" then reportFailure "basic test 50" else  stdout.WriteLine "YES"
let _ = if "" = "" then stdout.WriteLine "YES" else  reportFailure "basic test Q68"
let _ = if "" === "" then stdout.WriteLine "YES" else  reportFailure "basic test Q69"
let _ = if "" = "a" then reportFailure "basic test 51" else  stdout.WriteLine "YES"
let _ = if "" === "a" then reportFailure "basic test 52" else  stdout.WriteLine "YES"
let _ = if "a" = "" then reportFailure "basic test 53" else  stdout.WriteLine "YES"
let _ = if "a" === "" then reportFailure "basic test 54" else  stdout.WriteLine "YES"




type abcde = A of int | B of abcde | C of string | D  | E
let _ = printString "32432465: "; if A 1 = A 1 then stdout.WriteLine "YES" else  reportFailure "basic test Q70"
let _ = if B E = B E then stdout.WriteLine "YES" else  reportFailure "basic test Q71"
let _ = if B E === B E then stdout.WriteLine "YES" else  reportFailure "basic test Q72"
let _ = if C "3" = C "3" then stdout.WriteLine "YES" else  reportFailure "basic test Q73"
let _ = if C "3" === C "3" then stdout.WriteLine "YES" else  reportFailure "basic test Q74"
let _ = if D = D then stdout.WriteLine "YES" else  reportFailure "basic test Q75"
let _ = if D === D then stdout.WriteLine "YES" else  reportFailure "basic test Q76"
let _ = if E = E  then stdout.WriteLine "YES" else  reportFailure "basic test Q77"
let _ = if E === E  then stdout.WriteLine "YES" else  reportFailure "basic test Q78"
let _ =  printString "320-vrklm: "; if A 1 <= A 1 then stdout.WriteLine "YES" else  reportFailure "basic test Q79"
let _ = if B E <= B E then stdout.WriteLine "YES" else  reportFailure "basic test Q80"
let _ = if C "3" <= C "3" then stdout.WriteLine "YES" else  reportFailure "basic test Q81"
let _ = if D <= D then stdout.WriteLine "YES" else  reportFailure "basic test Q82"
let _ = if E <= E  then stdout.WriteLine "YES" else  reportFailure "basic test Q83"
let _ =  printString "9032c32nij: "; if A 1 >= A 1 then stdout.WriteLine "YES" else  reportFailure "basic test Q84"
let _ = if B E >= B E then stdout.WriteLine "YES" else  reportFailure "basic test Q85"
let _ = if C "3" >= C "3" then stdout.WriteLine "YES" else  reportFailure "basic test Q86"
let _ = if D >= D then stdout.WriteLine "YES" else  reportFailure "basic test Q87"
let _ = if E >= E  then stdout.WriteLine "YES" else  reportFailure "basic test Q88"


let _ =  printString "98vriu32: ";if A 1 < A 1 then reportFailure "basic test Q89" else  stdout.WriteLine "YES"
let _ = if A 1 < B E then stdout.WriteLine "YES" else  reportFailure "basic test Q90"
let _ = if A 1 < C "3" then stdout.WriteLine "YES" else  reportFailure "basic test Q91"
let _ = if A 1 < D then stdout.WriteLine "YES" else  reportFailure "basic test Q92"
let _ = if A 1 < E then stdout.WriteLine "YES" else  reportFailure "basic test Q93"

let _ = if B E < A 1 then reportFailure "basic test 55" else  stdout.WriteLine "YES"
let _ = if B E < B E then reportFailure "basic test 56" else  stdout.WriteLine "YES"
let _ = if B E < C "3" then stdout.WriteLine "YES" else  reportFailure "basic test Q94"
let _ = if B E < D then stdout.WriteLine "YES" else  reportFailure "basic test Q95"
let _ = if B E < E then stdout.WriteLine "YES" else  reportFailure "basic test Q96"

let _ = if C "3" < A 1 then reportFailure "basic test 57" else  stdout.WriteLine "YES"
let _ = if C "3" < B E then reportFailure "basic test 58" else  stdout.WriteLine "YES"
let _ = if C "3" < C "3" then reportFailure "basic test 59" else  stdout.WriteLine "YES"
let _ = if C "3" < D then stdout.WriteLine "YES" else  reportFailure "basic test Q97"
let _ = if C "3" < E then stdout.WriteLine "YES" else  reportFailure "basic test Q99"

let _ = if D < A 1 then reportFailure "basic test 60" else  stdout.WriteLine "YES"
let _ = if D < B E then reportFailure "basic test 61" else  stdout.WriteLine "YES"
let _ = if D < C "3" then reportFailure "basic test 62" else  stdout.WriteLine "YES"
let _ = if D < D then reportFailure "basic test 63" else  stdout.WriteLine "YES"
let _ = if D < E then stdout.WriteLine "YES" else  reportFailure "basic test Q100"

let _ = if E < A 1 then reportFailure "basic test 64" else  stdout.WriteLine "YES"
let _ = if E < B E then reportFailure "basic test 65" else  stdout.WriteLine "YES"
let _ = if E < C "3" then reportFailure "basic test 66" else  stdout.WriteLine "YES"
let _ = if E < D then reportFailure "basic test 67" else  stdout.WriteLine "YES"
let _ = if E < E then reportFailure "basic test 68" else  stdout.WriteLine "YES"


(* We put this test in as well as ILX uses a different rep. past 4 non-nullary constructors *)
type abcde2 = Z2 | A2 of int | B2 of abcde2 | C2 of string | D2 of string 
let _ = printString "32432ew465: "; if A2 1 = A2 1 then stdout.WriteLine "YES" else  reportFailure "basic test Q101"
let _ = printString "32432ew465: "; if A2 1 === A2 1 then stdout.WriteLine "YES" else  reportFailure "basic test Q102"
let _ = if B2 Z2 = B2 Z2 then stdout.WriteLine "YES" else  reportFailure "basic test Q103"
let _ = if B2 Z2 === B2 Z2 then stdout.WriteLine "YES" else  reportFailure "basic test Q104"
let _ = if C2 "3" = C2 "3" then stdout.WriteLine "YES" else  reportFailure "basic test Q105"
let _ = if C2 "3" === C2 "3" then stdout.WriteLine "YES" else  reportFailure "basic test Q106"
let _ = if D2 "a" = D2 "a" then stdout.WriteLine "YES" else  reportFailure "basic test Q107"
let _ = if D2 "a" === D2 "a" then stdout.WriteLine "YES" else  reportFailure "basic test Q108"
let _ = if Z2 = Z2  then stdout.WriteLine "YES" else  reportFailure "basic test Q109"
let _ = if Z2 === Z2  then stdout.WriteLine "YES" else  reportFailure "basic test Q110"
let _ =  printString "3vwa20-vrklm: "; if A2 1 <= A2 1 then stdout.WriteLine "YES" else  reportFailure "basic test Q111"
let _ = if B2 Z2 <= B2 Z2 then stdout.WriteLine "YES" else  reportFailure "basic test Q112"
let _ = if C2 "3" <= C2 "3" then stdout.WriteLine "YES" else  reportFailure "basic test Q113"
let _ = if D2 "a" <= D2 "a" then stdout.WriteLine "YES" else  reportFailure "basic test Q114"
let _ = if Z2 <= Z2  then stdout.WriteLine "YES" else  reportFailure "basic test Q115"
let _ =  printString "9vaw032c32nij: "; if A2 1 >= A2 1 then stdout.WriteLine "YES" else  reportFailure "basic test Q116"
let _ = if B2 Z2 >= B2 Z2 then stdout.WriteLine "YES" else  reportFailure "basic test Q117"
let _ = if C2 "3" >= C2 "3" then stdout.WriteLine "YES" else  reportFailure "basic test Q118"
let _ = if D2 "a" >= D2 "a" then stdout.WriteLine "YES" else  reportFailure "basic test Q119"
let _ = if Z2 >= Z2  then stdout.WriteLine "YES" else  reportFailure "basic test Q120"

let _ =  printString "vae98vriu32: "

let _ = if Z2 < A2 1 then stdout.WriteLine "YES" else  reportFailure "basic test Q121"
let _ = if Z2 < B2 Z2 then stdout.WriteLine "YES" else  reportFailure "basic test Q122"
let _ = if Z2 < C2 "3" then stdout.WriteLine "YES" else  reportFailure "basic test Q123"
let _ = if Z2 < D2 "a" then stdout.WriteLine "YES" else  reportFailure "basic test Q124"
let _ = if Z2 < Z2 then reportFailure "basic test 69" else  stdout.WriteLine "YES"

let _ =  printString "vae98312332: "

let _ = if None < None then reportFailure "basic test 70" else  stdout.WriteLine "YES"
let _ = if None > None then reportFailure "basic test 71" else  stdout.WriteLine "YES"
let _ = if [] < [] then reportFailure "basic test 72" else  stdout.WriteLine "YES"
let _ = if [] > [] then reportFailure "basic test 73" else  stdout.WriteLine "YES"
let _ = if None <= None then stdout.WriteLine "YES" else  reportFailure "basic test Q125"
let _ = if None >= None then stdout.WriteLine "YES" else  reportFailure "basic test Q126"
let _ = if [] <= [] then stdout.WriteLine "YES" else  reportFailure "basic test Q127"
let _ = if [] >= [] then stdout.WriteLine "YES" else  reportFailure "basic test Q128"

let _ =  printString "rege98312332: "

let _ = if A2 1 < Z2 then reportFailure "basic test 74" else  stdout.WriteLine "YES"
let _ = if A2 1 < A2 1 then reportFailure "basic test 75" else  stdout.WriteLine "YES"
let _ = if A2 1 < B2 Z2 then stdout.WriteLine "YES" else  reportFailure "basic test Q129"
let _ = if A2 1 < C2 "3" then stdout.WriteLine "YES" else  reportFailure "basic test Q130"
let _ = if A2 1 < D2 "a" then stdout.WriteLine "YES" else  reportFailure "basic test Q131"

let _ =  printString "328we32: "

let _ = if B2 Z2 < Z2 then reportFailure "basic test 76" else  stdout.WriteLine "YES"
let _ = if B2 Z2 < A2 1 then reportFailure "basic test 77" else  stdout.WriteLine "YES"
let _ = if B2 Z2 < B2 Z2 then reportFailure "basic test 78" else  stdout.WriteLine "YES"
let _ = if B2 Z2 < C2 "3" then stdout.WriteLine "YES" else  reportFailure "basic test Q132"
let _ = if B2 Z2 < D2 "a" then stdout.WriteLine "YES" else  reportFailure "basic test Q133"

let _ =  printString "ewknjs232: "

let _ = if C2 "3" < Z2 then reportFailure "basic test 79" else  stdout.WriteLine "YES"
let _ = if C2 "3" < A2 1 then reportFailure "basic test 80" else  stdout.WriteLine "YES"
let _ = if C2 "3" < B2 Z2 then reportFailure "basic test 81" else  stdout.WriteLine "YES"
let _ = if C2 "3" < C2 "3" then reportFailure "basic test 82" else  stdout.WriteLine "YES"
let _ = if C2 "3" < D2 "a" then stdout.WriteLine "YES" else  reportFailure "basic test Q134"

let _ =  printString "v30js232: "

let _ = if D2 "a" < Z2 then reportFailure "basic test 83" else  stdout.WriteLine "YES"
let _ = if D2 "a" < A2 1 then reportFailure "basic test 84" else  stdout.WriteLine "YES"
let _ = if D2 "a" < B2 Z2 then reportFailure "basic test 85" else  stdout.WriteLine "YES"
let _ = if D2 "a" < C2 "3" then reportFailure "basic test 86" else  stdout.WriteLine "YES"
let _ = if D2 "a" < D2 "a" then reportFailure "basic test 87" else  stdout.WriteLine "YES"

let _ =  printString "erv9232: "

exception E1 of int
exception E2 of int
exception E3 of int * exn
let _ = printString "exception equality 1"; if (E1(1) = E1(1)) then stdout.WriteLine "YES" else  reportFailure "basic test Q135"
let _ = printString "exception equality 2"; if (E1(1) <> E2(1)) then stdout.WriteLine "YES" else  reportFailure "basic test Q136"

let _ = printString "exception equality 3"; if (E3(1,E1(2)) = E3(1,E1(2))) then stdout.WriteLine "YES" else  reportFailure "basic test Q137"
let _ = printString "exception equality 4"; if (E3(1,E1(2)) <> E3(1,E2(2))) then stdout.WriteLine "YES" else  reportFailure "basic test Q138"


let _ = printString "match []? "; if (match [] with [] -> true | _ -> false) then stdout.WriteLine "YES" else  reportFailure "basic test Q139"
let _ = printString "[] = []? "; if ([] = []) then stdout.WriteLine "YES" else  reportFailure "basic test Q140"

let _ = printString "2033elk "
let _ = if 1 = 0 then reportFailure "basic test 88" else  stdout.WriteLine "YES"
let _ = if 0 = 1 then reportFailure "basic test 89" else  stdout.WriteLine "YES"
let _ = if -1 = -1 then stdout.WriteLine "YES" else  reportFailure "basic test Q141"

let _ = printString "209fedq3lk "
let _ = if 1 = 0 then reportFailure "basic test 90" else  stdout.WriteLine "YES"
let _ = if 1 === 0 then reportFailure "basic test 91" else  stdout.WriteLine "YES"
let _ = if 0 = 1 then reportFailure "basic test 92" else  stdout.WriteLine "YES"
let _ = if 0 === 1 then reportFailure "basic test 93" else  stdout.WriteLine "YES"
let _ = if -1 = -1 then stdout.WriteLine "YES" else  reportFailure "basic test Q142"
let _ = if -1 === -1 then stdout.WriteLine "YES" else  reportFailure "basic test Q143"
let _ = if 1 = 1 then stdout.WriteLine "YES" else  reportFailure "basic test Q144"
let _ = if 1 === 1 then stdout.WriteLine "YES" else  reportFailure "basic test Q145"
let _ = if (LanguagePrimitives.PhysicalEquality CaseB  CaseC) then reportFailure "basic test 94" else  stdout.WriteLine "YES"
let _ = if (CaseB === CaseC) then reportFailure "basic test 95" else  stdout.WriteLine "YES"

let _ = if (LanguagePrimitives.PhysicalEquality (ref 1) (ref 1)) then reportFailure "basic test 96" else  stdout.WriteLine "YES"


type abc = A | B | C


do test "cwewvewho5" (match box(None: int option) with :? option<int> as v -> (v = None) | _ -> false)


do test "cwewe0981" (LanguagePrimitives.IntrinsicFunctions.UnboxGeneric<int>(box(1)) = 1 )
do test "cwewe0982" ((try ignore(LanguagePrimitives.IntrinsicFunctions.UnboxGeneric<string>(box(1))); false with :? System.InvalidCastException -> true))
do test "cwewe0983" ((try ignore(LanguagePrimitives.IntrinsicFunctions.UnboxGeneric<int>(null)); false with :? System.NullReferenceException -> true))
do test "cwewe0984" (LanguagePrimitives.IntrinsicFunctions.UnboxGeneric<string>(box("a")) = "a")
do test "cwewe0985" (LanguagePrimitives.IntrinsicFunctions.UnboxGeneric<string>(null) = null)
do test "cwewe0986" (LanguagePrimitives.IntrinsicFunctions.UnboxGeneric<int option>(box(None: int option)) = None)
do test "cwewe0987" (LanguagePrimitives.IntrinsicFunctions.UnboxGeneric<string option>(box(None: int option))     = None)
do test "cwewe0988" (LanguagePrimitives.IntrinsicFunctions.UnboxGeneric<int list>(box([]: int list))    = [])
do test "cwewe0989" ((try ignore(LanguagePrimitives.IntrinsicFunctions.UnboxGeneric<int list>(null)); false with :? System.NullReferenceException -> true))
do test "cwewe0980" ((try ignore(LanguagePrimitives.IntrinsicFunctions.UnboxGeneric<string list>(null)); false with :? System.NullReferenceException -> true))

do test "cwewe0981" (unbox<int>(box(1)) = 1 )
do test "cwewe0982" ((try ignore(unbox<string>(box(1))); false with :? System.InvalidCastException -> true))
do test "cwewe0983" ((try ignore(unbox<int>(null)); false with :? System.NullReferenceException -> true))
do test "cwewe0984" (unbox<string>(box("a")) = "a")
do test "cwewe0985" (unbox<string>(null) = null)
do test "cwewe0986" (unbox<int option>(box(None: int option)) = None)
do test "cwewe0987" (unbox<string option>(box(None: int option))     = None)
do test "cwewe0988" (unbox<int list>(box([]: int list))    = [])
do test "cwewe0989" ((try ignore(unbox<int list>(null)); false with :? System.NullReferenceException -> true))
do test "cwewe0980" ((try ignore(unbox<string list>(null)); false with :? System.NullReferenceException -> true))

do test "cwewe098q" (LanguagePrimitives.IntrinsicFunctions.UnboxFast<int>(box(1)) = 1)
do test "cwewe098w" ((try ignore(LanguagePrimitives.IntrinsicFunctions.UnboxFast<string>(box(1))); false with :? System.InvalidCastException -> true))
do test "cwewe098e" ((try ignore(LanguagePrimitives.IntrinsicFunctions.UnboxFast<int>(null)); false with :? System.NullReferenceException -> true))
do test "cwewe098r" (LanguagePrimitives.IntrinsicFunctions.UnboxFast<string>(box("a")) = "a")
do test "cwewe098t" (LanguagePrimitives.IntrinsicFunctions.UnboxFast<string>(null) = null)
do test "cwewe098y" (LanguagePrimitives.IntrinsicFunctions.UnboxFast<int option>(box(None: int option)) = None)
do test "cwewe098u" (LanguagePrimitives.IntrinsicFunctions.UnboxFast<string option>(box(None: int option))     = None)
//These don't qualify for the quick entry
// unbox_quick<int list>(box([]: int list))    = []
// (try ignore(unbox_quick<int list>(null)); false with :? System.NullReferenceException -> true)
// (try ignore(unbox_quick<string list>(null)); false with :? System.NullReferenceException -> true)


do test "cwewe098a" (LanguagePrimitives.IntrinsicFunctions.TypeTestGeneric<int>(box(1)) )
do test "cwewe098s" (not(LanguagePrimitives.IntrinsicFunctions.TypeTestGeneric<int>(null)))
do test "cwewe098d" (LanguagePrimitives.IntrinsicFunctions.TypeTestGeneric<string>(box("a")) )
do test "cwewe098f" (not(LanguagePrimitives.IntrinsicFunctions.TypeTestGeneric<string>(null)))
do test "cwewe098g" (LanguagePrimitives.IntrinsicFunctions.TypeTestGeneric<int option>(box(None: int option)) )
do test "cwewe098h" (LanguagePrimitives.IntrinsicFunctions.TypeTestGeneric<string option>(box(None: int option))     )
do test "cwewe098j" (LanguagePrimitives.IntrinsicFunctions.TypeTestGeneric<int list>(box([]: int list)) )
do test "cwewe098k" (not(LanguagePrimitives.IntrinsicFunctions.TypeTestGeneric<int list>(null)))
do test "cwewe098l" (not(LanguagePrimitives.IntrinsicFunctions.TypeTestGeneric<string list>(null)))

do test "cwewe098z" (LanguagePrimitives.IntrinsicFunctions.TypeTestFast<int>(box(1)) )
do test "cwewe098x" (not(LanguagePrimitives.IntrinsicFunctions.TypeTestFast<int>(null)))
do test "cwewe098c" (LanguagePrimitives.IntrinsicFunctions.TypeTestFast<string>(box("a")) )
do test "cwewe098v" (not(LanguagePrimitives.IntrinsicFunctions.TypeTestFast<string>(null)))
do test "cwewe098b" (LanguagePrimitives.IntrinsicFunctions.TypeTestFast<int list>(box([]: int list)) )
do test "cwewe098n" (not(LanguagePrimitives.IntrinsicFunctions.TypeTestFast<int list>(null)))
do test "cwewe098m" (not(LanguagePrimitives.IntrinsicFunctions.TypeTestFast<string list>(null)))

(*
let istype<'a>(obj:obj) = (obj :? 'a) 
let _ = 
    test "cwewe098z" (istype<int>(box(1)) );
    test "cwewe098x" (not(istype<int>(null)));
    test "cwewe098c" (istype<string>(box("a")) );
    test "cwewe098v" (not(istype<string>(null)));
    test "cwewe098b" (istype<int list>(box([]: int list)) );
    test "cwewe098n" (not(istype<int list>(null)));
    test "cwewe098m" (not(istype<string list>(null)));
    ()

*)

do test "cwewvewho1" (match box(1) with :? int as v -> v = 1 | _ -> true)
do test "cwewvewho2" (match (null:obj) with :? int -> false | _ -> true)
do test "cwewvewho3" (match box("a") with :? string as v -> v = "a" | _ -> true)
do test "cwewvewho4" (match (null:obj) with :? string -> false | _ -> true)
do test "cwewvewho5" (match box(None: int option) with :? option<int> as v -> (v = None) | _ -> false)
do test "cwewvewho6" (match (null:obj)                  with :? option<int> as v -> (v = None) | _ -> false)
do test "cwewvewho7" (match box(None: int option) with :? option<string> as v -> (v = None) | _ -> false)
do test "cwewvewho8" (match (null:obj)            with :? option<string> as v -> (v = None) | _ -> false)
do test "cwewvewho9" (match box(Some 3)    with :? option<int> as v -> (v = Some(3)) | _ -> false)
do test "cwewvewho0" (match box(Some 3)    with :? option<string> -> false | _ -> true)
do test "cwewvewho-" (match box([3])    with :? list<int> as v -> (v = [3]) | _ -> false)
do test "cwewvewhoa" (match box([3])    with :? list<string> as v -> false | _ -> true)

do test "cwewvewhos" (match (null:obj)    with :? list<int> as v -> false | _ -> true)

let pattest<'a> (obj:obj) fail (succeed : 'a -> bool) = match obj  with :? 'a as x -> succeed x | _ -> fail()

do test "cwewvewhoq" (pattest<int>   (box(1))                       (fun () -> false) (fun v -> v = 1))
do test "cwewvewhow" (pattest<int>   (null)                         (fun () -> true ) (fun _ -> false))
do test "cwewvewhoe" (pattest<string>(box("a"))                     (fun () -> false) (fun v -> v = "a"))
do test "cwewvewhor" (pattest<string>(null)                         (fun () -> true)  (fun _ -> false))
do test "cwewvewhot" (pattest<int option>   (box(None: int option)) (fun () -> false) (function None -> true | _ -> false))
do test "cwewvewhoy" (pattest<int option>   (null)                  (fun () -> false) (function None -> true | _ -> false))
do test "cwewvewhou" (pattest<string option>(box(None: int option)) (fun () -> false) (function None -> true | _ -> false))
do test "cwewvewhoi" (pattest<string option>(null)                  (fun () -> false) (function None -> true | _ -> false))
do test "cwewvewhoo" (pattest<int option>   (box(Some 3))           (fun () -> false) (function Some 3 -> true | _ -> false))
do test "cwewvewhop" (pattest<string option>(box(Some 3))           (fun () -> true)  (fun _ -> false))
do test "cwewvewhog" (pattest<string list>  (box(["1"]))            (fun () -> false) (fun _ -> true))
do test "cwewvewhoj" (pattest<string list>  null                    (fun () -> true)  (fun _ -> false))





let _ = printString "string list structural equality (1): "; if ["abc"] = ["def"] then reportFailure "basic test Q146" else  stdout.WriteLine "YES"
let _ = printString "string list object equality (1): "; if ["abc"] === ["def"] then reportFailure "basic test Q147" else  stdout.WriteLine "YES"
let _ = printString "string list structural equality (2): ";  if ["abc"] = ["abc"] then stdout.WriteLine "YES" else  reportFailure "basic test Q148"
let _ = printString "string list object equality (2): ";  if ["abc"] === ["abc"] then stdout.WriteLine "YES" else  reportFailure "basic test Q149"
let _ = printString "hash respects equality (1): "; if hash [] = hash [] then stdout.WriteLine "YES" else  reportFailure "basic test Q150"
let _ = printString "hash respects equality (2): "; if hash [1] = hash [1] then stdout.WriteLine "YES" else  reportFailure "basic test Q151"
let _ = printString "hash respects equality (1a): "; if hash A = hash A then stdout.WriteLine "YES" else  reportFailure "basic test Q152"
let _ = printString "hash respects equality (3): "; if hash ["abc"] = hash ["abc"] then stdout.WriteLine "YES" else  reportFailure "basic test Q153"
let _ = printString "hash respects equality (4): "; if hash ("abc","def") = hash ("abc","def") then stdout.WriteLine "YES" else  reportFailure "basic test Q154"
let _ = printString "hash respects equality (4a): "; if hash (A,"def") = hash (A,"def") then stdout.WriteLine "YES" else  reportFailure "basic test Q155"
let _ = printString "hash respects equality (4b): "; if hash ([],"def") = hash ([],"def") then stdout.WriteLine "YES" else  reportFailure "basic test Q156"
let _ = printString "hash respects equality (4c): "; if hash ([],[]) = hash ([],[]) then stdout.WriteLine "YES" else  reportFailure "basic test Q157"
let _ = printString "hash respects equality (4d): "; if hash (A,B) = hash (A,B) then stdout.WriteLine "YES" else  reportFailure "basic test Q158"
let _ = printString "hash respects equality (5): "; if hash ("abc","def","efg") = hash ("abc","def","efg") then stdout.WriteLine "YES" else  reportFailure "basic test Q159"
let _ = printString "hash respects equality (6): "; if hash ("abc","def","efg","") = hash ("abc","def","efg","") then stdout.WriteLine "YES" else  reportFailure "basic test Q160"
let _ = printString "hash respects equality (7): "; if hash ("abc","def","efg","","q") = hash ("abc","def","efg","","q") then stdout.WriteLine "YES" else  reportFailure "basic test Q161"
let _ = printString "hash respects equality (8): "; if hash ("abc","def","efg","","q","r") = hash ("abc","def","efg","","q","r") then stdout.WriteLine "YES" else  reportFailure "basic test Q162"
let _ = printString "hash respects equality (9): "; if hash ("abc","def","efg","","q","r","s") = hash ("abc","def","efg","","q","r","s") then stdout.WriteLine "YES" else  reportFailure "basic test Q163"
let _ = printString "hash respects equality (int array,10): "; if hash [| 1 |] = hash [| 1 |] then stdout.WriteLine "YES" else  reportFailure "basic test Q164"
let _ = printString "hash respects equality (string array,11): "; if hash [| "a" |] = hash [| "a" |] then stdout.WriteLine "YES" else  reportFailure "basic test Q165"
let _ = printString "hash respects equality (string array,12): "; if hash [| "a";"b" |] = hash [| "a";"b" |] then stdout.WriteLine "YES" else  reportFailure "basic test Q166"
let _ = printString "hash respects equality (byte array,12): "; if hash "abc"B = hash "abc"B then stdout.WriteLine "YES" else  reportFailure "basic test Q167"
let _ = printString "hash respects equality (byte array,12): "; if hash ""B = hash ""B then stdout.WriteLine "YES" else  reportFailure "basic test Q169"
let _ = printString "hash respects equality (byte array,12): "; if hash [| |] = hash [| |] then stdout.WriteLine "YES" else  reportFailure "basic test Q170"


let _ = printString "hash is interesting (1): "; if hash "abc" = hash "def" then  reportFailure "basic test Q171" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (2): "; if hash 0 = hash 1 then  reportFailure "basic test Q172" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (3): "; if hash [0] = hash [1] then  reportFailure "basic test Q173" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (4): "; if hash (0,3) = hash (1,3) then  reportFailure "basic test Q174" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (5): "; if hash {contents=3} = hash {contents=4} then  reportFailure "basic test Q175" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (6): "; if hash [0;1;2] = hash [0;1;3] then  reportFailure "basic test Q176" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (7): "; if hash [0;1;2;3;4;5] = hash [0;1;2;3;4;6] then  reportFailure "basic test Q177" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (7): "; if hash [0;1;2;3;4] = hash [0;1;2;3;6] then  reportFailure "basic test Q178" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (7): "; if hash [0;1;2;3;4;5;6;7] = hash [0;1;2;3;4;5;6;8] then  reportFailure "basic test Q179" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (8): "; if hash [0;1;2;3;4;5;6;7;8] = hash [0;1;2;3;4;5;6;7;9] then  reportFailure "basic test Q180" else stdout.WriteLine "YES"

let _ = printString "hash is interesting (9): "; if hash [[0];[1];[2]] = hash [[0];[1];[3]] then  reportFailure "basic test Q181" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (10): "; if hash [[0];[1];[2];[3];[4];[5]] = hash [[0];[1];[2];[3];[4];[6]] then  reportFailure "basic test Q182" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (11): "; if hash [[0];[1];[2];[3];[4];[5];[6]] = hash [[0];[1];[2];[3];[4];[5];[7]] then  reportFailure "basic test Q183" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (bytearray 1): "; if hash "abc"B = hash "abd"B then  reportFailure "basic test Q184" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (string array 1): "; if hash [| "abc"; "e" |] = hash [| "abc"; "d" |] then  reportFailure "basic test Q185" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (intarray 1): "; if hash [| 3; 4 |] = hash [| 3; 5 |] then  reportFailure "basic test Q186" else stdout.WriteLine "YES"

(* F# compiler does many special tricks to get fast type-specific structural hashing. *)
(* A compiler could only work out that the following hash is type-specific if it inlines *)
(* the whole function, which is very unlikely. *)
let genericHash x =
  stdout.WriteLine "genericHash - hopefully not inlined\n";
  let mutable r = 0 in 
  for i = 1 to 100 do r <- r + 1; done;
  for i = 1 to 100 do r <- r + 1; done;
  for i = 1 to 100 do r <- r + 1; done;
  for i = 1 to 100 do r <- r + 1; done;
  (r - 400) + hash x

#if MONO // See https://github.com/fsharp/fsharp/issues/188
#else

type T = T of int * int


let hashes = 
   [hash (T(1, 1)) ;
    hash (T(4, -1)) ;
    hash (T(2, 0)) ;
    hash (T(0, 1)) ;
    hash (T(-2, 2)) ]

let _ = check "df390enj" (hashes |> Set.ofList |> Set.toList |> List.length) hashes.Length

let _ = printString "type specific hash matches generic hash (1): "; if hash [] = genericHash [] then stdout.WriteLine "YES" else  reportFailure "basic test Q187"
let _ = printString "type specific hash matches generic hash (2): "; if hash [1] = genericHash [1] then stdout.WriteLine "YES" else  reportFailure "basic test Q188"
let _ = printString "type specific hash matches generic hash (1a): "; if hash A = genericHash A then stdout.WriteLine "YES" else  reportFailure "basic test Q189"
let _ = printString "type specific hash matches generic hash (3): "; if hash ["abc"] = genericHash ["abc"] then stdout.WriteLine "YES" else  reportFailure "basic test Q190"
let _ = printString "type specific hash matches generic hash (4): "; if hash ("abc","def") = genericHash ("abc","def") then stdout.WriteLine "YES" else  reportFailure "basic test Q191"
let _ = printString "type specific hash matches generic hash (4a): "; if hash (A,"def") = genericHash (A,"def") then stdout.WriteLine "YES" else  reportFailure "basic test Q192"
let _ = printString "type specific hash matches generic hash (4b): "; if hash ([],"def") = genericHash ([],"def") then stdout.WriteLine "YES" else  reportFailure "basic test Q193"
let _ = printString "type specific hash matches generic hash (4c): "; if hash ([],[]) = genericHash ([],[]) then stdout.WriteLine "YES" else  reportFailure "basic test Q194"
let _ = printString "type specific hash matches generic hash (4d): "; if hash (A,B) = genericHash (A,B) then stdout.WriteLine "YES" else  reportFailure "basic test Q195"
let _ = printString "type specific hash matches generic hash (5): "; if hash ("abc","def","efg") = genericHash ("abc","def","efg") then stdout.WriteLine "YES" else  reportFailure "basic test Q196"
let _ = printString "type specific hash matches generic hash (6): "; if hash ("abc","def","efg","") = genericHash ("abc","def","efg","") then stdout.WriteLine "YES" else  reportFailure "basic test Q197"
let _ = printString "type specific hash matches generic hash (7): "; if hash ("abc","def","efg","","q") = genericHash ("abc","def","efg","","q") then stdout.WriteLine "YES" else  reportFailure "basic test Q198"
let _ = printString "type specific hash matches generic hash (8): "; if hash ("abc","def","efg","","q","r") = genericHash ("abc","def","efg","","q","r") then stdout.WriteLine "YES" else  reportFailure "basic test Q199"
let _ = printString "type specific hash matches generic hash (9): "; if hash ("abc","def","efg","","q","r","s") = genericHash ("abc","def","efg","","q","r","s") then stdout.WriteLine "YES" else  reportFailure "basic test Q200"
let _ = printString "type specific hash matches generic hash (int array,10): "; if hash [| 1 |] = genericHash [| 1 |] then stdout.WriteLine "YES" else  reportFailure "basic test Q201"
let _ = printString "type specific hash matches generic hash (string array,11): "; if hash [| "a" |] = genericHash [| "a" |] then stdout.WriteLine "YES" else  reportFailure "basic test Q202"
let _ = printString "type specific hash matches generic hash (string array,12): "; if hash [| "a";"b" |] = genericHash [| "a";"b" |] then stdout.WriteLine "YES" else  reportFailure "basic test Q203"
let _ = printString "type specific hash matches generic hash (byte array,12): "; if hash "abc"B = genericHash "abc"B then stdout.WriteLine "YES" else  reportFailure "basic test Q204"
let _ = printString "type specific hash matches generic hash (byte array,12): "; if hash ""B = genericHash ""B then stdout.WriteLine "YES" else  reportFailure "basic test Q205"
let _ = printString "type specific hash matches generic hash (byte array,12): "; if hash [| |] = genericHash [| |] then stdout.WriteLine "YES" else  reportFailure "basic test Q206"
#endif


(*---------------------------------------------------------------------------
!* check the same for GetHashCode
 *--------------------------------------------------------------------------- *)


let _ = printString "hash 1 = "; printInt (getObjectHashCode 1); printNewLine()
let _ = printString "hash [] = "; printInt (getObjectHashCode []); printNewLine()
let _ = printString "hash [1] = "; printInt (getObjectHashCode [1]); printNewLine()
let _ = printString "hash [2] = "; printInt (getObjectHashCode [2]); printNewLine()
let r3339 = ref 1 
let _ = printString "hash 2 = "; printInt (getObjectHashCode 2); printNewLine()
let _ = printString "hash 6 = "; printInt (getObjectHashCode 6); printNewLine()
let _ = printString "hash \"abc\" = "; printInt (getObjectHashCode "abc"); printNewLine()
let _ = printString "hash \"abd\" = "; printInt (getObjectHashCode "abd"); printNewLine()
let _ = printString "hash \"\" = "; printInt (getObjectHashCode ""); printNewLine()


let _ = printString "hash respects equality (1): "; if getObjectHashCode [] = getObjectHashCode [] then stdout.WriteLine "YES" else  reportFailure "basic test Q207"
let _ = printString "hash respects equality (2): "; if getObjectHashCode [1] = getObjectHashCode [1] then stdout.WriteLine "YES" else  reportFailure "basic test Q208"
let _ = printString "hash respects equality (1a): "; if getObjectHashCode A = getObjectHashCode A then stdout.WriteLine "YES" else  reportFailure "basic test Q209"
let _ = printString "hash respects equality (3): "; if getObjectHashCode ["abc"] = getObjectHashCode ["abc"] then stdout.WriteLine "YES" else  reportFailure "basic test Q210"
let _ = printString "hash respects equality (4): "; if getObjectHashCode ("abc","def") = getObjectHashCode ("abc","def") then stdout.WriteLine "YES" else  reportFailure "basic test Q211"
let _ = printString "hash respects equality (4a): "; if getObjectHashCode (A,"def") = getObjectHashCode (A,"def") then stdout.WriteLine "YES" else  reportFailure "basic test Q212"
let _ = printString "hash respects equality (4b): "; if getObjectHashCode ([],"def") = getObjectHashCode ([],"def") then stdout.WriteLine "YES" else  reportFailure "basic test Q213"
let _ = printString "hash respects equality (4c): "; if getObjectHashCode ([],[]) = getObjectHashCode ([],[]) then stdout.WriteLine "YES" else  reportFailure "basic test Q214"
let _ = printString "hash respects equality (4d): "; if getObjectHashCode (A,B) = getObjectHashCode (A,B) then stdout.WriteLine "YES" else  reportFailure "basic test Q215"
let _ = printString "hash respects equality (5): "; if getObjectHashCode ("abc","def","efg") = getObjectHashCode ("abc","def","efg") then stdout.WriteLine "YES" else  reportFailure "basic test Q216"
let _ = printString "hash respects equality (6): "; if getObjectHashCode ("abc","def","efg","") = getObjectHashCode ("abc","def","efg","") then stdout.WriteLine "YES" else  reportFailure "basic test Q217"
let _ = printString "hash respects equality (7): "; if getObjectHashCode ("abc","def","efg","","q") = getObjectHashCode ("abc","def","efg","","q") then stdout.WriteLine "YES" else  reportFailure "basic test Q218"
let _ = printString "hash respects equality (8): "; if getObjectHashCode ("abc","def","efg","","q","r") = getObjectHashCode ("abc","def","efg","","q","r") then stdout.WriteLine "YES" else  reportFailure "basic test Q219"
let _ = printString "hash respects equality (9): "; if getObjectHashCode ("abc","def","efg","","q","r","s") = getObjectHashCode ("abc","def","efg","","q","r","s") then stdout.WriteLine "YES" else  reportFailure "basic test Q220"

(* NOTE: GetHashCode guarantees do not apply to mutable data structures 

let _ = printString "hash respects equality (int array,10): "; if getObjectHashCode [| 1 |] = getObjectHashCode [| 1 |] then stdout.WriteLine "YES" else  reportFailure "basic test Q221"
let _ = printString "hash respects equality (string array,11): "; if getObjectHashCode [| "a" |] = getObjectHashCode [| "a" |] then stdout.WriteLine "YES" else  reportFailure "basic test Q222"
let _ = printString "hash respects equality (string array,12): "; if getObjectHashCode [| "a";"b" |] = getObjectHashCode [| "a";"b" |] then stdout.WriteLine "YES" else  reportFailure "basic test Q223"
let _ = printString "hash respects equality (byte array,12): "; if getObjectHashCode "abc"B = getObjectHashCode "abc"B then stdout.WriteLine "YES" else  reportFailure "basic test Q224"
let _ = printString "hash respects equality (byte array,12): "; if getObjectHashCode ""B = getObjectHashCode ""B then stdout.WriteLine "YES" else  reportFailure "basic test Q225"
*)


let _ = printString "hash is interesting (1): "; if getObjectHashCode "abc" = getObjectHashCode "def" then  reportFailure "basic test Q226" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (2): "; if getObjectHashCode 0 = getObjectHashCode 1 then  reportFailure "basic test Q227" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (3): "; if getObjectHashCode [0] = getObjectHashCode [1] then  reportFailure "basic test Q228" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (4): "; if getObjectHashCode (0,3) = getObjectHashCode (1,3) then  reportFailure "basic test Q229" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (6): "; if getObjectHashCode [0;1;2] = getObjectHashCode [0;1;3] then  reportFailure "basic test Q230" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (7): "; if getObjectHashCode [0;1;2;3;4;5] = getObjectHashCode [0;1;2;3;4;6] then  reportFailure "basic test Q231" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (7): "; if getObjectHashCode [0;1;2;3;4] = getObjectHashCode [0;1;2;3;6] then  reportFailure "basic test Q232" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (7): "; if getObjectHashCode [0;1;2;3;4;5;6;7] = getObjectHashCode [0;1;2;3;4;5;6;8] then  reportFailure "basic test Q233" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (8): "; if getObjectHashCode [0;1;2;3;4;5;6;7;8] = getObjectHashCode [0;1;2;3;4;5;6;7;9] then  reportFailure "basic test Q234" else stdout.WriteLine "YES"

let _ = printString "hash is interesting (9): "; if getObjectHashCode [[0];[1];[2]] = getObjectHashCode [[0];[1];[3]] then  reportFailure "basic test Q235" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (10): "; if getObjectHashCode [[0];[1];[2];[3];[4];[5]] = getObjectHashCode [[0];[1];[2];[3];[4];[6]] then  reportFailure "basic test Q236" else stdout.WriteLine "YES"
let _ = printString "hash is interesting (11): "; if getObjectHashCode [[0];[1];[2];[3];[4];[5];[6]] = getObjectHashCode [[0];[1];[2];[3];[4];[5];[7]] then  reportFailure "basic test Q237" else stdout.WriteLine "YES"

let _ = printString "type specific hash matches generic hash (1): "; if getObjectHashCode [] = genericHash [] then stdout.WriteLine "YES" else  reportFailure "basic test Q238"
let _ = printString "type specific hash matches generic hash (2): "; if getObjectHashCode [1] = genericHash [1] then stdout.WriteLine "YES" else  reportFailure "basic test Q239"
let _ = printString "type specific hash matches generic hash (1a): "; if getObjectHashCode A = genericHash A then stdout.WriteLine "YES" else  reportFailure "basic test Q240"
let _ = printString "type specific hash matches generic hash (3): "; if getObjectHashCode ["abc"] = genericHash ["abc"] then stdout.WriteLine "YES" else  reportFailure "basic test Q241"
let _ = printString "type specific hash matches generic hash (4): "; if getObjectHashCode ("abc","def") = genericHash ("abc","def") then stdout.WriteLine "YES" else  reportFailure "basic test Q242"
let _ = printString "type specific hash matches generic hash (4a): "; if getObjectHashCode (A,"def") = genericHash (A,"def") then stdout.WriteLine "YES" else  reportFailure "basic test Q243"
let _ = printString "type specific hash matches generic hash (4b): "; if getObjectHashCode ([],"def") = genericHash ([],"def") then stdout.WriteLine "YES" else  reportFailure "basic test Q244"
let _ = printString "type specific hash matches generic hash (4c): "; if getObjectHashCode ([],[]) = genericHash ([],[]) then stdout.WriteLine "YES" else  reportFailure "basic test Q245"
let _ = printString "type specific hash matches generic hash (4d): "; if getObjectHashCode (A,B) = genericHash (A,B) then stdout.WriteLine "YES" else  reportFailure "basic test Q246"
let _ = printString "type specific hash matches generic hash (5): "; if getObjectHashCode ("abc","def","efg") = genericHash ("abc","def","efg") then stdout.WriteLine "YES" else  reportFailure "basic test Q247"
let _ = printString "type specific hash matches generic hash (6): "; if getObjectHashCode ("abc","def","efg","") = genericHash ("abc","def","efg","") then stdout.WriteLine "YES" else  reportFailure "basic test Q248"
let _ = printString "type specific hash matches generic hash (7): "; if getObjectHashCode ("abc","def","efg","","q") = genericHash ("abc","def","efg","","q") then stdout.WriteLine "YES" else  reportFailure "basic test Q249"
let _ = printString "type specific hash matches generic hash (8): "; if getObjectHashCode ("abc","def","efg","","q","r") = genericHash ("abc","def","efg","","q","r") then stdout.WriteLine "YES" else  reportFailure "basic test Q250"
let _ = printString "type specific hash matches generic hash (9): "; if getObjectHashCode ("abc","def","efg","","q","r","s") = genericHash ("abc","def","efg","","q","r","s") then stdout.WriteLine "YES" else  reportFailure "basic test Q251"


(*---------------------------------------------------------------------------
!* check we can resolve overlapping constructor names using type names
 *--------------------------------------------------------------------------- *)

module OverlappingCOnstructorNames = 

  type XY = X | Y
  type YZ = Y | Z

  let x0 = X
  let x1 = XY.X
  let y0 = Y
  let y1 = XY.Y
  let y2 = YZ.Y
  let z0 = Z
  let z2 = YZ.Z


  let f xy = 
    match xy with 
    | XY.X -> "X"
    |  XY.Y -> "Y"

  let g yz = 
    match yz with 
    | YZ.Y -> "X"
    | YZ.Z -> "Y"


(*---------------------------------------------------------------------------
!* Equality tests over structured values for data likely to contain
 * values represented by "null" 
 *--------------------------------------------------------------------------- *)

let _ = printString "tuple inequality null test (1): "; if (1,2) = (1,3) then reportFailure "basic test Q252" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (2): "; if ([],2) = ([],1) then reportFailure "basic test Q253" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (3): "; if (1,[]) = (2,[]) then reportFailure "basic test Q254" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (4): "; if (1,2,3) = (1,2,4) then reportFailure "basic test Q255" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (5): "; if ([],2,3) = ([],2,4) then reportFailure "basic test Q256" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (6): "; if (1,[],2) = (1,[],3) then reportFailure "basic test Q257" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (7): "; if (1,2,[]) = (1,3,[]) then reportFailure "basic test Q258" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (8): "; if (1,2,3,4) = (1,2,3,5) then reportFailure "basic test Q259" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (9): "; if ([],2,3,4) = ([],2,4,4) then reportFailure "basic test Q260" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (10): "; if (1,[],3,4) = (1,[],3,5) then reportFailure "basic test Q261" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (11): "; if (1,2,[],4) = (1,2,[],5) then reportFailure "basic test Q262" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (12): "; if (1,2,3,[]) = (1,2,4,[]) then reportFailure "basic test Q263" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (13): "; if (1,2,3,4,5) = (1,2,3,4,6) then reportFailure "basic test Q264" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (14): "; if ([],2,3,4,5) = ([],2,3,5,5) then reportFailure "basic test Q265" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (15): "; if (1,[],3,4,5) = (1,[],3,6,5) then reportFailure "basic test Q266" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (16): "; if (1,2,[],4,5) = (1,2,[],3,5) then reportFailure "basic test Q267" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (17): "; if (1,2,3,[],5) = (1,2,3,[],6) then reportFailure "basic test Q268" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (18): "; if (1,2,3,4,[]) = (1,7,3,4,[]) then reportFailure "basic test Q269" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (19): "; if (1,2,3,4,5,6) = (1,2,3,4,5,7) then reportFailure "basic test Q270" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (20): "; if ([],2,3,4,5,6) = ([],2,3,4,5,7) then reportFailure "basic test Q271" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (21): "; if (1,[],3,4,5,6) = (1,[],3,4,5,7) then reportFailure "basic test Q272" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (22): "; if (1,2,[],4,5,6) = (1,2,[],4,5,7) then reportFailure "basic test Q273" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (23): "; if (1,2,3,[],5,6) = (1,2,3,[],5,7) then reportFailure "basic test Q274" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (24): "; if (1,2,3,4,[],6) = (1,2,3,4,[],7) then reportFailure "basic test Q275" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (25): "; if (1,2,3,4,5,[]) = (1,2,3,4,6,[]) then reportFailure "basic test Q276" else  stdout.WriteLine "YES"

let _ = printString "tuple equality null test (1): "; if (1,2) = (1,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q277"
let _ = printString "tuple equality null test (2): "; if ([],2) = ([],2) then stdout.WriteLine "YES" else  reportFailure "basic test Q278"
let _ = printString "tuple equality null test (3): "; if (1,[]) = (1,[]) then stdout.WriteLine "YES" else  reportFailure "basic test Q279"
let _ = printString "tuple equality null test (4): "; if (1,2,3) = (1,2,3) then stdout.WriteLine "YES" else  reportFailure "basic test Q280"
let _ = printString "tuple equality null test (5): "; if ([],2,3) = ([],2,3) then stdout.WriteLine "YES" else  reportFailure "basic test Q281"
let _ = printString "tuple equality null test (6): "; if (1,[],2) = (1,[],2) then stdout.WriteLine "YES" else  reportFailure "basic test Q282"
let _ = printString "tuple equality null test (7): "; if (1,2,[]) = (1,2,[]) then stdout.WriteLine "YES" else  reportFailure "basic test Q283"
let _ = printString "tuple equality null test (8): "; if (1,2,3,4) = (1,2,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q284"
let _ = printString "tuple equality null test (9): "; if ([],2,3,4) = ([],2,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q285"
let _ = printString "tuple equality null test (10): "; if (1,[],3,4) = (1,[],3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q286"
let _ = printString "tuple equality null test (11): "; if (1,2,[],4) = (1,2,[],4) then stdout.WriteLine "YES" else  reportFailure "basic test Q287"
let _ = printString "tuple equality null test (12): "; if (1,2,3,[]) = (1,2,3,[]) then stdout.WriteLine "YES" else  reportFailure "basic test Q288"
let _ = printString "tuple equality null test (13): "; if (1,2,3,4,5) = (1,2,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q289"
let _ = printString "tuple equality null test (14): "; if ([],2,3,4,5) = ([],2,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q290"
let _ = printString "tuple equality null test (15): "; if (1,[],3,4,5) = (1,[],3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q291"
let _ = printString "tuple equality null test (16): "; if (1,2,[],4,5) = (1,2,[],4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q292"
let _ = printString "tuple equality null test (17): "; if (1,2,3,[],5) = (1,2,3,[],5) then stdout.WriteLine "YES" else  reportFailure "basic test Q293"
let _ = printString "tuple equality null test (18): "; if (1,2,3,4,[]) = (1,2,3,4,[]) then stdout.WriteLine "YES" else  reportFailure "basic test Q294"
let _ = printString "tuple equality null test (19): "; if (1,2,3,4,5,6) = (1,2,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q295"
let _ = printString "tuple equality null test (20): "; if ([],2,3,4,5,6) = ([],2,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q296"
let _ = printString "tuple equality null test (21): "; if (1,[],3,4,5,6) = (1,[],3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q297"
let _ = printString "tuple equality null test (22): "; if (1,2,[],4,5,6) = (1,2,[],4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q298"
let _ = printString "tuple equality null test (23): "; if (1,2,3,[],5,6) = (1,2,3,[],5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q299"
let _ = printString "tuple equality null test (24): "; if (1,2,3,4,[],6) = (1,2,3,4,[],6) then stdout.WriteLine "YES" else  reportFailure "basic test Q300"
let _ = printString "tuple equality null test (25): "; if (1,2,3,4,5,[]) = (1,2,3,4,5,[]) then stdout.WriteLine "YES" else  reportFailure "basic test Q301"

let _ = printString "tuple inequality null test (a1): "; if (1,2) = (1,3) then reportFailure "basic test Q302" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a2): "; if (A,2) = (A,1) then reportFailure "basic test Q303" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a3): "; if (1,A) = (2,A) then reportFailure "basic test Q304" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a4): "; if (1,2,3) = (1,2,4) then reportFailure "basic test Q305" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a5): "; if (A,2,3) = (A,2,4) then reportFailure "basic test Q306" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a6): "; if (1,A,2) = (1,A,3) then reportFailure "basic test Q307" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a7): "; if (1,2,A) = (1,3,A) then reportFailure "basic test Q308" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a8): "; if (1,2,3,4) = (1,2,3,5) then reportFailure "basic test Q309" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a9): "; if (A,2,3,4) = (A,2,4,4) then reportFailure "basic test Q310" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a10): "; if (1,A,3,4) = (1,A,3,5) then reportFailure "basic test Q311" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a11): "; if (1,2,A,4) = (1,2,A,5) then reportFailure "basic test Q312" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a12): "; if (1,2,3,A) = (1,2,4,A) then reportFailure "basic test Q313" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a13): "; if (1,2,3,4,5) = (1,2,3,4,6) then reportFailure "basic test Q314" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a14): "; if (A,2,3,4,5) = (A,2,3,5,5) then reportFailure "basic test Q315" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a15): "; if (1,A,3,4,5) = (1,A,3,6,5) then reportFailure "basic test Q316" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a16): "; if (1,2,A,4,5) = (1,2,A,3,5) then reportFailure "basic test Q317" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a17): "; if (1,2,3,A,5) = (1,2,3,A,6) then reportFailure "basic test Q318" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a18): "; if (1,2,3,4,A) = (1,7,3,4,A) then reportFailure "basic test Q319" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a19): "; if (1,2,3,4,5,6) = (1,2,3,4,5,7) then reportFailure "basic test Q320" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a20): "; if (A,2,3,4,5,6) = (A,2,3,4,5,7) then reportFailure "basic test Q321" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a21): "; if (1,A,3,4,5,6) = (1,A,3,4,5,7) then reportFailure "basic test Q322" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a22): "; if (1,2,A,4,5,6) = (1,2,A,4,5,7) then reportFailure "basic test Q323" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a23): "; if (1,2,3,A,5,6) = (1,2,3,A,5,7) then reportFailure "basic test Q324" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a24): "; if (1,2,3,4,A,6) = (1,2,3,4,A,7) then reportFailure "basic test Q325" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (a25): "; if (1,2,3,4,5,A) = (1,2,3,4,6,A) then reportFailure "basic test Q326" else  stdout.WriteLine "YES"

let _ = printString "tuple equality null test (a1): "; if (1,2) = (1,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q327"
let _ = printString "tuple equality null test (a2): "; if (A,2) = (A,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q328"
let _ = printString "tuple equality null test (a3): "; if (1,A) = (1,A) then stdout.WriteLine "YES" else  reportFailure "basic test Q329"
let _ = printString "tuple equality null test (a4): "; if (1,2,3) = (1,2,3) then stdout.WriteLine "YES" else  reportFailure "basic test Q330"
let _ = printString "tuple equality null test (a5): "; if (A,2,3) = (A,2,3) then stdout.WriteLine "YES" else  reportFailure "basic test Q331"
let _ = printString "tuple equality null test (a6): "; if (1,A,2) = (1,A,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q332"
let _ = printString "tuple equality null test (a7): "; if (1,2,A) = (1,2,A) then stdout.WriteLine "YES" else  reportFailure "basic test Q333"
let _ = printString "tuple equality null test (a8): "; if (1,2,3,4) = (1,2,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q334"
let _ = printString "tuple equality null test (a9): "; if (A,2,3,4) = (A,2,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q335"
let _ = printString "tuple equality null test (a10): "; if (1,A,3,4) = (1,A,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q336"
let _ = printString "tuple equality null test (a11): "; if (1,2,A,4) = (1,2,A,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q337"
let _ = printString "tuple equality null test (a12): "; if (1,2,3,A) = (1,2,3,A) then stdout.WriteLine "YES" else  reportFailure "basic test Q338"
let _ = printString "tuple equality null test (a13): "; if (1,2,3,4,5) = (1,2,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q339"
let _ = printString "tuple equality null test (a14): "; if (A,2,3,4,5) = (A,2,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q340"
let _ = printString "tuple equality null test (a15): "; if (1,A,3,4,5) = (1,A,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q341"
let _ = printString "tuple equality null test (a16): "; if (1,2,A,4,5) = (1,2,A,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q342"
let _ = printString "tuple equality null test (a17): "; if (1,2,3,A,5) = (1,2,3,A,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q343"
let _ = printString "tuple equality null test (a18): "; if (1,2,3,4,A) = (1,2,3,4,A) then stdout.WriteLine "YES" else  reportFailure "basic test Q344"
let _ = printString "tuple equality null test (a19): "; if (1,2,3,4,5,6) = (1,2,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q345"
let _ = printString "tuple equality null test (a20): "; if (A,2,3,4,5,6) = (A,2,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q346"
let _ = printString "tuple equality null test (a21): "; if (1,A,3,4,5,6) = (1,A,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q347"
let _ = printString "tuple equality null test (a22): "; if (1,2,A,4,5,6) = (1,2,A,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q348"
let _ = printString "tuple equality null test (a23): "; if (1,2,3,A,5,6) = (1,2,3,A,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q349"
let _ = printString "tuple equality null test (a24): "; if (1,2,3,4,A,6) = (1,2,3,4,A,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q350"
let _ = printString "tuple equality null test (a25): "; if (1,2,3,4,5,A) = (1,2,3,4,5,A) then stdout.WriteLine "YES" else  reportFailure "basic test Q351"

let _ = printString "tuple inequality null test (b1): "; if (1,2) = (1,3) then reportFailure "basic test Q351" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b2): "; if (B,2) = (B,1) then reportFailure "basic test Q352" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b3): "; if (1,B) = (2,B) then reportFailure "basic test Q353" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b4): "; if (1,2,3) = (1,2,4) then reportFailure "basic test Q354" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b5): "; if (B,2,3) = (B,2,4) then reportFailure "basic test Q355" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b6): "; if (1,B,2) = (1,B,3) then reportFailure "basic test Q356" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b7): "; if (1,2,B) = (1,3,B) then reportFailure "basic test Q357" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b8): "; if (1,2,3,4) = (1,2,3,5) then reportFailure "basic test Q358" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b9): "; if (B,2,3,4) = (B,2,4,4) then reportFailure "basic test Q359" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b10): "; if (1,B,3,4) = (1,B,3,5) then reportFailure "basic test Q360" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b11): "; if (1,2,B,4) = (1,2,B,5) then reportFailure "basic test Q361" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b12): "; if (1,2,3,B) = (1,2,4,B) then reportFailure "basic test Q362" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b13): "; if (1,2,3,4,5) = (1,2,3,4,6) then reportFailure "basic test Q363" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b14): "; if (B,2,3,4,5) = (B,2,3,5,5) then reportFailure "basic test Q364" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b15): "; if (1,B,3,4,5) = (1,B,3,6,5) then reportFailure "basic test Q365" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b16): "; if (1,2,B,4,5) = (1,2,B,3,5) then reportFailure "basic test Q366" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b17): "; if (1,2,3,B,5) = (1,2,3,B,6) then reportFailure "basic test Q367" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b18): "; if (1,2,3,4,B) = (1,7,3,4,B) then reportFailure "basic test Q368" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b19): "; if (1,2,3,4,5,6) = (1,2,3,4,5,7) then reportFailure "basic test Q369" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b20): "; if (B,2,3,4,5,6) = (B,2,3,4,5,7) then reportFailure "basic test Q370" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b21): "; if (1,B,3,4,5,6) = (1,B,3,4,5,7) then reportFailure "basic test Q371" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b22): "; if (1,2,B,4,5,6) = (1,2,B,4,5,7) then reportFailure "basic test Q372" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b23): "; if (1,2,3,B,5,6) = (1,2,3,B,5,7) then reportFailure "basic test Q373" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b24): "; if (1,2,3,4,B,6) = (1,2,3,4,B,7) then reportFailure "basic test Q374" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b25): "; if (1,2,3,4,5,B) = (1,2,3,4,6,B) then reportFailure "basic test Q375" else  stdout.WriteLine "YES"

let _ = printString "tuple equality null test (b1): "; if (1,2) = (1,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b2): "; if (B,2) = (B,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b3): "; if (1,B) = (1,B) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b4): "; if (1,2,3) = (1,2,3) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b5): "; if (B,2,3) = (B,2,3) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b6): "; if (1,B,2) = (1,B,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b7): "; if (1,2,B) = (1,2,B) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b8): "; if (1,2,3,4) = (1,2,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b9): "; if (B,2,3,4) = (B,2,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b10): "; if (1,B,3,4) = (1,B,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b11): "; if (1,2,B,4) = (1,2,B,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b12): "; if (1,2,3,B) = (1,2,3,B) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b13): "; if (1,2,3,4,5) = (1,2,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b14): "; if (B,2,3,4,5) = (B,2,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b15): "; if (1,B,3,4,5) = (1,B,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b16): "; if (1,2,B,4,5) = (1,2,B,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b17): "; if (1,2,3,B,5) = (1,2,3,B,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b18): "; if (1,2,3,4,B) = (1,2,3,4,B) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b19): "; if (1,2,3,4,5,6) = (1,2,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b20): "; if (B,2,3,4,5,6) = (B,2,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b21): "; if (1,B,3,4,5,6) = (1,B,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b22): "; if (1,2,B,4,5,6) = (1,2,B,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b23): "; if (1,2,3,B,5,6) = (1,2,3,B,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b24): "; if (1,2,3,4,B,6) = (1,2,3,4,B,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b25): "; if (1,2,3,4,5,B) = (1,2,3,4,5,B) then stdout.WriteLine "YES" else  reportFailure "basic test Q"

let _ = printString "tuple inequality null test (b1): "; if (1,2) = (1,3) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b2): "; if (C,2) = (C,1) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b3): "; if (1,C) = (2,C) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b4): "; if (1,2,3) = (1,2,4) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b5): "; if (C,2,3) = (C,2,4) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b6): "; if (1,C,2) = (1,C,3) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b7): "; if (1,2,C) = (1,3,C) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b8): "; if (1,2,3,4) = (1,2,3,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b9): "; if (C,2,3,4) = (C,2,4,4) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b10): "; if (1,C,3,4) = (1,C,3,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b11): "; if (1,2,C,4) = (1,2,C,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b12): "; if (1,2,3,C) = (1,2,4,C) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b13): "; if (1,2,3,4,5) = (1,2,3,4,6) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b14): "; if (C,2,3,4,5) = (C,2,3,5,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b15): "; if (1,C,3,4,5) = (1,C,3,6,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b16): "; if (1,2,C,4,5) = (1,2,C,3,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b17): "; if (1,2,3,C,5) = (1,2,3,C,6) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b18): "; if (1,2,3,4,C) = (1,7,3,4,C) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b19): "; if (1,2,3,4,5,6) = (1,2,3,4,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b20): "; if (C,2,3,4,5,6) = (C,2,3,4,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b21): "; if (1,C,3,4,5,6) = (1,C,3,4,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b22): "; if (1,2,C,4,5,6) = (1,2,C,4,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b23): "; if (1,2,3,C,5,6) = (1,2,3,C,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b24): "; if (1,2,3,4,C,6) = (1,2,3,4,C,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple inequality null test (b25): "; if (1,2,3,4,5,C) = (1,2,3,4,6,C) then reportFailure "basic test Q" else  stdout.WriteLine "YES"

let _ = printString "tuple equality null test (b1): "; if (1,2) = (1,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b2): "; if (C,2) = (C,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b3): "; if (1,C) = (1,C) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b4): "; if (1,2,3) = (1,2,3) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b5): "; if (C,2,3) = (C,2,3) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b6): "; if (1,C,2) = (1,C,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b7): "; if (1,2,C) = (1,2,C) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b8): "; if (1,2,3,4) = (1,2,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b9): "; if (C,2,3,4) = (C,2,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b10): "; if (1,C,3,4) = (1,C,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b11): "; if (1,2,C,4) = (1,2,C,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b12): "; if (1,2,3,C) = (1,2,3,C) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b13): "; if (1,2,3,4,5) = (1,2,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b14): "; if (C,2,3,4,5) = (C,2,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b15): "; if (1,C,3,4,5) = (1,C,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b16): "; if (1,2,C,4,5) = (1,2,C,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b17): "; if (1,2,3,C,5) = (1,2,3,C,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b18): "; if (1,2,3,4,C) = (1,2,3,4,C) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b19): "; if (1,2,3,4,5,6) = (1,2,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b20): "; if (C,2,3,4,5,6) = (C,2,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b21): "; if (1,C,3,4,5,6) = (1,C,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b22): "; if (1,2,C,4,5,6) = (1,2,C,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b23): "; if (1,2,3,C,5,6) = (1,2,3,C,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b24): "; if (1,2,3,4,C,6) = (1,2,3,4,C,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple equality null test (b25): "; if (1,2,3,4,5,C) = (1,2,3,4,5,C) then stdout.WriteLine "YES" else  reportFailure "basic test Q"



let _ = printString "tuple object inequality null test (1): "; if (1,2) === (1,3) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (2): "; if ([],2) === ([],1) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (3): "; if (1,[]) === (2,[]) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (4): "; if (1,2,3) === (1,2,4) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (5): "; if ([],2,3) === ([],2,4) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (6): "; if (1,[],2) === (1,[],3) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (7): "; if (1,2,[]) === (1,3,[]) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (8): "; if (1,2,3,4) === (1,2,3,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (9): "; if ([],2,3,4) === ([],2,4,4) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (10): "; if (1,[],3,4) === (1,[],3,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (11): "; if (1,2,[],4) === (1,2,[],5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (12): "; if (1,2,3,[]) === (1,2,4,[]) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (13): "; if (1,2,3,4,5) === (1,2,3,4,6) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (14): "; if ([],2,3,4,5) === ([],2,3,5,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (15): "; if (1,[],3,4,5) === (1,[],3,6,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (16): "; if (1,2,[],4,5) === (1,2,[],3,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (17): "; if (1,2,3,[],5) === (1,2,3,[],6) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (18): "; if (1,2,3,4,[]) === (1,7,3,4,[]) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (19): "; if (1,2,3,4,5,6) === (1,2,3,4,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (20): "; if ([],2,3,4,5,6) === ([],2,3,4,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (21): "; if (1,[],3,4,5,6) === (1,[],3,4,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (22): "; if (1,2,[],4,5,6) === (1,2,[],4,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (23): "; if (1,2,3,[],5,6) === (1,2,3,[],5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (24): "; if (1,2,3,4,[],6) === (1,2,3,4,[],7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (25): "; if (1,2,3,4,5,[]) === (1,2,3,4,6,[]) then reportFailure "basic test Q" else  stdout.WriteLine "YES"

let _ = printString "tuple object equality null test (1): "; if (1,2) === (1,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (2): "; if ([],2) === ([],2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (3): "; if (1,[]) === (1,[]) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (4): "; if (1,2,3) === (1,2,3) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (5): "; if ([],2,3) === ([],2,3) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (6): "; if (1,[],2) === (1,[],2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (7): "; if (1,2,[]) === (1,2,[]) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (8): "; if (1,2,3,4) === (1,2,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (9): "; if ([],2,3,4) === ([],2,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (10): "; if (1,[],3,4) === (1,[],3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (11): "; if (1,2,[],4) === (1,2,[],4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (12): "; if (1,2,3,[]) === (1,2,3,[]) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (13): "; if (1,2,3,4,5) === (1,2,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (14): "; if ([],2,3,4,5) === ([],2,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (15): "; if (1,[],3,4,5) === (1,[],3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (16): "; if (1,2,[],4,5) === (1,2,[],4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (17): "; if (1,2,3,[],5) === (1,2,3,[],5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (18): "; if (1,2,3,4,[]) === (1,2,3,4,[]) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (19): "; if (1,2,3,4,5,6) === (1,2,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (20): "; if ([],2,3,4,5,6) === ([],2,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (21): "; if (1,[],3,4,5,6) === (1,[],3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (22): "; if (1,2,[],4,5,6) === (1,2,[],4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (23): "; if (1,2,3,[],5,6) === (1,2,3,[],5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (24): "; if (1,2,3,4,[],6) === (1,2,3,4,[],6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (25): "; if (1,2,3,4,5,[]) === (1,2,3,4,5,[]) then stdout.WriteLine "YES" else  reportFailure "basic test Q"

let _ = printString "tuple object inequality null test (a1): "; if (1,2) === (1,3) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a2): "; if (A,2) === (A,1) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a3): "; if (1,A) === (2,A) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a4): "; if (1,2,3) === (1,2,4) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a5): "; if (A,2,3) === (A,2,4) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a6): "; if (1,A,2) === (1,A,3) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a7): "; if (1,2,A) === (1,3,A) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a8): "; if (1,2,3,4) === (1,2,3,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a9): "; if (A,2,3,4) === (A,2,4,4) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a10): "; if (1,A,3,4) === (1,A,3,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a11): "; if (1,2,A,4) === (1,2,A,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a12): "; if (1,2,3,A) === (1,2,4,A) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a13): "; if (1,2,3,4,5) === (1,2,3,4,6) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a14): "; if (A,2,3,4,5) === (A,2,3,5,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a15): "; if (1,A,3,4,5) === (1,A,3,6,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a16): "; if (1,2,A,4,5) === (1,2,A,3,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a17): "; if (1,2,3,A,5) === (1,2,3,A,6) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a18): "; if (1,2,3,4,A) === (1,7,3,4,A) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a19): "; if (1,2,3,4,5,6) === (1,2,3,4,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a20): "; if (A,2,3,4,5,6) === (A,2,3,4,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a21): "; if (1,A,3,4,5,6) === (1,A,3,4,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a22): "; if (1,2,A,4,5,6) === (1,2,A,4,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a23): "; if (1,2,3,A,5,6) === (1,2,3,A,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a24): "; if (1,2,3,4,A,6) === (1,2,3,4,A,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (a25): "; if (1,2,3,4,5,A) === (1,2,3,4,6,A) then reportFailure "basic test Q" else  stdout.WriteLine "YES"

let _ = printString "tuple object equality null test (a1): "; if (1,2) === (1,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a2): "; if (A,2) === (A,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a3): "; if (1,A) === (1,A) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a4): "; if (1,2,3) === (1,2,3) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a5): "; if (A,2,3) === (A,2,3) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a6): "; if (1,A,2) === (1,A,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a7): "; if (1,2,A) === (1,2,A) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a8): "; if (1,2,3,4) === (1,2,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a9): "; if (A,2,3,4) === (A,2,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a10): "; if (1,A,3,4) === (1,A,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a11): "; if (1,2,A,4) === (1,2,A,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a12): "; if (1,2,3,A) === (1,2,3,A) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a13): "; if (1,2,3,4,5) === (1,2,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a14): "; if (A,2,3,4,5) === (A,2,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a15): "; if (1,A,3,4,5) === (1,A,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a16): "; if (1,2,A,4,5) === (1,2,A,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a17): "; if (1,2,3,A,5) === (1,2,3,A,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a18): "; if (1,2,3,4,A) === (1,2,3,4,A) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a19): "; if (1,2,3,4,5,6) === (1,2,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a20): "; if (A,2,3,4,5,6) === (A,2,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a21): "; if (1,A,3,4,5,6) === (1,A,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a22): "; if (1,2,A,4,5,6) === (1,2,A,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a23): "; if (1,2,3,A,5,6) === (1,2,3,A,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a24): "; if (1,2,3,4,A,6) === (1,2,3,4,A,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (a25): "; if (1,2,3,4,5,A) === (1,2,3,4,5,A) then stdout.WriteLine "YES" else  reportFailure "basic test Q"

let _ = printString "tuple object inequality null test (b1): "; if (1,2) === (1,3) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b2): "; if (B,2) === (B,1) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b3): "; if (1,B) === (2,B) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b4): "; if (1,2,3) === (1,2,4) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b5): "; if (B,2,3) === (B,2,4) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b6): "; if (1,B,2) === (1,B,3) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b7): "; if (1,2,B) === (1,3,B) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b8): "; if (1,2,3,4) === (1,2,3,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b9): "; if (B,2,3,4) === (B,2,4,4) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b10): "; if (1,B,3,4) === (1,B,3,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b11): "; if (1,2,B,4) === (1,2,B,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b12): "; if (1,2,3,B) === (1,2,4,B) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b13): "; if (1,2,3,4,5) === (1,2,3,4,6) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b14): "; if (B,2,3,4,5) === (B,2,3,5,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b15): "; if (1,B,3,4,5) === (1,B,3,6,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b16): "; if (1,2,B,4,5) === (1,2,B,3,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b17): "; if (1,2,3,B,5) === (1,2,3,B,6) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b18): "; if (1,2,3,4,B) === (1,7,3,4,B) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b19): "; if (1,2,3,4,5,6) === (1,2,3,4,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b20): "; if (B,2,3,4,5,6) === (B,2,3,4,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b21): "; if (1,B,3,4,5,6) === (1,B,3,4,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b22): "; if (1,2,B,4,5,6) === (1,2,B,4,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b23): "; if (1,2,3,B,5,6) === (1,2,3,B,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b24): "; if (1,2,3,4,B,6) === (1,2,3,4,B,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b25): "; if (1,2,3,4,5,B) === (1,2,3,4,6,B) then reportFailure "basic test Q" else  stdout.WriteLine "YES"

let _ = printString "tuple object equality null test (b1): "; if (1,2) === (1,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b2): "; if (B,2) === (B,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b3): "; if (1,B) === (1,B) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b4): "; if (1,2,3) === (1,2,3) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b5): "; if (B,2,3) === (B,2,3) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b6): "; if (1,B,2) === (1,B,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b7): "; if (1,2,B) === (1,2,B) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b8): "; if (1,2,3,4) === (1,2,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b9): "; if (B,2,3,4) === (B,2,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b10): "; if (1,B,3,4) === (1,B,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b11): "; if (1,2,B,4) === (1,2,B,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b12): "; if (1,2,3,B) === (1,2,3,B) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b13): "; if (1,2,3,4,5) === (1,2,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b14): "; if (B,2,3,4,5) === (B,2,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b15): "; if (1,B,3,4,5) === (1,B,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b16): "; if (1,2,B,4,5) === (1,2,B,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b17): "; if (1,2,3,B,5) === (1,2,3,B,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b18): "; if (1,2,3,4,B) === (1,2,3,4,B) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b19): "; if (1,2,3,4,5,6) === (1,2,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b20): "; if (B,2,3,4,5,6) === (B,2,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b21): "; if (1,B,3,4,5,6) === (1,B,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b22): "; if (1,2,B,4,5,6) === (1,2,B,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b23): "; if (1,2,3,B,5,6) === (1,2,3,B,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b24): "; if (1,2,3,4,B,6) === (1,2,3,4,B,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b25): "; if (1,2,3,4,5,B) === (1,2,3,4,5,B) then stdout.WriteLine "YES" else  reportFailure "basic test Q"

let _ = printString "tuple object inequality null test (b1): "; if (1,2) === (1,3) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b2): "; if (C,2) === (C,1) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b3): "; if (1,C) === (2,C) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b4): "; if (1,2,3) === (1,2,4) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b5): "; if (C,2,3) === (C,2,4) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b6): "; if (1,C,2) === (1,C,3) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b7): "; if (1,2,C) === (1,3,C) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b8): "; if (1,2,3,4) === (1,2,3,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b9): "; if (C,2,3,4) === (C,2,4,4) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b10): "; if (1,C,3,4) === (1,C,3,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b11): "; if (1,2,C,4) === (1,2,C,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b12): "; if (1,2,3,C) === (1,2,4,C) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b13): "; if (1,2,3,4,5) === (1,2,3,4,6) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b14): "; if (C,2,3,4,5) === (C,2,3,5,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b15): "; if (1,C,3,4,5) === (1,C,3,6,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b16): "; if (1,2,C,4,5) === (1,2,C,3,5) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b17): "; if (1,2,3,C,5) === (1,2,3,C,6) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b18): "; if (1,2,3,4,C) === (1,7,3,4,C) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b19): "; if (1,2,3,4,5,6) === (1,2,3,4,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b20): "; if (C,2,3,4,5,6) === (C,2,3,4,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b21): "; if (1,C,3,4,5,6) === (1,C,3,4,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b22): "; if (1,2,C,4,5,6) === (1,2,C,4,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b23): "; if (1,2,3,C,5,6) === (1,2,3,C,5,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b24): "; if (1,2,3,4,C,6) === (1,2,3,4,C,7) then reportFailure "basic test Q" else  stdout.WriteLine "YES"
let _ = printString "tuple object inequality null test (b25): "; if (1,2,3,4,5,C) === (1,2,3,4,6,C) then reportFailure "basic test Q" else  stdout.WriteLine "YES"

let _ = printString "tuple object equality null test (b1): "; if (1,2) === (1,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b2): "; if (C,2) === (C,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b3): "; if (1,C) === (1,C) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b4): "; if (1,2,3) === (1,2,3) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b5): "; if (C,2,3) === (C,2,3) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b6): "; if (1,C,2) === (1,C,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b7): "; if (1,2,C) === (1,2,C) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b8): "; if (1,2,3,4) === (1,2,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b9): "; if (C,2,3,4) === (C,2,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b10): "; if (1,C,3,4) === (1,C,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b11): "; if (1,2,C,4) === (1,2,C,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b12): "; if (1,2,3,C) === (1,2,3,C) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b13): "; if (1,2,3,4,5) === (1,2,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b14): "; if (C,2,3,4,5) === (C,2,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b15): "; if (1,C,3,4,5) === (1,C,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b16): "; if (1,2,C,4,5) === (1,2,C,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b17): "; if (1,2,3,C,5) === (1,2,3,C,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b18): "; if (1,2,3,4,C) === (1,2,3,4,C) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b19): "; if (1,2,3,4,5,6) === (1,2,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b20): "; if (C,2,3,4,5,6) === (C,2,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b21): "; if (1,C,3,4,5,6) === (1,C,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b22): "; if (1,2,C,4,5,6) === (1,2,C,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b23): "; if (1,2,3,C,5,6) === (1,2,3,C,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b24): "; if (1,2,3,4,C,6) === (1,2,3,4,C,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "tuple object equality null test (b25): "; if (1,2,3,4,5,C) === (1,2,3,4,5,C) then stdout.WriteLine "YES" else  reportFailure "basic test Q"


let _ = printString "ref equality test (b25): "; if ref 1 = ref 1 then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "ref equality test (b25): "; if ref 1 <> ref 2 then stdout.WriteLine "YES" else  reportFailure "basic test Q"

let _ = printString "compaure nativeint test (b25): "; if compare [0n] [1n] = -(compare [1n] [0n]) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "compaure nativeint test (b25): "; if compare [0un] [1un] = -(compare [1un] [0un]) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "compaure nativeint test (b25): "; if compare [0un] [0un] = 0 then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "compaure nativeint test (b25): "; if compare [0n] [0n] = 0 then stdout.WriteLine "YES" else  reportFailure "basic test Q"


(*---------------------------------------------------------------------------
!* Equality tests over structured values for data likely to contain
 * values represented by "null" 
 *--------------------------------------------------------------------------- *)

type ('a,'b) a2 = A2 of 'a * 'b
type ('a,'b,'c) a3 = A3 of 'a * 'b * 'c
type ('a,'b,'c,'d) a4 = A4 of 'a * 'b * 'c * 'd
type ('a,'b,'c,'d,'e) a5 = A5 of 'a * 'b * 'c * 'd * 'e
type ('a,'b,'c,'d,'e,'f) a6 = A6 of 'a * 'b * 'c * 'd * 'e * 'f
let _ = printString "data equality null test (1): "; if A2 (1,2) = A2 (1,2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (2): "; if A2 ([],2) = A2 ([],2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (3): "; if A2 (1,[]) = A2 (1,[]) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (4): "; if A3(1,2,3) = A3(1,2,3) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (5): "; if A3([],2,3) = A3([],2,3) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (6): "; if A3(1,[],2) = A3(1,[],2) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (7): "; if A3(1,2,[]) = A3(1,2,[]) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (8): "; if A4(1,2,3,4) = A4(1,2,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (9): "; if A4([],2,3,4) = A4([],2,3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (10): "; if A4(1,[],3,4) = A4(1,[],3,4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (11): "; if A4(1,2,[],4) = A4(1,2,[],4) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (12): "; if A4(1,2,3,[]) = A4(1,2,3,[]) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (13): "; if A5(1,2,3,4,5) = A5(1,2,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (14): "; if A5([],2,3,4,5) = A5([],2,3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (15): "; if A5(1,[],3,4,5) = A5(1,[],3,4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (16): "; if A5(1,2,[],4,5) = A5(1,2,[],4,5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (17): "; if A5(1,2,3,[],5) = A5(1,2,3,[],5) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (18): "; if A5(1,2,3,4,[]) = A5(1,2,3,4,[]) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (19): "; if A6(1,2,3,4,5,6) = A6(1,2,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (20): "; if A6([],2,3,4,5,6) = A6([],2,3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (21): "; if A6(1,[],3,4,5,6) = A6(1,[],3,4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (22): "; if A6(1,2,[],4,5,6) = A6(1,2,[],4,5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (23): "; if A6(1,2,3,[],5,6) = A6(1,2,3,[],5,6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (24): "; if A6(1,2,3,4,[],6) = A6(1,2,3,4,[],6) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "data equality null test (25): "; if A6(1,2,3,4,5,[]) = A6(1,2,3,4,5,[]) then stdout.WriteLine "YES" else  reportFailure "basic test Q"

let _ = printString "map test (1): "; if List.map (fun x -> x+1) [] = [] then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "map test (2): "; if List.map (fun x -> x+1) [1] = [2] then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "map test (3): "; if List.map (fun x -> x+1) [2;1] = [3;2] then stdout.WriteLine "YES" else  reportFailure "basic test Q"

let _ = printString "append test (1): "; if [2] @ [] = [2] then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "append test (2): "; if [] @ [2] = [2] then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "append test (3): "; if [2] @ [1] = [2;1] then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "append test (4): "; if [3;2] @ [1] = [3;2;1] then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "append test (5): "; if [3;2] @ [1;0] = [3;2;1;0] then stdout.WriteLine "YES" else  reportFailure "basic test Q"

let _ = printString "concat test (1): "; if List.concat [[2]] = [2] then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "concat test (2): "; if List.concat [[]] = [] then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "concat test (3): "; if List.concat [[2];[1]] = [2;1] then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "combine test (1): "; if List.zip [] [] = [] then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "combine test (2): "; if List.zip [1] [2] = [(1,2)] then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "combine test (3): "; if List.zip [1.0;2.0] [2.0;3.0] = [(1.0,2.0);(2.0,3.0)] then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "split test (1): "; if List.unzip [(1.0,2.0);(2.0,3.0)] = ([1.0;2.0],[2.0;3.0]) then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "split test (2): "; if List.unzip [] = ([],[]) then stdout.WriteLine "YES" else  reportFailure "basic test Q"

let _ = printfn "reduce test"; if List.reduce (fun x y -> x/y) [5*4*3*2; 4;3;2;1] = 5 then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printfn "reduceBack test"; if List.reduceBack (fun y x -> x/y) [4;3;2;1; 5*4*3*2] = 5 then stdout.WriteLine "YES" else  reportFailure "basic test Q"


(*---------------------------------------------------------------------------
!* List library 
 *--------------------------------------------------------------------------- *)


let pri s l = printString s; printString ": "; List.iter printInt l; printNewLine ()

let _ = pri "none" [1;2;3;4;5;6]
let _ = pri "rev" (List.rev [6;5;4;3;2;1])
let _ = pri "@" ([1;2;3] @ [4;5;6])
let _ = pri "map" (List.map (fun x -> x + 1) ([1;2;3]))
let _ = pri "concat" (List.concat [[1;2]; [3;4]; [5;6]])

let prs s l = printString s; printString ": "; List.iter printString l; printNewLine ()
let _ = prs "none" ["1";"2";"3";"4";"5";"6"]
let _ = prs "rev" (List.rev ["6";"5";"4";"3";"2";"1"])
let _ = prs "map" (List.map (fun x -> x ^ ".0") (["1";"2";"3"]))
let _ = prs "@" (["1";"2";"3"] @ ["4";"5";"6"])
let _ = prs "concat" (List.concat [["1";"2"]; ["3";"4"]; ["5";"6"]])

let _ = test "List.empty" (List.empty |> List.length = 0)
let _ = test "List.empty" (List.empty = [])
let _ = test "List.head" (List.head [1..4] = 1)
let _ = test "List.head" (try List.head []; false with _ -> true)
let _ = test "List.tail" (List.tail [1..10] = [2..10])
let _ = test "List.tail" (try List.tail []; false with _ -> true)
let _ = test "List.init" (List.init 20 (fun x -> x+1) = [1..20])
let _ = test "List.fold2" (List.fold2 (fun i j k -> i+j+k) 100 [1;2;3] [1;2;3] = 112)
let _ = test "List.fold2" (List.fold2 (fun i j k -> i-j-k) 100 [1;2;3] [1;2;3] = 100-12)
let _ = test "List.foldBack2" (List.foldBack2 (fun i j k -> i+j+k) [1;2;3] [1;2;3] 100 = 112)
let _ = test "List.foldBack2" (List.foldBack2 (fun i j k -> k-i-j) [1;2;3] [1;2;3] 100 = 100-12)

let _ = test "List.scan" (List.scan (+) 0 [1..5] = [0; 1; 3; 6; 10; 15])

let _ = test "List.scanBack" (List.scanBack (+) [1..5] 0 = [15; 14; 12; 9; 5; 0])

let _ = test "List.tryFindIndex" (List.tryFindIndex (fun x -> x = 4) [0..10] = Some 4)

let _ = test "List.tryfind_index_b" (List.tryFindIndex (fun x -> x = 42) [0..10] = None)


let mutable c = -1
do List.iter (fun x -> c <- (c + 1); test "List.iter" (x = c)) [0..100]
let _ = test "List.iter" (c = 100)

let _ = test "List.map" ([1..100] |> List.map ((+) 1) = [2..101])

let _ = test "List.mapi" ([0..100] |> List.mapi (+) = [0..+2..200])

do c <- -1
do List.iteri (fun i x -> c <- (c+1); test "List.iteri" (x = c && i = c)) [0..100]
let _ = test "List.iteri" (c = 100)

let _ = test "List.exists" ([1..100] |> List.exists ((=) 50))

let _ = test "List.exists b" <| not ([1..100] |> List.exists ((=) 150))

let _ = test "List.forall" ([1..100] |> List.forall (fun x -> x < 150))

let _ = test "List.forall b" <| not ([1..100] |> List.forall (fun x -> x < 80))

let _ = test "List.find" ([1..100] |> List.find (fun x -> x > 50) = 51)

let _ = test "List.find b" (try [1..100] |> List.find (fun x -> x > 180) |> ignore; false with _ -> true)

let _ = test "List.tryPick" ([1..100] |> List.tryPick (fun x -> if x > 50 then Some (x*x) else None) = Some (51*51))

let _ = test "List.tryPick b" ([1..100] |> List.tryPick (fun x -> None) = None)
        
let _ = test "List.tryPick c" ([] |> List.tryPick (fun _ -> Some 42) = None)

let _ = test "List.tryFind" ([1..100] |> List.tryFind (fun x -> x > 50) = Some 51)

let _ = test "List.tryFind b" ([1..100] |> List.tryFind (fun x -> x > 180) = None)

do c <- -1
do List.iter2 (fun x y -> c <- c + 1; test "List.iter2" (c = x && c = y)) [0..100] [0..100]
let _ = test "List.iter2" (c = 100)

let _ = test "List.map2" (List.map2 (+) [0..100] [0..100] = [0..+2..200])

let _ = test "List.choose" (List.choose (fun x -> if x % 2 = 0 then Some (x/2) else None) [0..100] = [0..50])

let _ = test "List.filter" (List.filter (fun x -> x % 2 = 0) [0..100] = [0..+2..100])

let _ = test "List.filter b" (List.filter (fun x -> false) [0..100] = [])

let _ = test "List.filter c" (List.filter (fun x -> true) [0..100] = [0..100])

let p1, p2 = List.partition (fun x -> x % 2 = 0) [0..100]
let _ = test "List.partition" (p1 = [0..+2..100] && p2 = [1..+2..100])

let _ = test "List.rev" (List.rev [0..100] = [100..-1 ..0])

let _ = test "List.rev b" (List.rev [1] = [1])

let _ = test "List.rev c" (List.rev [] = [])

let _ = test "List.rev d" (List.rev [1; 2] = [2; 1])



module MinMaxAverageSum = 
        do test "ceijoe9cewz" (Seq.sum [] = 0)
        do test "ceijoe9cewx" (Seq.sum [1;2;3] = 6)
        do test "ceijoe9cewv" (Seq.sum [0.0;1.0] = 1.0)
        do test "ceijoe9cewc" (Seq.average [1.0;2.0;3.0] = 2.0)
        do test "ceijoe9cewb" (Seq.averageBy id [1.0;2.0;3.0] = 2.0)
        do test "ceijoe9cewn" (Seq.averageBy id [1.0M;2.0M;3.0M] = 2.0M)
        do test "ceijoe9cewm" (Seq.sum [System.Int32.MinValue;System.Int32.MaxValue] = -1)
        do test "ceijoe9cewaa" (Seq.sum [System.Int32.MaxValue;System.Int32.MinValue] = -1)
        do test "ceijoe9cewss" (Seq.sum [System.Int32.MinValue;1;-1] = System.Int32.MinValue)
        //printfn "res = %g" (Seq.averageBy id [])
        //printfn "res = %g" (Seq.average { 0.0 .. 100000.0 })

        do test "ceijoe9cew1dd" (Seq.min [1;2;3] = 1)
        do test "ceijoe9cew2ff" (Seq.min [3;2;1] = 1)

        do test "ceijoe9cew3gg" (Seq.max [1;2;3] = 3)
        do test "ceijoe9cew4hh" (Seq.max [3;2;1] = 3)


        do test "ceijoe9cew5jj" (Seq.min [1.0;2.0;3.0] = 1.0)
        do test "ceijoe9cew6kk" (Seq.min [3.0;2.0;1.0] = 1.0)

        do test "ceijoe9cew7" (Seq.max [1.0;2.0;3.0] = 3.0)
        do test "ceijoe9cew8" (Seq.max [3.0;2.0;1.0] = 3.0)

        do test "ceijoe9cew9" (Seq.min [1.0M;2.0M;3.0M] = 1.0M)
        do test "ceijoe9cewq" (Seq.min [3.0M;2.0M;1.0M] = 1.0M)

        do test "ceijoe9ceww" (Seq.max [1.0M;2.0M;3.0M] = 3.0M)
        do test "ceijoe9cewe" (Seq.max [3.0M;2.0M;1.0M] = 3.0M)

        do test "ceijoe9cewz" (List.sum [] = 0)
        do test "ceijoe9cewx" (List.sum [1;2;3] = 6)
        do test "ceijoe9cewv" (List.sum [0.0;1.0] = 1.0)
        do test "ceijoe9cewc" (List.average [1.0;2.0;3.0] = 2.0)
        do test "ceijoe9cewb" (List.averageBy id [1.0;2.0;3.0] = 2.0)
        do test "ceijoe9cewn" (List.averageBy id [1.0M;2.0M;3.0M] = 2.0M)
        do test "ceijoe9cewm" (List.sum [System.Int32.MinValue;System.Int32.MaxValue] = -1)
        do test "ceijoe9cewaa" (List.sum [System.Int32.MaxValue;System.Int32.MinValue] = -1)
        do test "ceijoe9cewss" (List.sum [System.Int32.MinValue;1;-1] = System.Int32.MinValue)
        //printfn "res = %g" (List.averageBy id [])
        //printfn "res = %g" (List.average { 0.0 .. 100000.0 })

        do test "ceijoe9cew1dd" (List.min [1;2;3] = 1)
        do test "ceijoe9cew2ff" (List.min [3;2;1] = 1)

        do test "ceijoe9cew3gg" (List.max [1;2;3] = 3)
        do test "ceijoe9cew4hh" (List.max [3;2;1] = 3)


        do test "ceijoe9cew5jj" (List.min [1.0;2.0;3.0] = 1.0)
        do test "ceijoe9cew6kk" (List.min [3.0;2.0;1.0] = 1.0)

        do test "ceijoe9cew7" (List.max [1.0;2.0;3.0] = 3.0)
        do test "ceijoe9cew8" (List.max [3.0;2.0;1.0] = 3.0)

        do test "ceijoe9cew9" (List.min [1.0M;2.0M;3.0M] = 1.0M)
        do test "ceijoe9cewq" (List.min [3.0M;2.0M;1.0M] = 1.0M)

        do test "ceijoe9ceww" (List.max [1.0M;2.0M;3.0M] = 3.0M)
        do test "ceijoe9cewe" (List.max [3.0M;2.0M;1.0M] = 3.0M)



module Pow = 
        do test "cnod90km1" (pown 2.0 -3 = 0.125)
        do test "cnod90km2" (pown 2.0 -2 = 0.25)
        do test "cnod90km3" (pown 2.0 -1 = 0.5)
        do test "cnod90km4" (pown 2.0 0 = 1.0)
        do test "cnod90km5" (pown 2.0 1 = 2.0)
        do test "cnod90km6" (pown 2.0 2 = 4.0)
        do test "cnod90km7" (pown 2.0 3 = 8.0)
        do test "cnod90km8" (pown 2.0 4 = 16.0)
        do test "cnod90km9" (pown 2.0 5 = 32.0)

        do for exp in -5 .. 5 do 
             test "cnod90kma" (pown 0.5 exp = 0.5 ** float exp);
             test "cnod90kmb" (pown 1.0 exp = 1.0 ** float exp);
             test "cnod90kmc" (pown 2.0 exp = 2.0 ** float exp);
#if MONO
#else
             test "cnod90kmd" (pown 3.0 exp = 3.0 ** float exp)
#endif
           done

        do for exp in [ 5 .. -1 .. -5 ] @ [System.Int32.MinValue;System.Int32.MaxValue] do 
               // check powers of 0
               printfn "exp = %d" exp;
               test "cnod90kme" (pown 0.0f exp = (if exp = 0 then 1.0f else if exp < 0 then infinityf else 0.0f));
               test "cnod90kmf" (pown 0.0 exp = (if exp = 0 then 1.0 else if exp < 0 then infinity else 0.0));
               if exp >= 0 then (
                   test "cnod90kmg" (pown 0 exp = (if exp = 0 then 1 else 0));
                   test "cnod90kmh" (pown 0u exp = (if exp = 0 then 1u else 0u));
                   test "cnod90kmi" (pown 0us exp = (if exp = 0 then 1us else 0us));
                   test "cnod90kmj" (pown 0s exp = (if exp = 0 then 1s else 0s));
                   test "cnod90kmk" (pown 0L exp = (if exp = 0 then 1L else 0L));
                   test "cnod90kml" (pown 0UL exp = (if exp = 0 then 1UL else 0UL));
                   test "cnod90kmm" (pown 0n exp = (if exp = 0 then 1n else 0n));
                   test "cnod90kmn" (pown 0un exp = (if exp = 0 then 1un else 0un));
                   test "cnod90kmo" (pown 0y exp = (if exp = 0 then 1y else 0y));
                   test "cnod90kmp" (pown 0uy exp = (if exp = 0 then 1uy else 0uy));
                   test "cnod90kmq" (pown 0M exp = (if exp = 0 then 1M else 0M));
               ) else (
                   test "cnod90kmgE" (try pown 0 exp; false with :? System.DivideByZeroException -> true);
                   test "cnod90kmhE" (try pown 0u exp; false with :? System.DivideByZeroException -> true);
                   test "cnod90kmiE" (try pown 0us exp; false with :? System.DivideByZeroException -> true);
                   test "cnod90kmjE" (try pown 0s exp; false with :? System.DivideByZeroException -> true);
                   test "cnod90kmE" (try pown 0L exp; false with :? System.DivideByZeroException -> true);
                   test "cnod90kmhE" (try pown 0UL exp; false with :? System.DivideByZeroException -> true);
                   test "cnod90kmtE" (try pown 0n exp; false with :? System.DivideByZeroException -> true);
                   test "cnod90kmhrE" (try pown 0un exp; false with :? System.DivideByZeroException -> true);
                   test "cnod90kmheE" (try pown 0y exp; false with :? System.DivideByZeroException -> true);
                   test "cnod90kmhfrE" (try pown 0uy exp; false with :? System.DivideByZeroException -> true);
                   test "cnod90kmhvreE" (try pown 0M exp; false with :? System.DivideByZeroException -> true);
               );
               
               // check powerrs of -1
               test "cnod90kmr" (pown -1.0f exp = (if exp % 2 = 0 then 1.0f else -1.0f));
               test "cnod90kms" (pown -1.0 exp = (if exp % 2 = 0 then 1.0 else -1.0));
               test "cnod90kmt" (pown -1.0M exp = (if exp % 2 = 0 then 1.0M else -1.0M));
               test "cnod90kmu" (pown -1 exp = (if exp % 2 = 0 then 1 else -1));
               test "cnod90kmv" (pown -1L exp = (if exp % 2 = 0 then 1L else -1L));
               test "cnod90kmw" (pown -1s exp = (if exp % 2 = 0 then 1s else -1s));
               test "cnod90kmx" (pown -1y exp = (if exp % 2 = 0 then 1y else -1y));
               test "cnod90kmy" (pown -1n exp = (if exp % 2 = 0 then 1n else -1n));
               test "cnod90kmz" (pown 1.0f exp = 1.0f);
               test "cnod90kmaa" (pown 1.0 exp = 1.0)
           done

        do for baseIdx in [-5 .. 5]  do 
               // check x^0
               test "cnod90kmbb2" (pown (float32 baseIdx) 0 = 1.0f);
               test "cnod90kmcc2" (pown (float baseIdx) 0 = 1.0);
               test "cnod90kmcc3" (pown (decimal baseIdx) 0 = 1M);
               test "cnod90kmcc4" (pown (nativeint baseIdx) 0 = 1n);
               test "cnod90kmcc5" (pown (unativeint baseIdx) 0 = 1un);
               test "cnod90kmcc6" (pown (int64 baseIdx) 0 = 1L);
               test "cnod90kmcc7" (pown (uint64 baseIdx) 0 = 1UL);
               test "cnod90kmcc8" (pown (int32 baseIdx) 0 = 1);
               test "cnod90kmcc9" (pown (uint32 baseIdx) 0 = 1u);
               test "cnod90kmcca" (pown (int16 baseIdx) 0 = 1s);
               test "cnod90kmccs" (pown (uint16 baseIdx) 0 = 1us);
               test "cnod90kmccd" (pown (byte baseIdx) 0 = 1uy);
               test "cnod90kmccf" (pown (sbyte baseIdx) 0 = 1y);

               // check x^1
               test "cnod90kmbb21" (pown (float32 baseIdx) 1 = (float32 baseIdx));
               test "cnod90kmbb22" (pown (decimal baseIdx) 1 = (decimal baseIdx));
               test "cnod90kmbb23" (pown (nativeint baseIdx) 1 = (nativeint baseIdx));
               test "cnod90kmbb24" (pown (float baseIdx) 1 = (float baseIdx));
               test "cnod90kmbb25" (pown (unativeint baseIdx) 1 = (unativeint baseIdx));
               test "cnod90kmbb26" (pown (int64 baseIdx) 1 = (int64 baseIdx));
               test "cnod90kmbb27" (pown (uint64 baseIdx) 1 = (uint64 baseIdx));
               test "cnod90kmbb28" (pown (uint16 baseIdx) 1 = (uint16 baseIdx));
               test "cnod90kmbb29" (pown (int16 baseIdx) 1 = (int16 baseIdx));
               test "cnod90kmbb2q" (pown (byte baseIdx) 1 = (byte baseIdx));
               test "cnod90kmbb2w" (pown (sbyte baseIdx) 1 = (sbyte baseIdx));
               
               // check x^2
               test "cnod90kmbb11" (pown (float32 baseIdx) 2 = (float32 baseIdx) * (float32 baseIdx));
               test "cnod90kmbb12" (pown (decimal baseIdx) 2 = (decimal baseIdx) * (decimal baseIdx));
               test "cnod90kmbb13" (pown (nativeint baseIdx) 2 = (nativeint baseIdx) * (nativeint baseIdx));
               test "cnod90kmbb14" (pown (float baseIdx) 2 = (float baseIdx) * (float baseIdx));
               test "cnod90kmbb16" (pown (int64 baseIdx) 2 = (int64 baseIdx) * (int64 baseIdx));
               test "cnod90kmbb19" (pown (int16 baseIdx) 2 = (int16 baseIdx) * (int16 baseIdx));
               test "cnod90kmbb1b" (pown (sbyte baseIdx) 2 = (sbyte baseIdx) * (sbyte baseIdx));
               if baseIdx >= 0 then  (
                   test "cnod90kmbb15" (pown (unativeint baseIdx) 2 = (unativeint baseIdx) * (unativeint baseIdx));
                   test "cnod90kmbb17" (pown (uint64 baseIdx) 2 = (uint64 baseIdx) * (uint64 baseIdx));
                   test "cnod90kmbb18" (pown (uint16 baseIdx) 2 = (uint16 baseIdx) * (uint16 baseIdx));
                   test "cnod90kmbb1a" (pown (byte baseIdx) 2 = (byte baseIdx) * (byte baseIdx));
               )
           done


module TakeUntilSkipWhile = 

    do test "oewvjrrovvr1" ([ ] |> Seq.takeWhile (fun x -> x <= 5) |> Seq.toList = [ ])
    do test "oewvjrrovvr2" ([ 1 ] |> Seq.takeWhile (fun x -> x <= 5) |> Seq.toList = [ 1 ])
    do test "oewvjrrovvr3" ([ 1;2;3;4;5 ] |> Seq.takeWhile (fun x -> x <= 5) |> Seq.toList = [ 1..5 ])
    do test "oewvjrrovvr4" ([ 1;2;3;4;5;6 ] |> Seq.takeWhile (fun x -> x <= 5) |> Seq.toList = [ 1..5 ])
    do test "oewvjrrovvr5" ([ 1;2;3;4;5;6;7 ] |> Seq.takeWhile (fun x -> x <= 5) |> Seq.toList = [ 1..5 ])
    do test "oewvjrrovvr6" ([ 1;2;3;4;5;6;5;4;3;2;1 ] |> Seq.takeWhile (fun x -> x <= 5) |> Seq.toList = [ 1..5 ])

    do test "oewvjrrovvr7" ([ 1;2;3;4;5 ] |> Seq.skipWhile (fun x -> x <= 5) |> Seq.toList = [  ])
    do test "oewvjrrovvr8" ([ 1;2;3;4;5;6 ] |> Seq.skipWhile (fun x -> x <= 5) |> Seq.toList = [ 6 ])
    do test "oewvjrrovvr9" ([ 1;2;3;4;5;6;7 ] |> Seq.skipWhile (fun x -> x <= 5) |> Seq.toList = [ 6;7 ])
    do test "oewvjrrovvra" ([ 1;2;3;4;5;6;5;4;3;2;1 ] |> Seq.skipWhile (fun x -> x <= 5) |> Seq.toList = [ 6;5;4;3;2;1 ])



(*---------------------------------------------------------------------------
!* Infinite data structure tests
 *--------------------------------------------------------------------------- *)

(*
type ilist = Cons of int * ilist

let test () = let rec list = Cons (1,list)  in list

let test2 () = let rec list2 = (1 :: list2)  in list2
let pri2 s l = printString s; printString ": "; List.iter printInt l; printNewLine ()
let _ = pri2 "infinite list" (test2())

let pri3 s l = printString s; printString ": "; List.iter printInt l; printNewLine ()
let test3 () = let rec list3 = (1 :: list4) and list4 = 2::list3  in list3
let _ = pri3 "infinite list" (test3())
*
type r4 = { cells: r4 list; tag: int }

let rec pri4a x  =  printInt x.tag; pri4b x.cells
and pri4b l = iter pri4a l

let test4 () = 
  let rec r1 = { cells = list3; tag = 1} 
  and  r2 = { cells = list4; tag = 2} 
  and list3 = r2 :: list4
  and list4 = r1::list3  in
  r1

let _ = pri4a(test4())
*)


(*---------------------------------------------------------------------------
!* Perf tests
 *--------------------------------------------------------------------------- *)


let listtest1 () = 
  let pri2 s l = printString s; printString ": "; List.iter printInt l; printNewLine () in 
  let mutable r = [] in 
  for i = 1 to 100 do
    r <- i :: r;
    for j = 1 to 100 do
      let _ = List.rev r  in ()
    done;
  done;
  pri2 "list: " r

let _ = listtest1()

(*
let pri s l = printString s; printString ": "; List.iter printInt l; printNewLine ()
  let irev (l : int list) = 
   let res = ref [] in 
   let curr = ref l in 
   while (match curr.contents with [] -> false | _ -> true) do
     match curr.contents with 
       (h::t) -> res.contents <- h :: !res; curr.contents <- t;
   done;
   !res
let r = ref [] 
let _ = 
  for i = 1 to 100 do
    r := i :: r.contents;
    for j = 1 to 100 do
      let _ = irev r.contents in ()
    done;
  done
let _ = pri "list: " r.contents
*)

   
(*
let pri s l = printString s; printString ": "; List.iter printInt l; printNewLine ()
type iiref= { mutable icontents: int list}
let iiref x = { icontents = x }
let (!!!!) r = r.icontents
let (<--) r  x = r.icontents <- x

let irev l = 
   let res = iiref [] in 
   let curr = iiref l in 
   while (match !!!!curr with [] -> false | _ -> true) do
     match !!!!curr with 
       (h::t) -> res <-- h :: !!!!res; curr <-- t;
   done;
   !!!!res

let r = iiref []
let test() = 
  for i = 1 to 600 do
    r <-- i :: !!!!r;
    for j = 1 to 600 do
      let _ = irev !!!!r in ()
    done;
  done
let _ = test()
let _ = pri "list: "  !!!!r
*)


(*
type ilist = Nil | Cons of int * ilist
let rec iiter f = function Nil -> () | Cons (h,t) -> (f h; iiter f t)
let pri s l = printString s; printString ": "; iiter printInt l; printNewLine ()
type iref= { mutable icontents: ilist}
let iref x = { icontents = x }
let (!!!!) r = r.icontents
let (<--) r  x = r.icontents <- x

let irev l = 
   let res = iref Nil in 
   let curr = iref l in 
   while (match !!!!curr with Nil -> false | _ -> true) do
     match !!!!curr with 
       Cons(h,t) -> res <-- Cons (h, !!!!res); curr <-- t;
   done;
   !!!!res

let r = iref Nil
let test() = 
  for i = 1 to 600 do
    r <-- Cons (i,!!!!r);
    for j = 1 to 600 do
      let _ = irev !!!!r in ()
    done;
  done
let _ = test()
let _ = pri "list: "  !!!!r
*)

(*
type flist = Nil | Cons of float * flist
let rec fiter f = function Nil -> () | Cons (h,t) -> (f h; fiter f t)
let pri s l = printString s; printString ": "; fiter print_float l; printNewLine ()
type fref= { mutable fcontents: flist}
let fref x = { fcontents = x }
let (!!!!) r = r.fcontents
let (<--) r  x = r.fcontents <- x

let frev l = 
   let res = fref Nil in 
   let curr = fref l in 
   while (match !!!!curr with Nil -> false | _ -> true) do
     match !!!!curr with 
       Cons(h,t) -> res <-- Cons (h, !!!!res); curr <-- t;
   done;
   !!!!res

let r = fref Nil
let test() = 
  for i = 1 to 600 do
    r <-- Cons (float i,!!!!r);
    for j = 1 to 600 do
      let _ = frev !!!!r in ()
    done;
  done
let _ = test()
let _ = pri "list: "  !!!!r
*)


(* let rec not_inlined b = if b then not_inlined false else b *)
let not_inlined x = x
let inlined (x1:int) (x2:int) (x3:int) (x4:int) (x5:int) = 
  let not_eliminated = not_inlined 1 in 
  let not_eliminated2 = not_inlined 2 in 
  not_eliminated
let test2() = 
  let eliminated_to_value = inlined 1 1 1 1 1 in 
  let not_eliminated = not_inlined 2 in 
  eliminated_to_value

let _ = test2()

let ldexp22 (x:float) (n:int) = x * (2.0 ** float n)

(*
let rec fold_right2 : ('a -> 'b -> 'c -> 'c) -> 'a list -> 'b list -> 'c -> 'c
let rec for_all : ('a -> bool) -> 'a list -> bool
let rec exists : ('a -> bool) -> 'a list -> bool
let rec for_all2 : ('a -> 'b -> bool) -> 'a list -> 'b list -> bool
let rec exists2 : ('a -> 'b -> bool) -> 'a list -> 'b list -> bool
let rec mem : 'a -> 'a list -> bool
let rec memq : 'a -> 'a list -> bool
let rec find : ('a -> bool) -> 'a list -> 'a
let rec filter : ('a -> bool) -> 'a list -> 'a list
let rec find_all : ('a -> bool) -> 'a list -> 'a list
let rec partition : ('a -> bool) -> 'a list -> 'a list * 'a list
let rec assoc : 'a -> ('a * 'b) list -> 'b
let rec assq : 'a -> ('a * 'b) list -> 'b
let rec mem_assoc : 'a -> ('a * 'b) list -> bool
let rec mem_assq : 'a -> ('a * 'b) list -> bool
let rec remove_assoc : 'a -> ('a * 'b) list -> ('a * 'b) list
let rec remove_assq : 'a -> ('a * 'b) list -> ('a * 'b) list
let rec split : ('a * 'b) list -> 'a list * 'b list
let rec combine : 'a list -> 'b list -> ('a * 'b) list
let rec sort : ('a -> 'a -> int) -> 'a list -> 'a list
let rec stable_sort : ('a -> 'a -> int) -> 'a list -> 'a list
*)

let g x = match x with 2.0  -> 3.0

let _ = g 2.0
let _ = g (1.0 + 1.0)


type u16 = U16 of int 
type i32 = I32 of int32 
type bytes = Bytes of string

type assembly_name = string (* uses exact comparisons.  TODO: ECMA Partition 2 is inconsistent about this. *)
type module_name = string (* uses exact comparisons. TODO: ECMA Partition 2 is inconsistent about this. *)
type locale = (* should use case-insensitive comparison *)
  | Locale_bytes of bytes (* unicode *)
  | Locale_string of string

type assembly_ref = 
    { assemRefName: assembly_name;
      assemRefHash: bytes option;
      (* Note: only one of the following two are ever present. *)
      assemRefPublicKeyToken: bytes option; 
      assemRefPublicKey: bytes option;
      assemRefVersion: (i32 * i32 * i32 * i32) option;
      assemRefLocale: locale option } 

type modul_ref = 
    { modulRefName: module_name;
      modulRefNoMetadata: bool; (* only for file references *)
      modulRefHash: bytes option; (* only for file references *)
    }

type scope_ref = 
  | ScopeRef of assembly_ref * modul_ref option 

(* TODO: check the array types that are relevant for binding etc. *)
type array_bounds =  ((i32 * i32 option) list) option
type type_ref = 
  | TypeRef of (scope_ref * string list * string)
  | TypeRef_array of array_bounds * bool

type typ = 
  | Type_void                   (* -- Used only in return and pointer types. *)
  | Type_value of type_spec      (* -- Unboxed types, including built-in types. *)
  | Type_boxed of type_spec      (* -- Nb. used for both boxed value classes *)
                                (*    and classes. *)
  | Type_ptr of typ             (* -- Unmanaged pointers.  Nb. the type is *)
                                (*    effectively for tools and for binding *)
                                (*    only, not by the verifier. *)
  | Type_byref of typ           (* -- Managed pointers. *)
  | Type_typedref
  | Type_fptr of callsig (* -- Code pointers. *)
  | Type_modified of            (* -- Custom modifiers. *)
        bool *                  (*   -- True if modifier is "required" *)
        typ *                   (*   -- the class of the custom modifier *)
        typ                     (*   -- the type being modified *)

(* MS-ILX *) | Type_unit                      (* -- empty value *)
(* MS-ILX *) | Type_forall of genparam * typ (* -- indexed outside-in *) 
(* MS-ILX *) | Type_tyvar of u16              (* -- reference a generic arg *)
(* MS-ILX *) | Type_tyrepvar of u16   
(* MS-ILX *) | Type_func of typ list * typ  

and type_spec = TypeSpec of type_ref (* MS-ILX *) * genactuals
and callsig =  Callsig of callconv * typ list * typ  

(* MS-ILX *) (* ----------------------------------------------------------
(* MS-ILX *)  * Generic parameters, i.e. parameters reified statically. 
(* MS-ILX *)  * Currently only two kinds of parameters are supported in  
(* MS-ILX *)  * the term structure: types and type representations. 
(* MS-ILX *)  * Type representations are only used internally.
(* MS-ILX *)  * --------------------------------------------------------- *)
(* MS-ILX *) 
(* MS-ILX *) and genparams = genparam list
(* MS-ILX *) and genactuals = genactual list
(* MS-ILX *) and genactual = 
(* MS-ILX *)   | GenActual_type of typ
(* MS-ILX *)   | GenActual_tyrep of typ
(* MS-ILX *) and genparam = 
(* MS-ILX *)   | GenFormal_type
(* MS-ILX *)   | GenFormal_tyrep of exn  
(* MS-ILX *)          (* For compiler use only. *)                       
(* MS-ILX *)          (* We use exn as an annotation here. *)            
(* MS-ILX *)          (* Types are still used as actuals for type-reps *)


(* --------------------------------------------------------------------
!* Calling conventions.  These are used in method pointer types.
 * -------------------------------------------------------------------- *)

and bcallconv = 
  | CC_cdecl 
  | CC_stdcall 
  | CC_thiscall 
  | CC_fastcall 
  | CC_default
  | CC_vararg
      
and hasthis = 
  | CC_instance
  | CC_instance_explicit
  | CC_static

and callconv = Callconv of hasthis * bcallconv

let mk_empty_gactuals = ([]: genactuals)
let mk_mono_tspec tref =  TypeSpec (tref, mk_empty_gactuals)
let mscorlib_assembly_name =  "mscorlib"
let mscorlib_module_name =  "CommonLanguageRuntimeLibrary"
let mk_simple_assref n = 
  { assemRefName=n;
    assemRefHash=None;
    assemRefPublicKeyToken=None;
    assemRefPublicKey=None;
    assemRefVersion=None;
    assemRefLocale=None; } 
let mscorlib_aref = mk_simple_assref mscorlib_assembly_name
let mscorlib_scoref = ScopeRef(mscorlib_aref,None)
let mk_nested_tref (scope,l,nm) =  TypeRef (scope,l,nm)
let mk_tref (scope,nm) =  mk_nested_tref (scope,[],nm)

let tname_Object1 = "System.Object"
let tref_Object1 = mk_tref (mscorlib_scoref,tname_Object1)
let tspec_Object1 = mk_mono_tspec tref_Object1
let typ_Object1 = Type_boxed tspec_Object1

let tname_Object2 = "System.Object"
let tref_Object2 = mk_tref (mscorlib_scoref,tname_Object2)
let tspec_Object2 = mk_mono_tspec tref_Object2
let typ_Object2 = Type_boxed tspec_Object2



let _ = printString "advanced equality test (1): "; if tname_Object1 = tname_Object2 then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "advanced equality test (9): "; if (mscorlib_scoref,[],tname_Object1) =(mscorlib_scoref,[],tname_Object2)  then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "advanced equality test (10): "; if tref_Object1 = tref_Object2 then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "advanced equality test (11): "; if typ_Object1 = typ_Object2 then stdout.WriteLine "YES" else  reportFailure "basic test Q"


let _ = printString "array equality test (1): "; if [| 1 |] = [| 1 |] then stdout.WriteLine "YES" else  reportFailure "basic test Q"

let _ = printString "arr equality test (4): "; if [| |]  = [| |] then stdout.WriteLine "YES" else  reportFailure "basic test Q"
let _ = printString "arr hash-respects-equality test (4): "; if hash [| |] = hash [| |] then stdout.WriteLine "YES" else  reportFailure "basic test Q"

let _ = printString "array equality test (1): "; if [| 1 |] = [| 1 |] then stdout.WriteLine "YES" else  reportFailure "basic test Q"



(*
let  f x = 
   let g a b c d = (a=1) && (b = 2) && (c = 3) && (d = 4) && (x = 5) in 
   let bigcheck h (a:int) (b:int) (c:int) (d:int) = 
      h a b c d && 
      h a b c d && 
      (let f1 = h a in 
       let f2 = f1 b in 
       let f3 = f2 c in 
        f3 d) && 
      (let f1 = h a b in 
       let f2 = f1 c in 
        f2 d) && 
      (let f1 = h a b c in 
        f1 d) && 
      (let f1 = h a in 
       let f2 = f1 b c in 
        f2 d) && 
      (let f1 = h a in 
       let f2 = f1 b in 
        f2 c d) && 
      (let f1 = h a b in 
        f1 c d) && 
      (let f1 = h a in 
        f1 b c d) in  
   bigcheck g 1 2 3 4

let _ = if (f 5) then  stdout.WriteLine "YES" else  reportFailure "basic test Q"


let sort_test cmp ans = 
  for i0 = 0 to 5 do 
    for i1 = 0 to 5 do 
      for i2 = 0 to 5 do
        for i3 = 0 to 5 do
          for i4 = 0 to 5 do
            for i5 = 0 to 5 do
              if i0 <> i1 && i0 <> i2 && i0 <> i3 && i0 <> i4 && i0 <> i5 &
               i1 <> i2 && i1 <> i3 && i1 <> i4 && i1 <> i5 &
               i2 <> i3 && i2 <> i4 && i2 <> i5 &
               i3 <> i4 && i3 <> i5 &
               i4 <> i5 then 
              let a = Array.create 6 0 in
              a.(i0) <- 0;
              a.(i1) <- 1;
              a.(i2) <- 2;
              a.(i3) <- 3;
              a.(i4) <- 4;
              a.(i5) <- 5;
              (* list sort *)
              let l = Array.toList a in 
              let res = List.sortWith cmp l in
              if (res<> ans) then begin
                let printInt n = printString (string_of_int n) in
                printString "List.sort ";
                printInt a.(0);
                printInt a.[1];
                printInt a.[2];
                printInt a.(3);
                printInt a.(4);
                printInt a.(5);
                printString " = ";
                let resa = Array.ofList res in 
                printInt resa.(i0);
                printInt resa.(i1);
                printInt resa.(i2);
                printInt resa.(i3);
                printInt resa.(i4);
                printInt resa.(i5);
                reportFailure "unlabelled test"
              end;
              (* array sort *)
              let resa = Array.copy a in
              Array.sortInPlaceWith cmp resa; (* mutates resa array *)
              let res = Array.toList resa in
              if (res<> ans) then begin
                let printInt n = printString (string_of_int n) in
                printString "Array.sort ";
                printInt a.(0);
                printInt a.[1];
                printInt a.[2];
                printInt a.(3);
                printInt a.(4);
                printInt a.(5);
                printString " = ";
                (* recall Array.list_of resa = res *)
                printInt resa.(i0);
                printInt resa.(i1);
                printInt resa.(i2);
                printInt resa.(i3);
                printInt resa.(i4);
                printInt resa.(i5);
                reportFailure "unlabelled test"
              end
          done;
        done;
      done;
    done;
    done;
  done
  
let _ = sort_test compare [0;1;2;3;4;5]
let _ = sort_test (fun x y -> -(compare x y)) [5;4;3;2;1;0]
*)
module StrangeOperatorTest = 
    let (&&&) x y = x^y
    let (<<<) (x:string) (y:string) = x ^y^x

    let e1 = ("0" &&& ("1" <<< "2"))
    let e2= (("0" &&& "1") <<< "2") 
    let e3= ("0" &&& "1" <<< "2") 

    let _ = if (e1 <> e2) then stderr.WriteLine "Control Passed" else stderr.WriteLine "Control Failed"
    let _ = if (e1 = e3) then (stderr.WriteLine "Parsed to Right!  Wrong!" ; reportFailure "parsing")
    let _ = if (e2 = e3) then stderr.WriteLine "Parsed to Left - correct!" 



//let _ = if (3 then do ignore(4)) = 3 then stderr.WriteLine "OK!" else (stderr.WriteLine "Wrong!" ; reportFailure "unlabelled test")
//let _ = let x = ref 1 in if (!x then do x := !x + 1) = 1 then stderr.WriteLine "OK!" else (stderr.WriteLine "Wrong!" ; reportFailure "unlabelled test")


(* Check codegen for using functions of type (unit -> _) as first class values. *)
let _ = List.map printNewLine [(); (); ()]

(* Check codegen for tail recursive functions with argument and return types involving "unit" *)
let rec  unitElimTailRecursion1() = stdout.WriteLine "loop"; (unitElimTailRecursion1() : string)
let rec  unitElimTailRecursion2((),()) = stdout.WriteLine "loop"; (unitElimTailRecursion2((),()) : string)
let rec  unitElimTailRecursion3() = stdout.WriteLine "loop"; (unitElimTailRecursion3() : unit)
let rec  unitElimTailRecursion4((),()) = stdout.WriteLine "loop"; (unitElimTailRecursion4((),()) : unit)
let rec  unitElimTailRecursion5() = stdout.WriteLine "loop"; (unitElimTailRecursion5() : 'a)
let rec  unitElimTailRecursion6((),()) = stdout.WriteLine "loop"; (unitElimTailRecursion6((),()) : 'a)

(* Check codegen for inner tail recursive functions with argument and return types involving "unit". *)
let innerUnitElimTailRecursion1 () = 
  let rec  unitElimTailRecursion1() = stdout.WriteLine "loop"; (unitElimTailRecursion1() : string) in 
  let rec  unitElimTailRecursion2((),()) = stdout.WriteLine "loop"; (unitElimTailRecursion2((),()) : string) in
  let rec  unitElimTailRecursion3() = stdout.WriteLine "loop"; (unitElimTailRecursion3() : unit) in
  let rec  unitElimTailRecursion4((),()) = stdout.WriteLine "loop"; (unitElimTailRecursion4((),()) : unit) in 
  let rec  unitElimTailRecursion5() = stdout.WriteLine "loop"; (unitElimTailRecursion5() : 'a) in 
  let rec  unitElimTailRecursion6((),()) = stdout.WriteLine "loop"; (unitElimTailRecursion6((),()) : 'a) in 
  (unitElimTailRecursion1, unitElimTailRecursion2, unitElimTailRecursion3, unitElimTailRecursion4, unitElimTailRecursion5, unitElimTailRecursion6)

(* Check codegen for tail recursive functions with argument types involving " int * int" *)
let rec  tupleElimTailRecursion1((x:int), (y:int)) = stdout.WriteLine "loop"; (tupleElimTailRecursion1(x,y) : string)
let rec  tupleElimTailRecursion2((x1:int), (y1:int)) ((x2:int), (y2:int)) = stdout.WriteLine "loop"; (tupleElimTailRecursion2(x2,y2) (x1,y1) : string)

let innerTupleElimTailRecursion1 () = 
  let rec  tupleElimTailRecursion1((x:int), (y:int)) = stdout.WriteLine "loop"; (tupleElimTailRecursion1(x,y) : string) in 
  let rec  tupleElimTailRecursion2((x1:int), (y1:int)) ((x2:int), (y2:int)) = stdout.WriteLine "loop"; (tupleElimTailRecursion2(x2,y2) (x1,y1) : string) in 
  tupleElimTailRecursion1, tupleElimTailRecursion2

let test3d9cw90 () = 
  let set = (Set.add 1 (Set.add 0 (Set.add 1 (Set.add 5 (Set.add 4 (Set.add 3 Set.empty)))))) in 
  let i = (set :> seq<_>).GetEnumerator() in 
  check "set iterator" true  (i.MoveNext());
  check "set iterator" 0  i.Current;
  check "set iterator" true  (i.MoveNext());
  check "set iterator" 1  i.Current;
  check "set iterator" true  (i.MoveNext());
  check "set iterator" 3  i.Current;
  check "set iterator" true  (i.MoveNext());
  check "set iterator" 4  i.Current;
  check "set iterator" true  (i.MoveNext());
  check "set iterator" 5  i.Current;
  check "set iterator" false  (i.MoveNext())

do test3d9cw90 ()

do check "set comparison" 0 ((Seq.compareWith Operators.compare) Set.empty Set.empty)
do check "set comparison" 0   ((Seq.compareWith Operators.compare) (Set.add 1 Set.empty) (Set.add 1 Set.empty))
do check "set comparison" 0   ((Seq.compareWith Operators.compare) (Set.add 1 (Set.add 2 Set.empty)) (Set.add 2 (Set.add 1 Set.empty)))
do check "set comparison" 0   ((Seq.compareWith Operators.compare) (Set.add 1 (Set.add 2 (Set.add 3 Set.empty))) (Set.add 3 (Set.add 2 (Set.add 1 Set.empty))))
   
   
do check "set comparison" (-1) ((Seq.compareWith Operators.compare) Set.empty (Set.add 1 Set.empty))
do check "set comparison" (-1)  ((Seq.compareWith Operators.compare) (Set.add 1 Set.empty) (Set.add 2 Set.empty))
do check "set comparison" (-1)  ((Seq.compareWith Operators.compare) (Set.add 1 (Set.add 2 Set.empty)) (Set.add 3 (Set.add 1 Set.empty)))
do check "set comparison" (-1)  ((Seq.compareWith Operators.compare) (Set.add 1 (Set.add 2 (Set.add 3 Set.empty))) (Set.add 4 (Set.add 2 (Set.add 1 Set.empty))))

let checkReflexive f x y = (f x y = - f y x)

do check "set comparison" true (checkReflexive (Seq.compareWith Operators.compare) Set.empty (Set.add 1 Set.empty))
do check "set comparison" true (checkReflexive (Seq.compareWith Operators.compare) (Set.add 1 Set.empty) (Set.add 2 Set.empty))
do check "set comparison" true (checkReflexive (Seq.compareWith Operators.compare) (Set.add 1 (Set.add 2 Set.empty)) (Set.add 3 (Set.add 1 Set.empty)))
do check "set comparison" true (checkReflexive (Seq.compareWith Operators.compare) (Set.add 1 (Set.add 2 (Set.add 3 Set.empty))) (Set.add 4 (Set.add 2 (Set.add 1 Set.empty))))




(*================================================================================*)
    
(* Set ordering - tests *)
                 
let rec nlist i n =
  if n=0 then [] else
  if n % 2 = 1 then i :: nlist (i+1) (n / 2)
               else      nlist (i+1) (n / 2)

let orderTest n m =
    //printf "Check sorted-list order against ordered-set order: n=%-10d m=%-10d\n" n m;
    let nL = nlist 0 n in
    let nS = Set.ofList nL in
    let mL = nlist 0 m in
    let mS = Set.ofList mL in
    test "vwnwer" (compare nL mL = Seq.compareWith Operators.compare nS mS)

let nMax = 4096 * 4096
let ran = new System.Random()  
let testOrder() = orderTest (ran.Next(nMax)) (ran.Next(nMax))
do  for i = 1 to 1000 do testOrder() done
  
(*================================================================================*)  




(*
let test2398985() = 
  let l = ReadonlyArray.ofList [1;2;3] in
  let res = ref 2 in 
  for i in ReadonlyArray.toSeq l do res := !res + i done;
  check "test2398985: ReadonlyArray.toSeq" 8 !res

do test2398985()
*)

let test2398986() = 
  let l = Array.ofList [1;2;3] in
  let mutable res = 2 in 
  for i in Array.toSeq l do res <- res + i done;
  check "test2398986: Array.toSeq" 8 res

do test2398986()

let test2398987() = 
  let l = Set.ofList [1;2;3] in
  let res = ref 2 in 
  for i in Set.toSeq l do res := !res + i done;
  check "test2398987: Idioms.foreach, Set.toSeq" 8 !res

do test2398987()

let test2398987b() = 
  let l = Set.ofList [1;2;3] in
  let res = ref 2 in 
  for i in l do res := !res + i done;
  check "test2398987: Idioms.foreach, Set.toSeq" 8 !res

do test2398987b()


(*---------------------------------------------------------------------------
!* foreachG/to_seq
 *--------------------------------------------------------------------------- *)



let foreach e f = Seq.iter f e
let test2398993() = 
  let l = [1;2;3] in
  let res = ref 2 in 
  foreach (List.toSeq l) (fun i -> res := !res + i);
  check "test2398993: foreach, List.toSeq" 8 !res

do test2398993()

(*
let test2398995() = 
  let l = ReadonlyArray.ofList [1;2;3] in
  let res = ref 2 in 
  foreach (ReadonlyArray.toSeq l) (fun i -> res := !res + i);
  check "test2398995: foreach, ReadonlyArray.toSeq" 8 !res

do test2398995()
*)

let test2398996() = 
  let l = Array.ofList [1;2;3] in
  let res = ref 2 in 
  foreach (Array.toSeq l) (fun i -> res := !res + i);
  check "test2398996: foreach, Array.toSeq" 8 !res

do test2398996()

let test2398997() = 
  let l = Set.ofList [1;2;3] in
  let res = ref 2 in 
  foreach (Set.toSeq l) (fun i -> res := !res + i);
  check "test2398997: foreach, Set.toSeq" 8 !res

do test2398997()


(*---------------------------------------------------------------------------
!* Generic formatting
 *--------------------------------------------------------------------------- *)


do check "generic format 1"  "[1; 2]" (sprintf "%A" [1;2])
do check "generic format 2"  "Some [1; 2]" (sprintf "%A" (Some [1;2]))
do check "generic format a"  "1y" (sprintf "%A" 1y)
do check "generic format b"  "1uy" (sprintf "%A" 1uy)
do check "generic format c"  "1s" (sprintf "%A" 1s)
do check "generic format d"  "1us" (sprintf "%A" 1us)
do check "generic format e"  "1" (sprintf "%A" 1)
do check "generic format f"  "1u" (sprintf "%A" 1ul)
do check "generic format g"  "1L" (sprintf "%A" 1L)
do check "generic format j"  "1.0" (sprintf "%A" 1.0)
do check "generic format k"  "1.01" (sprintf "%A" 1.01)
do check "generic format l"  "1000.0" (sprintf "%A" 1000.0)

do check "generic format m"  "-1y" (sprintf "%A" (-1y))
do check "generic format n"  "-1s" (sprintf "%A" (-1s))
do check "generic format o"  "-1" (sprintf "%A" (-1))
do check "generic format p"  "-1L" (sprintf "%A" (-1L))
#if !NETCOREAPP
// See FSHARP1.0:4797
// On NetFx4.0 and above we do not emit the 'I' suffix
let bigintsuffix = if (System.Environment.Version.Major, System.Environment.Version.Minor) > (2,0) then "" else "I"
do check "generic format i"  ("1" + bigintsuffix) ( printf "%A" 1I
                                                    sprintf "%A" 1I)
do check "generic format r"  ("-1" + bigintsuffix)  (sprintf "%A" (-1I))
#endif


(*---------------------------------------------------------------------------
!* For loop variables can escape
 *--------------------------------------------------------------------------- *)

do for i = 1 to 10 do List.iter (fun x -> Printf.printf "x = %d\n" x) (List.map (fun x -> x + i) [1;2;3]) done


(*---------------------------------------------------------------------------
!* Type tests
 *--------------------------------------------------------------------------- *)

do check "type test string" "right" (match box("right") with | :? System.String as s -> s | _ -> "wrong")
do check "type test string (2)" "right" (match box("right") with| :? System.Int32 -> "wrong"  | :? System.String as s -> s | _ -> "wrong")
do check "type test int32" "right" (match box(1) with | :? System.String -> "wrong" | :? System.Int32 -> "right" | _ -> "wrong")
do check "type test int32 (2)" "right" (match box(1) with | :? System.Int32 -> "right" | :? System.String -> "wrong"  | _ -> "wrong")
do check "type test int32 (3)" 4 (match box(4) with | :? System.Int32 as d -> d | :? System.String -> 3  | _ -> 2)
do check "type test double" 1.0 (match box(1.0) with | :? System.Int32 -> 3.14 | :? System.Double as d -> d  | _ -> 2.71)



(*---------------------------------------------------------------------------
!* type syntax
 *--------------------------------------------------------------------------- *)

module TypeSyntax = 
    let x1  = [Map.add 1 (Map.add 1 1 Map.empty) Map.empty]
    let x2 : Map<'a,'b> list  = [Map.empty]
    let x3 : Map<'a,'b> list  = []


module IEnumerableTests = begin

  // This one gave a stack overflow when we weren't tail-calling on 64-bit
  do check "Seq.filter-length" ({ 1 .. 1000000 } |> Seq.filter (fun n -> n <> 1) |> Seq.length) 999999
  do check "Seq.filter-length" ({ 1 .. 1000000 } |> Seq.filter (fun n -> n = 1) |> Seq.length) 1
  do check "Seq.filter-length" ({ 1 .. 1000000 } |> Seq.filter (fun n -> n % 2 = 0) |> Seq.length) 500000

  do check "IEnumerableTest.empty-length" (Seq.length Seq.empty) 0
  do check "IEnumerableTest.length-of-array" (Seq.length [| 1;2;3 |]) 3
  do check "IEnumerableTest.head-of-array" (Seq.head [| 1;2;3 |]) 1
  do check "IEnumerableTest.take-0-of-array" (Seq.take 0 [| 1;2;3 |] |> Seq.toList) []
  do check "IEnumerableTest.take-1-of-array" (Seq.take 1 [| 1;2;3 |] |> Seq.toList) [1]
  do check "IEnumerableTest.take-3-of-array" (Seq.take 3 [| 1;2;3 |] |> Seq.toList) [1;2;3]
  do check "IEnumerableTest.nonempty-true" (Seq.isEmpty [| 1;2;3 |]) false
  do check "IEnumerableTest.nonempty-false" (Seq.isEmpty [| |]) true
  do check "IEnumerableTest.fold" (Seq.fold (+) 0 [| 1;2;3 |]   ) 6
  do check "IEnumerableTest.unfold" (Seq.unfold (fun _ -> None) 1 |> Seq.toArray) [| |]
  do check "IEnumerableTest.unfold" (Seq.unfold (fun x -> if x = 1 then Some("a",2) else  None) 1 |> Seq.toArray) [| "a" |]
  do check "IEnumerableTest.exists" (Seq.exists ((=) "a") [| |]) false
  do check "IEnumerableTest.exists" (Seq.exists ((=) "a") [| "a" |]) true
  do check "IEnumerableTest.exists" (Seq.exists ((=) "a") [| "1"; "a" |]) true
  do check "IEnumerableTest.exists" (Seq.forall ((=) "a") [| |]) true
  do check "IEnumerableTest.exists" (Seq.forall ((=) "a") [| "a" |]) true
  do check "IEnumerableTest.exists" (Seq.forall ((=) "a") [| "1"; "a" |]) false
  do check "IEnumerableTest.map on finite" ([| "a" |] |> Seq.map (fun x -> x.Length) |> Seq.toArray) [| 1 |]
  do check "IEnumerableTest.filter on finite" ([| "a";"ab";"a" |] |> Seq.filter (fun x -> x.Length = 1) |> Seq.toArray) [| "a";"a" |]
  do check "IEnumerableTest.choose on finite" ([| "a";"ab";"a" |] |> Seq.choose (fun x -> if x.Length = 1 then Some(x^"a") else None) |> Seq.toArray) [| "aa";"aa" |]
  do check "Seq.tryPick on finite (succeeding)" ([| "a";"ab";"a" |] |> Seq.tryPick (fun x -> if x.Length = 1 then Some(x^"a") else None)) (Some "aa")
  do check "Seq.tryPick on finite (failing)" ([| "a";"ab";"a" |] |> Seq.tryPick (fun x -> if x.Length = 6 then Some(x^"a") else None)) None
  do check "IEnumerableTest.find on finite (succeeding)" ([| "a";"ab";"a" |] |> Seq.find (fun x -> x.Length = 1)) "a"
  do check "IEnumerableTest.find on finite (failing)" (try Some ([| "a";"ab";"a" |] |> Seq.find (fun x -> x.Length = 6)) with :? System.Collections.Generic.KeyNotFoundException -> None) None
  do check "IEnumerableTest.map_with_type (string up to obj,finite)" ([| "a" |] |> Seq.cast |> Seq.toArray) [| ("a" :> obj) |]
  do check "IEnumerableTest.map_with_type (obj down to string, finite)" ([| ("a" :> obj) |] |> Seq.cast |> Seq.toArray) [| "a" |]
  do check "IEnumerableTest.append, finite, finite" (Seq.append [| "a" |] [| "b" |]  |> Seq.toArray) [| "a"; "b" |]
  do check "IEnumerableTest.concat, finite" (Seq.concat [| [| "a" |]; [| |]; [| "b";"c" |] |]  |> Seq.toList) [ "a";"b";"c" ]
  do check "IEnumerableTest.init_infinite, then take" (Seq.take 2 (Seq.initInfinite (fun i -> i+1)) |> Seq.toList) [ 1;2 ]
  do check "IEnumerableTest.to_array, empty" (Seq.init 0 (fun i -> i+1) |> Seq.toArray) [|  |]
  do check "IEnumerableTest.to_array, small" (Seq.init 1 (fun i -> i+1) |> Seq.toArray) [| 1 |]
  do check "IEnumerableTest.to_array, large" (Seq.init 100000 (fun i -> i+1) |> Seq.toArray |> Array.length) 100000
  do check "IEnumerableTest.to_array, very large" (Seq.init 1000000 (fun i -> i+1) |> Seq.toArray |> Array.length) 1000000
  do check "IEnumerableTest.to_list, empty" (Seq.init 0 (fun i -> i+1) |> Seq.toList) [  ]
  do check "IEnumerableTest.to_list, small" (Seq.init 1 (fun i -> i+1) |> Seq.toList) [ 1 ]
  do check "IEnumerableTest.to_list, large" (Seq.init 100000 (fun i -> i+1) |> Seq.toList |> List.length) 100000
  do check "IEnumerableTest.to_list, large" (Seq.init 1000000 (fun i -> i+1) |> Seq.toList |> List.length) 1000000
  do check "IEnumerableTest.to_list, large" (Seq.init 1000000 (fun i -> i+1) |> List.ofSeq |> List.length) 1000000
  do check "List.unzip, large" (Seq.init 1000000 (fun i -> (i,i+1)) |> List.ofSeq |> List.unzip |> fst |> List.length) 1000000
  let dup x = x,x
  let uncurry f (x,y) = f x y
  do check "List.zip, large" (Seq.init 1000000 (fun i -> (i,i+1)) |> List.ofSeq |> dup |> uncurry List.zip |> List.length) 1000000
  
(*
  // Currently disabled, since IStructuralEquatable.Equals will cause this to stack overflow around 140000 elements
  do check "List.sort, large" ((Seq.init 140000 (fun i -> 139999 - i) |> List.ofSeq |> List.sort) = 
                               (Seq.init 140000 (fun i -> i) |> List.ofSeq |> List.sort))   true
*)

  
  do check "Seq.singleton" (Seq.singleton 42 |> Seq.length) 1
  do check "Seq.singleton" (Seq.singleton 42 |> Seq.toList) [42]

  do check "Seq.truncate" (Seq.truncate 20 [1..100] |> Seq.toList) [1..20]
  do check "Seq.truncate" (Seq.truncate 1 [1..100] |> Seq.toList) [1]
  do check "Seq.truncate" (Seq.truncate 0 [1..100] |> Seq.toList) []

  do check "Seq.scan" (Seq.scan (+) 0 [|1..5|] |> Seq.toArray) [|0; 1; 3; 6; 10; 15|]
  //do check "Seq.scan1" (Seq.scan1 (+) [|1..5|] |> Seq.toArray) [|3; 6; 10; 15|]

  do check "Seq.exists2" (Seq.exists2 (=) [|1; 2; 3; 4; 5; 6|] [|2; 3; 4; 5; 6; 6|]) true
  do check "Seq.exists2" (Seq.exists2 (=) [|1; 2; 3; 4; 5; 6|] [|2; 3; 4; 5; 6; 7|]) false

  do check "Seq.forall2" (Seq.forall2 (=) [|1..10|] [|1..10|]) true
  do check "Seq.forall2" (Seq.forall2 (=) [|1;2;3;4;5|] [|1;2;3;0;5|]) false


//  do check "Seq.find_index" (Seq.find_index (fun i -> i >= 4) [|0..10|]) 4
//  do check "Seq.find_index" (try Seq.find_index (fun i -> i >= 20) [|0..10|] |> ignore; false
//                             with _ -> true) true
   
//  do check "Seq.find_indexi" (Seq.find_indexi (=) [|1; 2; 3; 3; 2; 1|]) 3
//  do check "Seq.find_indexi" (try Seq.find_indexi (=) [|1..10|] |> ignore; false
//                              with _ -> true) true

  do check "Seq.tryFind" ([|1..100|] |> Seq.tryFind (fun x -> x > 50)) (Some 51)
  do check "Seq.tryFind" ([|1..100|] |> Seq.tryFind (fun x -> x > 180)) None

//   do check "Seq.tryfind_index" (Seq.tryfind_index (fun x -> x = 4) [|0..10|]) (Some 4)
//   do check "Seq.tryfind_index" (Seq.tryfind_index (fun x -> x = 42) [|0..10|]) None

//   do check "Seq.tryfind_indexi" (Seq.tryfind_indexi (=) [|1;2;3;4;4;3;2;1|]) (Some 4)
//   do check "Seq.tryfind_indexi" (Seq.tryfind_indexi (=) [|1..10|]) None

  do check "Seq.compareWith" (Seq.compareWith compare [1;2] [2;1]) -1
  do check "Seq.compareWith" (Seq.compareWith compare [2;1] [1;2])  1
  do check "Seq.compareWith" (Seq.compareWith compare [1;2] [1;2])  0
  do check "Seq.compareWith" (Seq.compareWith compare []    [1;2]) -1

  do check "Seq.ofList" (Seq.toList (Seq.ofList [1..20])) [1..20]

  do check "Seq.cast" (Seq.cast [1..10] |> Seq.toList) [1..10]
  do check "Seq.collect" (Seq.collect (fun i -> [i*10 .. i*10+9]) [0..9] |> Seq.toList) [0..99]

  let c = ref -1
  do Seq.iter2 (fun x y -> incr c; test "Seq.iter2" (!c = x && !c = y)) [0..10] [0..10]
  do check "Seq.iter2" !c 10

  do check "Seq.zip"
       (Seq.zip [1..10] [2..11] |> Seq.toList) [for i in 1..10 -> i, i+1]


  do check "Seq.zip3"
       (Seq.zip3 [1..10] [2..11] [3..12] |> Seq.toList) [for i in 1..10 -> i, i+1, i+2]

  do c := -1
  do Seq.iteri (fun n x -> incr c; test "Seq.iter2" (!c = n && !c+1 = x)) [1..11]
  do check "Seq.iter2" !c 10

  do check "Seq.pairwise" (Seq.pairwise [1..20] |> Seq.toList) [for i in 1 .. 19 -> i, i+1]

  do check "Seq.windowed 1" (Seq.windowed 1 [1..20] |> Seq.toList) [for i in 1 .. 20 -> [|i|]]
  do check "Seq.windowed 2" (Seq.windowed 2 [1..20] |> Seq.toList) [for i in 1 .. 19 -> [|i; i+1|]]
  do check "Seq.windowed 3" (Seq.windowed 3 [1..20] |> Seq.toList) [for i in 1 .. 18 -> [|i; i+1; i+2|]]
  do check "Seq.windowed 4" (Seq.windowed 4 [1..20] |> Seq.toList) [for i in 1 .. 17 -> [|i; i+1; i+2; i+3|]]

  let group = Seq.groupBy (fun x -> x % 5) [1..100]
  do for n, s in group do
      check "Seq.groupBy" (Seq.forall (fun x -> x % 5 = n) s) true
     done
  do check "Seq.groupBy" ([for n,_ in group -> n] |> List.sort) [0..4]

  let sorted = Seq.sortBy abs [2; 4; 3; -5; 2; -4; -8; 0; 5; 2]
  do check "Seq.sortBy" (Seq.pairwise sorted |> Seq.forall (fun (x, y) -> abs x <= abs y)) true

  let counts = Seq.countBy id [for i in 1..10 do yield! [10..-1..i] done]
  do check "Seq.countBy" (counts |> Seq.toList) [for i in 10..-1..1 -> i, i]

  do check "Seq.sum" (Seq.sum [1..100]) (100*101/2)
  do check "Seq.sumBy" (Seq.sumBy float [1..100]) (100.*101./2.)

  do check "Seq.average" (Seq.average [1.; 2.; 3.]) 2.
  do check "Seq.averageBy" (Seq.averageBy float [0..100]) 50.
  do check "Seq.min" (Seq.min [1; 4; 2; 5; 8; 4; 0; 3]) 0
  do check "Seq.max" (Seq.max [1; 4; 2; 5; 8; 4; 0; 3]) 8
  do check "Seq.minBy" (Seq.minBy int "this is a test") ' '
  do check "Seq.maxBy" (Seq.maxBy int "this is a test") 't'

  // Test where the key includes null values
  do check "dict - option key" (dict [ (None,10); (Some 3, 220) ]).[None] 10
  do check "dict - option key" (dict [ (None,10); (Some 3, 220) ]).[Some 3] 220
  do check "dict - option key" (([ (None,10); (Some 3, 220) ] |> Seq.groupBy fst) |> Seq.length) 2
  do check "dict - option key" (([ (None,10); (Some 3, 220); (None,10); (Some 3, 220) ] |> Seq.distinct ) |> Seq.length) 2
  do check "dict - option key" (([ (None,10); (Some 3, 220); (None,10); (Some 4, 220) ] |> Seq.distinctBy fst) |> Seq.length) 3
  do check "dict - option key" (([ (None,10); (Some 3, 220); (None,10); (Some 4, 220) ] |> Seq.countBy fst) |> Seq.length) 3

  // Test where the key includes null values
  do check "dict - option key" (dict [ ([| |],10); ([| 3 |], 220) ]).[[| |]] 10
  do check "dict - option key" (dict [ ([| |],10); ([| 3 |], 220) ]).[[| 3 |]] 220
  do check "dict - option key" (([ ([| |],10); ([| 3 |], 220) ] |> Seq.groupBy fst) |> Seq.length) 2
  do check "dict - option key" (([ ([| |],10); ([| 3 |], 220); ([| |],10); ([| 3 |], 220) ] |> Seq.distinct ) |> Seq.length) 2
  do check "dict - option key" (([ ([| |],10); ([| 3 |], 220); ([| |],10); ([| 4 |], 220) ] |> Seq.distinctBy fst) |> Seq.length) 3
  do check "dict - option key" (([ ([| |],10); ([| 3 |], 220); ([| |],10); ([| 4 |], 220) ] |> Seq.countBy fst) |> Seq.length) 3

end

module SeqTestsOnEnumerableEnforcingDisposalAtEnd = begin
   
   let mutable numActiveEnumerators = 0
   
   let countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd (seq: seq<'a>) =
       let enumerator() = 
                 numActiveEnumerators <- numActiveEnumerators + 1;
                 let disposed = ref false in
                 let endReached = ref false in
                 let ie = seq.GetEnumerator() in
                 { new System.Collections.Generic.IEnumerator<'a> with 
                      member x.Current =
                          test "rvlrve0" (not !endReached);
                          test "rvlrve1" (not !disposed);
                          ie.Current
                      member x.Dispose() = 
                          test "rvlrve2" !endReached;
                          test "rvlrve4" (not !disposed);
                          numActiveEnumerators <- numActiveEnumerators - 1;
                          disposed := true;
                          ie.Dispose() 
                   interface System.Collections.IEnumerator with 
                      member x.MoveNext() = 
                          test "rvlrve0" (not !endReached);
                          test "rvlrve3" (not !disposed);
                          endReached := not (ie.MoveNext());
                          not !endReached
                      member x.Current = 
                          test "qrvlrve0" (not !endReached);
                          test "qrvlrve1" (not !disposed);
                          box ie.Current
                      member x.Reset() = 
                          ie.Reset()
                   } in

       { new seq<'a> with 
             member x.GetEnumerator() =  enumerator() 
         interface System.Collections.IEnumerable with 
             member x.GetEnumerator() =  (enumerator() :> _) }

   let countEnumeratorsAndCheckedDisposedAtMostOnce (seq: seq<'a>) =
       let enumerator() = 
                 let disposed = ref false in
                 let endReached = ref false in
                 let ie = seq.GetEnumerator() in
                 numActiveEnumerators <- numActiveEnumerators + 1;
                 { new System.Collections.Generic.IEnumerator<'a> with 
                      member x.Current =
                          test "qrvlrve0" (not !endReached);
                          test "qrvlrve1" (not !disposed);
                          ie.Current
                      member x.Dispose() = 
                          test "qrvlrve4" (not !disposed);
                          numActiveEnumerators <- numActiveEnumerators - 1;
                          disposed := true;
                          ie.Dispose() 
                   interface System.Collections.IEnumerator with 
                      member x.MoveNext() = 
                          test "qrvlrve0" (not !endReached);
                          test "qrvlrve3" (not !disposed);
                          endReached := not (ie.MoveNext());
                          not !endReached
                      member x.Current = 
                          test "qrvlrve0" (not !endReached);
                          test "qrvlrve1" (not !disposed);
                          box ie.Current
                      member x.Reset() = 
                          ie.Reset()
                   } in

       { new seq<'a> with 
             member x.GetEnumerator() =  enumerator() 
         interface System.Collections.IEnumerable with 
             member x.GetEnumerator() =  (enumerator() :> _) }

   // This one gave a stack overflow when we weren't tail-calling on 64-bit
   do check "Seq.filter-length" ({ 1 .. 1000000 } |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.filter (fun n -> n <> 1) |> Seq.length) 999999
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.filter-length" ({ 1 .. 1000000 } |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.filter (fun n -> n = 1) |> Seq.length) 1
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.filter-length" ({ 1 .. 1000000 } |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.filter (fun n -> n % 2 = 0) |> Seq.length) 500000
   do check "<dispoal>" numActiveEnumerators 0
    
   do check "IEnumerableTest.empty-length" (Seq.length (countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd Seq.empty)) 0
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.length-of-array" (Seq.length (countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd [| 1;2;3 |])) 3
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.head-of-array" (Seq.head (countEnumeratorsAndCheckedDisposedAtMostOnce [| 1;2;3 |])) 1
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.take-0-of-array" (Seq.take 0 (countEnumeratorsAndCheckedDisposedAtMostOnce [| 1;2;3 |]) |> Seq.toList) []
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.take-1-of-array" (Seq.take 1 (countEnumeratorsAndCheckedDisposedAtMostOnce [| 1;2;3 |]) |> Seq.toList) [1]
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.take-3-of-array" (Seq.take 3 (countEnumeratorsAndCheckedDisposedAtMostOnce [| 1;2;3 |]) |> Seq.toList) [1;2;3]
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.nonempty-true" (Seq.isEmpty (countEnumeratorsAndCheckedDisposedAtMostOnce [| 1;2;3 |])) false
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.nonempty-false" (Seq.isEmpty (countEnumeratorsAndCheckedDisposedAtMostOnce [| |])) true
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.fold" (Seq.fold (+) 0 (countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd [| 1;2;3 |])   ) 6
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.unfold" (Seq.unfold (fun _ -> None) 1 |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.toArray) [| |]
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.unfold" (Seq.unfold (fun x -> if x = 1 then Some("a",2) else  None) 1 |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.toArray) [| "a" |]
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.exists" (Seq.exists ((=) "a") (countEnumeratorsAndCheckedDisposedAtMostOnce [| |])) false
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.exists" (Seq.exists ((=) "a") (countEnumeratorsAndCheckedDisposedAtMostOnce [| "a" |])) true
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.exists" (Seq.exists ((=) "a") (countEnumeratorsAndCheckedDisposedAtMostOnce [| "1"; "a" |])) true
   do check "<dispoal>" numActiveEnumerators 0


   do check "IEnumerableTest.exists" (Seq.forall ((=) "a") (countEnumeratorsAndCheckedDisposedAtMostOnce [| |])) true
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.exists" (Seq.forall ((=) "a") (countEnumeratorsAndCheckedDisposedAtMostOnce [| "a" |])) true
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.exists" (Seq.forall ((=) "a") (countEnumeratorsAndCheckedDisposedAtMostOnce [| "1"; "a" |])) false
   do check "<dispoal>" numActiveEnumerators 0

   do check "IEnumerableTest.map on finite" ([| "a" |] |> Seq.map (fun x -> x.Length) |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.toArray) [| 1 |]
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.filter on finite" ([| "a";"ab";"a" |] |> Seq.filter (fun x -> x.Length = 1) |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.toArray) [| "a";"a" |]
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.choose on finite" ([| "a";"ab";"a" |] |> Seq.choose (fun x -> if x.Length = 1 then Some(x^"a") else None) |> Seq.toArray) [| "aa";"aa" |]
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.pick on finite (succeeding)" ([| "a";"ab";"a" |] |> countEnumeratorsAndCheckedDisposedAtMostOnce |> Seq.pick (fun x -> if x.Length = 1 then Some(x^"a") else None)) "aa"
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.tryPick on finite (succeeding)" ([| "a";"ab";"a" |] |> countEnumeratorsAndCheckedDisposedAtMostOnce |> Seq.tryPick (fun x -> if x.Length = 1 then Some(x^"a") else None)) (Some "aa")
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.tryPick on finite (failing)" ([| "a";"ab";"a" |] |> countEnumeratorsAndCheckedDisposedAtMostOnce |> Seq.tryPick (fun x -> if x.Length = 6 then Some(x^"a") else None)) None
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.find on finite (succeeding)" ([| "a";"ab";"a" |] |> countEnumeratorsAndCheckedDisposedAtMostOnce |> Seq.find (fun x -> x.Length = 1)) "a"
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.find on finite (failing)" (try Some ([| "a";"ab";"a" |] |> countEnumeratorsAndCheckedDisposedAtMostOnce |> Seq.find (fun x -> x.Length = 6)) with :? System.Collections.Generic.KeyNotFoundException -> None) None
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.map_with_type (string up to obj,finite)" ([| "a" |] |> Seq.cast |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.toArray) [| ("a" :> obj) |]
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.map_with_type (obj down to string, finite)" ([| ("a" :> obj) |] |> Seq.cast |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.toArray) [| "a" |]
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.append, finite, finite" (Seq.append [| "a" |] [| "b" |]  |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.toArray) [| "a"; "b" |]
   do check "<dispoal>" numActiveEnumerators 0



   do check "IEnumerableTest.concat, finite" (Seq.concat [| [| "a" |]; [| |]; [| "b";"c" |] |]  |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.toList) [ "a";"b";"c" ]
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.init_infinite, then take" (Seq.take 2 (countEnumeratorsAndCheckedDisposedAtMostOnce (Seq.initInfinite (fun i -> i+1))) |> Seq.toList) [ 1;2 ]
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.to_array, empty" (Seq.init 0 (fun i -> i+1) |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.toArray) [|  |]
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.to_array, small" (Seq.init 1 (fun i -> i+1) |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.toArray) [| 1 |]
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.to_array, large" (Seq.init 100000 (fun i -> i+1) |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.toArray |> Array.length) 100000
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.to_array, very large" (Seq.init 1000000 (fun i -> i+1) |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.toArray |> Array.length) 1000000
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.to_list, empty" (Seq.init 0 (fun i -> i+1) |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.toList) [  ]
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.to_list, small" (Seq.init 1 (fun i -> i+1) |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.toList) [ 1 ]
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.to_list, large" (Seq.init 100000 (fun i -> i+1) |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.toList |> List.length) 100000
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.to_list, large" (Seq.init 1000000 (fun i -> i+1) |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.toList |> List.length) 1000000
   do check "<dispoal>" numActiveEnumerators 0
   do check "IEnumerableTest.to_list, large" (Seq.init 1000000 (fun i -> i+1) |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> List.ofSeq |> List.length) 1000000
   do check "<dispoal>" numActiveEnumerators 0
   do check "List.unzip, large" (Seq.init 1000000 (fun i -> (i,i+1)) |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> List.ofSeq |> List.unzip |> fst |> List.length) 1000000
   do check "<dispoal>" numActiveEnumerators 0
   let dup x = x,x
   let uncurry f (x,y) = f x y
    
   do check "List.zip, large" (Seq.init 1000000 (fun i -> (i,i+1)) |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> List.ofSeq |> dup |> uncurry List.zip |> List.length) 1000000
   do check "<dispoal>" numActiveEnumerators 0
    
(*
    // Currently disabled, since IStructuralEquatable.Equals will cause this to stack overflow around 140000 elements
    do check "List.sort, large" ((Seq.init 140000 (fun i -> 139999 - i) |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> List.ofSeq |> List.sort) = 
                                 (Seq.init 140000 (fun i -> i) |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> List.ofSeq |> List.sort))   true
    do check "<dispoal>" numActiveEnumerators 0
*)
    
   do check "Seq.singleton" (Seq.singleton 42 |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.length) 1
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.singleton" (Seq.singleton 42 |> countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd |> Seq.toList) [42]
   do check "<dispoal>" numActiveEnumerators 0

   do check "Seq.truncate" (Seq.truncate 20 (countEnumeratorsAndCheckedDisposedAtMostOnce [1..100]) |> Seq.toList) [1..20]
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.truncate" (Seq.truncate 1 (countEnumeratorsAndCheckedDisposedAtMostOnce [1..100]) |> Seq.toList) [1]
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.truncate" (Seq.truncate 0 (countEnumeratorsAndCheckedDisposedAtMostOnce [1..100]) |> Seq.toList) []
   do check "<dispoal>" numActiveEnumerators 0

   do check "Seq.scan" (Seq.scan (+) 0 (countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd [|1..5|]) |> Seq.toArray) [|0; 1; 3; 6; 10; 15|]
   do check "<dispoal>" numActiveEnumerators 0
   //do check "Seq.scan1" (Seq.scan1 (+) (countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd [|1..5|]) |> Seq.toArray) [|3; 6; 10; 15|]
   //do check "<dispoal>" numActiveEnumerators 0

   do check "Seq.exists2" (Seq.exists2 (=) (countEnumeratorsAndCheckedDisposedAtMostOnce [|1; 2; 3; 4; 5; 6|]) (countEnumeratorsAndCheckedDisposedAtMostOnce [|2; 3; 4; 5; 6; 6|])) true
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.exists2" (Seq.exists2 (=) (countEnumeratorsAndCheckedDisposedAtMostOnce [|1; 2; 3; 4; 5; 6|]) (countEnumeratorsAndCheckedDisposedAtMostOnce [|2; 3; 4; 5; 6; 7|])) false
   do check "<dispoal>" numActiveEnumerators 0

   do check "Seq.forall2" (Seq.forall2 (=) (countEnumeratorsAndCheckedDisposedAtMostOnce [|1..10|]) (countEnumeratorsAndCheckedDisposedAtMostOnce [|1..10|])) true
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.forall2" (Seq.forall2 (=) (countEnumeratorsAndCheckedDisposedAtMostOnce [|1;2;3;4;5|]) (countEnumeratorsAndCheckedDisposedAtMostOnce [|1;2;3;0;5|])) false
   do check "<dispoal>" numActiveEnumerators 0



   do check "Seq.tryFind" ([|1..100|] |> countEnumeratorsAndCheckedDisposedAtMostOnce |> Seq.tryFind (fun x -> x > 50)) (Some 51)
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.tryFind" ([|1..100|] |> countEnumeratorsAndCheckedDisposedAtMostOnce |> Seq.tryFind (fun x -> x > 180)) None
   do check "<dispoal>" numActiveEnumerators 0


   do check "Seq.compareWith" (Seq.compareWith compare (countEnumeratorsAndCheckedDisposedAtMostOnce [1;2]) (countEnumeratorsAndCheckedDisposedAtMostOnce [2;1])) -1
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.compareWith" (Seq.compareWith compare (countEnumeratorsAndCheckedDisposedAtMostOnce [2;1]) (countEnumeratorsAndCheckedDisposedAtMostOnce [1;2]))  1
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.compareWith" (Seq.compareWith compare (countEnumeratorsAndCheckedDisposedAtMostOnce [1;2]) (countEnumeratorsAndCheckedDisposedAtMostOnce [1;2]))  0
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.compareWith" (Seq.compareWith compare (countEnumeratorsAndCheckedDisposedAtMostOnce []) (countEnumeratorsAndCheckedDisposedAtMostOnce    [1;2])) -1
   do check "<dispoal>" numActiveEnumerators 0

   do check "Seq.collect" (Seq.collect (fun i -> [i*10 .. i*10+9]) (countEnumeratorsAndCheckedDisposedAtMostOnce [0..9]) |> Seq.toList) [0..99]
   do check "<dispoal>" numActiveEnumerators 0

   let c = ref -1
   do Seq.iter2 (fun x y -> incr c; test "Seq.iter2" (!c = x && !c = y)) (countEnumeratorsAndCheckedDisposedAtMostOnce [0..10]) (countEnumeratorsAndCheckedDisposedAtMostOnce [0..10])
   do check "Seq.iter2" !c 10
   do check "<dispoal>" numActiveEnumerators 0

   do check "Seq.zip"
         (Seq.zip [1..10] (countEnumeratorsAndCheckedDisposedAtMostOnce [2..11]) |> Seq.toList) [for i in 1..10 -> i, i+1]
   do check "<dispoal>" numActiveEnumerators 0


   do check "Seq.zip3"
         (Seq.zip3 (countEnumeratorsAndCheckedDisposedAtMostOnce [1..10]) (countEnumeratorsAndCheckedDisposedAtMostOnce [2..11]) (countEnumeratorsAndCheckedDisposedAtMostOnce [3..12]) |> Seq.toList) [for i in 1..10 -> i, i+1, i+2]
   do check "<dispoal>" numActiveEnumerators 0

   do c := -1
   do Seq.iteri (fun n x -> incr c; test "Seq.iter2" (!c = n && !c+1 = x)) (countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd [1..11])
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.iter2" !c 10

   do check "Seq.pairwise" (Seq.pairwise (countEnumeratorsAndCheckedDisposedAtMostOnce [1..20]) |> Seq.toList) [for i in 1 .. 19 -> i, i+1]
   do check "<dispoal>" numActiveEnumerators 0

   do check "Seq.windowed 1" (Seq.windowed 1 (countEnumeratorsAndCheckedDisposedAtMostOnce [1..20]) |> Seq.toList) [for i in 1 .. 20 -> [|i|]]
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.windowed 2" (Seq.windowed 2 (countEnumeratorsAndCheckedDisposedAtMostOnce [1..20]) |> Seq.toList) [for i in 1 .. 19 -> [|i; i+1|]]
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.windowed 3" (Seq.windowed 3 (countEnumeratorsAndCheckedDisposedAtMostOnce [1..20]) |> Seq.toList) [for i in 1 .. 18 -> [|i; i+1; i+2|]]
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.windowed 4" (Seq.windowed 4 (countEnumeratorsAndCheckedDisposedAtMostOnce [1..20]) |> Seq.toList) [for i in 1 .. 17 -> [|i; i+1; i+2; i+3|]]
   do check "<dispoal>" numActiveEnumerators 0

   let group = Seq.groupBy (fun x -> x % 5) (countEnumeratorsAndCheckedDisposedAtMostOnce [1..100])
   do for n, s in group do
        check "Seq.groupBy" (Seq.forall (fun x -> x % 5 = n) s) true;
        check "<dispoal>" numActiveEnumerators 0
      done

   let sorted = Seq.sortBy abs (countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd [2; 4; 3; -5; 2; -4; -8; 0; 5; 2])
   do check "Seq.sortBy" (Seq.pairwise sorted |> Seq.forall (fun (x, y) -> abs x <= abs y)) true
   do check "<dispoal>" numActiveEnumerators 0
   let counts = Seq.countBy id (countEnumeratorsAndCheckedDisposedAtMostOnce [for i in 1..10 do yield! [10..-1..i] done ])
   do check "Seq.countBy" (counts |> Seq.toList) [for i in 10..-1..1 -> i, i]
   do check "<dispoal>" numActiveEnumerators 0

   do check "Seq.sum" (Seq.sum (countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd [1..100])) (100*101/2)
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.sumBy" (Seq.sumBy float [1..100]) (100.*101./2.)
   do check "<dispoal>" numActiveEnumerators 0

   do check "Seq.average" (Seq.average (countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd [1.; 2.; 3.])) 2.
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.averageBy" (Seq.averageBy float (countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd [0..100])) 50.
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.min" (Seq.min (countEnumeratorsAndCheckedDisposedAtMostOnceAtEnd [1; 4; 2; 5; 8; 4; 0; 3])) 0
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.max" (Seq.max (countEnumeratorsAndCheckedDisposedAtMostOnce [1; 4; 2; 5; 8; 4; 0; 3])) 8
   do check "<dispoal>" numActiveEnumerators 0
#if !NETCOREAPP
// strings don't have enumerators in portable
   do check "Seq.minBy" (Seq.minBy int (countEnumeratorsAndCheckedDisposedAtMostOnce "this is a test")) ' '
   do check "<dispoal>" numActiveEnumerators 0
   do check "Seq.maxBy" (Seq.maxBy int (countEnumeratorsAndCheckedDisposedAtMostOnce "this is a test")) 't'
   do check "<dispoal>" numActiveEnumerators 0
#endif

end

let (lsr) (a:int) (b:int) = int32 (uint32 a >>> b)
let (lsl) (a:int) (b:int) = a <<< b
let (lor) (a:int) (b:int) = a ||| b
let (lxor) (a:int) (b:int) = a ^^^ b
let (land) (a:int) (b:int) = a &&& b
// check precedence of lsl, lsr etc.
let _ = fun (x:int) -> x > x lsr 1
let _ = fun (x:int) -> x > (x lsr 1)
let _ = fun (x:int) -> x > x lsl 1
let _ = fun (x:int) -> x > (x lsl 1)
let _ = fun (x:int) -> x > x lor 1
let _ = fun (x:int) -> x > (x lor 1)
let _ = fun (x:int) -> x > x lxor 1
let _ = fun (x:int) -> x > (x lxor 1)
let _ = fun (x:int) -> x > x land 1
let _ = fun (x:int) -> x > (x land 1)


// check ordering of NaN
(*
The predefined floating-point comparison operators are:
bool operator ==(float x, float y);
bool operator ==(double x, double y);
bool operator !=(float x, float y);
bool operator !=(double x, double y);
bool operator <(float x, float y);
bool operator <(double x, double y);
bool operator >(float x, float y);
bool operator >(double x, double y);
bool operator <=(float x, float y);
bool operator <=(double x, double y);
bool operator >=(float x, float y);
bool operator >=(double x, double y);
The operators compare the operands according to the rules of the IEC 60559 standard:
If either operand is NaN, the result is false for all operators except !=, for which the result is true. For
any two operands, x != y always produces the same result as !(x == y). However, when one or both
operands are NaN, the <, >, <=, and >= operators do not produce the same results as the logical negation of
the opposite operator. [Example: If either of x and y is NaN, then x < y is false, but !(x >= y) is true.
end example]
? When neither operand is NaN, the operators compare the values of the two floating-point operands with
respect to the ordering
-inf < ?max < ? < ?min < ?0.0 == +0.0 < +min < ? < +max < +inf
where min and max are the smallest and largest positive finite values that can be represented in the given
floating-point format. Notable effects of this ordering are:
o Negative and positive zeros are considered equal.
o A negative infinity is considered less than all other values, but equal to another negative infinity.
o A positive infinity is considered greater than all other values, but equal to another positive infinity.
*)
open System

(* ----- NaN tests for DOUBLE ----- *)

module DoubleNaN =
    let nan1 = (let r = ref Double.NaN in (if sprintf "Hello" = "Hello" then !r else 0.0))
    let nan2 = (let r = ref Double.NaN in (if sprintf "Hello" = "Hello" then !r else 0.0))

    do printf "checking floating point relational operators\n"
    let _ = check "d3wiojd30a" ((Double.NaN > Double.NaN)) false
    check "d3wiojd30a" (if (Double.NaN > Double.NaN) then "a" else "b") "b"
    check "d3wiojd30b" ((Double.NaN >= Double.NaN)) false
    check "d3wiojd30b" (if (Double.NaN >= Double.NaN) then "a" else "b") "b"
    check "d3wiojd30c" ((Double.NaN < Double.NaN)) false
    check "d3wiojd30c" (if (Double.NaN < Double.NaN) then "a" else "b") "b"
    check "d3wiojd30d" ((Double.NaN <= Double.NaN)) false
    check "d3wiojd30d" (if (Double.NaN <= Double.NaN) then "a" else "b") "b"
    check "d3wiojd30e" ((Double.NaN = Double.NaN)) false
    check "d3wiojd30e" (if (Double.NaN = Double.NaN) then "a" else "b") "b"
    check "d3wiojd30q" ((Double.NaN <> Double.NaN)) true
    check "d3wiojd30w" ((Double.NaN > 1.0)) false
    check "d3wiojd30e" ((Double.NaN >= 1.0)) false
    check "d3wiojd30r" ((Double.NaN < 1.0)) false
    check "d3wiojd30t" ((Double.NaN <= 1.0)) false
    check "d3wiojd30y" ((Double.NaN = 1.0)) false
    check "d3wiojd30u" ((Double.NaN <> 1.0)) true
    check "d3wiojd30i" ((1.0 > Double.NaN)) false
    check "d3wiojd30o" ((1.0 >= Double.NaN)) false
    check "d3wiojd30p" ((1.0 < Double.NaN)) false
    check "d3wiojd30a" ((1.0 <= Double.NaN)) false
    check "d3wiojd30s" ((1.0 = Double.NaN)) false
    check "d3wiojd30d" ((1.0 <> Double.NaN)) true
    check "d3wiojd30a" ((nan1 > Double.NaN)) false
    check "d3wiojd30b" ((nan1 >= nan2)) false
    check "d3wiojd30c" ((nan1 < nan2)) false
    check "d3wiojd30d" ((nan1 <= nan2)) false
    check "d3wiojd30e" ((nan1 = nan2)) false
    check "d3wiojd30q" ((nan1 <> nan2)) true
    check "d3wiojd30w" ((nan1 > 1.0)) false
    check "d3wiojd30e" ((nan1 >= 1.0)) false
    check "d3wiojd30r" ((nan1 < 1.0)) false
    check "d3wiojd30t" ((nan1 <= 1.0)) false
    check "d3wiojd30y" ((nan1 = 1.0)) false
    check "d3wiojd30u" ((nan1 <> 1.0)) true
    check "d3wiojd30i" ((1.0 > nan2)) false
    check "d3wiojd30o" ((1.0 >= nan2)) false
    check "d3wiojd30p" ((1.0 < nan2)) false
    check "d3wiojd30a" ((1.0 <= nan2)) false
    check "d3wiojd30s" ((1.0 = nan2)) false
    check "d3wiojd30d" ((1.0 <> nan2)) true
    check "d3wiojd30f" ((Double.NegativeInfinity = Double.NegativeInfinity)) true
    check "d3wiojd30g" ((Double.NegativeInfinity < Double.PositiveInfinity)) true
    check "d3wiojd30h" ((Double.NegativeInfinity > Double.PositiveInfinity)) false
    check "d3wiojd30j" ((Double.NegativeInfinity <= Double.NegativeInfinity)) true

    check "D1nancompare01" (0 = (compare Double.NaN Double.NaN)) true
    check "D1nancompare02" (0 = (compare Double.NaN nan1)) true
    check "D1nancompare03" (0 = (compare nan1 Double.NaN)) true
    check "D1nancompare04" (0 = (compare nan1 nan1)) true
    check "D1nancompare05" (1 = (compare 1. Double.NaN)) true
    check "D1nancompare06" (1 = (compare 0. Double.NaN)) true
    check "D1nancompare07" (1 = (compare -1. Double.NaN)) true
    check "D1nancompare08" (1 = (compare Double.NegativeInfinity Double.NaN)) true
    check "D1nancompare09" (1 = (compare Double.PositiveInfinity Double.NaN)) true
    check "D1nancompare10" (1 = (compare Double.MaxValue Double.NaN)) true
    check "D1nancompare11" (1 = (compare Double.MinValue Double.NaN)) true
    check "D1nancompare12" (-1 = (compare Double.NaN 1.)) true
    check "D1nancompare13" (-1 = (compare Double.NaN 0.)) true
    check "D1nancompare14" (-1 = (compare Double.NaN -1.)) true
    check "D1nancompare15" (-1 = (compare Double.NaN Double.NegativeInfinity)) true
    check "D1nancompare16" (-1 = (compare Double.NaN Double.PositiveInfinity)) true
    check "D1nancompare17" (-1 = (compare Double.NaN Double.MaxValue)) true
    check "D1nancompare18" (-1 = (compare Double.NaN Double.MinValue)) true

module DoubleNaNNonStructuralComparison1 = 
    open NonStructuralComparison
    let nan1 = (let r = ref Double.NaN in (if sprintf "Hello" = "Hello" then !r else 0.0))
    let nan2 = (let r = ref Double.NaN in (if sprintf "Hello" = "Hello" then !r else 0.0))
    
    check "d3wiojd30a" (if (Double.NaN > Double.NaN) then "a" else "b") "b"
    check "d3wiojd30b" ((Double.NaN >= Double.NaN)) false
    check "d3wiojd30b" (if (Double.NaN >= Double.NaN) then "a" else "b") "b"
    check "d3wiojd30c" ((Double.NaN < Double.NaN)) false
    check "d3wiojd30c" (if (Double.NaN < Double.NaN) then "a" else "b") "b"
    check "d3wiojd30d" ((Double.NaN <= Double.NaN)) false
    check "d3wiojd30d" (if (Double.NaN <= Double.NaN) then "a" else "b") "b"
    check "d3wiojd30e" ((Double.NaN = Double.NaN)) false
    check "d3wiojd30e" (if (Double.NaN = Double.NaN) then "a" else "b") "b"
    check "d3wiojd30q" ((Double.NaN <> Double.NaN)) true
    check "d3wiojd30w" ((Double.NaN > 1.0)) false
    check "d3wiojd30e" ((Double.NaN >= 1.0)) false
    check "d3wiojd30r" ((Double.NaN < 1.0)) false
    check "d3wiojd30t" ((Double.NaN <= 1.0)) false
    check "d3wiojd30y" ((Double.NaN = 1.0)) false
    check "d3wiojd30u" ((Double.NaN <> 1.0)) true
    check "d3wiojd30i" ((1.0 > Double.NaN)) false
    check "d3wiojd30o" ((1.0 >= Double.NaN)) false
    check "d3wiojd30p" ((1.0 < Double.NaN)) false
    check "d3wiojd30a" ((1.0 <= Double.NaN)) false
    check "d3wiojd30s" ((1.0 = Double.NaN)) false
    check "d3wiojd30d" ((1.0 <> Double.NaN)) true
    check "d3wiojd30a" ((nan1 > Double.NaN)) false
    check "d3wiojd30b" ((nan1 >= nan2)) false
    check "d3wiojd30c" ((nan1 < nan2)) false
    check "d3wiojd30d" ((nan1 <= nan2)) false
    check "d3wiojd30e" ((nan1 = nan2)) false
    check "d3wiojd30q" ((nan1 <> nan2)) true
    check "d3wiojd30w" ((nan1 > 1.0)) false
    check "d3wiojd30e" ((nan1 >= 1.0)) false
    check "d3wiojd30r" ((nan1 < 1.0)) false
    check "d3wiojd30t" ((nan1 <= 1.0)) false
    check "d3wiojd30y" ((nan1 = 1.0)) false
    check "d3wiojd30u" ((nan1 <> 1.0)) true
    check "d3wiojd30i" ((1.0 > nan2)) false
    check "d3wiojd30o" ((1.0 >= nan2)) false
    check "d3wiojd30p" ((1.0 < nan2)) false
    check "d3wiojd30a" ((1.0 <= nan2)) false
    check "d3wiojd30s" ((1.0 = nan2)) false
    check "d3wiojd30d" ((1.0 <> nan2)) true
    check "d3wiojd30f" ((Double.NegativeInfinity = Double.NegativeInfinity)) true
    check "d3wiojd30g" ((Double.NegativeInfinity < Double.PositiveInfinity)) true
    check "d3wiojd30h" ((Double.NegativeInfinity > Double.PositiveInfinity)) false
    check "d3wiojd30j" ((Double.NegativeInfinity <= Double.NegativeInfinity)) true

    check "D2nancompare01" (0 = (compare Double.NaN Double.NaN)) true
    check "D2nancompare02" (0 = (compare Double.NaN nan1)) true
    check "D2nancompare03" (0 = (compare nan1 Double.NaN)) true
    check "D2nancompare04" (0 = (compare nan1 nan1)) true
    check "D2nancompare05" (1 = (compare 1. Double.NaN)) true
    check "D2nancompare06" (1 = (compare 0. Double.NaN)) true
    check "D2nancompare07" (1 = (compare -1. Double.NaN)) true
    check "D2nancompare08" (1 = (compare Double.NegativeInfinity Double.NaN)) true
    check "D2nancompare09" (1 = (compare Double.PositiveInfinity Double.NaN)) true
    check "D2nancompare10" (1 = (compare Double.MaxValue Double.NaN)) true
    check "D2nancompare11" (1 = (compare Double.MinValue Double.NaN)) true
    check "D2nancompare12" (-1 = (compare Double.NaN 1.)) true
    check "D2nancompare13" (-1 = (compare Double.NaN 0.)) true
    check "D2nancompare14" (-1 = (compare Double.NaN -1.)) true
    check "D2nancompare15" (-1 = (compare Double.NaN Double.NegativeInfinity)) true
    check "D2nancompare16" (-1 = (compare Double.NaN Double.PositiveInfinity)) true
    check "D2nancompare17" (-1 = (compare Double.NaN Double.MaxValue)) true
    check "D2nancompare18" (-1 = (compare Double.NaN Double.MinValue)) true

module DoubleNaNStructured = 
    type www = W of float
    let nan1 = (let r = ref Double.NaN in (if sprintf "Hello" = "Hello" then !r else 0.0))
    let nan2 = (let r = ref Double.NaN in (if sprintf "Hello" = "Hello" then !r else 0.0))
    
    do printf "checking floating point relational operators on structured data\n"
    // NOTE: SPECIFICATION: The relational operators work differently when applied to
    // floats embedded in structured data than when applied to raw floats. 

    let _ = check "d3wiojd31q" ((W Double.NaN > W Double.NaN)) false
    let _ = check "d3wiojd31w" ((W Double.NaN >= W Double.NaN)) false
    let _ = check "d3wiojd31e" ((W Double.NaN < W Double.NaN)) false
    let _ = check "d3wiojd31r" ((W Double.NaN <= W Double.NaN)) false
    let _ = check "d3wiojd31ty" ((W Double.NaN = W Double.NaN)) false
    let _ = check "d3wiojd31y" ((W Double.NaN <> W Double.NaN)) true
    let _ = check "d3wiojd31dy" (0 = compare (W Double.NaN) (W Double.NaN)) true
    let _ = check "d3wiojd31u" ((W Double.NaN > W 1.0)) false
    let _ = check "d3wiojd31i" ((W Double.NaN >= W 1.0)) false
    let _ = check "d3wiojd31o" ((W Double.NaN < W 1.0)) false
    let _ = check "d3wiojd31p" ((W Double.NaN <= W 1.0)) false
    let _ = check "d3wiojd31a" ((W Double.NaN = W 1.0)) false
    let _ = check "d3wiojd31s" ((W Double.NaN <> W 1.0)) true
    let _ = check "d3wiojd31d" ((W 1.0 > W Double.NaN)) false
    let _ = check "d3wiojd31f" ((W 1.0 >= W Double.NaN)) false
    let _ = check "d3wiojd31g" ((W 1.0 < W Double.NaN)) false
    let _ = check "d3wiojd31h" ((W 1.0 <= W Double.NaN)) false
    let _ = check "d3wiojd31j" ((W 1.0 = W Double.NaN)) false
    let _ = check "d3wiojd31k" ((W 1.0 <> W Double.NaN)) true
    let _ = check "d3wiojd31l" ((W Double.NegativeInfinity = W Double.NegativeInfinity)) true
    let _ = check "d3wiojd31c" ((W Double.NegativeInfinity < W Double.PositiveInfinity)) true
    let _ = check "d3wiojd3xx" ((W Double.NegativeInfinity > W Double.PositiveInfinity)) false
    let _ = check "d3wiojd31z" ((W Double.NegativeInfinity <= W Double.NegativeInfinity)) true

    let _ = check "D3nancompare01" (0 = (compare (W Double.NaN) (W Double.NaN))) true
    let _ = check "D3nancompare02" (0 = (compare (W Double.NaN) (W nan1))) true
    let _ = check "D3nancompare03" (0 = (compare (W nan1) (W Double.NaN))) true
    let _ = check "D3nancompare04" (0 = (compare (W nan1) (W nan1))) true
    let _ = check "D3nancompare05" (1 = (compare (W 1.) (W Double.NaN))) true
    let _ = check "D3nancompare06" (1 = (compare (W 0.) (W Double.NaN))) true
    let _ = check "D3nancompare07" (1 = (compare (W -1.) (W Double.NaN))) true
    let _ = check "D3nancompare08" (1 = (compare (W Double.NegativeInfinity) (W Double.NaN))) true
    let _ = check "D3nancompare09" (1 = (compare (W Double.PositiveInfinity) (W Double.NaN))) true
    let _ = check "D3nancompare10" (1 = (compare (W Double.MaxValue) (W Double.NaN))) true
    let _ = check "D3nancompare11" (1 = (compare (W Double.MinValue) (W Double.NaN))) true
    let _ = check "D3nancompare12" (-1 = (compare (W Double.NaN) (W 1.))) true
    let _ = check "D3nancompare13" (-1 = (compare (W Double.NaN) (W 0.))) true
    let _ = check "D3nancompare14" (-1 = (compare (W Double.NaN) (W -1.))) true
    let _ = check "D3nancompare15" (-1 = (compare (W Double.NaN) (W Double.NegativeInfinity))) true
    let _ = check "D3nancompare16" (-1 = (compare (W Double.NaN) (W Double.PositiveInfinity))) true
    let _ = check "D3nancompare17" (-1 = (compare (W Double.NaN) (W Double.MaxValue))) true
    let _ = check "D3nancompare18" (-1 = (compare (W Double.NaN) (W Double.MinValue))) true

module DoubleNaNStructuredPoly = 
    type 'a www = W of 'a
    let nan1 = (let r = ref Double.NaN in (if sprintf "Hello" = "Hello" then !r else 0.0))
    let nan2 = (let r = ref Double.NaN in (if sprintf "Hello" = "Hello" then !r else 0.0))
    do printf "checking floating point relational operators on polymorphic structured data\n"

    let _ = check "d3wiojd32q" ((W Double.NaN > W Double.NaN)) false
    let _ = check "d3wiojd32w" ((W Double.NaN >= W Double.NaN)) false
    let _ = check "d3wiojd32e" ((W Double.NaN < W Double.NaN)) false
    let _ = check "d3wiojd32r" ((W Double.NaN <= W Double.NaN)) false
    let _ = check "d3wiojd32t" ((W Double.NaN = W Double.NaN)) false
    let _ = check "d3wiojd32dt" ((W Double.NaN).Equals(W Double.NaN)) true
    let _ = check "d3wiojd32y" ((W Double.NaN <> W Double.NaN)) true
    let _ = check "d3wiojd32u" ((W Double.NaN > W 1.0)) false
    let _ = check "d3wiojd32i" ((W Double.NaN >= W 1.0)) false
    let _ = check "d3wiojd32o" ((W Double.NaN < W 1.0)) false
    let _ = check "d3wiojd32p" ((W Double.NaN <= W 1.0)) false
    let _ = check "d3wiojd32a" ((W Double.NaN = W 1.0)) false
    let _ = check "d3wiojd32s" ((W Double.NaN <> W 1.0)) true
    let _ = check "d3wiojd32d" ((W 1.0 > W Double.NaN)) false
    let _ = check "d3wiojd32f" ((W 1.0 >= W Double.NaN)) false
    let _ = check "d3wiojd32g" ((W 1.0 < W Double.NaN)) false
    let _ = check "d3wiojd32h" ((W 1.0 <= W Double.NaN)) false
    let _ = check "d3wiojd32j" ((W 1.0 = W Double.NaN)) false
    let _ = check "d3wiojd32k" ((W 1.0 <> W Double.NaN)) true
    let _ = check "d3wiojd32l" ((W Double.NegativeInfinity = W Double.NegativeInfinity)) true
    let _ = check "d3wiojd32z" ((W Double.NegativeInfinity < W Double.PositiveInfinity)) true
    let _ = check "d3wiojd32x" ((W Double.NegativeInfinity > W Double.PositiveInfinity)) false
    let _ = check "d3wiojd32c" ((W Double.NegativeInfinity <= W Double.NegativeInfinity)) true

    let _ = check "D4nancompare01" (0 = (compare (W Double.NaN) (W Double.NaN))) true
    let _ = check "D4nancompare02" (0 = (compare (W Double.NaN) (W nan1))) true
    let _ = check "D4nancompare03" (0 = (compare (W nan1) (W Double.NaN))) true
    let _ = check "D4nancompare04" (0 = (compare (W nan1) (W nan1))) true
    let _ = check "D4nancompare05" (1 = (compare (W 1.) (W Double.NaN))) true
    let _ = check "D4nancompare06" (1 = (compare (W 0.) (W Double.NaN))) true
    let _ = check "D4nancompare07" (1 = (compare (W -1.) (W Double.NaN))) true
    let _ = check "D4nancompare08" (1 = (compare (W Double.NegativeInfinity) (W Double.NaN))) true
    let _ = check "D4nancompare09" (1 = (compare (W Double.PositiveInfinity) (W Double.NaN))) true
    let _ = check "D4nancompare10" (1 = (compare (W Double.MaxValue) (W Double.NaN))) true
    let _ = check "D4nancompare11" (1 = (compare (W Double.MinValue) (W Double.NaN))) true
    let _ = check "D4nancompare12" (-1 = (compare (W Double.NaN) (W 1.))) true
    let _ = check "D4nancompare13" (-1 = (compare (W Double.NaN) (W 0.))) true
    let _ = check "D4nancompare14" (-1 = (compare (W Double.NaN) (W -1.))) true
    let _ = check "D4nancompare15" (-1 = (compare (W Double.NaN) (W Double.NegativeInfinity))) true
    let _ = check "D4nancompare16" (-1 = (compare (W Double.NaN) (W Double.PositiveInfinity))) true
    let _ = check "D4nancompare17" (-1 = (compare (W Double.NaN) (W Double.MaxValue))) true
    let _ = check "D4nancompare18" (-1 = (compare (W Double.NaN) (W Double.MinValue))) true

(* ----- NaN tests for SINGLE ----- *)

module SingleNaN =
    let nan1 = (let r = ref Single.NaN in (if sprintf "Hello" = "Hello" then !r else 0.0f))
    let nan2 = (let r = ref Single.NaN in (if sprintf "Hello" = "Hello" then !r else 0.0f))

    do printf "checking floating point relational operators\n"
    let _ = check "d3wiojd30a" ((Single.NaN > Single.NaN)) false
    check "d3wiojd30a" (if (Single.NaN > Single.NaN) then "a" else "b") "b"
    check "d3wiojd30b" ((Single.NaN >= Single.NaN)) false
    check "d3wiojd30b" (if (Single.NaN >= Single.NaN) then "a" else "b") "b"
    check "d3wiojd30c" ((Single.NaN < Single.NaN)) false
    check "d3wiojd30c" (if (Single.NaN < Single.NaN) then "a" else "b") "b"
    check "d3wiojd30d" ((Single.NaN <= Single.NaN)) false
    check "d3wiojd30d" (if (Single.NaN <= Single.NaN) then "a" else "b") "b"
    check "d3wiojd30e" ((Single.NaN = Single.NaN)) false
    check "d3wiojd30e" (if (Single.NaN = Single.NaN) then "a" else "b") "b"
    check "d3wiojd30q" ((Single.NaN <> Single.NaN)) true
    check "d3wiojd30w" ((Single.NaN > 1.0f)) false
    check "d3wiojd30e" ((Single.NaN >= 1.0f)) false
    check "d3wiojd30r" ((Single.NaN < 1.0f)) false
    check "d3wiojd30t" ((Single.NaN <= 1.0f)) false
    check "d3wiojd30y" ((Single.NaN = 1.0f)) false
    check "d3wiojd30u" ((Single.NaN <> 1.0f)) true
    check "d3wiojd30i" ((1.0f > Single.NaN)) false
    check "d3wiojd30o" ((1.0f >= Single.NaN)) false
    check "d3wiojd30p" ((1.0f < Single.NaN)) false
    check "d3wiojd30a" ((1.0f <= Single.NaN)) false
    check "d3wiojd30s" ((1.0f = Single.NaN)) false
    check "d3wiojd30d" ((1.0f <> Single.NaN)) true
    check "d3wiojd30a" ((nan1 > Single.NaN)) false
    check "d3wiojd30b" ((nan1 >= nan2)) false
    check "d3wiojd30c" ((nan1 < nan2)) false
    check "d3wiojd30d" ((nan1 <= nan2)) false
    check "d3wiojd30e" ((nan1 = nan2)) false
    check "d3wiojd30q" ((nan1 <> nan2)) true
    check "d3wiojd30w" ((nan1 > 1.0f)) false
    check "d3wiojd30e" ((nan1 >= 1.0f)) false
    check "d3wiojd30r" ((nan1 < 1.0f)) false
    check "d3wiojd30t" ((nan1 <= 1.0f)) false
    check "d3wiojd30y" ((nan1 = 1.0f)) false
    check "d3wiojd30u" ((nan1 <> 1.0f)) true
    check "d3wiojd30i" ((1.0f > nan2)) false
    check "d3wiojd30o" ((1.0f >= nan2)) false
    check "d3wiojd30p" ((1.0f < nan2)) false
    check "d3wiojd30a" ((1.0f <= nan2)) false
    check "d3wiojd30s" ((1.0f = nan2)) false
    check "d3wiojd30d" ((1.0f <> nan2)) true
    check "d3wiojd30f" ((Single.NegativeInfinity = Single.NegativeInfinity)) true
    check "d3wiojd30g" ((Single.NegativeInfinity < Single.PositiveInfinity)) true
    check "d3wiojd30h" ((Single.NegativeInfinity > Single.PositiveInfinity)) false
    check "d3wiojd30j" ((Single.NegativeInfinity <= Single.NegativeInfinity)) true

    check "S1nancompare01" (0 = (compare Single.NaN Single.NaN)) true
    check "S1nancompare02" (0 = (compare Single.NaN nan1)) true
    check "S1nancompare03" (0 = (compare nan1 Single.NaN)) true
    check "S1nancompare04" (0 = (compare nan1 nan1)) true
    check "S1nancompare05" (1 = (compare 1.f Single.NaN)) true
    check "S1nancompare06" (1 = (compare 0.f Single.NaN)) true
    check "S1nancompare07" (1 = (compare -1.f Single.NaN)) true
    check "S1nancompare08" (1 = (compare Single.NegativeInfinity Single.NaN)) true
    check "S1nancompare09" (1 = (compare Single.PositiveInfinity Single.NaN)) true
    check "S1nancompare10" (1 = (compare Single.MaxValue Single.NaN)) true
    check "S1nancompare11" (1 = (compare Single.MinValue Single.NaN)) true
    check "S1nancompare12" (-1 = (compare Single.NaN 1.f)) true
    check "S1nancompare13" (-1 = (compare Single.NaN 0.f)) true
    check "S1nancompare14" (-1 = (compare Single.NaN -1.f)) true
    check "S1nancompare15" (-1 = (compare Single.NaN Single.NegativeInfinity)) true
    check "S1nancompare16" (-1 = (compare Single.NaN Single.PositiveInfinity)) true
    check "S1nancompare17" (-1 = (compare Single.NaN Single.MaxValue)) true
    check "S1nancompare18" (-1 = (compare Single.NaN Single.MinValue)) true

module SingleNaNNonStructuralComparison1 = 
    open NonStructuralComparison
    
    let nan1 = (let r = ref Single.NaN in (if sprintf "Hello" = "Hello" then !r else 0.0f))
    let nan2 = (let r = ref Single.NaN in (if sprintf "Hello" = "Hello" then !r else 0.0f))
    
    check "d3wiojd30a" (if (Single.NaN > Single.NaN) then "a" else "b") "b"
    check "d3wiojd30b" ((Single.NaN >= Single.NaN)) false
    check "d3wiojd30b" (if (Single.NaN >= Single.NaN) then "a" else "b") "b"
    check "d3wiojd30c" ((Single.NaN < Single.NaN)) false
    check "d3wiojd30c" (if (Single.NaN < Single.NaN) then "a" else "b") "b"
    check "d3wiojd30d" ((Single.NaN <= Single.NaN)) false
    check "d3wiojd30d" (if (Single.NaN <= Single.NaN) then "a" else "b") "b"
    check "d3wiojd30e" ((Single.NaN = Single.NaN)) false
    check "d3wiojd30e" (if (Single.NaN = Single.NaN) then "a" else "b") "b"
    check "d3wiojd30q" ((Single.NaN <> Single.NaN)) true
    check "d3wiojd30w" ((Single.NaN > 1.0f)) false
    check "d3wiojd30e" ((Single.NaN >= 1.0f)) false
    check "d3wiojd30r" ((Single.NaN < 1.0f)) false
    check "d3wiojd30t" ((Single.NaN <= 1.0f)) false
    check "d3wiojd30y" ((Single.NaN = 1.0f)) false
    check "d3wiojd30u" ((Single.NaN <> 1.0f)) true
    check "d3wiojd30i" ((1.0f > Single.NaN)) false
    check "d3wiojd30o" ((1.0f >= Single.NaN)) false
    check "d3wiojd30p" ((1.0f < Single.NaN)) false
    check "d3wiojd30a" ((1.0f <= Single.NaN)) false
    check "d3wiojd30s" ((1.0f = Single.NaN)) false
    check "d3wiojd30d" ((1.0f <> Single.NaN)) true
    check "d3wiojd30a" ((nan1 > Single.NaN)) false
    check "d3wiojd30b" ((nan1 >= nan2)) false
    check "d3wiojd30c" ((nan1 < nan2)) false
    check "d3wiojd30d" ((nan1 <= nan2)) false
    check "d3wiojd30e" ((nan1 = nan2)) false
    check "d3wiojd30q" ((nan1 <> nan2)) true
    check "d3wiojd30w" ((nan1 > 1.0f)) false
    check "d3wiojd30e" ((nan1 >= 1.0f)) false
    check "d3wiojd30r" ((nan1 < 1.0f)) false
    check "d3wiojd30t" ((nan1 <= 1.0f)) false
    check "d3wiojd30y" ((nan1 = 1.0f)) false
    check "d3wiojd30u" ((nan1 <> 1.0f)) true
    check "d3wiojd30i" ((1.0f > nan2)) false
    check "d3wiojd30o" ((1.0f >= nan2)) false
    check "d3wiojd30p" ((1.0f < nan2)) false
    check "d3wiojd30a" ((1.0f <= nan2)) false
    check "d3wiojd30s" ((1.0f = nan2)) false
    check "d3wiojd30d" ((1.0f <> nan2)) true
    check "d3wiojd30f" ((Single.NegativeInfinity = Single.NegativeInfinity)) true
    check "d3wiojd30g" ((Single.NegativeInfinity < Single.PositiveInfinity)) true
    check "d3wiojd30h" ((Single.NegativeInfinity > Single.PositiveInfinity)) false
    check "d3wiojd30j" ((Single.NegativeInfinity <= Single.NegativeInfinity)) true

    check "S2nancompare01" (0 = (compare Single.NaN Single.NaN)) true
    check "S2nancompare02" (0 = (compare Single.NaN nan1)) true
    check "S2nancompare03" (0 = (compare nan1 Single.NaN)) true
    check "S2nancompare04" (0 = (compare nan1 nan1)) true
    check "S2nancompare05" (1 = (compare 1.f Single.NaN)) true
    check "S2nancompare06" (1 = (compare 0.f Single.NaN)) true
    check "S2nancompare07" (1 = (compare -1.f Single.NaN)) true
    check "S2nancompare08" (1 = (compare Single.NegativeInfinity Single.NaN)) true
    check "S2nancompare09" (1 = (compare Single.PositiveInfinity Single.NaN)) true
    check "S2nancompare10" (1 = (compare Single.MaxValue Single.NaN)) true
    check "S2nancompare11" (1 = (compare Single.MinValue Single.NaN)) true
    check "S2nancompare12" (-1 = (compare Single.NaN 1.f)) true
    check "S2nancompare13" (-1 = (compare Single.NaN 0.f)) true
    check "S2nancompare14" (-1 = (compare Single.NaN -1.f)) true
    check "S2nancompare15" (-1 = (compare Single.NaN Single.NegativeInfinity)) true
    check "S2nancompare16" (-1 = (compare Single.NaN Single.PositiveInfinity)) true
    check "S2nancompare17" (-1 = (compare Single.NaN Single.MaxValue)) true
    check "S2nancompare18" (-1 = (compare Single.NaN Single.MinValue)) true

module SingleNaNStructured = 
    type www = W of single

    let nan1 = (let r = ref Single.NaN in (if sprintf "Hello" = "Hello" then !r else 0.0f))
    let nan2 = (let r = ref Single.NaN in (if sprintf "Hello" = "Hello" then !r else 0.0f))
    
    do printf "checking floating point relational operators on structured data\n"
    // NOTE: SPECIFICATION: The relational operators work differently when applied to
    // floats embedded in structured data than when applied to raw floats. 

    let _ = check "d3wiojd31q" ((W Single.NaN > W Single.NaN)) false
    let _ = check "d3wiojd31w" ((W Single.NaN >= W Single.NaN)) false
    let _ = check "d3wiojd31e" ((W Single.NaN < W Single.NaN)) false
    let _ = check "d3wiojd31r" ((W Single.NaN <= W Single.NaN)) false
    let _ = check "d3wiojd31ty" ((W Single.NaN = W Single.NaN)) false
    let _ = check "d3wiojd31y" ((W Single.NaN <> W Single.NaN)) true
    let _ = check "d3wiojd31dy" (0 = compare (W Single.NaN) (W Single.NaN)) true
    let _ = check "d3wiojd31u" ((W Single.NaN > W 1.0f)) false
    let _ = check "d3wiojd31i" ((W Single.NaN >= W 1.0f)) false
    let _ = check "d3wiojd31o" ((W Single.NaN < W 1.0f)) false
    let _ = check "d3wiojd31p" ((W Single.NaN <= W 1.0f)) false
    let _ = check "d3wiojd31a" ((W Single.NaN = W 1.0f)) false
    let _ = check "d3wiojd31s" ((W Single.NaN <> W 1.0f)) true
    let _ = check "d3wiojd31d" ((W 1.0f > W Single.NaN)) false
    let _ = check "d3wiojd31f" ((W 1.0f >= W Single.NaN)) false
    let _ = check "d3wiojd31g" ((W 1.0f < W Single.NaN)) false
    let _ = check "d3wiojd31h" ((W 1.0f <= W Single.NaN)) false
    let _ = check "d3wiojd31j" ((W 1.0f = W Single.NaN)) false
    let _ = check "d3wiojd31k" ((W 1.0f <> W Single.NaN)) true
    let _ = check "d3wiojd31l" ((W Single.NegativeInfinity = W Single.NegativeInfinity)) true
    let _ = check "d3wiojd31c" ((W Single.NegativeInfinity < W Single.PositiveInfinity)) true
    let _ = check "d3wiojd3xx" ((W Single.NegativeInfinity > W Single.PositiveInfinity)) false
    let _ = check "d3wiojd31z" ((W Single.NegativeInfinity <= W Single.NegativeInfinity)) true

    let _ = check "S3nancompare01" (0 = (compare (W Single.NaN) (W Single.NaN))) true
    let _ = check "S3nancompare02" (0 = (compare (W Single.NaN) (W nan1))) true
    let _ = check "S3nancompare03" (0 = (compare (W nan1) (W Single.NaN))) true
    let _ = check "S3nancompare04" (0 = (compare (W nan1) (W nan1))) true
    let _ = check "S3nancompare05" (1 = (compare (W 1.f) (W Single.NaN))) true
    let _ = check "S3nancompare06" (1 = (compare (W 0.f) (W Single.NaN))) true
    let _ = check "S3nancompare07" (1 = (compare (W -1.f) (W Single.NaN))) true
    let _ = check "S3nancompare08" (1 = (compare (W Single.NegativeInfinity) (W Single.NaN))) true
    let _ = check "S3nancompare09" (1 = (compare (W Single.PositiveInfinity) (W Single.NaN))) true
    let _ = check "S3nancompare10" (1 = (compare (W Single.MaxValue) (W Single.NaN))) true
    let _ = check "S3nancompare11" (1 = (compare (W Single.MinValue) (W Single.NaN))) true
    let _ = check "S3nancompare12" (-1 = (compare (W Single.NaN) (W 1.f))) true
    let _ = check "S3nancompare13" (-1 = (compare (W Single.NaN) (W 0.f))) true
    let _ = check "S3nancompare14" (-1 = (compare (W Single.NaN) (W -1.f))) true
    let _ = check "S3nancompare15" (-1 = (compare (W Single.NaN) (W Single.NegativeInfinity))) true
    let _ = check "S3nancompare16" (-1 = (compare (W Single.NaN) (W Single.PositiveInfinity))) true
    let _ = check "S3nancompare17" (-1 = (compare (W Single.NaN) (W Single.MaxValue))) true
    let _ = check "S3nancompare18" (-1 = (compare (W Single.NaN) (W Single.MinValue))) true

module SingleNaNStructuredPoly = 
    type 'a www = W of 'a

    let nan1 = (let r = ref Single.NaN in (if sprintf "Hello" = "Hello" then !r else 0.0f))
    let nan2 = (let r = ref Single.NaN in (if sprintf "Hello" = "Hello" then !r else 0.0f))
    
    do printf "checking floating point relational operators on polymorphic structured data\n"

    let _ = check "d3wiojd32q" ((W Single.NaN > W Single.NaN)) false
    let _ = check "d3wiojd32w" ((W Single.NaN >= W Single.NaN)) false
    let _ = check "d3wiojd32e" ((W Single.NaN < W Single.NaN)) false
    let _ = check "d3wiojd32r" ((W Single.NaN <= W Single.NaN)) false
    let _ = check "d3wiojd32t" ((W Single.NaN = W Single.NaN)) false
    let _ = check "d3wiojd32dt" ((W Single.NaN).Equals(W Single.NaN)) true
    let _ = check "d3wiojd32y" ((W Single.NaN <> W Single.NaN)) true
    let _ = check "d3wiojd32u" ((W Single.NaN > W 1.0f)) false
    let _ = check "d3wiojd32i" ((W Single.NaN >= W 1.0f)) false
    let _ = check "d3wiojd32o" ((W Single.NaN < W 1.0f)) false
    let _ = check "d3wiojd32p" ((W Single.NaN <= W 1.0f)) false
    let _ = check "d3wiojd32a" ((W Single.NaN = W 1.0f)) false
    let _ = check "d3wiojd32s" ((W Single.NaN <> W 1.0f)) true
    let _ = check "d3wiojd32d" ((W 1.0f > W Single.NaN)) false
    let _ = check "d3wiojd32f" ((W 1.0f >= W Single.NaN)) false
    let _ = check "d3wiojd32g" ((W 1.0f < W Single.NaN)) false
    let _ = check "d3wiojd32h" ((W 1.0f <= W Single.NaN)) false
    let _ = check "d3wiojd32j" ((W 1.0f = W Single.NaN)) false
    let _ = check "d3wiojd32k" ((W 1.0f <> W Single.NaN)) true
    let _ = check "d3wiojd32l" ((W Single.NegativeInfinity = W Single.NegativeInfinity)) true
    let _ = check "d3wiojd32z" ((W Single.NegativeInfinity < W Single.PositiveInfinity)) true
    let _ = check "d3wiojd32x" ((W Single.NegativeInfinity > W Single.PositiveInfinity)) false
    let _ = check "d3wiojd32c" ((W Single.NegativeInfinity <= W Single.NegativeInfinity)) true

    let _ = check "S4nancompare01" (0 = (compare (W Single.NaN) (W Single.NaN))) true
    let _ = check "S4nancompare02" (0 = (compare (W Single.NaN) (W nan1))) true
    let _ = check "S4nancompare03" (0 = (compare (W nan1) (W Single.NaN))) true
    let _ = check "S4nancompare04" (0 = (compare (W nan1) (W nan1))) true
    let _ = check "S4nancompare05" (1 = (compare (W 1.f) (W Single.NaN))) true
    let _ = check "S4nancompare06" (1 = (compare (W 0.f) (W Single.NaN))) true
    let _ = check "S4nancompare07" (1 = (compare (W -1.f) (W Single.NaN))) true
    let _ = check "S4nancompare08" (1 = (compare (W Single.NegativeInfinity) (W Single.NaN))) true
    let _ = check "S4nancompare09" (1 = (compare (W Single.PositiveInfinity) (W Single.NaN))) true
    let _ = check "S4nancompare10" (1 = (compare (W Single.MaxValue) (W Single.NaN))) true
    let _ = check "S4nancompare11" (1 = (compare (W Single.MinValue) (W Single.NaN))) true
    let _ = check "S4nancompare12" (-1 = (compare (W Single.NaN) (W 1.f))) true
    let _ = check "S4nancompare13" (-1 = (compare (W Single.NaN) (W 0.f))) true
    let _ = check "S4nancompare14" (-1 = (compare (W Single.NaN) (W -1.f))) true
    let _ = check "S4nancompare15" (-1 = (compare (W Single.NaN) (W Single.NegativeInfinity))) true
    let _ = check "S4nancompare16" (-1 = (compare (W Single.NaN) (W Single.PositiveInfinity))) true
    let _ = check "S4nancompare17" (-1 = (compare (W Single.NaN) (W Single.MaxValue))) true
    let _ = check "S4nancompare18" (-1 = (compare (W Single.NaN) (W Single.MinValue))) true
    
module MoreStructuralEqHashCompareNaNChecks = 
    let test398275413() =
        let floats = [1.0; 0.0; System.Double.NaN; System.Double.NegativeInfinity; System.Double.PositiveInfinity; nan] in
        for x in floats do
          for y in floats do
            let xnan = System.Double.IsNaN(x) in
            let ynan = System.Double.IsNaN(y) in
            let test1 x y op b = if not b then (printfn "\n****failure on %A %s %A\n" x op y; reportFailure "unlabelled test") in
            if (xnan && not(ynan)) || (ynan && not(xnan)) then (
                
                let testEq x y = 
                    test1 x y "=" ((x = y) = false);
                    test1 x y "<>" ((x <> y) = true) in
                let testRel x y = 
                    test1 x y "<" ((x < y) = false );
                    test1 x y ">" ((x > y) = false );
                    test1 x y ">=" ((x >= y) = false);
                    test1 x y "<=" ((x <= y) = false) in
                testEq x y; 
                testEq [x] [y];
                testEq [| x |] [| y |];
                testEq (x,x) (y,y);
                testEq (x,1) (y,1);
                testEq (1,x) (1,y);

                testRel x y;

                testRel [x] [y];
                testRel [| x |] [| y |];
                testRel (x,x) (y,y);
                testRel (x,1) (y,1);
                testRel (1,x) (1,y);
            );
            if xnan && ynan  then
               test1 x y "compare" ((compare x y) = 0)
          done
        done            

    let _ = test398275413()

    let test398275414() =
        let floats = [1.0f; 0.0f; System.Single.NaN; System.Single.NegativeInfinity; System.Single.PositiveInfinity; nanf] in
        for x in floats do
          for y in floats do
            let xnan = System.Single.IsNaN(x) in
            let ynan = System.Single.IsNaN(y) in
            let test1 x y op b = if not b then (printfn "\n****failure on %A %s %A\n" x op y; reportFailure "unlabelled test") in
            if (xnan && not(ynan)) || (ynan && not(xnan)) then (
                
                let testEq x y = 
                    test1 x y "=" ((x = y) = false);
                    test1 x y "<>" ((x <> y) = true) in
                let testRel x y = 
                    test1 x y "<" ((x < y) = false );
                    test1 x y ">" ((x > y) = false );
                    test1 x y ">=" ((x >= y) = false);
                    test1 x y "<=" ((x <= y) = false) in
                testEq x y; 
                testEq [x] [y];
                testEq [| x |] [| y |];
                testEq (x,x) (y,y);
                testEq (x,1) (y,1);
                testEq (1,x) (1,y);

                testRel x y;

                testRel [x] [y];
                testRel [| x |] [| y |];
                testRel (x,x) (y,y);
                testRel (x,1) (y,1);
                testRel (1,x) (1,y);
            );
            if xnan && ynan  then
               test1 x y "compare" ((compare x y) = 0)
          done
        done            

    let _ = test398275414()
    
    type A<'a,'b> = {h : 'a ; w : 'b}
    type C<'T> = {n : string ; s : 'T}
    type D = {x : float ; y : float}
    type D2 = {x2 : float32 ; y2 : float32}
    exception E of float 
    exception E2 of float32
    type F = | F of float
    type F2 = | F2 of float32

    // Test ER semantics for obj.Equals and PER semantics for (=)
    let test398275415() =
    
        let l1 = [nan; 1.0] in
        let l2 = [nan; 1.0] in
        
        let a1 = [|nan; 1.0|] in
        let a2 = [|nan; 1.0|] in
        
        let t1 = (nan, 1.0) in
        let t2 = (nan, 1.0) in
        
        let d1 = {x=1.0;y=nan} in
        let d2 = {x=1.0;y=nan} in
        
        let e1 = E nan in
        let e2 = E nan in
        
        let f1 = F nan in
        let f2 = F nan in
        
        
        let j1 : C<A<float,float>> = {n="Foo" ; s={h=5.9 ; w=nan}} in
        let j2 : C<A<float,float>> = {n="Foo" ; s={h=5.9 ; w=nan}} in

        let jT1 = ("Foo", {h=5.9 ; w=nan}) in
        let jT2 = ("Foo", {h=5.9 ; w=nan}) in
        let id x = x in
        
        let testER x y f = if f(not(x.Equals(y))) then (printfn "\n****failure on %A %A\n" x y ; reportFailure "unlabelled test") in
        let testPER x y f = if f((x = y)) then (printfn "\n****failure on %A %A\n" x y ; reportFailure "unlabelled test") in
        
        testER l1 l2 id ;
        testER l2 l1 id ;
        testPER l1 l2 id ;
        testPER l2 l1 id ;
        
        testER a1 a2 not ;
        testER a2 a1 not ;
        testPER a1 a2 id ;
        testPER a2 a1 id ;
        
        testER t1 t2 id ;
        testER t2 t1 id ;
        testPER t1 t2 id ;
        testPER t2 t1 id ;
        
        testER j1 j2 id ;
        testER j2 j1 id ;
        testPER j1 j2 id ;
        testPER j2 j1 id ;
        
        testER jT1 jT2 id ;
        testER jT2 jT1 id ;
        testPER jT1 jT2 id ;
        testPER jT2 jT1 id ;
        
        testER d1 d2 id ;
        testER d2 d1 id ;
        testPER d1 d2 id ;
        testPER d2 d1 id ;
        
        testER e1 e2 id ;
        testER e2 e1 id ;
        testPER e1 e2 id ;
        testPER e2 e1 id ;
        
        testER f1 f2 id ;
        testER f2 f1 id ;
        testPER f1 f2 id ;
        testPER f2 f1 id       


    let _ = test398275415()

    // Test ER semantics for obj.Equals and PER semantics for (=)
    let test398275416() =
    
        let l1 = [nanf; 1.0f] in
        let l2 = [nanf; 1.0f] in
        
        let a1 = [|nanf; 1.0f|] in
        let a2 = [|nanf; 1.0f|] in
        
        let t1 = (nanf, 1.0f) in
        let t2 = (nanf, 1.0f) in
        
        let d1 = {x2=1.0f;y2=nanf} in
        let d2 = {x2=1.0f;y2=nanf} in
        
        let e1 = E2 nanf in
        let e2 = E2 nanf in
        
        let f1 = F2 nanf in
        let f2 = F2 nanf in        
        
        let j1 : C<A<float32,float32>> = {n="Foo" ; s={h=5.9f ; w=nanf}} in
        let j2 : C<A<float32,float32>> = {n="Foo" ; s={h=5.9f ; w=nanf}} in

        let jT1 = ("Foo", {h=5.9f ; w=nanf}) in
        let jT2 = ("Foo", {h=5.9f ; w=nanf}) in
        let id x = x in
        
        let testER x y f = if f(not(x.Equals(y))) then (printfn "\n****failure on %A %A\n" x y ; reportFailure "unlabelled test") in
        let testPER x y f = if f((x = y)) then (printfn "\n****failure on %A %A\n" x y ; reportFailure "unlabelled test") in
        
        testER l1 l2 id ;
        testER l2 l1 id ;
        testPER l1 l2 id ;
        testPER l2 l1 id ;
        
        testER a1 a2 not ;
        testER a2 a1 not ;
        testPER a1 a2 id ;
        testPER a2 a1 id ;
        
        testER t1 t2 id ;
        testER t2 t1 id ;
        testPER t1 t2 id ;
        testPER t2 t1 id ;
        
        testER j1 j2 id ;
        testER j2 j1 id ;
        testPER j1 j2 id ;
        testPER j2 j1 id ;
        
        testER jT1 jT2 id ;
        testER jT2 jT1 id ;
        testPER jT1 jT2 id ;
        testPER jT2 jT1 id ;
        
        testER d1 d2 id ;
        testER d2 d1 id ;
        testPER d1 d2 id ;
        testPER d2 d1 id ;
        
        testER e1 e2 id ;
        testER e2 e1 id ;
        testPER e1 e2 id ;
        testPER e2 e1 id ;
        
        testER f1 f2 id ;
        testER f2 f1 id ;
        testPER f1 f2 id ;
        testPER f2 f1 id
        
        
    let _ = test398275416()    
    


// This test tests basic behavior of IEquatable<T> and IComparable<T> augmentations     
module GenericComparisonAndEquality = begin
    open System.Collections.Generic
    open System
    
    // over records and unions   
    [<StructuralEquality ; StructuralComparison>]
    type UnionTypeA =
        | Foo of float
        | Int of int
        | Recursive of UnionTypeA

    [<StructuralEquality ; StructuralComparison>]
    type RecordTypeA<'T> = {f1 : string ; f2 : 'T}

    // IComparable<T>
    let _ = 
        
        let sl = SortedList<RecordTypeA<float>,string>() in
        sl.Add({f1="joj";f2=69.0},"prg") ;
        sl.Add({f1="bri";f2=68.0},"prg") ;
        sl.Add({f1="jom";f2=70.0},"prg") ;
        sl.Add({f1="tmi";f2=75.0},"lde") ;
        
        // add items to sl2 in a different order than sl1
        let sl2 = SortedList<RecordTypeA<float>,string>() in
        sl2.Add({f1="jom";f2=70.0},"prg") ;
        sl2.Add({f1="bri";f2=68.0},"prg") ;
        sl2.Add({f1="joj";f2=69.0},"prg") ;
        sl2.Add({f1="tmi";f2=75.0},"lde") ;
        
        let sl3 = SortedList<UnionTypeA, float>() in
        sl3.Add(Foo(2.0), 0.0) ;
        sl3.Add(Int(1), 1.0) ;
        sl3.Add(Recursive(Foo(3.0)),2.0) ;
        
        let sl4 = SortedList<UnionTypeA, float>() in
        sl4.Add(Foo(2.0), 0.0) ;
        sl4.Add(Int(1), 1.0) ;
        sl4.Add(Recursive(Foo(3.0)),2.0) ;
           

        let l1 = List.ofSeq sl.Keys in
        let l2 = List.ofSeq sl2.Keys in
        
        let l3 = List.ofSeq sl3.Keys in
        let l4 = List.ofSeq sl4.Keys in
        
        check "d3wiojd32icr" (l1 = l2) true ;
        check "d3wiojd32icu" (l3 = l4) true              

    // IEquatable<T>        
    let _ = 
        
        let l = List<RecordTypeA<float>>() in
        l.Add({f1="joj";f2=69.0}) ;
        l.Add({f1="bri";f2=68.0}) ;
        l.Add({f1="jom";f2=70.0}) ;
        l.Add({f1="tmi";f2=75.0}) ;
        
        let l2 = List<UnionTypeA>() in
        l2.Add(Foo(2.0)) ;
        l2.Add(Int(1)) ;
        l2.Add(Recursive(Foo(3.0))) ;
        
        check "d3wiojd32ier" (l.Contains({f1="joj";f2=69.0})) true ;
        check "d3wiojd32ieu" (l2.Contains(Recursive(Foo(3.0)))) true 

end


(*---------------------------------------------------------------------------
!* check optimizations 
 *--------------------------------------------------------------------------- *)

module Optimiations = begin

    let _ = check "opt.oi20c77u" (1 + 1) (2)
    let _ = check "opt.oi20c77i" (-1 + 1) (0)
    let _ = check "opt.oi20c77o" (1 + 2) (3)
    let _ = check "opt.oi20c77p" (2 + 1) (3)
    let _ = check "opt.oi20c77a" (1 * 0) (0)
    let _ = check "opt.oi20c77s" (0 * 1) (0)
    let _ = check "opt.oi20c77d" (2 * 2) (4)
    let _ = check "opt.oi20c77f" (2 * 3) (6)
    let _ = check "opt.oi20c77g" (-2 * 3) (-6)
    let _ = check "opt.oi20c77h" (1 - 2) (-1)
    let _ = check "opt.oi20c77j" (2 - 1) (1)

    let _ = check "opt.oi20c77uL" (1L + 1L) (2L)
    let _ = check "opt.oi20c77iL" (-1L + 1L) (0L)
    let _ = check "opt.oi20c77oL" (1L + 2L) (3L)
    let _ = check "opt.oi20c77pL" (2L + 1L) (3L)
    let _ = check "opt.oi20c77aL" (1L * 0L) (0L)
    let _ = check "opt.oi20c77sL" (0L * 1L) (0L)
    let _ = check "opt.oi20c77dL" (2L * 2L) (4L)
    let _ = check "opt.oi20c77fL" (2L * 3L) (6L)
    let _ = check "opt.oi20c77gL" (-2L * 3L) (-6L)
    let _ = check "opt.oi20c77hL" (1L - 2L) (-1L)
    let _ = check "opt.oi20c77jL" (2L - 1L) (1L)

    let _ = check "opt.oi20cnq" (1 <<< 0) (1)
    let _ = check "opt.oi20cnw" (1 <<< 1) (2)
    let _ = check "opt.oi20cne" (1 <<< 2) (4)
    let _ = check "opt.oi20cnr" (1 <<< 31) (0x80000000)
    let _ = check "opt.oi20cnt" (1 <<< 32) (1)
    let _ = check "opt.oi20cny" (1 <<< 33) (2)
    let _ = check "opt.oi20cnu" (1 <<< 63) (0x80000000)

    let _ = check "or.oi20cnq" (1 ||| 0) (1)
    let _ = check "or.oi20cnw" (1 ||| 1) (1)
    let _ = check "or.oi20cne" (1 ||| 2) (3)
    let _ = check "or.oi20cnr" (0x80808080 ||| 0x08080808) (0x88888888)
    let _ = check "or.oi20cnr" (0x8080808080808080L ||| 0x0808080808080808L) (0x8888888888888888L)

    let _ = check "and.oi20cnq" (1 &&& 0) (0)
    let _ = check "and.oi20cnw" (1 &&& 1) (1)
    let _ = check "and.oi20cne" (1 &&& 2) (0)
    let _ = check "and.oi20cnr" (0x80808080 &&& 0x08080808) (0)
    let _ = check "and.oi20cnr" (0x8080808080808080L &&& 0x0808080808080808L) (0L)

    let _ = check "opt.oi20cna" (1L <<< 0) (1L)
    let _ = check "opt.oi20cns" (1L <<< 1) (2L)
    let _ = check "opt.oi20cnd" (1L <<< 2) (4L)
    let _ = check "opt.oi20cnf" (1L <<< 31) (0x80000000L)
    let _ = check "opt.oi20cng" (1L <<< 32) (0x100000000L)
    let _ = check "opt.oi20cnh" (1L <<< 63) (0x8000000000000000L)
    let _ = check "opt.oi20cnj" (1L <<< 64) (1L)
    let _ = check "opt.oi20cnk" (1L <<< 127) (0x8000000000000000L)

    let _ = check "opt.oi20cnza" (0x80000000l >>> 0) (0x80000000)
    let _ = check "opt.oi20cnxa" (0x80000000l >>> 1) (0xC0000000)
    let _ = check "opt.oi20cnca" (0x80000000l >>> 31) (0xFFFFFFFF)
    let _ = check "opt.oi20cnva" (0x80000000l >>> 32) (0x80000000)

    let _ = check "opt.oi20cnzb" (0x80000000ul >>> 0) (0x80000000ul)
    let _ = check "opt.oi20cnxb" (0x80000000ul >>> 1) (0x40000000ul)
    let _ = check "opt.oi20cncb" (0x80000000ul >>> 31) (1ul)
    let _ = check "opt.oi20cnvb" (0x80000000ul >>> 32) (0x80000000ul)

    let _ = check "opt.oi20c77qa" (0x80000000UL >>> 0) (0x80000000UL)
    let _ = check "opt.oi20c77wa" (0x80000000UL >>> 1) (0x40000000UL)
    let _ = check "opt.oi20c77ea" (0x80000000UL >>> 31) (1UL)
    let _ = check "opt.oi20c77ra" (0x80000000UL >>> 32) (0UL)
    let _ = check "opt.oi20c77ta" (0x8000000000000000UL >>> 63) (1UL)
    let _ = check "opt.oi20c77ya" (0x8000000000000000UL >>> 64) (0x8000000000000000UL)

    let _ = check "opt.oi20c77qb" (0x80000000L >>> 0) (0x80000000L)
    let _ = check "opt.oi20c77wb" (0x80000000L >>> 1) (0x40000000L)
    let _ = check "opt.oi20c77ebb" (0x80000000L >>> 31) (1L)
    let _ = check "opt.oi20c77rb" (0x80000000L >>> 32) (0L)
    let _ = check "opt.oi20c77tb" (0x8000000000000000L >>> 63) (0xFFFFFFFFFFFFFFFFL)
    let _ = check "opt.oi20c77yb" (0x8000000000000000L >>> 64) (0x8000000000000000L)

    let _ = check "opt.oi20c77qc" ('a' + '\025') ('z')
    let _ = check "opt.oi20c77wc" ('z' - '\025') ('a')
    let _ = check "opt.oi20c77ec" (nativeint -3m) (-3n)
    let _ = check "opt.oi20c77rc" (nativeint 3m) (3n)
    let _ = check "opt.oi20c77tc" (unativeint 3m) (3un)
    let _ = check "opt.oi20c77yc" (char 65535m) ('\uFFFF')
    let _ = check "opt.oi20c77uc" (decimal '\uFFFF') (65535m)
    let _ = check "opt.oi20c77ic" (nativeint "3") (3n)
    let _ = check "opt.oi20c77oc" (nativeint "-3") (-3n)
    let _ = check "opt.oi20c77pc" (unativeint "3") (3un)
    let _ = check "opt.oi20c77ac" (Checked.(+) 'a' '\025') ('z')
    let _ = check "opt.oi20c77sc" (Checked.(-) 'z' '\025') ('a')
    let _ = check "opt.oi20c77dc" (Checked.nativeint -3m) (-3n)
    let _ = check "opt.oi20c77fc" (Checked.nativeint 3m) (3n)
    let _ = check "opt.oi20c77gc" (Checked.unativeint 3m) (3un)
    let _ = check "opt.oi20c77hc" (Checked.char 65535m) ('\uFFFF')
    let _ = check "opt.oi20c77jc" (Checked.nativeint "3") (3n)
    let _ = check "opt.oi20c77kc" (Checked.nativeint "-3") (-3n)
    let _ = check "opt.oi20c77lc" (Checked.unativeint "3") (3un)
    let _ = check "opt.oi20c77zc" (int8 3.9m) (3y)
    let _ = check "opt.oi20c77xc" (uint8 3.9m) (3uy)
    let _ = check "opt.oi20c77cc" (int16 3.9m) (3s)
    let _ = check "opt.oi20c77vc" (uint16 3.9m) (3us)
    let _ = check "opt.oi20c77bc" (int32 3.9m) (3l)
    let _ = check "opt.oi20c77nc" (uint32 3.9m) (3ul)
    let _ = check "opt.oi20c77mc" (int64 3.9m) (3L)
    let _ = check "opt.oi20c77,c" (uint64 3.9m) (3uL)
    let _ = check "opt.oi20c77.c" (nativeint 3.9m) (3n)
    let _ = check "opt.oi20c77/c" (unativeint 3.9m) (3un)
    let _ = check "opt.oi20c77zc'" (Checked.int8 3.9m) (3y)
    let _ = check "opt.oi20c77xc'" (Checked.uint8 3.9m) (3uy)
    let _ = check "opt.oi20c77cc'" (Checked.int16 3.9m) (3s)
    let _ = check "opt.oi20c77vc'" (Checked.uint16 3.9m) (3us)
    let _ = check "opt.oi20c77bc'" (Checked.int32 3.9m) (3l)
    let _ = check "opt.oi20c77nc'" (Checked.uint32 3.9m) (3ul)
    let _ = check "opt.oi20c77mc'" (Checked.int64 3.9m) (3L)
    let _ = check "opt.oi20c77,c'" (Checked.uint64 3.9m) (3uL)
    let _ = check "opt.oi20c77.c'" (Checked.nativeint 3.9m) (3n)
    let _ = check "opt.oi20c77/c'" (Checked.unativeint 3.9m) (3un)

end


(*---------------------------------------------------------------------------
!* BUG 868: repro - mod_float
 *--------------------------------------------------------------------------- *)

let mod_float (x:float) (y:float) = x % y

do check "mod_floatvrve" (mod_float  3.0  2.0)  1.0
do check "mod_float3121" (mod_float  3.0 -2.0)  1.0
do check "mod_float2e12" (mod_float -3.0  2.0)  -1.0
do check "mod_floatve23" (mod_float -3.0 -2.0)  -1.0    
do check "mod_floatvr24" (mod_float  3.0  1.0)  0.0
do check "mod_floatcw34" (mod_float  3.0 -1.0)  0.0


(*---------------------------------------------------------------------------
!* misc tests of IEnumerable functions
 *--------------------------------------------------------------------------- *)

module Seq = 

    let generate openf compute closef = 
        seq { let r = openf() 
              try 
                let mutable x = None
                while (x <- compute r; x.IsSome) do
                    yield x.Value
              finally
                 closef r }

module MiscIEnumerableTests = begin

    open System.Net
    open System.IO

#if !NETCOREAPP
    /// generate the sequence of lines read off an internet connection
    let httpSeq (nm:string) = 
           Seq.generate 
             (fun () -> new StreamReader(((WebRequest.Create(nm)).GetResponse()).GetResponseStream()) ) 
             (fun os -> try Some(os.ReadLine()) with _ -> None) 
             (fun os -> os.Close())
#endif

    /// generate an infinite sequence using an functional cursor
    let dataSeq1 = Seq.unfold (fun s -> Some(s,s+1)) 0

    /// generate an infinite sequence using an imperative cursor
    let dataSeq2 = Seq.generate 
                      (fun () -> ref 0) 
                      (fun r -> r := !r + 1; Some(!r)) 
                      (fun r -> ())
end


(*---------------------------------------------------------------------------
!* systematic tests of IEnumerable functions
 *--------------------------------------------------------------------------- *)

(* Assertive IEnumerators should:
   a) fail if .Current is called before .MoveNext().
   b) return the items in order.
   c) allow for calling .Current multiple times without "effects".
   d) fail if .Current is called after .MoveNext() returned false.
   e) fail if .MoveNext is called after it returned false.
*)   


let expectFailure desc thunk = try thunk(); printf "expectFailure: no exn from %s " desc; reportFailure "unlabelled test" with e -> ()

open System.Collections.Generic

let checkIEnumerable (ie:'a System.Collections.Generic.IEnumerable) =
  let e = ie.GetEnumerator() in e.Dispose()
  let e = ie.GetEnumerator() in
  expectFailure "checkIEnumerable: current before next" (fun () -> e.Current |> ignore);
  expectFailure "checkIEnumerable: current before next" (fun () -> e.Current |> ignore);
  let mutable ritems = [] in
  while e.MoveNext() do
    let xA = e.Current in
    let xB = e.Current in
    test "vwnwer" (xA = xB);
    ritems <- xA :: ritems
  done;
  expectFailure "checkIEnumerable: .Current  should fail after .MoveNext() return false" (fun () -> e.Current |> ignore);
  test "vwnwer" (e.MoveNext() = false);
  //expectFailure "checkIEnumerable: .MoveNext should fail after .MoveNext() return false" (fun () -> e.MoveNext());
  (* again! *)
  expectFailure "checkIEnumerable: .Current  should fail after .MoveNext() return false" (fun () -> e.Current |> ignore); 
  test "vwnwer" (e.MoveNext() = false); 
  //expectFailure "checkIEnumerable: .MoveNext should fail after .MoveNext() return false" (fun () -> e.MoveNext());
  List.rev ritems

let xxs  = [  0;1;2;3;4;5;6;7;8;9  ]
let xxa  = [| 0;1;2;3;4;5;6;7;8;9 |]  
let xie = Seq.ofArray xxa
let verify = test ""

do verify(xxs = checkIEnumerable xie)
do printf "Test c2eh2\n"; stdout.Flush(); let pred x = x<4                 in verify(List.choose (fun x -> if pred x then Some x else None)  xxs = checkIEnumerable (Seq.choose (fun x -> if pred x then Some x else None) xie))
do printf "Test c2e23ch2\n"; stdout.Flush();  let pred x = x<4                 in verify(List.filter pred xxs = checkIEnumerable (Seq.filter pred xie))
do printf "Test cc42eh2\n"; stdout.Flush();  let pred x = x%3=0               in verify(List.filter pred xxs = checkIEnumerable (Seq.filter pred xie))
do printf "Test c2f3eh2\n"; stdout.Flush();  let pred x = x%3=0               in verify(List.choose (fun x -> if pred x then Some x else None)  xxs = checkIEnumerable (Seq.choose (fun x -> if pred x then Some x else None) xie))
do printf "Test c2eh2\n"; stdout.Flush();  let pred x = x>100               in verify(List.filter pred xxs = checkIEnumerable (Seq.filter pred xie))
do printf "Test c2egr3h2\n"; stdout.Flush();  let pred x = x>100               in verify(List.choose (fun x -> if pred x then Some x else None)  xxs = checkIEnumerable (Seq.choose (fun x -> if pred x then Some x else None) xie))
do printf "Test c2eh2\n"; stdout.Flush();  let f   x  = x*2                 in verify(List.map  f xxs = checkIEnumerable (Seq.map  f xie))
// disabling this guy for now, as it's failing
// do printf "Test cvaw2eh2\n"; stdout.Flush(); verify ([ 2;3 ] = checkIEnumerable (Seq.generate (fun () -> ref 1) (fun r -> incr r; if !r > 3 then None else Some(!r)) (fun r -> ())))
do printf "Test c2r5eh2\n"; stdout.Flush();  let f i x  = x*20+i              in verify(List.mapi f xxs = checkIEnumerable (Seq.mapi f xie))
do printf "Test c2vreeh2\n"; stdout.Flush();  let f _ x  = x*x                 in verify(List.map2 f xxs xxs = checkIEnumerable (Seq.map2 f xie xie))
do printf "Test c2vreeh2\n"; stdout.Flush();  let f _ x  = x*x                 in verify(List.map2 f xxs xxs = checkIEnumerable (Seq.map2 f xie xie))
do let f _ x  = x*x                 in verify(List.concat [] = checkIEnumerable (Seq.concat [| |]))
do let f _ x  = x*x                 in verify(List.concat [xxs] = checkIEnumerable (Seq.concat [|  xie |]))
do let f _ x  = x*x                 in verify(List.concat [xxs;xxs] = checkIEnumerable (Seq.concat [| xie; xie |]))
do let f _ x  = x*x                 in verify(List.concat [xxs;xxs;xxs] = checkIEnumerable (Seq.concat [| xie; xie;xie |]))
do printf "Test c25reh2\n"; stdout.Flush();  let f _ x  = x*x                 in verify(List.append xxs xxs = checkIEnumerable (Seq.append xie xie))
do printf "Test c27mog7keh2\n"; stdout.Flush();  let f x    = if x%2 =0 then Some x
                                                              else None in verify(List.choose f xxs = checkIEnumerable (Seq.choose f xie))
do printf "Test c2e8,h2\n"; stdout.Flush();  let f z x  = (z+1) * x % 1397    in verify(List.fold f 2 xxs = Seq.fold f 2 xie)
do printfn "seq reduce"; if Seq.reduce (fun x y -> x/y) [5*4*3*2; 4;3;2;1] = 5 then stdout.WriteLine "YES" else  reportFailure "basic test Q"

do printf "Test c2grgeh2\n"; stdout.Flush();  verify(List.item 3 xxs = Seq.item 3 xie)


(*---------------------------------------------------------------------------
!* record effect order
 *--------------------------------------------------------------------------- *)

let last = ref (-1)
let increasing n = if !last < n then ( last := n; n ) else (printf "increasing failed for %d\n" n; reportFailure "unlabelled test"; n)

do increasing 0 |> ignore
do increasing 1 |> ignore

type recordAB = { a : int; b : int }

let ab1 = {a = increasing 2;
           b = increasing 3;}

let ab2 = {b = increasing 4;
           a = increasing 5;
          }

type recordABC = { mutable a : int; b : int; c : int }

do printf "abc1a\n"
let abc1a = {a = increasing 6;
             b = increasing 7;
             c = increasing 8;}

do printf "abc1b\n"  
let abc1b = {b = increasing 9;
             c = increasing 10;
             a = increasing 11;}

do printf "abc1c\n"    
let abc1c = {c = increasing 12;
             a = increasing 13;
             b = increasing 14;
            }

do printf "abc2a\n"      
let abc2a = {abc1a with
               b = increasing 15;
               c = increasing 16;}

do printf "abc2b\n"        
let abc2b = {abc1a with
               c = increasing 17;
               a = increasing 18;}

do printf "abc2c\n"          
let abc2c = {abc1a with
               a = increasing 19;
               b = increasing 20;}

module FloatParseTests = begin
    let to_bits (x:float) = System.BitConverter.DoubleToInt64Bits(x)
    let of_bits (x:int64) = System.BitConverter.Int64BitsToDouble(x)

    let to_string (x:float) = (box x).ToString()
    let of_string (s:string) = 
      (* Note System.Double.Parse doesn't handle -0.0 correctly (it returns +0.0) *)
      let s = s.Trim()  
      let l = s.Length 
      let p = 0 
      let p,sign = if (l >= p + 1 && s.[p] = '-') then 1,false else 0,true 
      let n = 
        try 
          if p >= l then raise (new System.FormatException()) 
          System.Double.Parse(s.[p..],System.Globalization.CultureInfo.InvariantCulture)
        with :? System.FormatException -> failwith "Float.of_string"
      if sign then n else -n

    do check "FloatParse.1" (to_bits (of_string "0.0")) 0L
    do check "FloatParse.0" (to_bits (of_string "-0.0"))      0x8000000000000000L // (-9223372036854775808L)
    do check "FloatParse.2" (to_bits (of_string "-1E-127"))   0xa591544581b7dec2L // (-6516334528322609470L)
    do check "FloatParse.3" (to_bits (of_string "-1E-323"))   0x8000000000000002L // (-9223372036854775806L)
    do check "FloatParse.4" (to_bits (of_string "-1E-324"))   0x8000000000000000L // (-9223372036854775808L)
    do check "FloatParse.5" (to_bits (of_string "-1E-325"))   0x8000000000000000L // (-9223372036854775808L)
    do check "FloatParse.6" (to_bits (of_string "1E-325")) 0L
    do check "FloatParse.7" (to_bits (of_string "1E-322")) 20L
    do check "FloatParse.8" (to_bits (of_string "1E-323")) 2L
    do check "FloatParse.9" (to_bits (of_string "1E-324")) 0L
    do check "FloatParse.A" (to_bits (of_string "Infinity"))  0x7ff0000000000000L // 9218868437227405312L
    do check "FloatParse.B" (to_bits (of_string "-Infinity")) 0xfff0000000000000L // (-4503599627370496L)
    do check "FloatParse.C" (to_bits (of_string "NaN"))       0xfff8000000000000L  // (-2251799813685248L)
#if !NETCOREAPP
    do check "FloatParse.D" (to_bits (of_string "-NaN"))    ( // http://en.wikipedia.org/wiki/NaN
                                                              let bit64 = System.IntPtr.Size = 8 in
                                                              if bit64 && System.Environment.Version.Major < 4 then
                                                                  // 64-bit (on NetFx2.0) seems to have same repr for -nan and nan
                                                                  0xfff8000000000000L // (-2251799813685248L)
                                                              else
                                                                  // 64-bit (on NetFx4.0) and 32-bit (any NetFx) seems to flip the sign bit on negation.
                                                                  // However:
                                                                  // it seems nan has the negative-bit set from the start,
                                                                  // and -nan then has the negative-bit cleared!
                                                                  0x7ff8000000000000L // 9221120237041090560L
                                                            )
#endif
end


(*---------------------------------------------------------------------------
!* BUG 709: repro
 *--------------------------------------------------------------------------- *)
(*
// Currently disabled, because IStructuralEquatable.GetHashCode does not support limited hashing
module CyclicHash = begin
    type cons = {x : int; mutable xs : cons option}
    let cycle n =
      let start = {x = 0; xs = None} in
      let rec loop cell i =
        if i >= n then
          cell.xs <- Some start
        else (
          let next = {x = i; xs = None} in
          cell.xs <- Some next;
          loop next (i+1)
        )
      in
      loop start 1;
      start

    type 'a nest = Leaf of 'a | Nest of 'a nest
    let rec nest n x = if n>0 then Nest(nest (n-1) x) else Leaf x

    let xs = Array.init 100 (fun n -> cycle n)
    let n = 1

    do
      for n = 1 to 200 do // <--- should exceed max number of nodes used to hash
        printf "Hashing array of cyclic structures in %d nest = %d\n" n (hash (nest n xs))
      done

end
*)


(*---------------------------------------------------------------------------
!* BUG 701: possible repro
 *--------------------------------------------------------------------------- *)

(*
#r "dnAnalytics.dll"
open dnAnalytics.LinearAlgebra

//   Matrix.op_multiply : Matrix * Vector -> Vector
//   Matrix.op_multiply : Matrix * Matrix -> Matrix
//   Matrix.op_multiply : float  * Matrix -> Matrix
//   Matrix.op_multiply : Matrix * float  -> Matrix
//   etc...

let gpPredict (kinv:Matrix) (cx:Vector) = 
        let tmpA = Matrix.op_Multiply(kinv,cx) in
        let tmpB = kinv * cx in
        12
29/09/2006 06:43        Resolved as Fixed by dsyme
fixed by reverting change in 1.1.12.3
*)

(*

BUG:
type Vector = VECTOR
type Matrix = class
  val i : int
  new i = {i=i}
  static member ( * )((x:Matrix),(y:Vector)) = (y:Vector)
  static member ( * )((x:Matrix),(y:Matrix)) = (y:Matrix)
  static member ( * )((x:float),(y:Matrix)) = (y:Matrix)
  static member ( * )((x:Matrix),(y:float)) = (x:Matrix)   
end

let gpPredict (kinv:Matrix) (cx:Vector) = 
        let tmpA = Matrix.op_Multiply(kinv,cx) in
        let tmpB = kinv * cx in
        12
*)


(*---------------------------------------------------------------------------
!* BUG 737: repro - do not expand sharing in large constants...
 *--------------------------------------------------------------------------- *)
    
module BigMamaConstants = begin
    type constant = Leaf of string | List of constant list
    let leaf s = Leaf s
    let list xs  = List xs
    let constant_test () =
     let a,b,c = leaf "a",leaf "b",leaf "c" in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     let a,b,c = list [a;b],list [b;c],list [c;a] in
     a,b,c

end

module StringForAll = begin
    let _ = "123" |> String.forall (fun c ->  System.Char.IsDigit(c)) |> check "4287fejlk2" true
    let _ = "123a" |> String.forall (fun c ->  System.Char.IsDigit(c))  |> check "4287f62k2" false
    let _ = "123a1" |> String.forall (fun c ->  System.Char.IsDigit(c))  |> check "4287wfg2ejlk2" false
    let _ = "" |> String.forall (fun c ->  System.Char.IsDigit(c))  |> check "4287fbt42" true
    let _ = "123" |> String.exists (fun c ->  System.Char.IsDigit(c))  |> check "428btwt4ejlk2" true
    let _ = "123a" |> String.exists (fun c ->  System.Char.IsDigit(c))  |> check "42bwejlk2" true
    let _ = "123a1" |> String.exists (fun c ->  System.Char.IsDigit(c))  |> check "42b4wt4btwejlk2" true
    let _ = "" |> String.exists (fun c ->  System.Char.IsDigit(c))  |> check "428bt4w2" false
    let _ = "a" |> String.exists (fun c ->  System.Char.IsDigit(c))  |> check "428b4wtw4jlk2" false

end
   
module RecordLabelTest1 = begin
 type r = { a: int; b : int }
 let f x = { x with a = 3 }
end

module RecordLabelTest2 = begin
 module SomeOtherPath = begin
    type r = { a: int; b : int }
 end
 let f1 x = { x with SomeOtherPath.a = 3 }
 let f2 x = { x with SomeOtherPath.r.a = 3 }
 let f3 (x:SomeOtherPath.r) = { x with a = 3 }
 open SomeOtherPath
 let f4 (x:r) = { x with a = 3 }
 let f5 x = { x with a = 3 }
end


(*---------------------------------------------------------------------------
!* set union - timings w.r.t. "union by fold"
 *--------------------------------------------------------------------------- *)

module SetTests = begin
    let union0 xs ys = Set.fold (fun ys x -> Set.add x ys) ys xs 
    let union1 xs ys = Set.union xs ys

    let randomInts n =
      let r = new System.Random() in
      let rec collect s n = if n>0 then collect (Set.add (r.Next()) s) (n-1) else s in
      collect Set.empty n
    let rec rapp2 n f x y = if n=1 then f x y else (let z = f x y in rapp2 (n-1) f x y)

    let time f =
      let sw = new System.Diagnostics.Stopwatch() in
      sw.Reset(); sw.Start();
      let res = f() in
      float sw.ElapsedMilliseconds / 1000.0

    let unionTest n (nx,ny) =
      let check (xs:'a Set) = 
          xs in
      let xs = randomInts nx |> check in
      let ys = randomInts ny |> check in
      (* time union ops *)
      let t0 = time (fun () -> rapp2 n union0 xs ys) in
      let t1 = time (fun () -> rapp2 n union1 xs ys) in
      test "vwnwer" (Set.toList (union0 xs ys |> check) = Set.toList (union1 xs ys |> check));
      printf "-- Union times: (fold = %.6f) (divquonq = %.6f) with t0 = %f on sizes %8d,%-8d and x %d\n" (t0/t0) (t1/t0) t0 nx ny n;

      let test_fold() =
          let m = Set.ofList [for i in 1..20 -> i] in
          test "fold 1" (Set.fold (fun acc _ -> acc + 1) 0 m = 20);
          test "fold 2" (Set.foldBack (fun _ acc -> acc + 1) m 0 = 20);
          test "fold 3" (Set.fold (fun acc n -> acc + " " + string n) "0" m = String.concat " " [for i in 0..20 -> string i]);
          test "fold 4" (Set.foldBack (fun n acc -> acc + " " + string n) m "21" = String.concat " " [for i in 21..-1..1 -> string i]);
          let mmax x y = if x > y then x else y in
          test "fold 5" (Set.foldBack mmax m 0 = 20);
          test "fold 6" (m |> Set.fold mmax 0 = 20);
      in test_fold ()


    (* A sample of set sizes for timings *)    
    do unionTest 500   (100   ,100)
    do unionTest 500    (100  ,10000)
    do unionTest 500    (100  ,100)
    do unionTest 50     (1000 ,1000)
    #if PERF
    do unionTest 5      (10000,10000)
    #endif
    do unionTest 2000  (10   ,10)
    do unionTest 20000 (5    ,5)
    do unionTest 20000 (5    ,10)
    do unionTest 20000 (5    ,20)
    do unionTest 20000 (5    ,30)
    #if PERF
    do unionTest 900000 (4     ,8)
    do unionTest 900000 (2     ,10)
    #endif
    do unionTest 5000   (100   ,10000)
end

let checkEqualInt x y = if x<>y then (printf "%d <> %d" x y; reportFailure "unlabelled test")
do checkEqualInt 1  (int32 1u)
do checkEqualInt 12 (int32 12u)
do checkEqualInt 1  (int 1uy)
do checkEqualInt 12 (int 12uy)

(*---------------------------------------------------------------------------
!* set/map filter: was bug
 *--------------------------------------------------------------------------- *)
do printf "FilterTests: next:\n"; stdout.Flush()
module FilterTests = begin
    do printf "FilterTests: start:\n"          
    let check s e r = 
      if r = e then  stdout.WriteLine (s^": YES") 
      else (stdout.WriteLine ("\n***** "^s^": FAIL\n"); reportFailure "basic test Q")
        
    let degen() = 
        let map = 
            Map.add 1 () 
             (Map.add 2 () 
               (Map.add 3 () 
                 (Map.add 4 () 
                   (Map.add 5 () Map.empty)))) in
        let map2 = Map.filter (fun i () -> i < 3) map in
        Map.toList map2 |> List.map fst
    do  check "Map.filter (degen)" (degen()) [1;2]


    let checkFilters pred xs =
      let xz = Set.ofList xs in
      let xz = Set.filter pred xz in
      let xz = Set.toList xz in
      let xm = Map.ofList (List.map (fun x -> (x,x)) xs) in
      let xm = Map.filter (fun x y -> pred x) xm in
      let xm = Map.toList xm |> List.map fst in
      let xs = List.filter pred xs in
      check "Set.filter" (List.sort xs) (List.sort xz);
      check "Set.filter" (List.sortWith Operators.compare xs) (List.sortWith Operators.compare xz);
      check "Map.filter" (List.sort xs) (List.sort xm);
      check "Map.filter" (List.sortWith Operators.compare xs) (List.sortWith Operators.compare xm)


    do for i = 0 to 100 do
        printf "Checking %d " i; checkFilters (fun x -> x<20) [0..i]
       done

    do printf "FilterTests: end:\n"
end



let _ = assert(1=1) 


(*---------------------------------------------------------------------------
!* Bug 1028: conversion functions like int32 do not accept strings, suggested by Mort.
 *--------------------------------------------------------------------------- *)

do printf "Check bug 1028: conversion functions like int32 do not accept strings, suggested by Mort.\n"
do check "1028: byte"   (byte   "123") 123uy
do check "1028: sbyte"  (sbyte  "123") 123y
do check "1028: int16"  (int16  "123") 123s
do check "1028: uint16" (uint16 "123") 123us
do check "1028: int32"  (int32  "123") 123
do check "1028: uint32" (uint32 "123") 123u
do check "1028: int64"  (int64  "123") 123L
do check "1028: uint64" (uint64 "123") 123uL

do check "coiewj01" (char 32) ' '
do check "coiewj02" (char 32.0) ' '
do check "coiewj03" (char 32.0f) ' '
do check "coiewj04" (char 32uy) ' '
do check "coiewj05" (char 32y) ' '
do check "coiewj06" (char 32s) ' '
do check "coiewj07" (char 32us) ' '
do check "coiewj08" (char 32L) ' '
do check "coiewj09" (char 32UL) ' '
do check "coiewj0q" (char 32u) ' '
do check "coiewj0w" (char 32n) ' '
do check "coiewj0e" (char 32un) ' '


do check "coiewj0r" (Checked.char 32) ' '
do check "coiewj0t" (Checked.char 32.0) ' '
do check "coiewj0y" (Checked.char 32.0f) ' '
do check "coiewj0u" (Checked.char 32uy) ' '
do check "coiewj0i" (Checked.char 32y) ' '
do check "coiewj0o" (Checked.char 32s) ' '
do check "coiewj0p" (Checked.char 32us) ' '
do check "coiewj0a" (Checked.char 32L) ' '
do check "coiewj0s" (Checked.char 32UL) ' '
do check "coiewj0d" (Checked.char 32u) ' '
do check "coiewj0f" (Checked.char 32n) ' '
do check "coiewj0g" (Checked.char 32un) ' '

do check "coiewj0z" (try Checked.char (-1) with _ -> ' ') ' '
do check "coiewj0x" (try Checked.char (-1.0) with _ -> ' ') ' '
do check "coiewj0c" (try Checked.char (-1.0f) with _ -> ' ') ' '
do check "coiewj0v" (try Checked.char (-1y) with _ -> ' ') ' '
do check "coiewj0b" (try Checked.char (-1s) with _ -> ' ') ' '
do check "coiewj0n" (try Checked.char (-1L) with _ -> ' ') ' '
do check "coiewj0m" (try Checked.char (-1n) with _ -> ' ') ' '


do check "coiewj0aa" (try Checked.char (0x10000) with _ -> ' ') ' '
do check "coiewj0ss" (try Checked.char (65537.0) with _ -> ' ') ' '
do check "coiewj0dd" (try Checked.char (65537.0f) with _ -> ' ') ' '
do check "coiewj0ff" (try Checked.char (0x10000L) with _ -> ' ') ' '
do check "coiewj0gg" (try Checked.char (0x10000n) with _ -> ' ') ' '

do check "coiewj0z" (try Checked.uint16 (-1) with _ -> 17us) 17us
do check "coiewj0x" (try Checked.uint16 (-1.0) with _ -> 17us) 17us
do check "coiewj0c" (try Checked.uint16 (-1.0f) with _ -> 17us) 17us
do check "coiewj0v" (try Checked.uint16 (-1y) with _ -> 17us) 17us
do check "coiewj0b" (try Checked.uint16 (-1s) with _ -> 17us) 17us
do check "coiewj0n" (try Checked.uint16 (-1L) with _ -> 17us) 17us
do check "coiewj0m" (try Checked.uint16 (-1n) with _ -> 17us) 17us

do check "coiewj0aa" (try Checked.uint16 (0x10000) with _ -> 17us) 17us
do check "coiewj0ss" (try Checked.uint16 (65537.0) with _ -> 17us) 17us
do check "coiewj0dd" (try Checked.uint16 (65537.0f) with _ -> 17us) 17us
do check "coiewj0ff" (try Checked.uint16 (0x10000L) with _ -> 17us) 17us
do check "coiewj0gg" (try Checked.uint16 (0x10000n) with _ -> 17us) 17us


do check "clwnwe9831" (sprintf "%A" 1) "1"
do check "clwnwe9832" (sprintf "%A" 10.0) "10.0"
do check "clwnwe9833" (sprintf "%A" 10.0f) "10.0f"
do check "clwnwe9834" (sprintf "%A" 1s) "1s"
do check "clwnwe9835" (sprintf "%A" 1us) "1us"
do check "clwnwe9836" (sprintf "%A" 'c') "'c'"
do check "clwnwe9837" (sprintf "%A" "c") "\"c\""
do check "clwnwe9838" (sprintf "%A" 1y) "1y"
do check "clwnwe9839" (sprintf "%A" 1uy) "1uy"
do check "clwnwe983q" (sprintf "%A" 1L) "1L"
do check "clwnwe983w" (sprintf "%A" 1UL) "1UL"
do check "clwnwe983e" (sprintf "%A" 1u) "1u"
do check "clwnwe983r" (sprintf "%A" [1]) "[1]"
do check "clwnwe983t" (sprintf "%A" [1;2]) "[1; 2]"
do check "clwnwe983y" (sprintf "%A" [1,2]) "[(1, 2)]"
do check "clwnwe983u" (sprintf "%A" (1,2)) "(1, 2)"
do check "clwnwe983i" (sprintf "%A" (1,2,3)) "(1, 2, 3)"
do check "clwnwe983o" (sprintf "%A" (Some(1))) "Some 1"
do check "clwnwe983p" (sprintf "%A" (Some(Some(1)))) "Some (Some 1)"

do check "clwnwe91" 10m 10m
do check "clwnwe92" 10m 10.000m
do check "clwnwe93" 1000000000m 1000000000m
do check "clwnwe94" (4294967296000000000m.ToString()) "4294967296000000000"
#if !NETCOREAPP
do check "clwnwe95" (10.000m.ToString(System.Globalization.CultureInfo.GetCultureInfo(1033).NumberFormat)) "10.000"  // The actual output of a vanilla .ToString() depends on current culture UI. For this reason I am specifying the en-us culture.
#endif
do check "clwnwe96" (10m.ToString()) "10"
do check "clwnwe97" (sprintf "%A" 10m) "10M"
do check "clwnwe98" (sprintf "%A" 10M) "10M"
do check "clwnwe99" (sprintf "%A" 10.00M) "10.00M"
do check "clwnwe9q" (sprintf "%A" -10.00M) "-10.00M"
do check "clwnwe9w" (sprintf "%A" -0.00M) "0.00M"
do check "clwnwe9w" (sprintf "%A" 0.00M) "0.00M"
do check "clwnwe9e" (sprintf "%A" -0M) "0M"
do check "clwnwe9r" (sprintf "%A" 0M) "0M"
do check "clwnwe9t" (sprintf "%A" (+0M)) "0M"
do check "clwnwe9t1" (sprintf "%A" 18446744073709551616000000000m) "18446744073709551616000000000M"
do check "clwnwe9t2" (sprintf "%A" -79228162514264337593543950335m) "-79228162514264337593543950335M"
do check "clwnwe9t3" (sprintf "%A" -0.0000000000000000002147483647m) "-0.0000000000000000002147483647M"
do check "clwnwe9t4" (sprintf "%A" (10.00M + 10M)) "20.00M"
do check "clwnwe9t5" (sprintf "%A" (10.00M + 10M)) "20.00M"

do check "clwnwe9t6" 13.00M (10.00M + decimal 3.0)
do check "clwnwe9t8" 13.00M (10.00M + decimal 3)
do check "clwnwe9t9" 13.00M (10.00M + decimal 3y)
do check "clwnwe9tq" 13.00M (10.00M + decimal 3uy)
do check "clwnwe9tw" 13.00M (10.00M + decimal 3us)
do check "clwnwe9te" 13.00M (10.00M + decimal 3s)
do check "clwnwe9tr" 13.00M (10.00M + decimal 3l)
do check "clwnwe9tt" 13.00M (10.00M + decimal 3ul)
do check "clwnwe9ty" 13.00M (10.00M + decimal 3L)
do check "clwnwe9tu" 13.00M (10.00M + decimal 3UL)
do check "clwnwe9ti" 13.00M (10.00M + decimal 3n)
do check "clwnwe9to" 13.00M (10.00M + decimal 3un)
do check "clwnwe9tp" 13.00M (10.00M + decimal 3.0f)

do check "clwnwe9ta" 13.00 (float 13.00M)
do check "clwnwe9ts" 13 (int32 13.00M)
do check "clwnwe9td" 13s (int16 13.00M)
do check "clwnwe9tf" 13y (sbyte 13.00M)
do check "clwnwe9tg" 13L (int64 13.00M)
do check "clwnwe9th" 13u (uint32 13.00M)
do check "clwnwe9tj" 13us (uint16 13.00M)
do check "clwnwe9tk" 13uy (byte 13.00M)
do check "clwnwe9tl" 13UL (uint64 13.00M)

do check "lkvcnwd09a" 10.0M (20.0M - 10.00M)
do check "lkvcnwd09s" 200.000M (20.0M * 10.00M)
do check "lkvcnwd09d" 2.0M (20.0M / 10.00M)
do check "lkvcnwd09f" 0.0M (20.0M % 10.00M)
do check "lkvcnwd09g" 2.0M (20.0M % 6.00M)
do check "lkvcnwd09h" 20.0M (floor 20.300M)
do check "lkvcnwd09j" 20.0 (floor 20.300)
do check "lkvcnwd09k" 20.0f (floor 20.300f)
#if !NETCOREAPP
do check "lkvcnwd09l" 20.0M (round 20.300M)
do check "lkvcnwd09z" 20.0M (round 20.500M)
do check "lkvcnwd09x" 22.0M (round 21.500M)
#endif
do check "lkvcnwd09c" 20.0 (round 20.300)
do check "lkvcnwd09v" 20.0 (round 20.500)
do check "lkvcnwd09b" 22.0 (round 21.500)
do check "lkvcnwd09n" 1 (sign 20.300)
do check "lkvcnwd09m" (-1) (sign (-20.300))
do check "lkvcnwd091" (-1) (sign (-20))
do check "lkvcnwd092" 1 (sign 20)
do check "lkvcnwd093" 0 (sign 0)
do check "lkvcnwd094" 0 (sign (-0))
do check "lkvcnwd095" 0 (sign (-0))
do check "lkvcnwd096" 0 (sign 0y)
do check "lkvcnwd097" 0 (sign 0s)
do check "lkvcnwd098" 0 (sign 0L)

do check "lkvcnwd099" 1 (sign 1y)
do check "lkvcnwd09q" 1 (sign 1s)
do check "lkvcnwd09w" 1 (sign 1L)

do check "lkvcnwd09e" (-1) (sign (-1y))
do check "lkvcnwd09r" (-1) (sign (-1s))
do check "lkvcnwd09t" (-1) (sign (-1L))

// Check potential optimization bugs

do check "cenonoiwe1" (3 > 1) true
do check "cenonoiwe2" (3y > 1y) true
do check "cenonoiwe3" (3uy > 1uy) true
do check "cenonoiwe4" (3s > 1s) true
do check "cenonoiwe5" (3us > 1us) true
do check "cenonoiwe6" (3 > 1) true
do check "cenonoiwe7" (3u > 1u) true
do check "cenonoiwe8" (3L > 1L) true
do check "cenonoiwe9" (3UL > 1UL) true

do check "cenonoiweq" (3 >= 1) true
do check "cenonoiwew" (3y >= 1y) true
do check "cenonoiwee" (3uy >= 1uy) true
do check "cenonoiwer" (3s >= 1s) true
do check "cenonoiwet" (3us >= 1us) true
do check "cenonoiwey" (3 >= 1) true
do check "cenonoiweu" (3u >= 1u) true
do check "cenonoiwei" (3L >= 1L) true
do check "cenonoiweo" (3UL >= 1UL) true

do check "cenonoiwea" (3 >= 3) true
do check "cenonoiwes" (3y >= 3y) true
do check "cenonoiwed" (3uy >= 3uy) true
do check "cenonoiwef" (3s >= 3s) true
do check "cenonoiweg" (3us >= 3us) true
do check "cenonoiweh" (3 >= 3) true
do check "cenonoiwej" (3u >= 3u) true
do check "cenonoiwek" (3L >= 3L) true
do check "cenonoiwel" (3UL >= 3UL) true


do check "cenonoiwd1" (3 < 1) false
do check "cenonoiwd2" (3y < 1y) false
do check "cenonoiwd3" (3uy < 1uy) false
do check "cenonoiwd4" (3s < 1s) false
do check "cenonoiwd5" (3us < 1us) false
do check "cenonoiwd6" (3 < 1) false
do check "cenonoiwd7" (3u < 1u) false
do check "cenonoiwd8" (3L < 1L) false
do check "cenonoiwd9" (3UL < 1UL) false

do check "cenonoiwdq" (3 <= 1) false
do check "cenonoiwdw" (3y <= 1y) false
do check "cenonoiwde" (3uy <= 1uy) false
do check "cenonoiwdr" (3s <= 1s) false
do check "cenonoiwdt" (3us <= 1us) false
do check "cenonoiwdy" (3 <= 1) false
do check "cenonoiwdu" (3u <= 1u) false
do check "cenonoiwdi" (3L <= 1L) false
do check "cenonoiwdo" (3UL <= 1UL) false

do check "cenonoiwda" (3 <= 3) true
do check "cenonoiwds" (3y <= 3y) true
do check "cenonoiwdd" (3uy <= 3uy) true
do check "cenonoiwdf" (3s <= 3s) true
do check "cenonoiwdg" (3us <= 3us) true
do check "cenonoiwdh" (3 <= 3) true
do check "cenonoiwdj" (3u <= 3u) true
do check "cenonoiwdk" (3L <= 3L) true
do check "cenonoiwdl" (3UL <= 3UL) true

do check "cenonoiwdz" (4 + 2) 6
do check "cenonoiwdx" (4y + 2y) 6y
do check "cenonoiwdc" (4uy + 2uy) 6uy
do check "cenonoiwdv" (4s + 2s) 6s
do check "cenonoiwdb" (4us + 2us) 6us
do check "cenonoiwdn" (4 + 2) 6
do check "cenonoiwdm" (4u + 2u) 6u
do check "cenonoiwdl" (4L + 2L) 6L
do check "cenonoiwdp" (4UL + 2UL) 6UL

do check "cenonoiwc1" (4 - 2) 2
do check "cenonoiwc2" (4y - 2y) 2y
do check "cenonoiwc3" (4uy - 2uy) 2uy
do check "cenonoiwc4" (4s - 2s) 2s
do check "cenonoiwc5" (4us - 2us) 2us
do check "cenonoiwc6" (4 - 2) 2
do check "cenonoiwc7" (4u - 2u) 2u
do check "cenonoiwc8" (4L - 2L) 2L
do check "cenonoiwc9" (4UL - 2UL) 2UL

do check "cenonoiwcq" (4 * 2) 8
do check "cenonoiwcw" (4y * 2y) 8y
do check "cenonoiwce" (4uy * 2uy) 8uy
do check "cenonoiwcr" (4s * 2s) 8s
do check "cenonoiwct" (4us * 2us) 8us
do check "cenonoiwcy" (4 * 2) 8
do check "cenonoiwcu" (4u * 2u) 8u
do check "cenonoiwci" (4L * 2L) 8L
do check "cenonoiwco" (4UL * 2UL) 8UL

do check "cenonoiwc" (4 / 2) 2
do check "cenonoiwc" (4y / 2y) 2y
do check "cenonoiwc" (4uy / 2uy) 2uy
do check "cenonoiwc" (4s / 2s) 2s
do check "cenonoiwc" (4us / 2us) 2us
do check "cenonoiwc" (4 / 2) 2
do check "cenonoiwc" (4u / 2u) 2u
do check "cenonoiwc" (4L / 2L) 2L
do check "cenonoiwc" (4UL / 2UL) 2UL

do check "cenonoiwc" (-(4)) (-4)
do check "cenonoiwc" (-(4y)) (-4y)
do check "cenonoiwc" (-(4s)) (-4s)
do check "cenonoiwc" (-(4L)) (-4L)


do check "cenonoiwc" (4 % 3) 1
do check "cenonoiwc" (4y % 3y) 1y
do check "cenonoiwc" (4uy % 3uy) 1uy
do check "cenonoiwc" (4s % 3s) 1s
do check "cenonoiwc" (4us % 3us) 1us
do check "cenonoiwc" (4 % 3) 1
do check "cenonoiwc" (4u % 3u) 1u
do check "cenonoiwc" (4L % 3L) 1L
do check "cenonoiwc" (4UL % 3UL) 1UL

do check "cenonoiwc" (0b010 <<< 3) 0b010000
do check "cenonoiwc" (0b010y <<< 3) 0b010000y
do check "cenonoiwc" (0b010uy <<< 3) 0b010000uy
do check "cenonoiwc" (0b010s <<< 3) 0b010000s
do check "cenonoiwc" (0b010us <<< 3) 0b010000us
do check "cenonoiwc" (0b010u <<< 3) 0b010000u
do check "cenonoiwc" (0b010L <<< 3) 0b010000L
do check "cenonoiwc" (0b010UL <<< 3) 0b010000UL

do check "cenonoiwc" (0b010000 >>> 3) 0b010
do check "cenonoiwc" (0b010000y >>> 3) 0b010y
do check "cenonoiwc" (0b010000uy >>> 3) 0b010uy
do check "cenonoiwc" (0b010000s >>> 3) 0b010s
do check "cenonoiwc" (0b010000us >>> 3) 0b010us
do check "cenonoiwc" (0b010000u >>> 3) 0b010u
do check "cenonoiwc" (0b010000L >>> 3) 0b010L
do check "cenonoiwc" (0b010000UL >>> 3) 0b010UL

do check "cenonoiwc" (sbyte 4) 4y
do check "cenonoiwc" (byte 4) 4uy
do check "cenonoiwc" (int16 4) 4s
do check "cenonoiwc" (uint16 4) 4us
do check "cenonoiwc" (int32 4) 4
do check "cenonoiwc" (uint32 4) 4u
do check "cenonoiwc" (int64 4) 4L
do check "cenonoiwc" (uint64 4) 4UL

do check "cenonoiwc" (sbyte 4y) 4y
do check "cenonoiwc" (byte 4y) 4uy
do check "cenonoiwc" (int16 4y) 4s
do check "cenonoiwc" (uint16 4y) 4us
do check "cenonoiwc" (int32 4y) 4
do check "cenonoiwc" (uint32 4y) 4u
do check "cenonoiwc" (int64 4y) 4L
do check "cenonoiwc" (uint64 4y) 4UL


do check "cenonoiwc" (sbyte 4uy) 4y
do check "cenonoiwc" (byte 4uy) 4uy
do check "cenonoiwc" (int16 4uy) 4s
do check "cenonoiwc" (uint16 4uy) 4us
do check "cenonoiwc" (int32 4uy) 4
do check "cenonoiwc" (uint32 4uy) 4u
do check "cenonoiwc" (int64 4uy) 4L
do check "cenonoiwc" (uint64 4uy) 4UL


do check "cenonoiwc" (sbyte 4s) 4y
do check "cenonoiwc" (byte 4s) 4uy
do check "cenonoiwc" (int16 4s) 4s
do check "cenonoiwc" (uint16 4s) 4us
do check "cenonoiwc" (int32 4s) 4
do check "cenonoiwc" (uint32 4s) 4u
do check "cenonoiwc" (int64 4s) 4L
do check "cenonoiwc" (uint64 4s) 4UL

do check "cenonoiwc" (sbyte 4us) 4y
do check "cenonoiwc" (byte 4us) 4uy
do check "cenonoiwc" (int16 4us) 4s
do check "cenonoiwc" (uint16 4us) 4us
do check "cenonoiwc" (int32 4us) 4
do check "cenonoiwc" (uint32 4us) 4u
do check "cenonoiwc" (int64 4us) 4L
do check "cenonoiwc" (uint64 4us) 4UL

do check "cenonoiwc" (sbyte 4u) 4y
do check "cenonoiwc" (byte 4u) 4uy
do check "cenonoiwc" (int16 4u) 4s
do check "cenonoiwc" (uint16 4u) 4us
do check "cenonoiwc" (int32 4u) 4
do check "cenonoiwc" (uint32 4u) 4u
do check "cenonoiwc" (int64 4u) 4L
do check "cenonoiwc" (uint64 4u) 4UL

do check "cenonoiwc" (sbyte 4L) 4y
do check "cenonoiwc" (byte 4L) 4uy
do check "cenonoiwc" (int16 4L) 4s
do check "cenonoiwc" (uint16 4L) 4us
do check "cenonoiwc" (int32 4L) 4
do check "cenonoiwc" (uint32 4L) 4u
do check "cenonoiwc" (int64 4L) 4L
do check "cenonoiwc" (uint64 4L) 4UL


do check "cenonoiwc" (sbyte 4UL) 4y
do check "cenonoiwc" (byte 4UL) 4uy
do check "cenonoiwc" (int16 4UL) 4s
do check "cenonoiwc" (uint16 4UL) 4us
do check "cenonoiwc" (int32 4UL) 4
do check "cenonoiwc" (uint32 4UL) 4u
do check "cenonoiwc" (int64 4UL) 4L
do check "cenonoiwc" (uint64 4UL) 4UL
do check "cenonoiwc" (match null with null -> 2 | _ -> 1) 2

module SameTestsUsingNonStructuralComparison2 =
    open NonStructuralComparison

    do check "ffcenonoiwe1" (3 > 1) true
    do check "ffcenonoiwe2" (3y > 1y) true
    do check "ffcenonoiwe3" (3uy > 1uy) true
    do check "ffcenonoiwe4" (3s > 1s) true
    do check "ffcenonoiwe5" (3us > 1us) true
    do check "ffcenonoiwe6" (3 > 1) true
    do check "ffcenonoiwe7" (3u > 1u) true
    do check "ffcenonoiwe8" (3L > 1L) true
    do check "ffcenonoiwe9" (3UL > 1UL) true
    do check "ffcenonoiwe9" (3.14 > 3.1) true
    do check "ffcenonoiwe9" (3.14f > 3.1f) true
    do check "ffcenonoiwe9" ("bbb" > "aaa") true
    do check "ffcenonoiwe9" ("bbb" > "bbb") false
    do check "ffcenonoiwe9" ("aaa" > "bbb") false
    do check "ffcenonoiwe9" ('b' > 'a') true
    do check "ffcenonoiwe9" ('a' > 'b') false
    do check "ffcenonoiwe9" ('b' > 'b') false

    do check "ffcenonoiwea" (3 >= 3) true
    do check "ffcenonoiwes" (3y >= 3y) true
    do check "ffcenonoiwed" (3uy >= 3uy) true
    do check "ffcenonoiwef" (3s >= 3s) true
    do check "ffcenonoiweg" (3us >= 3us) true
    do check "ffcenonoiweh" (3 >= 3) true
    do check "ffcenonoiwej" (3u >= 3u) true
    do check "ffcenonoiwek" (3L >= 3L) true
    do check "ffcenonoiwel" (3UL >= 3UL) true
    do check "ffcenonoiwem" (3.14 >= 3.1) true
    do check "ffcenonoiwen" (3.14f >= 3.1f) true
    do check "ffcenonoiwen" (3.14M >= 3.1M) true
    do check "ffcenonoiwe91r" ("bbb" >= "aaa") true
    do check "ffcenonoiwe92r" ("bbb" >= "bbb") true
    do check "ffcenonoiwe93r" ("aaa" >= "bbb") false
    do check "ffcenonoiwe94r" ('b' >= 'a') true
    do check "ffcenonoiwe95r" ('a' >= 'b') false
    do check "ffcenonoiwe96r" ('b' >= 'b') true


    do check "ffcenonoiwd1" (3 < 1) false
    do check "ffcenonoiwd2" (3y < 1y) false
    do check "ffcenonoiwd3" (3uy < 1uy) false
    do check "ffcenonoiwd4" (3s < 1s) false
    do check "ffcenonoiwd5" (3us < 1us) false
    do check "ffcenonoiwd6" (3 < 1) false
    do check "ffcenonoiwd7" (3u < 1u) false
    do check "ffcenonoiwd8" (3L < 1L) false
    do check "ffcenonoiwd9" (3UL < 1UL) false
    do check "ffcenonoiwd9" (3.14 < 1.0) false
    do check "ffcenonoiwd9" (3.14f < 1.0f) false
    do check "ffcenonoiwd9" (3.14M < 1.0M) false
    do check "ffcenonoiwe91a" ("bbb" < "aaa") false
    do check "ffcenonoiwe92a" ("bbb" < "bbb") false
    do check "ffcenonoiwe93a" ("aaa" < "bbb") true
    do check "ffcenonoiwe94a" ('b' < 'a') false
    do check "ffcenonoiwe95a" ('a' < 'b') true
    do check "ffcenonoiwe96a" ('b' < 'b') false


    do check "ffcenonoiwdq" (3 <= 1) false
    do check "ffcenonoiwdw" (3y <= 1y) false
    do check "ffcenonoiwde" (3uy <= 1uy) false
    do check "ffcenonoiwdr" (3s <= 1s) false
    do check "ffcenonoiwdt" (3us <= 1us) false
    do check "ffcenonoiwdy" (3 <= 1) false
    do check "ffcenonoiwdu" (3u <= 1u) false
    do check "ffcenonoiwdi" (3L <= 1L) false
    do check "ffcenonoiwdo" (3UL <= 1UL) false
    do check "ffcenonoiwdg" (3.14 <= 1.0) false
    do check "ffcenonoiwdt" (3.14f <= 1.0f) false
    do check "ffcenonoiwdt" (3.14M <= 1.0M) false
    do check "ffcenonoiwe91q" ("bbb" <= "aaa") false
    do check "ffcenonoiwe92q" ("bbb" <= "bbb") true
    do check "ffcenonoiwe93q" ("aaa" <= "bbb") true
    do check "ffcenonoiwe94q" ('b' <= 'a') false
    do check "ffcenonoiwe95q" ('a' <= 'b') true
    do check "ffcenonoiwe96q" ('b' <= 'b') true


    do check "ffcenonoiwda" (3 <= 3) true
    do check "ffcenonoiwds" (3y <= 3y) true
    do check "ffcenonoiwdd" (3uy <= 3uy) true
    do check "ffcenonoiwdf" (3s <= 3s) true
    do check "ffcenonoiwdg" (3us <= 3us) true
    do check "ffcenonoiwdh" (3 <= 3) true
    do check "ffcenonoiwdj" (3u <= 3u) true
    do check "ffcenonoiwdk" (3L <= 3L) true
    do check "ffcenonoiwdl" (3UL <= 3UL) true
    do check "ffcenonoiwdo" (3.14 <= 3.14) true
    do check "ffcenonoiwdp" (3.14f <= 3.14f) true
    do check "ffcenonoiwdp" (3.14M <= 3.14M) true


module NonStructuralComparisonOverDateTime =
    open NonStructuralComparison
    let now = System.DateTime.Now
    let tom = now.AddDays 1.0
    do check "ffcenonoiwe90" (now = tom) false
    do check "ffcenonoiwe9q" (now <> tom) true
    do check "ffcenonoiwe91" (now < tom) true
    do check "ffcenonoiwe92" (now <= now) true
    do check "ffcenonoiwe93" (now <= tom) true
    do check "ffcenonoiwe94" (tom > now) true
    do check "ffcenonoiwe95" (now >= now) true
    do check "ffcenonoiwe96" (tom >= now) true
    do check "ffcenonoiwe97" (compare now now) 0
    do check "ffcenonoiwe98" (compare now tom) -1
    do check "ffcenonoiwe99" (compare tom now) 1
    do check "ffcenonoiwe9a" (max tom tom) tom
    do check "ffcenonoiwe9b" (max tom now) tom
    do check "ffcenonoiwe9c" (max now tom) tom
    do check "ffcenonoiwe9d" (min tom tom) tom
    do check "ffcenonoiwe9e" (min tom now) now
    do check "ffcenonoiwe9f" (min now tom) now

    do check "ffcenonoiwe97a1" (ComparisonIdentity.NonStructural.Compare (1, 1)) 0
    do check "ffcenonoiwe98b2" (ComparisonIdentity.NonStructural.Compare (0, 1)) -1
    do check "ffcenonoiwe99c3" (ComparisonIdentity.NonStructural.Compare (1, 0)) 1

    do check "ffcenonoiwe97a4" (ComparisonIdentity.NonStructural.Compare (now, now)) 0
    do check "ffcenonoiwe98b5" (ComparisonIdentity.NonStructural.Compare (now, tom)) -1
    do check "ffcenonoiwe99c6" (ComparisonIdentity.NonStructural.Compare (tom, now)) 1

    do check "ffcenonoiwe97a7" (HashIdentity.NonStructural.Equals (now, now)) true
    do check "ffcenonoiwe98b8" (HashIdentity.NonStructural.Equals (now, tom)) false
    do check "ffcenonoiwe99c9" (HashIdentity.NonStructural.Equals (tom, now)) false

    do check "ffcenonoiwe97a7" (HashIdentity.NonStructural.GetHashCode now) (hash now)
    do check "ffcenonoiwe97a7" (HashIdentity.NonStructural.GetHashCode tom) (hash tom)
    do check "ffcenonoiwe97a7" (HashIdentity.NonStructural.GetHashCode 11) (hash 11)
    do check "ffcenonoiwe97a7" (HashIdentity.NonStructural.GetHashCode 11L) (hash 11L)
    do check "ffcenonoiwe97a7" (HashIdentity.NonStructural.GetHashCode 11UL) (hash 11UL)

    do check "ffcenonoiwe97aa" (HashIdentity.NonStructural.Equals (1, 1)) true
    do check "ffcenonoiwe98bb" (HashIdentity.NonStructural.Equals (1, 0)) false
    do check "ffcenonoiwe99cc" (HashIdentity.NonStructural.Equals (0, 1)) false


module NonStructuralComparisonOverTimeSpan =
    open NonStructuralComparison
    let now = System.TimeSpan.Zero
    let tom = System.TimeSpan.FromDays 1.0
    do check "tscenonoiwe90" (now = tom) false
    do check "tscenonoiwe9q" (now <> tom) true
    do check "tscenonoiwe91" (now < tom) true
    do check "tscenonoiwe92" (now <= now) true
    do check "tscenonoiwe93" (now <= tom) true
    do check "tscenonoiwe94" (tom > now) true
    do check "tscenonoiwe95" (now >= now) true
    do check "tscenonoiwe96" (tom >= now) true
    do check "tscenonoiwe97" (compare now now) 0
    do check "tscenonoiwe98" (compare now tom) -1
    do check "tscenonoiwe99" (compare tom now) 1
    do check "tscenonoiwe9a" (max tom tom) tom
    do check "tscenonoiwe9b" (max tom now) tom
    do check "tscenonoiwe9c" (max now tom) tom
    do check "tscenonoiwe9d" (min tom tom) tom
    do check "tscenonoiwe9e" (min tom now) now
    do check "tscenonoiwe9f" (min now tom) now


// Check you can use the operators without opening the module by naming them
module NonStructuralComparisonOverTimeSpanDirect =
    let now = System.TimeSpan.Zero
    let tom = System.TimeSpan.FromDays 1.0
    do check "tscenonoiwe90" (NonStructuralComparison.(=) now tom) false
    do check "tscenonoiwe9q" (NonStructuralComparison.(<>) now tom) true
    do check "tscenonoiwe91" (NonStructuralComparison.(<) now tom) true
    do check "tscenonoiwe92" (NonStructuralComparison.(<=) now now) true
    do check "tscenonoiwe94" (NonStructuralComparison.(>) tom now) true
    do check "tscenonoiwe95" (NonStructuralComparison.(>=) now now) true
    do check "tscenonoiwe97" (NonStructuralComparison.compare now now) 0
    do check "tscenonoiwe9a" (NonStructuralComparison.max tom now) tom
    do check "tscenonoiwe9e" (NonStructuralComparison.min tom now) now

    do check "ffcenonoiwe97a7" (NonStructuralComparison.hash now) (Operators.hash now)
    do check "ffcenonoiwe97a7" (NonStructuralComparison.hash tom) (Operators.hash tom)
    do check "ffcenonoiwe97a7" (NonStructuralComparison.hash 11) (Operators.hash 11)
    do check "ffcenonoiwe97a7" (NonStructuralComparison.hash 11L) (Operators.hash 11L)
    do check "ffcenonoiwe97a7" (NonStructuralComparison.hash 11UL) (Operators.hash 11UL)

(*---------------------------------------------------------------------------
!* Bug 1029: Support conversion functions named after C# type names? e.g. uint for uint32
 *--------------------------------------------------------------------------- *)

do check "1029: byte"   (byte   "123") 123uy
do check "1029: sbyte"  (sbyte  "123") 123y
(*
do printf "Check bug 1029: Support conversion functions named after C# type names? e.g. uint for uint32\n"
do check "1029: short"  (short  "123") 123s
do check "1029: ushort" (ushort "123") 123us
do check "1029: int"    (int    "123") 123
do check "1029: uint"   (uint   "123") 123u
do check "1029: long"   (int64  "123") 123L
do check "1029: ulong"  (uint64 "123") 123uL    
do check "1029: int8"   (int8   "123") 123y
do check "1029: uint8"  (uint8  "123") 123uy
*)

do check "1029: int16"  (int16  "123") 123s
do check "1029: uint16" (uint16 "123") 123us
do check "1029: int32"  (int32  "123") 123
do check "1029: uint32" (uint32 "123") 123u
do check "1029: int64"  (int64  "123") 123L
do check "1029: uint64" (uint64 "123") 123uL

do check "1029: float32" (float32 "1.2") 1.2f    
do check "1029: float"   (float   "1.2") 1.2

do check "1029: single"  (single  "1.2") 1.2f
do check "1029: double"  (double  "1.2") 1.2


(*---------------------------------------------------------------------------
!* BUG 945: comment lexing does not handle slash-quote inside quoted strings
 *--------------------------------------------------------------------------- *)

(* THIS COMMENT IS THE TEST, DO NOT DELETE
   let x = "abc"
   let x = "abc\""
   let x = "\"abc"
*)


(*---------------------------------------------------------------------------
!* BUG 946: comment lexing does not handle double-quote and backslash inside @-strings
 *--------------------------------------------------------------------------- *)

(* THIS COMMENT IS THE TEST, DO NOT DELETE
   let x = @"abc"
   let x = @"abc\"
   let x = @"\a\bc\"
   let x = @"abc\""and still @-string here tested be ending as follows \"
*)


(*---------------------------------------------------------------------------
!* BUG 1080: Seq.cache_all does not have the properties of cache
 *--------------------------------------------------------------------------- *)


module SeqCacheTests = begin
    let countStart = ref 0
    let countIter  = ref 0
    let countStop  = ref 0
    let oneUseSequence =
        Seq.generate (fun () -> incr countStart; ref 0)
                     (fun r  -> incr countIter;  incr r; if !r=10 then None else Some !r)
                     (fun r  -> incr countStop)
    let manyUseSeq = Seq.cache oneUseSequence

    do check "Bug1080" (!countStart,!countIter,!countStop) (0,0,0)
    let () =
      let xs = manyUseSeq |> Seq.truncate 0  |> Seq.toArray in
      let xs = manyUseSeq |> Seq.truncate 2  |> Seq.toArray in
      let xs = manyUseSeq |> Seq.truncate 3  |> Seq.toArray in
      ()
    manyUseSeq (* In fsi, printing forces some walking of manyUseSeq *)
    let () =
      let xs = manyUseSeq |> Seq.truncate 6   |> Seq.toArray in
      let xs = manyUseSeq |> Seq.truncate 100 |> Seq.toArray in
      ()
    do check "Bug1080" (!countStart,!countIter,!countStop) (1,10,1)
    
    do (box manyUseSeq :?> System.IDisposable) .Dispose() 
    do countStart := 0; countIter  := 0; countStop  := 0

    do check "Bug1080" (!countStart,!countIter,!countStop) (0,0,0)
    let () =
      let xs = manyUseSeq |> Seq.truncate 0  |> Seq.toArray in
      let xs = manyUseSeq |> Seq.truncate 2  |> Seq.toArray in
      let xs = manyUseSeq |> Seq.truncate 3  |> Seq.toArray in
      ()
    manyUseSeq (* In fsi, printing forces some walking of manyUseSeq *)
    let () =
      let xs = manyUseSeq |> Seq.truncate 6   |> Seq.toArray in
      let xs = manyUseSeq |> Seq.truncate 100 |> Seq.toArray in
      ()
    do check "Bug1080" (!countStart,!countIter,!countStop) (1,10,1)
    do (box manyUseSeq :?> System.IDisposable) .Dispose() 
    do countStart := 0; countIter  := 0; countStop  := 0
    do check "Bug1080" (!countStart,!countIter,!countStop) (0,0,0)

    let () =
      let xs = manyUseSeq |> Seq.truncate 0  |> Seq.toArray in
      let xs = manyUseSeq |> Seq.truncate 2  |> Seq.toArray in
      let xs = manyUseSeq |> Seq.truncate 3  |> Seq.toArray in
      ()
    manyUseSeq (* In fsi, printing forces some walking of manyUseSeq *)
    let () =
      let xs = manyUseSeq |> Seq.truncate 6   |> Seq.toArray in
      let xs = manyUseSeq |> Seq.truncate 100 |> Seq.toArray in
      ()

    do check "Bug1080" (!countStart,!countIter,!countStop) (1,10,1)

end


(*---------------------------------------------------------------------------
!* BUG 747: Parsing (expr :> type) as top-level expression in fsi requires brackets, grammar issue
 *--------------------------------------------------------------------------- *)

(*Pending inclusion...
(* Do not delete  below, they are part of the test, since they delimit the interactions *)

12 : int                 (* <-- without brackets, this was rejected by fsi *)
"12" :> System.Object    (* <-- without brackets, this was rejected by fsi *)
(box "12") :?> string    (* <-- without brackets, this was rejected by fsi *)
  
*)


(*---------------------------------------------------------------------------
!* BUG 1049: Adding string : 'a -> string, test cases.
 *--------------------------------------------------------------------------- *)

do printf "Bug1049: string conversion checks\n"
do check "Bug1049.int8"       (string 123y)     "123"
do check "Bug1049.int16"      (string 123s)     "123"
do check "Bug1049.int32"      (string 123)      "123"
do check "Bug1049.int64"      (string 123L)     "123"
do check "Bug1049.nativeint"  (string 123n)     "123"
    
do check "Bug1049.-int8"      (string (-123y))  "-123"
do check "Bug1049.-int16"     (string (-123s))  "-123"
do check "Bug1049.-int32"     (string (-123))   "-123"
do check "Bug1049.-int64"     (string (-123L))  "-123"
do check "Bug1049.-nativeint" (string (-123n))  "-123"
    
do check "Bug1049.uint8"      (string 123uy)    "123"
do check "Bug1049.uint16"     (string 123us)    "123"
do check "Bug1049.uint32"     (string 123u)     "123"
do check "Bug1049.uint64"     (string 123uL)    "123"
do check "Bug1049.unativeint" (string 123un)    "123"

do check "Bug1049.float64"    (string 1.234)                           "1.234"
do check "Bug1049.float64"    (string (-1.234))                        "-1.234"
do check "Bug1049.float64"    (string System.Double.NaN)               "NaN"
do check "Bug1049.float64"    (string System.Double.PositiveInfinity)  "Infinity"
do check "Bug1049.float64"    (string System.Double.NegativeInfinity)  "-Infinity"
do check "Bug1049.float64"    (string nan)                             "NaN"
do check "Bug1049.float64"    (string infinity)                        "Infinity"
do check "Bug1049.float64"    (string (-infinity))                     "-Infinity"

do check "Bug1049.float32"    (string 1.234f)                          "1.234"
do check "Bug1049.float32"    (string (-1.234f))                       "-1.234"
do check "Bug1049.float32"    (string System.Single.NaN)               "NaN"
do check "Bug1049.float32"    (string System.Single.PositiveInfinity)  "Infinity"
do check "Bug1049.float32"    (string System.Single.NegativeInfinity)  "-Infinity"

type ToStringClass(x) =
  class
    override this.ToString() = x
  end
do check "Bug1049.customClass" (string (ToStringClass("fred"))) "fred"

[<Struct>]
type ToStringStruct =
  struct
    val x : int
    new(x) = {x=x}
    override this.ToString() = string this.x
  end
do check "Bug1049.customStruct" (string (ToStringStruct(123))) "123"
type ToStringEnum = 
    | A = 1
    | B = 2
do check "bug5995.Enum.A" (string ToStringEnum.A) "A"
do check "bug5995.Enum.B" (string ToStringEnum.B) "B"    



(*---------------------------------------------------------------------------
!* BUG 1178: Seq.init and Seq.initInfinite implemented using Seq.unfold which evaluates Current on every step
 *--------------------------------------------------------------------------- *)

module Check1178 = begin
  do printf "\n\nTest 1178: check finite/infinite sequences have lazy (f i) for each i\n\n"  
  (* Test cases for Seq.item. *)
  let counter = ref 0
  let reset r = r := 0
  let fails f = try f() |> ignore;false with _ -> true
  let claim x = check "Bugs 1178/1482" x true
  
  (* Bug 1178: Check Seq.init only computes f on the items requested *)
  let initial_100 = Seq.init 100 (fun i -> incr counter; i)
  do reset counter; claim(Seq.item 0  initial_100=0);  claim(!counter = 1)
  do reset counter; claim(Seq.item 50 initial_100=50); claim(!counter = 1)
  do reset counter; claim(fails (fun () -> Seq.item 100 initial_100));   claim(!counter = 0)
  do reset counter; claim(fails (fun () -> Seq.item (-10) initial_100)); claim(!counter = 0)

  let initial_w = Seq.initInfinite (fun i -> incr counter; i)
  do reset counter; claim(Seq.item 0  initial_w=0);  claim(!counter = 1)
  do reset counter; claim(Seq.item 50 initial_w=50); claim(!counter = 1)
  do reset counter; claim(fails (fun () -> Seq.item (-10) initial_w)); claim(!counter = 0)
  do reset counter; claim(fails (fun () -> Seq.item (-1) initial_w)); claim(!counter = 0)

  (* Check *)
  let on p f x y = f (p x) (p y)
  do claim(on Seq.toArray (=) (Seq.init 10 (fun x -> x*10)) (seq { for x in 0 .. 9 -> x*10 }))


(*---------------------------------------------------------------------------
!* BUG 1482: Seq.initInfinite overflow and Seq.init negative count to be trapped
 *--------------------------------------------------------------------------- *)

#if LONGTESTS
  do printf "\n\nTest 1482: check that an infinite sequence fails after i = Int32.MaxValue. This may take ~40 seconds.\n\n"
  do claim(fails (fun () -> Seq.length initial_w))
#endif
end

module IntegerLoopsWithMinAndMaxIntAndKnownBounds =  begin
    let x0() = 
      let r = new ResizeArray<_>() in
      for i = 0 to 10 do
         r.Add i
      done;
      check "clkevrw1" (List.ofSeq r) [ 0 .. 10]

    let x1() = 
     let r = new ResizeArray<_>() in
     for i = System.Int32.MinValue to System.Int32.MinValue + 2 do
        r.Add i
     done;
     check "clkevrw2" (List.ofSeq r) [System.Int32.MinValue .. System.Int32.MinValue + 2]

    let x2() = 
     let r = new ResizeArray<_>() in
     for i = System.Int32.MaxValue - 3 to System.Int32.MaxValue - 1 do
        r.Add i
     done;
     check "clkevrw3" (List.ofSeq r) [System.Int32.MaxValue - 3 .. System.Int32.MaxValue - 1]

    let x3() = 
     let r = new ResizeArray<_>() in
     for i = System.Int32.MaxValue - 3 to System.Int32.MaxValue  do
        r.Add i 
     done;
     check "clkevrw4" (List.ofSeq r) [System.Int32.MaxValue - 3 .. System.Int32.MaxValue ]

    let x4() = 
     let r = new ResizeArray<_>() in
     for i = System.Int32.MaxValue to System.Int32.MaxValue  do
        r.Add i
     done;
     check "clkevrw5" (List.ofSeq r) [System.Int32.MaxValue .. System.Int32.MaxValue ]

    let x5() = 
     let r = new ResizeArray<_>() in
     for i = System.Int32.MinValue to System.Int32.MinValue do
        r.Add i
     done;
     check "clkevrw6" (List.ofSeq r) [System.Int32.MinValue .. System.Int32.MinValue ]

    let x6() = 
      for lower in [ -5 .. 5 ] do
         for upper in [ -5 .. 5 ] do
              let r = new ResizeArray<_>() in
              for i = lower to upper do
                 r.Add i
              done;
              check "clkevrw7" (List.ofSeq r) [ lower .. upper ]
         done;
      done
    do x0()
    do x1()
    do x2()
    do x3()
    do x4()
    do x5()
    do x6()
end

module IntegerLoopsWithMinAndMaxIntAndKnownBoundsGoingDown = begin

    let x0() = 
      let r = new ResizeArray<_>() in
      for i = 10 downto 0 do
         r.Add i
      done;
      check "clkevrw1" (List.ofSeq r |> List.rev) [ 0 .. 10]

    let x1() = 
     let r = new ResizeArray<_>() in
     for i = System.Int32.MinValue + 2 downto System.Int32.MinValue do
        r.Add i
     done;
     check "clkevrw2" (List.ofSeq r |> List.rev) [System.Int32.MinValue .. System.Int32.MinValue + 2]

    let x2() = 
     let r = new ResizeArray<_>() in
     for i = System.Int32.MaxValue - 1 downto System.Int32.MaxValue - 3 do
        r.Add i
     done;
     check "clkevrw3" (List.ofSeq r |> List.rev) [System.Int32.MaxValue - 3 .. System.Int32.MaxValue - 1]

    let x3() = 
     let r = new ResizeArray<_>() in
     for i = System.Int32.MaxValue downto System.Int32.MaxValue - 3  do
        r.Add i 
     done;
     check "clkevrw4" (List.ofSeq r |> List.rev) [System.Int32.MaxValue - 3 .. System.Int32.MaxValue ]

    let x4() = 
     let r = new ResizeArray<_>() in
     for i = System.Int32.MaxValue downto System.Int32.MaxValue  do
        r.Add i
     done;
     check "clkevrw5" (List.ofSeq r |> List.rev) [System.Int32.MaxValue .. System.Int32.MaxValue ]

    let x5() = 
     let r = new ResizeArray<_>() in
     for i = System.Int32.MinValue downto System.Int32.MinValue do
        r.Add i
     done;
     check "clkevrw6" (List.ofSeq r |> List.rev) [System.Int32.MinValue .. System.Int32.MinValue ]

    let x6() = 
      for lower in [ -5 .. 5 ] do
         for upper in [ -5 .. 5 ] do
              let r = new ResizeArray<_>() in
              for i = upper downto lower  do
                 r.Add i
              done;
              check "clkevrw7" (List.ofSeq r |> List.rev) [ lower .. upper ]
          done;
      done
 
    do x0()
    do x1()
    do x2()
    do x3()
    do x4()
    do x5()
    do x6()


end 


(*---------------------------------------------------------------------------
!* BUG 1477: struct with field offset attributes on fields throws assertions in fsi
 *--------------------------------------------------------------------------- *)

module Check1477 = begin
  (* FSI ilreflect regression *)
  open System.Runtime.InteropServices
  [<Struct>] 
  [<StructLayout(LayoutKind.Explicit)>]
  type Align16 = struct
    [<FieldOffset(0)>]
    val x0      : string
    [<FieldOffset(8)>]
    val x1      : string
  end
end

(*---------------------------------------------------------------------------
!* BUG 1561: (-star-star-) opens a comment but does not close it and other XML Doc issues.
 *--------------------------------------------------------------------------- *)

(* QA note: how to test for XML doc? Programatically? *)
module Check1561 = begin
 (** Should be XML Doc *)
 let itemA = () 
 (** Should be XML Doc too even with a * inside *)      
 let itemB = ()
 (*** No longer XML Doc, since it starts with 3 stars *)
 let itemC = ()
 (**)
 let itemD = ()
end


(*---------------------------------------------------------------------------
!* BUG 1750: ilxgen stack incorrect during multi-branch match tests
 *--------------------------------------------------------------------------- *)

module Repro1750 = begin
    let rec loop x = loop x
    let f p x =
      let test = 
        match int16 x with
          | 0s  when (try p x finally ()) -> 3
    (*    | 1  when (for x = 1 to 100 do printf "" done; true) -> 3 *)
          | _ -> 5
      in
      loop test
end


(*---------------------------------------------------------------------------
!* BUG 2247: Unverifiable code from struct valued tyfunc
 *--------------------------------------------------------------------------- *)

module Repro2247 = begin

  [<Struct>]
  type MyLazy<'a> = struct
    [<DefaultValue(false)>]
    val mutable f : unit -> int
    member this.InitFun ff = this.f <- ff
  end

  let my_lazy_from_fun f =
    let r = MyLazy<_>() in    (* Keep this binding, required for repro *)
    r                         (* Warning: Fragile repro *)

  (* With optimisations off, code failed to verify *)
end


(*---------------------------------------------------------------------------
!* BUG 1190, 3569: record and list patterns do not permit trailing seperator
 *--------------------------------------------------------------------------- *)

module Repro_1190 = begin
  type R = {p:int;q:int}
  let fA {p=p;q=q}  = p+q
  let fB {p=p;q=q;} = p+q (* Fix 1190 *)
end

module Repro_3569 = begin
  let f    []     = 12
(*let fx   [;]    = 12 -- this is not permitted *)
  let fA   [p]    = p
  let fAx  [p;]   = p       
  let fAA  [p;q]  = p+q
  let fAAx [p;q;] = p+q (* Fix 3569 *)
end




(*---------------------------------------------------------------------------
!* BUG 3947
 *--------------------------------------------------------------------------- *)

module Repro_3947 = begin
  type          PublicType   = PTA | PTB of int
  type internal InternalType = ITA | ITB of int
#if COMPILING_WITH_EMPTY_SIGNATURE
  // PublicType is not actually public if there is a signature
#else
  do  check "Bug3947.Public%A"    (sprintf "%A (%A)"   PTA (PTB 2)) "PTA (PTB 2)"
#endif
  do  check "Bug3947.Public%+A"   (sprintf "%+A (%+A)" PTA (PTB 2)) "PTA (PTB 2)"
  do  check "Bug3947.Internal%+A" (sprintf "%+A (%+A)" ITA (ITB 2)) "ITA (ITB 2)"

  // The follow are not very useful outputs, but adding regression tests to pick up any changes...
  do  check "Bug3947.Internal%A.ITA" (sprintf "%A" ITA) "ITA"
  do  check "Bug3947.Internal%A.ITB" (sprintf "%A" (ITB 2)) "ITB 2"
end


(*---------------------------------------------------------------------------
!* BUG 4063: ilreflect ctor emit regression - Type/TypeBuilder change
 *--------------------------------------------------------------------------- *)

let _ = printf "========== Bug 4063 repro ==========\n"

// ctor case
type 'a T4063 = AT4063 of 'a
let valAT3063 = AT4063 12

type M4036<'a> = class new(x:'a) = {} end
let v4063 =  M4036<int>(1)

// method case?
type Taaaaa<'a>() = class end
type Taaaaa2<'a>() = class inherit Taaaaa<'a>() member x.M() = x end

// method case?
type Tbbbbb<'a>(x:'a) = class member this.M() = x end
type T2bbbbbb(x) = class inherit Tbbbbb<string>(x) end
(let t2 = T2bbbbbb("2") in t2.M)

let _ = printf "========== Bug 4063 done. ==========\n"


(*---------------------------------------------------------------------------
!* BUG 4139: %A formatter does not accept width, e.g. printf "%10000A"
 *--------------------------------------------------------------------------- *)

module Check4139 = begin
  do  check "Bug4139.percent.A" (sprintf "%8A"   [1..10]) "[1; 2; 3;\n 4; 5; 6;\n 7; 8; 9;\n 10]"
  do  check "Bug4139.percent.A" (sprintf "%8.4A" [1..10]) "[1; 2; 3;\n 4; ...]"
  do  check "Bug4139.percent.A" (sprintf "%0.4A" [1..10]) "[1; 2; 3; 4; ...]"
end


(*---------------------------------------------------------------------------
!* BUG 1043: Can (-star-) again be lexed specially and recognised as bracket-star-bracket?
 *--------------------------------------------------------------------------- *)

(* Make this the last test since it confuses fontification in tuareg mode *)  
module Check1043 = begin
 (* LBRACKET STAR RBRACKET becomes a valid operator identifier *)
 let (*) = 12            
 let x   = (*)           
 let f (*) = 12 + (*) 
 let x24 = f 12
end


(*---------------------------------------------------------------------------
!* Tail-cons optimized operators temporarily put 'null' in lists
 *--------------------------------------------------------------------------- *)

let rec checkForNoNullsInList a = 
  test "non-null list check" (match box a with null -> false | _ -> true);
  match a with 
  | [] -> ()
  | h::t -> checkForNoNullsInList t
    

module TestNoNullElementsInListChainFromPartition = begin



 let test l = 
   printfn "testing %A" l;
   let a,b = List.partition (fun x -> x = 1) l in
   checkForNoNullsInList a;
   checkForNoNullsInList b


 let _ = 
  test []

 let _ = 
  for i in [1;2] do 
       test [i]
  done

 let _ = 
  for i in [1;2] do 
    for j in [1;2] do 
       test [i;j]
    done
  done

 let _ = 
  for i in [1;2] do 
    for j in [1;2] do 
      for k in [1;2] do 
        test [i;j;k]
      done
    done
  done

end

module TestNoNullElementsInListChainFromInit = begin

 let test n x = 
   printfn "testing %A" n;
   let a = List.init n x in
   checkForNoNullsInList a


 let _ = 
  for i in 0..5 do
      test i (fun i -> i + 1)

  done

end

module TestNoNullElementsInListChainFromUnzip = begin

 let test x = 
   printfn "testing %A" x;
   let a,b = List.unzip x in
   checkForNoNullsInList a;
   checkForNoNullsInList b


 let _ = 
  for i in 0..5 do
      test (List.init i (fun i -> (i,i)))

  done

end


module TestNoNullElementsInListChainFromUnzip3 = begin

 let test x = 
   printfn "testing %A" x;
   let a,b,c = List.unzip3 x in
   checkForNoNullsInList a;
   checkForNoNullsInList b;
   checkForNoNullsInList c


 let _ = 
  for i in 0..5 do
      test (List.init i (fun i -> (i,i,i)))

  done

end

module TestListToString = begin

  do check "lknsdv0vrknl0" ([].ToString())  "[]"
  do check "lknsdv0vrknl1" ([1].ToString())  "[1]"
  do check "lknsdv0vrknl2" ([1; 2].ToString())  "[1; 2]"
  do check "lknsdv0vrknl3" ([1; 2; 3].ToString())  "[1; 2; 3]"
  do check "lknsdv0vrknl4" ([1; 2; 3; 4].ToString())  "[1; 2; 3; ... ]"
  do check "lknsdv0vrknl5" (["1"].ToString())  "[1]"  // unfortunate but true - no quotes
  do check "lknsdv0vrknl6" (["1"; null].ToString())  "[1; null]"
  do check "lknsdv0vrknl7" ([None].ToString())  "[null]" // unfortunate but true

  do check "lknsdv0vrknl01" (Some(1).ToString())  "Some(1)" 
  do check "lknsdv0vrknl02" (Some(None).ToString())  "Some(null)" // unfortunate but true
  do check "lknsdv0vrknl03" (Some(1,2).ToString())  "Some((1, 2))" // unfortunate but true
  do check "lknsdv0vrknl04" (Some(Some(1)).ToString())  "Some(Some(1))"
end



module SetToString = begin
    do check "cewjhnkrveo1" ((set []).ToString()) "set []"
    do check "cewjhnkrveo2" ((set [1]).ToString()) "set [1]"
    do check "cewjhnkrveo3" ((set [1;2]).ToString()) "set [1; 2]"
    do check "cewjhnkrveo4" ((set [1;2;3]).ToString()) "set [1; 2; 3]"
    do check "cewjhnkrveo5" ((set [3;2;1]).ToString()) "set [1; 2; 3]"
    do check "cewjhnkrveo6" ((set [2;3;4]).ToString()) "set [2; 3; 4]"
    do check "cewjhnkrveo7" ((set [1;2;3;4]).ToString()) "set [1; 2; 3; ... ]"
    do check "cewjhnkrveo8" ((set [4;3;2;1]).ToString()) "set [1; 2; 3; ... ]"

    do check "cewjhnkrveo11" ((Map.ofList []).ToString()) "map []"
    do check "cewjhnkrveo21" ((Map.ofList [(1,10)]).ToString()) "map [(1, 10)]"
    do check "cewjhnkrveo31" ((Map.ofList [(1,10);(2,20)]).ToString()) "map [(1, 10); (2, 20)]"
    do check "cewjhnkrveo41" ((Map.ofList [(1,10);(2,20);(3,30)]).ToString()) "map [(1, 10); (2, 20); (3, 30)]"
    do check "cewjhnkrveo51" ((Map.ofList [(3,30);(2,20);(1,10)]).ToString()) "map [(1, 10); (2, 20); (3, 30)]"
    do check "cewjhnkrveo61" ((Map.ofList [(2,20);(3,30);(4,40)]).ToString()) "map [(2, 20); (3, 30); (4, 40)]"
    do check "cewjhnkrveo71" ((Map.ofList [(1,10);(2,20);(3,30);(4,40)]).ToString()) "map [(1, 10); (2, 20); (3, 30); ... ]"
    do check "cewjhnkrveo81" ((Map.ofList [(4,40);(3,30);(2,20);(1,10)]).ToString()) "map [(1, 10); (2, 20); (3, 30); ... ]"

    do check "cewjhnkrveo1p" ((set []) |> sprintf "%A") "set []"
    do check "cewjhnkrveo2p" ((set [1]) |> sprintf "%A") "set [1]"
    do check "cewjhnkrveo3p" ((set [1;2]) |> sprintf "%A") "set [1; 2]"
    do check "cewjhnkrveo4p" ((set [1;2;3]) |> sprintf "%A") "set [1; 2; 3]"
    do check "cewjhnkrveo5p" ((set [3;2;1]) |> sprintf "%A") "set [1; 2; 3]"
    do check "cewjhnkrveo6p" ((set [2;3;4]) |> sprintf "%A") "set [2; 3; 4]"
    do check "cewjhnkrveo7p" ((set [1;2;3;4]) |> sprintf "%A") "set [1; 2; 3; 4]"
    do check "cewjhnkrveo8p" ((set [4;3;2;1]) |> sprintf "%A") "set [1; 2; 3; 4]"

    do check "cewjhnkrveo11p" ((Map.ofList []) |> sprintf "%A") "map []"
    do check "cewjhnkrveo21p" ((Map.ofList [(1,10)]) |> sprintf "%A") "map [(1, 10)]"
    do check "cewjhnkrveo31p" ((Map.ofList [(1,10);(2,20)]) |> sprintf "%A") "map [(1, 10); (2, 20)]"
    do check "cewjhnkrveo41p" ((Map.ofList [(1,10);(2,20);(3,30)]) |> sprintf "%A") "map [(1, 10); (2, 20); (3, 30)]"
    do check "cewjhnkrveo51p" ((Map.ofList [(3,30);(2,20);(1,10)]) |> sprintf "%A") "map [(1, 10); (2, 20); (3, 30)]"
    do check "cewjhnkrveo61p" ((Map.ofList [(2,20);(3,30);(4,40)]) |> sprintf "%A") "map [(2, 20); (3, 30); (4, 40)]"
    do check "cewjhnkrveo71p" ((Map.ofList [(1,10);(2,20);(3,30);(4,40)]) |> sprintf "%A") "map [(1, 10); (2, 20); (3, 30); (4, 40)]"
    do check "cewjhnkrveo81p" ((Map.ofList [(4,40);(3,30);(2,20);(1,10)]) |> sprintf "%A") "map [(1, 10); (2, 20); (3, 30); (4, 40)]"
end

(*---------------------------------------------------------------------------
!* Bug 5816: Unable to define mutually recursive types with mutually recursive generic constraints within FSI
 *--------------------------------------------------------------------------- *)
module Bug5816 = begin
  type IView<'v, 'vm when 'v :> IView<'v,'vm> and 'vm :> IViewModel<'v,'vm>> = interface
        abstract ViewModel : 'vm
    end 
  and IViewModel<'v, 'vm when 'v :> IView<'v,'vm> and 'vm :> IViewModel<'v,'vm>> = interface
        abstract View : 'v
    end 
end

(*---------------------------------------------------------------------------
!* Bug 5825: Constraints with nested types
 *--------------------------------------------------------------------------- *)
module Bug5825 = begin
  type I = interface
        abstract member m : unit 
    end
  type C() = class
        interface I with 
            member this.m = () 
        end
    end
  let f (c : #C) = () 
end

module Bug5981 = begin
    // guard against type variable tokens leaking into the IL stream
    // (in this case, we're trying to ensure that the lambda is handled properly)
    let t1 = Array2D.init 2 2 (fun x y -> x,y)
    
    // test basic use of indirect calling
    let t2 = Array2D.zeroCreate<int> 10 10
    
    let throwAnException (a: int) =
        raise (System.Exception("Foo!"))
        Array2D.zeroCreate<'T> a a
            
    let throwAnExceptionUnit () =
        raise (System.Exception("Foo!"))
        Array2D.zeroCreate<'T> 10 10
        
    // ensure that TargetInvocationException is properly unwrapped
    do test "cwewe0982" ((try throwAnException<int>(10) |> ignore ; false with | :? System.Reflection.TargetInvocationException -> false | _ -> true))
    
    // same as above, making sure that we handle methods that take no args properly
    do test "cwewe0982" ((try throwAnExceptionUnit<int>() |> ignore ; false with | :? System.Reflection.TargetInvocationException -> false | _ -> true))
        
end

module Bug920236 = 
  open System.Collections
  open System.Collections.Generic

  type Arr(a : int[]) =
      interface IEnumerable with
          member this.GetEnumerator() = 
              let i = ref -1
              { new IEnumerator with
                  member this.Reset() = failwith "not supported"
                  member this.MoveNext() = incr i; !i < a.Length
                  member this.Current = box (a.[!i]) 
              }

  let a = Arr([|1|])
  let result = ref []
  for i in a do
      result := i::(!result)  
  do test "hfduweyr" (!result = [box 1])

module TripleQuoteStrings = 

    check "ckjenew-0ecwe1" """Hello world""" "Hello world"
    check "ckjenew-0ecwe2" """Hello "world""" "Hello \"world"
    check "ckjenew-0ecwe3" """Hello ""world""" "Hello \"\"world"
#if UNIX
#else
#if INTERACTIVE // FSI prints \r\n or \n depending on PIPE vs FEED so we'll just skip it
#else
    if System.Environment.GetEnvironmentVariable("APPVEYOR_CI") <> "1" then
        check "ckjenew-0ecwe4" """Hello 
""world""" "Hello \r\n\"\"world"
#endif
#endif
    // cehcek there is no escaping...
    check "ckjenew-0ecwe5" """Hello \"world""" "Hello \\\"world"
    check "ckjenew-0ecwe6" """Hello \\"world""" "Hello \\\\\"world"
    check "ckjenew-0ecwe7" """Hello \nworld""" "Hello \\nworld"
    check "ckjenew-0ecwe8" """Hello \""" "Hello \\"
    check "ckjenew-0ecwe9" """Hello \\""" "Hello \\\\"
    check "ckjenew-0ecwe10" """Hello \n""" "Hello \\n"

    // check some embedded comment terminators
    check "ckjenew-0ecwe11" (* """Hello *) world""" *) """Hello world""" "Hello world"
    check "ckjenew-0ecwe1" (* (* """Hello *) world""" *) *) """Hello world""" "Hello world"
    check "ckjenew-0ecwe2" (* """Hello *) "world""" *) """Hello "world""" "Hello \"world"


#if MONO
#else
module FloatInRegisterConvertsToFloat = 

    let simpleTest() = 
        let x : float = -1.7976931348623157E+308 
        let sum = x + x 
        let equal = (sum = float (x + x))
        test "vw09rwejkn" equal 

    simpleTest()
#endif

(*---------------------------------------------------------------------------
!* Bug 122495: Bad code generation in code involving structs/property settings/slice operator
 *--------------------------------------------------------------------------- *)  
module bug122495 =
    [<Struct>]
    [<NoComparison;NoEquality>]
    type C =
        [<DefaultValue>]
        val mutable private goo : byte []
        // Note: you need some kind of side effect or use of 'x' here
        member this.P with set(x) = this.goo <- x
    
    let a = [|0uy..10uy|]
    // this should not throw an InvalidProgramException    
    let c = C( P = a.[0..1])


#if !NETCOREAPP
(*---------------------------------------------------------------------------
!* Bug 33760: wrong codegen for params[] Action overload
 *--------------------------------------------------------------------------- *)      
module bug33760 =
    open System
    open System.Threading.Tasks
    
    type C() = 
        static member M1([<ParamArray>] arg: System.Action []) = ()

    // these just need to typecheck
    C.M1(fun () -> ())
    Parallel.Invoke(fun () -> ())
#endif


module Regression_139182 = 
    [<Struct>]
    type S =
      member x.TheMethod() = x.TheMethod() : int64
    let theMethod (s:S) = s.TheMethod()
    type T() =
      static let s = S()
      static let str = "test"
      let s2 = S()
      static member Prop1 = s.TheMethod()                // error FS0422
      static member Prop2 = theMethod s                  // ok
      static member Prop3 = let s' = s in s'.TheMethod() // ok
      static member Prop4 = str.ToLower()                // ok
      member x.Prop5      = s2.TheMethod()               // ok

module LittleTestFor823 = 
    let x, y = 1, 2
    let v = Some ((x = y), (x = x))


(*---------------------------------------------------------------------------
!* wrap up
 *--------------------------------------------------------------------------- *)

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

