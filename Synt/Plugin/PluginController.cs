using System.Collections.Generic;
using System.Linq;
using Jacobi.Vst.Core;
using Jacobi.Vst.Framework;
using Syntage.Framework;
using Syntage.Framework.MIDI;
using SynthNet.Logic;
using SynthNet.UI;

namespace SynthNet.Plugin
{
    public class PluginController : SyntagePlugin
    {
        public AudioProcessor AudioProcessor { get; }
        public MidiListener MidiListener { get; }

        public PluginController() : base(
            "Synth.NET",
            new VstProductInfo("Test", " ", 1),
            VstPluginCategory.Synth,
            VstPluginCapabilities.None,
            0,1)
        { 
            MidiListener = new MidiListener();
            AudioProcessor = new AudioProcessor(this);
            PluginUI.Instance.PluginController = this;

            ParametersManager.SetParameters(AudioProcessor.CreateParameters());

            ParametersManager.SetPrograms(new Dictionary<string, string>
            {
                {"Sine", Properties.Resources.Sine},
                {"Wooble", Properties.Resources.Wooble},
                {"Whistle", Properties.Resources.Whistle},
                {"Synth", Properties.Resources.Synth1},
                {"BassLong", Properties.Resources.Bass_Long},
                {"BassShort", Properties.Resources.Bass_Short},
                {"Siren", Properties.Resources.Siren},
                {"8BitSeq", Properties.Resources._8bitMadness },
                {"WobbleSeq", Properties.Resources.WoobleMad }
            }.Select(x => ParametersManager.CreateProgramFromSerializedParameters(x.Key, x.Value)));
        }

        protected override IVstPluginAudioProcessor CreateAudioProcessor(IVstPluginAudioProcessor instance)
        {
            return AudioProcessor;
        }

        protected override IVstPluginEditor CreateEditor(IVstPluginEditor instance)
        {
            return PluginUI.Instance;
        }

        protected override IVstPluginBypass CreateBypass(IVstPluginBypass instance)
        {
            return AudioProcessor;
        }

        protected override IVstMidiProcessor CreateMidiProcessor(IVstMidiProcessor instance)
        {
            return MidiListener;
        }
    }
}
