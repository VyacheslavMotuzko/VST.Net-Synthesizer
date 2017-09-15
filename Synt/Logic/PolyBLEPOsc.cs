using System;
using Syntage.Framework.Audio;
using Syntage.Framework.Parameters;
using Syntage.Framework.Tools;

namespace SynthNet.Logic
{
    public enum EOscillatorType
    {
        Sine,
        Triangle,
        Square,
        Saw,
        Noise
    }

    public class Oscillator : SyntageAudioProcessorComponent<AudioProcessor>
    {
        private static double twoPI = 2 * Math.PI;
        private static Random _random = new Random();
        private int note;
        private double phase;
        private double phaseIncrement;
        private double lastOutput = 0.0;
        private double calc = 0.0;
        private EOscillatorType type;
        private double detune;
        public int Note { set { note = value; } }
        private OscParameters paramOwner;

        public Oscillator(AudioProcessor audioProcessor, OscParameters paramOwner,int note,double detune, double phase) :
            base(audioProcessor)
        {
            audioProcessor.OnSampleRateChanged += SampleRateChanged;
            paramOwner.OscillatorType.OnValueChange += TypeChanged;

            type = paramOwner.OscillatorType.Value;
            calc = twoPI / Processor.SampleRate;

            this.note = note;
            this.phase = phase;
            this.detune = detune;
            this.paramOwner = paramOwner;
        }

        private void TypeChanged(Parameter.EChangeType obj)
        {
            type = paramOwner.OscillatorType.Value;
        }

        private void SampleRateChanged(object sender, SyntageAudioProcessor.SampleRateEventArgs e)
        {
            calc = twoPI / Processor.SampleRate;
        }

        void UpdateIncrement(int i)
        {
            phaseIncrement = GetToneFrequency(i) * calc;
        }

        public double NextSample(int i)
        {
            double value = 0.0;
            double t = phase / twoPI;

            if (type == EOscillatorType.Sine)
            {
                value = naiveWaveformForMode(EOscillatorType.Sine);
            }
            else if (type == EOscillatorType.Saw)
            {
                value = naiveWaveformForMode(EOscillatorType.Saw);
                value -= PolyBlep(t);
            }
            else if (type == EOscillatorType.Noise)
            {
                value = naiveWaveformForMode(EOscillatorType.Noise);
            }
            else
            {
                value = naiveWaveformForMode(EOscillatorType.Square);
                value += PolyBlep(t);
                value -= PolyBlep(DSPFunctions.Fmod(t + 0.5, 1.0));
                if (type == EOscillatorType.Triangle)
                {
                    // Leaky integrator: y[n] = A * x[n] + (1 - A) * y[n-1]
                    value = phaseIncrement * value + (1 - phaseIncrement) * lastOutput;
                    lastOutput = value;
                }
            }

            UpdateIncrement(i);

            phase += phaseIncrement;
            while (phase >= twoPI)
            {
                phase -= twoPI;
            }
            return value;
        }

        double naiveWaveformForMode(EOscillatorType mode)
        {
            switch (mode)
            {
                case EOscillatorType.Sine:
                    return Math.Sin(phase);
                case EOscillatorType.Saw:
                    return (2.0 * phase / twoPI) - 1.0;
                case EOscillatorType.Square:
                    if (phase < Math.PI)
                    {
                        return 1.0;
                    }
                    else
                    {
                        return -1.0;
                    }
                case EOscillatorType.Noise:
                    return _random.NextDouble() * 2 - 1;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        double PolyBlep(double t)
        {
            double dt = phaseIncrement / twoPI;
            // 0 <= t < 1
            if (t < dt)
            {
                t /= dt;
                return t + t - t * t - 1.0;
            }
            // -1 < t < 0
            else if (t > 1.0 - dt)
            {
                t = (t - 1.0) / dt;
                return t * t + t + t + 1.0;
            }
            // 0 otherwise
            else return 0.0;
        }

        private double GetToneFrequency(int sampleNumber)
        {
            return DSPFunctions.GetNoteFrequency(note + paramOwner.Semitone.ProcessedValue(sampleNumber) + paramOwner.Fine.ProcessedValue(sampleNumber) + detune);
        }


    }
}
