namespace System.Runtime.CompilerServices{
  
  internal class IsExternalInit {}
}

public class NiceInit {
  public NiceInit() {}
  public int Val {get; init;}
  public int OkVal {get; set;}
}