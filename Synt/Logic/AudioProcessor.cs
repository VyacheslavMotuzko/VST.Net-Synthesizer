using System.Collections.Generic;
using Jacobi.Vst.Core;
using Syntage.Framework.Audio;
using Syntage.Framework.Parameters;
using SynthNet.Plugin;

namespace SynthNet.Logic
{
    public class AudioProcessor : SyntageAudioProcessor
    {
        public readonly PluginController PluginController;

        public BypassParameter Power { get; private set; }
        public RealParameter OscillatorsMix { get; private set; }
        public VolumeParameter MasterVolume { get; private set; }
        public VoiceManager VoicesGenereator { get; set; }
        public EnumParameter<EPowerStatus> Unison { get; set; }
        
        public OscParameters OscillatorA { get; }
        public OscParameters OscillatorB { get; }
        public EnvelopeParameters EnvelopeSound { get; }
        public EnvelopeParameters FilterEnv { get; }
        public FilterParameters Filter { get; }
        public Distortion Distortion { get; }
        public LFO LFOModifierA { get; }
        public LFO LFOModifierB { get; }

        public AudioProcessor(PluginController pluginController) :
			base(0, 2, 0)
        {
           
            PluginController = pluginController;
            
            OscillatorA = new OscParameters(this);
            OscillatorB = new OscParameters(this);
            EnvelopeSound = new EnvelopeParameters(this);
            Filter = new FilterParameters(this);
            FilterEnv = new EnvelopeParameters(this);
            Distortion = new Distortion(this);
            LFOModifierA = new LFO(this);
            LFOModifierB = new LFO(this);

            VoicesGenereator = new VoiceManager(this);

            OnBypassChanged += (sender, args) =>
            {
                Power.Value = (Bypass) ? EPowerStatus.Off : EPowerStatus.On;
            };
        }
        
        public override IEnumerable<Parameter> CreateParameters()
        {
            var parameters = new List<Parameter>();
            
            parameters.Add(Power = new BypassParameter("CPwr", "Power", this, "Pwr", false));
            parameters.Add(OscillatorsMix = new RealParameter("CMix", "Oscillators Mix", "Mix", 0, 1, 0.01));
            parameters.Add(MasterVolume = new VolumeParameter("MVol", "Master Volume", false));
            parameters.Add(Unison = new EnumParameter<EPowerStatus>("UPwr","Unison","Pwr",false));
            OscillatorsMix.SetDefaultValue(0.5);
            parameters.AddRange(OscillatorA.CreateParameters("A"));
            parameters.AddRange(OscillatorB.CreateParameters("B"));
            parameters.AddRange(EnvelopeSound.CreateParameters("EM"));
            parameters.AddRange(FilterEnv.CreateParameters("EF"));
            parameters.AddRange(Filter.CreateParameters("F"));
            parameters.AddRange(Distortion.CreateParameters("D"));
            parameters.AddRange(LFOModifierA.CreateParameters("LA"));
            parameters.AddRange(LFOModifierB.CreateParameters("LB"));

            return parameters;
        }

        public override void Process(VstAudioBuffer[] inChannels, VstAudioBuffer[] outChannels)
        {
            if (Power.Value == EPowerStatus.Off)
            {
                return;
            }

            base.Process(inChannels, outChannels);

            var leftChannel = outChannels[0];
            var rightChannel = outChannels[1];

            var count = CurrentStreamLenght;
            for (int i = 0; i < count; i++)
            {
                var volume = MasterVolume.Value;

                var output = VoicesGenereator.NextSample(i);

                output = Distortion.Process(output);

                leftChannel[i] = rightChannel[i] = (float)output * (float)volume;
            }

            LFOModifierA.Process();
            LFOModifierB.Process();
        }
    }
}
