using Syntage.Framework.Parameters;

namespace SynthNet.UI
{
    public interface IUIParameterController
    {
        void SetParameter(Parameter parameter);
        void UpdateController();
    }
}
