namespace Syntage.Framework.Parameters
{
    public interface IParameterModifier
    {
        double ModifyValue(double currentValue, int sampleNumber);
    }
}
