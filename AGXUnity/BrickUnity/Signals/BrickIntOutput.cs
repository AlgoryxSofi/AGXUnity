namespace AGXUnity.BrickUnity.Signals
{
  public class BrickIntOutput : BrickOutput<long, long>
  {
    protected override long GetSignalData(long internalData)
    {
      return internalData;
    }
  }
}
