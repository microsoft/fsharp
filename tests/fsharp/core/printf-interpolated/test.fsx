// #Conformance #Printing 

#if TESTS_AS_APP
module Core_printf_interp
#endif

open Printf

let failures = ref []

let report_failure (s : string) = 
    stderr.Write" NO: "
    stderr.WriteLine s
    failures := !failures @ [s]

// change this to true to run every test case
// leave as false to randomly execute a subset of cases (this is a very expensive test area)
let runEveryTest = true

let test t (s1:Lazy<string>) s2 = 
  stdout.WriteLine ("running test "+t+"...")   
  let s1 = s1.Force()
  if s1 <> s2 then 
    report_failure ("test "+t+": expected \n\t'"+s2+"' but produced \n\t'"+s1+"'")
  else
    stdout.WriteLine ("test "+t+": correctly produced '"+s1+"'")   

let verify actual expected = test expected actual expected

let adjust1 obj n1 = unbox ((unbox obj) n1)

let _ = test "cewoui20" (lazy(sprintf $"")) ""
let _ = test "cewoui21" (lazy(sprintf $"abc")) "abc"
let _ = test "cewoui22" (lazy(sprintf $"%d{3}")) "3"
let _ = test "cewoui2a" (lazy(sprintf $"%o{0}")) "0"
let _ = test "cewoui2b" (lazy(sprintf $"%o{0}")) "0"
let _ = test "cewoui2c" (lazy(sprintf $"%o{5}")) "5"
let _ = test "cewoui2q" (lazy(sprintf $"%o{8}")) "10"
let _ = test "cewoui2w" (lazy(sprintf $"%o{15}")) "17"
let _ = test "cewoui2e" (lazy(sprintf $"%o{System.Int32.MinValue}" )) "20000000000"
let _ = test "cewoui2r" (lazy(sprintf $"%o{0xffffffff}" )) "37777777777"
let _ = test "cewoui2t" (lazy(sprintf $"%o{System.Int32.MinValue+1}")) "20000000001"
let _ = test "cewoui2y" (lazy(sprintf $"%o{System.Int32.MaxValue}")) "17777777777"
let _ = test "cewoui2u" (lazy(sprintf $"%o{-1}" )) "37777777777"
let _ = test "cewoui2aB" (lazy(sprintf $"%B{0}")) "0"
let _ = test "cewoui2bB" (lazy(sprintf $"%B{0}")) "0"
let _ = test "cewoui2cB" (lazy(sprintf $"%B{5}")) "101"
let _ = test "cewoui2qB" (lazy(sprintf $"%B{8}")) "1000"
let _ = test "cewoui2wB" (lazy(sprintf $"%B{15}")) "1111"
let _ = test "cewoui2eB" (lazy(sprintf $"%B{System.Int32.MinValue}" )) "10000000000000000000000000000000"
let _ = test "cewoui2rB" (lazy(sprintf $"%B{0xffffffff}" )) "11111111111111111111111111111111"
let _ = test "cewoui2tB" (lazy(sprintf $"%B{System.Int32.MinValue+1}")) "10000000000000000000000000000001"
let _ = test "cewoui2yB" (lazy(sprintf $"%B{System.Int32.MaxValue}")) "1111111111111111111111111111111"
let _ = test "cewoui2uB" (lazy(sprintf $"%B{-1}" )) "11111111111111111111111111111111"

let f z = sprintf $"%o{z}"

let _ = test "cewoui2a" (lazy(f 0)) "0"
let _ = test "cewoui2s" (lazy(f 0)) "0"
let _ = test "cewoui2d" (lazy(f 5)) "5"
let _ = test "cewoui2f" (lazy(f 8)) "10"
let _ = test "cewoui2g" (lazy(f 15)) "17"
let _ = test "cewoui2h" (lazy(f System.Int32.MinValue)) "20000000000"
let _ = test "cewoui2j" (lazy(f 0xffffffff)) "37777777777"
let _ = test "cewoui2lk" (lazy(f (System.Int32.MinValue+1))) "20000000001"
let _ = test "cewoui2l" (lazy(f System.Int32.MaxValue)) "17777777777"
let _ = test "cewoui212" (lazy(f (-1))) "37777777777"

let fB z = sprintf $"%B{z}"

let _ = test "cewoui2aB" (lazy(fB 0)) "0"
let _ = test "cewoui2sB" (lazy(fB 0)) "0"
let _ = test "cewoui2dB" (lazy(fB 5)) "101"
let _ = test "cewoui2fB" (lazy(fB 8)) "1000"
let _ = test "cewoui2gB" (lazy(fB 15)) "1111"
let _ = test "cewoui2hB" (lazy(fB System.Int32.MinValue)) "10000000000000000000000000000000"
let _ = test "cewoui2jB" (lazy(fB 0xffffffff)) "11111111111111111111111111111111"
let _ = test "cewoui2lkB" (lazy(fB (System.Int32.MinValue+1))) "10000000000000000000000000000001"
let _ = test "cewoui2lB" (lazy(fB System.Int32.MaxValue)) "1111111111111111111111111111111"
let _ = test "cewoui212B" (lazy(fB (-1))) "11111111111111111111111111111111"

// check bprintf
let _ = test "csd3oui2!" (lazy(let buf = new System.Text.StringBuilder() in ignore (bprintf buf $"%x{0}%x{1}"); buf.ToString())) "01"

// check kbprintf
let _ = test "csd3oui2!1" (lazy(let buf = new System.Text.StringBuilder() in kbprintf (fun () -> buf.ToString())  buf $"%x{0}%x{1}")) "01"

let _ = test "cewoui2!" (lazy(sprintf $"%x{0}")) "0"
let _ = test "cewoui26" (lazy(sprintf $"%x{5}")) "5"
let _ = test "cewoui2f" (lazy(sprintf $"%x{8}")) "8"
let _ = test "cewoui29" (lazy(sprintf $"%x{15}")) "f"
let _ = test "cewoui2Z" (lazy(sprintf $"%x{System.Int32.MinValue}" )) "80000000"
let _ = test "cewoui2X" (lazy(sprintf $"%x{0xffffffff}" )) "ffffffff"
let _ = test "cewoui2A" (lazy(sprintf $"%x{System.Int32.MinValue+1}" )) "80000001"
let _ = test "cewoui2Q" (lazy(sprintf $"%x{System.Int32.MaxValue}" )) "7fffffff"

let fx z = sprintf $"%x{z}"
let _ = test "cewoui2W" (lazy(fx 0)) "0"
let _ = test "cewoui2E" (lazy(fx 5)) "5"
let _ = test "cewoui2R" (lazy(fx 8)) "8"
let _ = test "cewoui2T" (lazy(fx 15)) "f"
let _ = test "cewoui2Y" (lazy(fx System.Int32.MinValue)) "80000000"
let _ = test "cewoui2U" (lazy(fx 0xffffffff)) "ffffffff"
let _ = test "cewoui2I" (lazy(fx (System.Int32.MinValue+1))) "80000001"
let _ = test "cewoui2O" (lazy(fx System.Int32.MaxValue)) "7fffffff"

let _ = test "cewoui2Z" (lazy(sprintf $"%X{0}")) "0"
let _ = test "cewoui2X" (lazy(sprintf $"%X{5}")) "5"
let _ = test "cewoui2C" (lazy(sprintf $"%X{8}")) "8"
let _ = test "cewoui2V" (lazy(sprintf $"%X{15}")) "F"
let _ = test "cewoui2v" (lazy(sprintf $"%X{System.Int32.MinValue}" )) "80000000"
let _ = test "cewoui2B" (lazy(sprintf $"%X{0xffffffff}" )) "FFFFFFFF"
let _ = test "cewoui2N" (lazy(sprintf $"%X{System.Int32.MinValue+1}")) "80000001"
let _ = test "cewoui2M" (lazy(sprintf $"%X{System.Int32.MaxValue}" )) "7FFFFFFF"

let _ = test "cewou44a" (lazy(sprintf $"%u{0}")) "0"
let _ = test "cewou44b" (lazy(sprintf $"%u{5}" )) "5"
let _ = test "cewou44c" (lazy(sprintf $"%u{8}" )) "8"
let _ = test "cewou44d" (lazy(sprintf $"%u{15}" )) "15"
let _ = test "cewou44e" (lazy(sprintf $"%u{System.Int32.MinValue}" )) "2147483648"
let _ = test "cewou44f" (lazy(sprintf $"%u{0xffffffff}" )) "4294967295"
let _ = test "cewou44g" (lazy(sprintf $"%u{System.Int32.MinValue+1}" )) "2147483649"
let _ = test "cewou44h" (lazy(sprintf $"%u{System.Int32.MaxValue}" )) "2147483647"

let _ = test "cewou45a" (lazy(sprintf $"%d{0ul}" )) "0"
let _ = test "cewou45b" (lazy(sprintf $"%d{5ul}" )) "5"
let _ = test "cewou45c" (lazy(sprintf $"%d{8ul}" )) "8"
let _ = test "cewou45d" (lazy(sprintf $"%d{15ul}" )) "15"
let _ = test "cewou45e" (lazy(sprintf $"%d{2147483648ul}" )) "2147483648"
let _ = test "cewou45f" (lazy(sprintf $"%d{4294967295ul}" )) "4294967295"
let _ = test "cewou45g" (lazy(sprintf $"%d{2147483649ul}" )) "2147483649"
let _ = test "cewou45h" (lazy(sprintf $"%d{2147483647ul}" )) "2147483647"

let _ = test "cewou46a" (lazy(sprintf $"%d{0ul}" )) "0"
let _ = test "cewou46b" (lazy(sprintf $"%d{5ul}" )) "5"
let _ = test "cewou46c" (lazy(sprintf $"%d{8ul}" )) "8"
let _ = test "cewou46d" (lazy(sprintf $"%d{15ul}" )) "15"
let _ = test "cewou46e" (lazy(sprintf $"%d{2147483648ul}" )) "2147483648"
let _ = test "cewou46f" (lazy(sprintf $"%d{4294967295ul}" )) "4294967295"
let _ = test "cewou46g" (lazy(sprintf $"%d{2147483649ul}" )) "2147483649"
let _ = test "cewou46h" (lazy(sprintf $"%d{2147483647ul}" )) "2147483647"

let _ = test "ceew903" (lazy(sprintf $"%u{System.Int64.MaxValue}" )) "9223372036854775807"
let _ = test "ceew903" (lazy(sprintf $"%u{System.Int64.MinValue}" )) "9223372036854775808"
let _ = test "ceew903" (lazy(sprintf $"%d{System.Int64.MaxValue}" )) "9223372036854775807"
let _ = test "ceew903" (lazy(sprintf $"%d{System.Int64.MinValue}" )) "-9223372036854775808"

let _ = test "ceew903" (lazy(sprintf $"%u{System.Int64.MaxValue}" )) "9223372036854775807"
let _ = test "ceew903" (lazy(sprintf $"%u{System.Int64.MinValue}" )) "9223372036854775808"
let _ = test "ceew903" (lazy(sprintf $"%d{System.Int64.MaxValue}" )) "9223372036854775807"
let _ = test "ceew903" (lazy(sprintf $"%d{System.Int64.MinValue}" )) "-9223372036854775808"

let _ = test "cewou47a" (lazy(sprintf $"%d{0n}" )) "0"
let _ = test "cewou47b" (lazy(sprintf $"%d{5n}" )) "5"
let _ = test "cewou47c" (lazy(sprintf $"%d{8n}" )) "8"
let _ = test "cewou47d" (lazy(sprintf $"%d{15n}" )) "15"
let _ = test "cewou47e" (lazy(sprintf $"%u{2147483648n}" )) "2147483648"
let _ = test "cewou47f" (lazy(sprintf $"%u{4294967295n}" )) "4294967295"
let _ = test "cewou47g" (lazy(sprintf $"%u{2147483649n}" )) "2147483649"
let _ = test "cewou47h" (lazy(sprintf $"%u{2147483647n}" )) "2147483647"

let _ = test "cewou47a" (lazy(sprintf $"%d{0n}" )) "0"
let _ = test "cewou47b" (lazy(sprintf $"%d{5n}" )) "5"
let _ = test "cewou47c" (lazy(sprintf $"%d{8n}" )) "8"
let _ = test "cewou47d" (lazy(sprintf $"%d{15n}" )) "15"
let _ = test "cewou47e" (lazy(sprintf $"%u{2147483648n}" )) "2147483648"
let _ = test "cewou47f" (lazy(sprintf $"%u{4294967295n}" )) "4294967295"
let _ = test "cewou47g" (lazy(sprintf $"%u{2147483649n}" )) "2147483649"
let _ = test "cewou47h" (lazy(sprintf $"%u{2147483647n}" )) "2147483647"

let _ = test "cewou48a" (lazy(sprintf $"%d{0un}" )) "0"
let _ = test "cewou48b" (lazy(sprintf $"%d{5un}" )) "5"
let _ = test "cewou48c" (lazy(sprintf $"%d{8un}" )) "8"
let _ = test "cewou48d" (lazy(sprintf $"%d{15un}" )) "15"
let _ = test "cewou48e" (lazy(sprintf $"%u{2147483648un}" )) "2147483648"
let _ = test "cewou48f" (lazy(sprintf $"%u{4294967295un}" )) "4294967295"
let _ = test "cewou48g" (lazy(sprintf $"%u{2147483649un}" )) "2147483649"
let _ = test "cewou48h" (lazy(sprintf $"%u{2147483647un}" )) "2147483647"

let _ = test "cewou49c" (lazy(sprintf $"%+d{5}" )) "+5"
let _ = test "cewou49d" (lazy(sprintf $"% d{5}" )) " 5"
let _ = test "cewou49e" (lazy(sprintf $"%+4d{5}" )) "  +5"
let _ = test "cewou49f" (lazy(sprintf $"%-+4d{5}" )) "+5  "
let _ = test "cewou49g" (lazy(sprintf $"%-4d{5}" )) "5   "
let _ = test "cewou49h" (lazy(sprintf $"%- 4d{5}" )) " 5  "
let _ = test "cewou49i" (lazy(sprintf $"%- d{5}" )) " 5"
let _ = test "cewou49j" (lazy(sprintf $"% d{5}" )) " 5"
let _ = test "weioj31" (lazy(sprintf $"%3d{5}")) "  5"
let _ = test "weioj32" (lazy(sprintf $"%1d{5}")) "5"
let _ = test "weioj33" (lazy(sprintf $"%2d{500}")) "500"

let _ =
    test "test8535" (lazy(sprintf $"""%t{(fun _ -> "???")}""" )) "???"
    test "test8536" (lazy(sprintf $"A%d{0}B")) "A0B"
    test "test8537" (lazy(sprintf $"A%d{0}B%d{1}C")) "A0B1C"
    test "test8538" (lazy(sprintf $"A%d{0}B%d{1}C%d{2}D")) "A0B1C2D"
    test "test8539" (lazy(sprintf $"A%d{0}B%d{1}C%d{2}D%d{3}E")) "A0B1C2D3E"
    test "test8540" (lazy(sprintf $"A%d{0}B%d{1}C%d{2}D%d{3}E%d{4}F")) "A0B1C2D3E4F"
    test "test8541" (lazy(sprintf $"A%d{0}B%d{1}C%d{2}D%d{3}E%d{4}F%d{5}G")) "A0B1C2D3E4F5G"
    test "test8542" (lazy(sprintf $"A%d{0}B%d{1}C%d{2}D%d{3}E%d{4}F%d{5}G%d{6}H")) "A0B1C2D3E4F5G6H"
    test "test8543" (lazy(sprintf $"A%d{0}B%d{1}C%d{2}D%d{3}E%d{4}F%d{5}G%d{6}H%d{7}I")) "A0B1C2D3E4F5G6H7I"


let _ = 
    test "test8361" (lazy(sprintf $"""%A{"abc"}""")) "\"abc\""
    test "test8362" (lazy(sprintf $"""%5A{"abc"}""")) "\"abc\""
    test "test8363" (lazy(sprintf $"""%1A{"abc"}""")) "\"abc\""
    test "test8365" (lazy(sprintf $"""%.5A{"abc"}""")) "\"abc\""
    test "test8368" (lazy(sprintf $"""%-A{"abc"}""")) "\"abc\""
    test "test8369" (lazy(sprintf $"""%-5A{"abc"}""")) "\"abc\""
    test "test8370" (lazy(sprintf $"""%-1A{"abc"}""")) "\"abc\""
    test "test8372" (lazy(sprintf $"""%-.5A{"abc"}""")) "\"abc\""
    test "test8375" (lazy(sprintf $"""%+A{"abc"}""")) "\"abc\""
    test "test8376" (lazy(sprintf $"""%+5A{"abc"}""")) "\"abc\""
    test "test8377" (lazy(sprintf $"""%+1A{"abc"}""")) "\"abc\""
    test "test8379" (lazy(sprintf $"""%+.5A{"abc"}""")) "\"abc\""
    test "test8382" (lazy(sprintf $"""%-+A{"abc"}""")) "\"abc\""
    test "test8383" (lazy(sprintf $"""%-+5A{"abc"}""")) "\"abc\""
    test "test8384" (lazy(sprintf $"""%-+1A{"abc"}""")) "\"abc\""
    test "test8386" (lazy(sprintf $"""%-+.5A{"abc"}""")) "\"abc\""
    test "test8389" (lazy(sprintf $"""%A{15}""")) "15"
    test "test8390" (lazy(sprintf $"""%5A{15}""")) "15"
    test "test8391" (lazy(sprintf $"""%1A{15}""")) "15"
    test "test8393" (lazy(sprintf $"""%.5A{15}""")) "15"
    test "test8396" (lazy(sprintf $"""%-A{15}""")) "15"
    test "test8397" (lazy(sprintf $"""%-5A{15}""")) "15"
    test "test8398" (lazy(sprintf $"""%-1A{15}""")) "15"
    test "test8400" (lazy(sprintf $"""%-.5A{15}""")) "15"
    test "test8403" (lazy(sprintf $"""%+A{15}""")) "15"
    test "test8404" (lazy(sprintf $"""%+5A{15}""")) "15"
    test "test8405" (lazy(sprintf $"""%+1A{15}""")) "15"
    test "test8407" (lazy(sprintf $"""%+.5A{15}""")) "15"
    test "test8410" (lazy(sprintf $"""%-+A{15}""")) "15"
    test "test8411" (lazy(sprintf $"""%-+5A{15}""")) "15"
    test "test8412" (lazy(sprintf $"""%-+1A{15}""")) "15"
    test "test8414" (lazy(sprintf $"""%-+.5A{15}""")) "15"

    test "test8417" (lazy(sprintf $"""%A{-10}""")) "-10"
    test "test8418" (lazy(sprintf $"""%5A{-10}""")) "-10"
    test "test8419" (lazy(sprintf $"""%1A{-10}""")) "-10"
    test "test8421" (lazy(sprintf $"""%.5A{-10}""")) "-10"
    test "test8424" (lazy(sprintf $"""%-A{-10}""")) "-10"
    test "test8425" (lazy(sprintf $"""%-5A{-10}""")) "-10"
    test "test8426" (lazy(sprintf $"""%-1A{-10}""")) "-10"
    test "test8428" (lazy(sprintf $"""%-.5A{-10}""")) "-10"
    test "test8431" (lazy(sprintf $"""%+A{-10}""")) "-10"
    test "test8432" (lazy(sprintf $"""%+5A{-10}""")) "-10"
    test "test8433" (lazy(sprintf $"""%+1A{-10}""")) "-10"
    test "test8435" (lazy(sprintf $"""%+.5A{-10}""")) "-10"
    test "test8438" (lazy(sprintf $"""%-+A{-10}""")) "-10"
    test "test8439" (lazy(sprintf $"""%-+5A{-10}""")) "-10"
    test "test8440" (lazy(sprintf $"""%-+1A{-10}""")) "-10"
    test "test8442" (lazy(sprintf $"""%-+.5A{-10}""")) "-10"

    // Check the static type matters for %A holes
    test "test8445b1" (lazy(sprintf $"""%A{(Unchecked.defaultof<int option>)}""")) "None"
    test "test8445b2" (lazy(sprintf $"""%A{box (None: int option)}""")) "<null>"
    test "test8445b3" (lazy(sprintf $"""%A{(None: int option)}""")) "None"
    test "test8445b4" (lazy(sprintf $"""%A{(None: string option)}""")) "None"
    test "test8445b5" (lazy(sprintf $"""%A{(None: obj option)}""")) "None"
    test "test8445b6" (lazy($"""%A{(Unchecked.defaultof<int option>)}""")) "None"
    test "test8445b7a" (lazy($"""{null}""")) ""
    test "test8445b7b" (lazy($"""%O{null}""")) "<null>"
    test "test8445b8" (lazy($"""%A{null}""")) "<null>"
    test "test8445b9" (lazy($"""%A{box (None: int option)}""")) "<null>"
    test "test8445b10" (lazy($"""%A{(None: int option)}""")) "None"
    test "test8445b11" (lazy($"""%A{(None: string option)}""")) "None"
    test "test8445b12" (lazy($"""%A{(None: obj option)}""")) "None"

    test "test8445" (lazy(sprintf $"""%A{null}""")) "<null>"
    test "test8446" (lazy(sprintf $"""%5A{null}""")) "<null>"
    test "test8447" (lazy(sprintf $"""%1A{null}""")) "<null>"
    test "test8449" (lazy(sprintf $"""%.5A{null}""")) "<null>"
    test "test8452" (lazy(sprintf $"""%-A{null}""")) "<null>"
    test "test8453" (lazy(sprintf $"""%-5A{null}""")) "<null>"
    test "test8454" (lazy(sprintf $"""%-1A{null}""")) "<null>"
    test "test8456" (lazy(sprintf $"""%-.5A{null}""")) "<null>"
    test "test8459" (lazy(sprintf $"""%+A{null}""")) "<null>"
    test "test8460" (lazy(sprintf $"""%+5A{null}""")) "<null>"
    test "test8461" (lazy(sprintf $"""%+1A{null}""")) "<null>"
    test "test8463" (lazy(sprintf $"""%+.5A{null}""")) "<null>"
    test "test8466" (lazy(sprintf $"""%-+A{null}""")) "<null>"
    test "test8467" (lazy(sprintf $"""%-+5A{null}""")) "<null>"
    test "test8468" (lazy(sprintf $"""%-+1A{null}""")) "<null>"
    test "test8470" (lazy(sprintf $"""%-+.5A{null}""")) "<null>"


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

