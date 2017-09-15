using System;
using System.Collections.Generic;
using Syntage.Framework.Audio;
using Syntage.Framework.MIDI;
using Syntage.Framework.Parameters;
using Syntage.Framework.Tools;

namespace SynthNet.Logic
{
    public class LFO : SyntageAudioProcessorComponentWithParameters<AudioProcessor>, IParameterModifier
    {
        private double _time;
        private Parameter _target;
        private static readonly Random _random = new Random();

        private class ParameterName : IntegerParameter
        {
            private readonly ParametersManager _parametersManager;

            public ParameterName(string parameterPrefix, ParametersManager parametersManager) :
                base(parameterPrefix + "Num", "LFO Parameter Number", "Num", -1, 34, 1, false)
            {
                _parametersManager = parametersManager;
            }

            public override int FromStringToValue(string s)
            {
                var parameter = _parametersManager.FindParameter(s);
                return (parameter == null) ? -1 : _parametersManager.GetParameterIndex(parameter);
            }

            public override string FromValueToString(int value)
            {
                return (value >= 0) ? _parametersManager.GetParameter(value).Name : "--";
            }
        }

        public EnumParameter<EOscillatorType> OscillatorType { get; private set; }
        public FrequencyParameter Frequency { get; private set; }
        public BooleanParameter MatchKey { get; private set; }
        public RealParameter Gain { get; private set; }
        public IntegerParameter TargetParameter { get; private set; }

        public LFO(AudioProcessor audioProcessor) :
            base(audioProcessor)
        {
            audioProcessor.PluginController.MidiListener.OnNoteOn += MidiListenerOnNoteOn;
        }

        public override IEnumerable<Parameter> CreateParameters(string parameterPrefix)
        {
            OscillatorType = new EnumParameter<EOscillatorType>(parameterPrefix + "Osc", "LFO Type", "Osc", false);
            Frequency = new FrequencyParameter(parameterPrefix + "Frq", "LFO Frequency", "Frq", 0.01, 1000);
            MatchKey = new BooleanParameter(parameterPrefix + "Mtch", "LFO Phase Key Link", "Match", false);
            Gain = new RealParameter(parameterPrefix + "Gain", "LFO Gain", "Gain", 0, 1, 0.01, false);
            TargetParameter = new ParameterName(parameterPrefix, Processor.PluginController.ParametersManager);

            TargetParameter.OnValueChange += TargetParameterNumberOnValueChange;

            return new List<Parameter> {OscillatorType, Frequency, MatchKey, Gain, TargetParameter};
        }

        public void Process()
        {
            _time += Processor.CurrentStreamLenght / Processor.SampleRate;
        }

        public double ModifyValue(double currentValue, int sampleNumber)
        {
            var gain = Gain.Value;
            if (DSPFunctions.IsZero(gain))
                return currentValue;

            var amplitude = GetCurrentAmplitude(sampleNumber);
            gain *= amplitude * Math.Min(currentValue, 1 - currentValue);

            return DSPFunctions.Clamp01(currentValue + gain);
        }

        private double GetCurrentAmplitude(int sampleNumber)
        {
            var timePass = sampleNumber / Processor.SampleRate;
            var currentTime = _time + timePass;
            var sample = GenerateNextSample(OscillatorType.Value, Frequency.ProcessedValue(sampleNumber), currentTime);

            return sample;
        }

        private void MidiListenerOnNoteOn(object sender, MidiListener.NoteEventArgs e)
        {
            if (MatchKey.Value)
                _time = 0;
        }

        private void TargetParameterNumberOnValueChange(Parameter.EChangeType obj)
        {
            var number = TargetParameter.Value;
            var parameter = (number >= 0) ? Processor.PluginController.ParametersManager.GetParameter(number) : null;

            if (_target != null)
                _target.ParameterModifier = null;

            _target = parameter;

            if (_target != null)
                _target.ParameterModifier = this;
        }

        private static double GenerateNextSample(EOscillatorType oscillatorType, double frequency, double time)
        {
            var ph = time * frequency;
            ph -= (int)ph;

            return GetTableSample(oscillatorType, ph);
        }

        private static double GetTableSample(EOscillatorType oscillatorType, double t)
        {
            switch (oscillatorType)
            {
                case EOscillatorType.Sine:
                    return Math.Sin(DSPFunctions.Pi2 * t);

                case EOscillatorType.Triangle:
                    if (t < 0.25) return 4 * t;
                    if (t < 0.75) return 2 - 4 * t;
                    return 4 * (t - 1);

                case EOscillatorType.Square:
                    return (t < 0.5f) ? 1 : -1;

                case EOscillatorType.Saw:
                    return 2 * t - 1;

                case EOscillatorType.Noise:
                    return _random.NextDouble() * 2 - 1;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
