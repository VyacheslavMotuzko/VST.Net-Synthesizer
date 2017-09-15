using System;
using Syntage.Framework.Parameters;
using Syntage.Framework.Tools;

namespace SynthNet.Logic
{
    public class WaveTableOsc
    {
        public struct WaveTable
        {
            public double topFreq;
            public int waveTableLen;
            public float[] waveTable;
        }

        private const int numWaveTableSlots = 32;
        private const float baseFrequency = 20f;
        private const uint overSamp = 2;

        private int note;
        public int Note { set { note = value; } }
        private OscParameters paramOwner;
        private AudioProcessor processor;

        protected double phasor;      // phase accumulator
        protected double phaseInc;    // phase increment
        protected double phaseOfs;    // phase offset for PWM

        // list of wavetables
        int numWaveTables;
        WaveTable[] waveTables = new WaveTable[numWaveTableSlots];

        // note: if you don't keep this in the range of 0-1, you'll need to make changes elsewhere
        public void SetFrequency(double inc)
        {
            phaseInc = inc;
        }

        // note: if you don't keep this in the range of 0-1, you'll need to make changes elsewhere
        public void SetPhaseOffset(double offset)
        {
            phaseOfs = offset;
        }

        public void UpdatePhase()
        {
            phasor += phaseInc;
            if (phasor >= 1.0)
                phasor -= 1.0;
        }

       
        public WaveTableOsc(AudioProcessor Processor,OscParameters paramOwnr)
        {
            processor = Processor;
            paramOwner = paramOwnr;
            phasor = 0.0;
            phaseInc = 0.0;
            phaseOfs = 0.5;
            numWaveTables = 0;
            for (int idx = 0; idx < numWaveTableSlots; idx++)
            {
                waveTables[idx].topFreq = 0;
                waveTables[idx].waveTableLen = 0;
                waveTables[idx].waveTable = null;
            }

            paramOwner.OscillatorType.OnValueChange += OscTypeChanged;
            if (paramOwner.OscillatorType.Value == EOscillatorType.Sine || paramOwner.OscillatorType.Value == EOscillatorType.Saw)
                SetOsc(baseFrequency, EOscillatorType.Saw);
            else if (paramOwner.OscillatorType.Value == EOscillatorType.Square)
                SetOsc(baseFrequency, EOscillatorType.Square);
            else if (paramOwner.OscillatorType.Value == EOscillatorType.Triangle)
                SetOsc(baseFrequency, EOscillatorType.Triangle);
        }

        private void OscTypeChanged(Parameter.EChangeType obj)
        {
            if (paramOwner.OscillatorType.Value == EOscillatorType.Saw)
                SetOsc(baseFrequency, EOscillatorType.Saw);
            else if(paramOwner.OscillatorType.Value == EOscillatorType.Square)
                SetOsc(baseFrequency, EOscillatorType.Square);
            else if (paramOwner.OscillatorType.Value == EOscillatorType.Triangle)
                SetOsc(baseFrequency, EOscillatorType.Triangle);
        }

        private double GetToneFrequency(int sampleNumber)
        {
            return DSPFunctions.GetNoteFrequency(note + paramOwner.Semitone.ProcessedValue(sampleNumber) + paramOwner.Fine.ProcessedValue(sampleNumber));
        }

        //
        // getOutput
        //
        // returns the current oscillator output
        //
        public double GetOutput(int i)
        {
            SetFrequency(GetToneFrequency(i) / processor.SampleRate);
            // grab the appropriate wavetable
            int waveTableIdx = 0;
            while ((this.phaseInc >= this.waveTables[waveTableIdx].topFreq) && (waveTableIdx < (this.numWaveTables - 1)))
            {
                ++waveTableIdx;
            }
            WaveTable waveTable = this.waveTables[waveTableIdx];
            // linear interpolation
            double temp = this.phasor * waveTable.waveTableLen;
            int intPart = (int)temp;
            double fracPart = temp - intPart;
            float samp0 = waveTable.waveTable[intPart];
            if (++intPart >= waveTable.waveTableLen)
                intPart = 0;
            float samp1 = waveTable.waveTable[intPart];
            UpdatePhase();
            return (samp0 + (samp1 - samp0) * fracPart);
        }

        //
        // getOutputMinusOffset
        //
        // for variable pulse width: initialize to sawtooth,
        // set phaseOfs to duty cycle, use this for osc output
        //
        // returns the current oscillator output
        //
        public double GetOutputMinusOffset()
        {
            // grab the appropriate wavetable
            int waveTableIdx = 0;
            while ((this.phaseInc >= this.waveTables[waveTableIdx].topFreq) && (waveTableIdx < (this.numWaveTables - 1)))
            {
                ++waveTableIdx;
            }
            WaveTable waveTable = this.waveTables[waveTableIdx];
            // linear
            double temp = this.phasor * waveTable.waveTableLen;
            int intPart = (int)temp;
            double fracPart = temp - intPart;
            float samp0 = waveTable.waveTable[intPart];
            if (++intPart >= waveTable.waveTableLen)
                intPart = 0;
            float samp1 = waveTable.waveTable[intPart];
            double samp = samp0 + (samp1 - samp0) * fracPart;

            // and linear again for the offset part
            double offsetPhasor = this.phasor + this.phaseOfs;
            if (offsetPhasor > 1.0)
                offsetPhasor -= 1.0;
            temp = offsetPhasor * waveTable.waveTableLen;
            intPart = (int)temp;
            fracPart = temp - intPart;
            samp0 = waveTable.waveTable[intPart];
            if (++intPart >= waveTable.waveTableLen)
                intPart = 0;
            samp1 = waveTable.waveTable[intPart];

            return samp - (samp0 + (samp1 - samp0) * fracPart);
        }

        void SetOsc(float baseFreq , EOscillatorType osctype)
        {
            ClearWaveTable();

            var sampleRate = paramOwner.Processor.SampleRate;
            // calc number of harmonics where the highest harmonic baseFreq and lowest alias an octave higher would meet
            int maxHarms = (int)(sampleRate / (3.0 * baseFreq) + 0.5);

            // round up to nearest power of two
            uint v = (uint)maxHarms;
            v--;            // so we don't go up if already a power of 2
            v |= v >> 1;    // roll the highest bit into all lower bits...
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;            // and increment to power of 2
            int tableLen = (int)(v * 2 * overSamp);  // double for the sample rate, then oversampling

            double[] ar = new double[tableLen];   // for ifft
            double[] ai = new double[tableLen];

            double topFreq = baseFreq * 2.0 / sampleRate;
            double scale = 0.0;
            for (; maxHarms >= 1; maxHarms >>= 1)
            {
                if(osctype == EOscillatorType.Saw)
                    DefineSawtooth(tableLen, maxHarms, ar, ai);
                else if (osctype == EOscillatorType.Triangle)
                    DefineTriangle(tableLen, maxHarms, ar, ai);
                else if (osctype == EOscillatorType.Square)
                    DefineSquare(tableLen, maxHarms, ar, ai);

                scale = makeWaveTable(tableLen, ar, ai, scale, topFreq);
                topFreq *= 2;
                if (tableLen > 99999) // variable table size (constant oversampling but with minimum table size)
                    tableLen >>= 1;
            }
        }

        public void DefineSawtooth(int len, int numHarmonics, double[] ar, double[] ai)
        {
            if (numHarmonics > (len >> 1))
                numHarmonics = (len >> 1);

            // clear
            for (int idx = 0; idx < len; idx++)
            {
                ai[idx] = 0;
                ar[idx] = 0;
            }

            // sawtooth
            for (int idx = 1, jdx = len - 1; idx <= numHarmonics; idx++, jdx--)
            {
                double temp = -1.0 / idx;
                ar[idx] = -temp;
                ar[jdx] = temp;
            }
        }

        public void DefineTriangle(int len, int numHarmonics, double[] ar, double[] ai)
        {
            if (numHarmonics > (len >> 1))
                numHarmonics = (len >> 1);

            // clear
            for (int idx = 0; idx < len; idx++)
            {
                ai[idx] = 0;
                ar[idx] = 0;
            }
            // triangle
             float sign = 1;
             for (int idx = 1, jdx = len - 1; idx <= numHarmonics; idx++, jdx--) {
             double temp = (idx & 0x01) > 0 ? 1.0 / (idx * idx) * (sign = -sign) : 0.0;
             ar[idx] = -temp;
             ar[jdx] = temp;
             }
        }

        public void DefineSquare(int len, int numHarmonics, double[] ar, double[] ai)
        {
            if (numHarmonics > (len >> 1))
                numHarmonics = (len >> 1);

            // clear
            for (int idx = 0; idx < len; idx++)
            {
                ai[idx] = 0;
                ar[idx] = 0;
            }

             // square
             for (int idx = 1, jdx = len - 1; idx <= numHarmonics; idx++, jdx--) {
             double temp = (idx & 0x01) > 0 ? 1.0 / idx : 0.0;
             ar[idx] = -temp;
             ar[jdx] = temp;
             }
             
        }

        //
        // if scale is 0, auto-scales
        // returns scaling factor (0.0 if failure), and wavetable in ai array
        //
        float makeWaveTable(int len, double[] ar, double[] ai, double scale, double topFreq)
        {
            fft(len, ar, ai);

            if (scale == 0.0)
            {
                // calc normal
                double max = 0;
                for (int idx = 0; idx < len; idx++)
                {
                    double temp = Math.Abs(ai[idx]);
                    if (max < temp)
                        max = temp;
                }
                scale = 1.0 / max * .999;
            }

            // normalize
            float[] wave = new float[len];
            for (int idx = 0; idx < len; idx++)
                wave[idx] = (float)(ai[idx] * scale);

            if (AddWaveTable(len, wave, topFreq) != 0)
                scale = 0.0;

            return (float)scale;
        }

        //
        // addWaveTable
        //
        // add wavetables in order of lowest frequency to highest
        // topFreq is the highest frequency supported by a wavetable
        // wavetables within an oscillator can be different lengths
        //
        // returns 0 upon success, or the number of wavetables if no more room is available
        //
        public int AddWaveTable(int len, float[] waveTableIn, double topFreq)
        {
            if (this.numWaveTables < numWaveTableSlots)
            {
                float[] waveTable = this.waveTables[this.numWaveTables].waveTable = new float[len];
                this.waveTables[this.numWaveTables].waveTableLen = len;
                this.waveTables[this.numWaveTables].topFreq = topFreq;
                ++this.numWaveTables;

                // fill in wave
                for (long idx = 0; idx < len; idx++)
                    waveTable[idx] = waveTableIn[idx];

                return 0;
            }
            return this.numWaveTables;
        }

        public int ClearWaveTable()
        {
            numWaveTables = 0;
            for (int idx = 0; idx < numWaveTableSlots; idx++)
            {
                waveTables[idx].topFreq = 0;
                waveTables[idx].waveTableLen = 0;
                waveTables[idx].waveTable = null;
            }

            return numWaveTables;
        }

        //
        // fft
        //
        // I grabbed (and slightly modified) this Rabiner & Gold translation...
        //
        // (could modify for real data, could use a template version, blah blah--just keeping it short)
        //
        void fft(int N, double[] ar, double[] ai)
        {
            int i, j, k, L;            /* indexes */
            int M, TEMP, LE, LE1, ip;  /* M = log N */
            int NV2, NM1;
            double t;               /* temp */
            double Ur, Ui, Wr, Wi, Tr, Ti;
            double Ur_old;

            // if ((N > 1) && !(N & (N - 1)))   // make sure we have a power of 2

            NV2 = N >> 1;
            NM1 = N - 1;
            TEMP = N; /* get M = log N */
            M = 0;
            while ((TEMP >>= 1) > 0) ++M;

            /* shuffle */
            j = 1;
            for (i = 1; i <= NM1; i++)
            {
                if (i < j)
                {             /* swap a[i] and a[j] */
                    t = ar[j - 1];
                    ar[j - 1] = ar[i - 1];
                    ar[i - 1] = t;
                    t = ai[j - 1];
                    ai[j - 1] = ai[i - 1];
                    ai[i - 1] = t;
                }

                k = NV2;             /* bit-reversed counter */
                while (k < j)
                {
                    j -= k;
                    k /= 2;
                }

                j += k;
            }

            LE = 1;
            for (L = 1; L <= M; L++)
            {            // stage L
                LE1 = LE;                         // (LE1 = LE/2) 
                LE *= 2;                          // (LE = 2^L)
                Ur = 1.0;
                Ui = 0;
                Wr = Math.Cos(Math.PI / (float)LE1);
                Wi = -Math.Sin(Math.PI / (float)LE1); // Cooley, Lewis, and Welch have "+" here
                for (j = 1; j <= LE1; j++)
                {
                    for (i = j; i <= N; i += LE)
                    { // butterfly
                        ip = i + LE1;
                        Tr = ar[ip - 1] * Ur - ai[ip - 1] * Ui;
                        Ti = ar[ip - 1] * Ui + ai[ip - 1] * Ur;
                        ar[ip - 1] = ar[i - 1] - Tr;
                        ai[ip - 1] = ai[i - 1] - Ti;
                        ar[i - 1] = ar[i - 1] + Tr;
                        ai[i - 1] = ai[i - 1] + Ti;
                    }
                    Ur_old = Ur;
                    Ur = Ur_old * Wr - Ui * Wi;
                    Ui = Ur_old * Wi + Ui * Wr;
                }
            }
        }

    }
}
        
    /*
        void testPWM()
        {
            // make an oscillator with wavetable
            WaveTableOsc osc = new WaveTableOsc();
            setSawtoothOsc(osc, baseFrequency);

            // pwm
            WaveTableOsc* mod = new WaveTableOsc();
            const int sineTableLen = 2048;
            float sineTable[sineTableLen];
            for (int idx = 0; idx < sineTableLen; ++idx)
                sineTable[idx] = sin((float)idx / sineTableLen * M_PI * 2);
            mod->addWaveTable(sineTableLen, sineTable, 1.0);
            mod->setFrequency(0.3 / sampleRate);

            osc->setFrequency(110.0 / sampleRate);

            // run the oscillator
            const int numSamples = sampleRate * numSecs;
            float* soundBuf = new float[numSamples];

            for (int idx = 0; idx < numSamples; idx++)
            {
                osc->setPhaseOffset((mod->getOutput() * 0.95 + 1.0) * 0.5);
                soundBuf[idx] = osc->getOutputMinusOffset() * gainMult;    // square wave from sawtooth
                mod->updatePhase();
                osc->updatePhase();
            }
        }

        // three unison detuned saws
        void TestThreeOsc()
        {
            // make an oscillator with wavetable
            WaveTableOsc osc1 = new WaveTableOsc();
            WaveTableOsc osc2 = new WaveTableOsc();
            WaveTableOsc osc3 = new WaveTableOsc();

            osc1->setFrequency(111.0 * .5 / sampleRate);
            osc2->setFrequency(112.0 * .5 / sampleRate);
            osc3->setFrequency(55.0 / sampleRate);

            for (int idx = 0; idx < numSamples; idx++)
            {
                soundBuf[idx] = (osc1->getOutput() + osc2->getOutput() + osc3->getOutput()) * 0.5 * gainMult;    // square wave from sawtooth
                osc1->updatePhase();
                osc2->updatePhase();
                osc3->updatePhase();
            }
        }
        
    }
}
*/