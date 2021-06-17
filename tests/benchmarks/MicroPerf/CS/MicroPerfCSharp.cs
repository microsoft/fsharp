
using System.Runtime.CompilerServices;

public class MicroPerfCSharp
{
    //
    // FSharp will not inline the code so we shouldn't eiter.
    //
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Cond(int x)
    {
        if (x == 1 || x == 2) return 1;
        else if (x == 3 || x == 4) return 2;

        else return 8;
    }
}