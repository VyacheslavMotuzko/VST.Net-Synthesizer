using System;
using System.Collections.Generic;
using Syntage.Framework.Audio;
using Syntage.Framework.Parameters;
using Syntage.Framework.Tools;

namespace SynthNet.Logic
{
    public class OscParameters : SyntageAudioProcessorComponentWithParameters<AudioProcessor>
    {
        public VolumeParameter Volume { get; private set; }
        public EnumParameter<EOscillatorType> OscillatorType { get; private set; }
        public RealParameter Fine { get; private set; }
        public RealParameter Panning { get; private set; }
        public RealParameter Semitone { get; private set; }

        public OscParameters(AudioProcessor audioProcessor) : base(audioProcessor)
        {
        }

        public override IEnumerable<Parameter> CreateParameters(string parameterPrefix)
        {
            Volume = new VolumeParameter(parameterPrefix + "Vol", "Oscillator Volume");
            OscillatorType = new EnumParameter<EOscillatorType>(parameterPrefix + "Osc", "Oscillator Type", "Osc", false);

            Fine = new RealParameter(parameterPrefix + "Fine", "Oscillator pitch", "Fine", -1, 1, 0.01);
            Fine.SetDefaultValue(0);

            Panning = new RealParameter(parameterPrefix + "Pan", "Oscillator Panorama", "", 0, 1, 0.01);
            Panning.SetDefaultValue(0.5);

            Semitone = new RealParameter(parameterPrefix + "Semi", "Oscillator Semitone", "Semitone", -24, 24, 1);
            Semitone.SetDefaultValue(0);

            return new List<Parameter> { Volume, OscillatorType, Fine, Panning, Semitone };
        }
    }
}
