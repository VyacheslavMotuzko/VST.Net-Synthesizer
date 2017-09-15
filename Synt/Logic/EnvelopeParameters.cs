using System;
using System.Collections.Generic;
using Syntage.Framework.Audio;
using Syntage.Framework.Parameters;

namespace SynthNet.Logic
{
    public class EnvelopeParameters : SyntageAudioProcessorComponentWithParameters<AudioProcessor>
    {
        public RealParameter Attack { get; private set; }
        public RealParameter Decay { get; private set; }
        public RealParameter Sustain { get; private set; }
        public RealParameter Release { get; private set; }

        public EnvelopeParameters(AudioProcessor audioProcessor) : base(audioProcessor)
        {
        }

        public override IEnumerable<Parameter> CreateParameters(string parameterPrefix)
        {
            Attack = new RealParameter(parameterPrefix + "Atk", "Envelope Attack", "", 0.01, 4, 0.01,false);
            Decay = new RealParameter(parameterPrefix + "Dec", "Envelope Decay", "", 0.01, 2, 0.01,false);
            Sustain = new RealParameter(parameterPrefix + "Stn", "Envelope Sustain", "", 0, 1, 0.01,false);
            Release = new RealParameter(parameterPrefix + "Rel", "Envelope Release", "", 0.01, 1, 0.01,false);

            return new List<Parameter> {Attack, Decay, Sustain, Release};
        }
    }
}
