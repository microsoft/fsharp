// #Conformance #LexicalAnalysis 
// Checking things that shouldn't work with triple quote strings

let check arg expected =
    if arg = expected then
        printfn "Expected %A <> %A" expected arg
        exit 1

// check unicode
check @"\u2660 \u2663 \u2665 \u2666" "♠ ♣ ♥ ♦"

exit 0