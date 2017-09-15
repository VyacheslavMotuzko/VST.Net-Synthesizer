using System.Collections.Generic;
using Syntage.Framework.MIDI;
using Syntage.Framework.Audio;
using System;

namespace SynthNet.Logic
{
    public class VoiceManager : SyntageAudioProcessorComponent<AudioProcessor>
    {
        class Voice : SyntageAudioProcessorComponent<AudioProcessor>
        {
            private static  Random random = new Random();
            public bool isActive;
            public int note;
            public Oscillator oscA;
            public Oscillator oscB;
            public List<Oscillator> ListOscA;
            public List<Oscillator> ListOscB;
            public Envelope soundEnv;
            public Filter filter;
            public bool unison;

            public Voice(int _note, AudioProcessor processor) : base(processor)
            {
                note = _note;
                isActive = true;

                if (processor.Unison.Value == Syntage.Framework.Parameters.EPowerStatus.On)
                {
                    ListOscA = new List<Oscillator> { new Oscillator(processor, processor.OscillatorA,_note, 0, 0), new Oscillator(processor, processor.OscillatorA, _note, 0.15, 0.5), new Oscillator(processor, processor.OscillatorA, _note, -0.15, 0.20) };
                    ListOscB = new List<Oscillator> { new Oscillator(processor, processor.OscillatorB, _note, 0, 0.10), new Oscillator(processor, processor.OscillatorB, _note, 0.5, 0), new Oscillator(processor, processor.OscillatorB, _note, -0.15, 0.15) };
                    unison = true;
                }
                else
                {
                    oscA = new Oscillator(processor, processor.OscillatorA, _note, 0, 0);//(random.NextDouble() > 0.5 ? 0 : 0.5));
                    oscB = new Oscillator(processor, processor.OscillatorB, _note, 0, 0);// (random.NextDouble() > 0.5 ? 0 : 0.5));
                }

                soundEnv = new Envelope(processor.EnvelopeSound, processor);
                filter = new Filter(processor, processor.Filter);

                soundEnv.OnReleaseEnd += ReleaseEnded;

            }

            private void ReleaseEnded()
            {
                isActive = false;
            }

            public double NextSample(int i)
            {
                if (!isActive) return 0.0;

                var oscillatorOneOutput = 0.0;
                var oscillatorTwoOutput = 0.0;

                if (unison)
                {
                    foreach (var osc in ListOscA)
                    {
                        oscillatorOneOutput += osc.NextSample(i) * 0.5;
                    }
                    foreach (var osc in ListOscB)
                    {
                        oscillatorTwoOutput += osc.NextSample(i) * 0.5;
                    }
                }
                else
                {
                    oscillatorOneOutput = oscA.NextSample(i);
                    oscillatorTwoOutput = oscB.NextSample(i);
                }

                oscillatorOneOutput *= Processor.OscillatorA.Volume.ProcessedValue(i);
                oscillatorTwoOutput *= Processor.OscillatorB.Volume.ProcessedValue(i);

                var oscMix = Processor.OscillatorsMix.ProcessedValue(i);
                var oscillatorSum = ((1 - oscMix) * oscillatorOneOutput) + (oscMix * oscillatorTwoOutput);

                var volumeEnvelopeValue = soundEnv.GetNextMultiplier(i);
            
                if(!isActive) volumeEnvelopeValue = 0;
                return filter.Process(oscillatorSum * volumeEnvelopeValue, i);
                }
        }

        private List<Voice> Voices;

        public VoiceManager(AudioProcessor audioProcessor) : base(audioProcessor)
        {
            Voices = new List<Voice>();
            audioProcessor.PluginController.MidiListener.OnNoteOn += MidiListenerOnNoteOn;
            audioProcessor.PluginController.MidiListener.OnNoteOff += MidiListenerOnNoteOff;
        }

        private void MidiListenerOnNoteOff(object sender, MidiListener.NoteEventArgs e)
        {
            var voices = Voices.FindAll(x => x.note == e.NoteAbsolute);
            foreach(var voice in voices)
            {
                voice.soundEnv.Release();
                voice.filter.filterEnv.Release();
            }
        }

        private void MidiListenerOnNoteOn(object sender, MidiListener.NoteEventArgs e)
        {
            var voice = new Voice(e.NoteAbsolute, Processor);
            voice.soundEnv.Press();
            voice.filter.filterEnv.Press();
            Voices.Add(voice);
        }

        public double NextSample(int c)
        {
            double output = 0.0;
            for (int i = 0; i < Voices.Count; i++)
            {
                if (Voices[i].isActive == false) Voices.RemoveAt(i);  
                else
                    output += Voices[i].NextSample(c);
            }
            return output * 0.5;
        }
    }
}
