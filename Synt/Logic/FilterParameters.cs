using System;
using System.Collections.Generic;
using Syntage.Framework.Audio;
using Syntage.Framework.Parameters;

namespace SynthNet.Logic
{
    public class FilterParameters : SyntageAudioProcessorComponentWithParameters<AudioProcessor>
    {
        public FilterParameters(AudioProcessor audioProcessor) : base(audioProcessor)
        {
        }

        public EnumParameter<EFilterPass> FilterType { get; private set; }
        public RealParameter CutoffFrequency { get; private set; }
        public RealParameter Resonance { get; private set; }
        public RealParameter EnvAmount { get; private set; }

        public override IEnumerable<Parameter> CreateParameters(string parameterPrefix)
        {
            FilterType = new EnumParameter<EFilterPass>(parameterPrefix + "Pass", "Filter Type", "Filter", false);
            CutoffFrequency = new RealParameter(parameterPrefix + "Cutoff", "Filter Cutoff Frequency", "Cutoff", 0.0, 0.99, 0.01);
            Resonance = new RealParameter(parameterPrefix + "Res", "Filter Resonance", "Resonance", 0.0, 1.0, 0.01);
            EnvAmount = new RealParameter(parameterPrefix + "Amnt", "Envelope Amount", "%", 0.01, 0.99, 0.01, false);
            return new List<Parameter> { FilterType, CutoffFrequency,Resonance,EnvAmount };
        }

    }
}
