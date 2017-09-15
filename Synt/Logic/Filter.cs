using System;
using Syntage.Framework.Audio;
using Syntage.Framework.Parameters;

namespace SynthNet.Logic
{
    public enum EFilterPass
    {
        None,
        LowPass,
        HiPass,
        BandPass
    }

    public class Filter : SyntageAudioProcessorComponent<AudioProcessor>
    {     
        private double feedbackAmount;
        private double buf0;
        private double buf1;
        private double buf2;
        private double buf3;
        
        private FilterParameters paramsOwner;
        public Envelope filterEnv;
        private EFilterPass pass;

        public Filter(AudioProcessor audioProcessor,FilterParameters owner) : base(audioProcessor)
        {
            filterEnv = new Envelope(Processor.FilterEnv, audioProcessor);
            paramsOwner = owner;
            pass = owner.FilterType.Value;
            owner.FilterType.OnValueChange += PassChanged;
        }

        private void PassChanged(Parameter.EChangeType obj)
        {
            pass = paramsOwner.FilterType.Value;
        }

        void CalculateFeedbackAmount(int i)
        {
            feedbackAmount = paramsOwner.Resonance.ProcessedValue(i) + paramsOwner.Resonance.ProcessedValue(i) / (1.0 - CalculatedCutoff(i));
        }

        double CalculatedCutoff(int i)
        {
            return Math.Max(Math.Min(paramsOwner.CutoffFrequency.ProcessedValue(i) + 
                (filterEnv.GetNextMultiplier(i) * paramsOwner.EnvAmount.Value), 0.99), 0.01);
        }

        public double Process(double input,int i)
        {
            if (pass == EFilterPass.None)
                return input;
            if (input == 0.0) return input ;
            CalculateFeedbackAmount(i);
            double calculatedCutoff = CalculatedCutoff(i);
            buf0 += calculatedCutoff * (input - buf0 + feedbackAmount * (buf0 - buf1));
            buf1 += calculatedCutoff * (buf0 - buf1);
            buf2 += calculatedCutoff * (buf1 - buf2);
            buf3 += calculatedCutoff * (buf2 - buf3);
            switch (pass)
            {
                case EFilterPass.LowPass:
                    input = buf3;
                    break;
                case EFilterPass.HiPass:
                    input = input- buf3;
                    break;
                case EFilterPass.BandPass:
                    input = buf0 - buf3;
                    break;
            }

            return input;
        }        
    }
}
    

