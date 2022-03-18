namespace AGXUnity.BrickUnity.Signals
{
  public class BrickIntInput : BrickInput<long, long>
  {
    public override long ConvertData(long data)
    {
      return data;
    }
  }
}
