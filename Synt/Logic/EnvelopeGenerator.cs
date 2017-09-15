using Syntage.Framework.Audio;
using SynthNet.Logic;
using System;

namespace Syntage.Logic
{
    class EnvelopeGenerator : SyntageAudioProcessorComponent<AudioProcessor>
    {
        public enum EnvelopeStage
        {
            ENVELOPE_STAGE_OFF = 0,
            ENVELOPE_STAGE_ATTACK,
            ENVELOPE_STAGE_DECAY,
            ENVELOPE_STAGE_SUSTAIN,
            ENVELOPE_STAGE_RELEASE,
            kNumEnvelopeStages
        };

        private EnvelopeParameters parametersOwner;
        private EnvelopeStage currentStage;
        private double currentLevel;
        private double multiplier;
        private ulong currentSampleIndex;
        private ulong nextStageSampleIndex;
        private double[] stageValue;
        public EnvelopeStage Stage { get => currentStage; private set { } }
        static double minimumLevel = 0.0001;

        public event Action OnReleaseEnd;

        public EnvelopeGenerator(EnvelopeParameters owner, AudioProcessor audioProcessor) : base(audioProcessor)
        {
            parametersOwner = owner;
            currentStage = EnvelopeStage.ENVELOPE_STAGE_OFF;
            currentLevel = minimumLevel;
            multiplier = 1.0;
            currentSampleIndex = 0;
            nextStageSampleIndex = 0;
            stageValue = new double[(int)EnvelopeStage.kNumEnvelopeStages];
            stageValue[(int)EnvelopeStage.ENVELOPE_STAGE_OFF] = 0.0;
            stageValue[(int)EnvelopeStage.ENVELOPE_STAGE_ATTACK] = 0.01;
            stageValue[(int)EnvelopeStage.ENVELOPE_STAGE_DECAY] = 0.5;
            stageValue[(int)EnvelopeStage.ENVELOPE_STAGE_SUSTAIN] = 0.1;
            stageValue[(int)EnvelopeStage.ENVELOPE_STAGE_RELEASE] = 1.0;
        }

        public void EnterStage(EnvelopeStage newStage)
        {
            if (currentStage == newStage) return;
            if (currentStage == EnvelopeStage.ENVELOPE_STAGE_OFF)
            {
                
            }
            if (newStage == EnvelopeStage.ENVELOPE_STAGE_OFF)
            {
                OnReleaseEnd?.Invoke();
            }

            currentStage = newStage;
            currentSampleIndex = 0;
            if (currentStage == EnvelopeStage.ENVELOPE_STAGE_OFF ||
                currentStage == EnvelopeStage.ENVELOPE_STAGE_SUSTAIN)
            {
                nextStageSampleIndex = 0;
            }
            else
            {
                nextStageSampleIndex = (ulong)(stageValue[(int)currentStage] * Processor.SampleRate);
            }
            switch (newStage)
            {
                case EnvelopeStage.ENVELOPE_STAGE_OFF:
                    currentLevel = 0.0;
                    multiplier = 1.0;
                    break;
                case EnvelopeStage.ENVELOPE_STAGE_ATTACK:
                    currentLevel = minimumLevel;
                    calculateMultiplier(currentLevel,
                                        1.0,
                                        nextStageSampleIndex);
                    break;
                case EnvelopeStage.ENVELOPE_STAGE_DECAY:
                    currentLevel = 1.0;
                    calculateMultiplier(currentLevel,
                                        Math.Max(stageValue[(int)EnvelopeStage.ENVELOPE_STAGE_SUSTAIN], minimumLevel),
                                        nextStageSampleIndex);
                    break;
                case EnvelopeStage.ENVELOPE_STAGE_SUSTAIN:
                    currentLevel = stageValue[(int)EnvelopeStage.ENVELOPE_STAGE_SUSTAIN];
                    multiplier = 1.0;
                    break;
                case EnvelopeStage.ENVELOPE_STAGE_RELEASE:
                    // We could go from ATTACK/DECAY to RELEASE,
                    // so we're not changing currentLevel here.
                    calculateMultiplier(currentLevel,
                                        minimumLevel,
                                        nextStageSampleIndex);
                    break;
                default:
                    break;
            }
        }


        public void Press()
        {
            EnterStage(EnvelopeStage.ENVELOPE_STAGE_ATTACK);
        }

        public void Release()
        {
            EnterStage(EnvelopeStage.ENVELOPE_STAGE_RELEASE);
        }

        public double GetNextMultiplier(int i)
        {
            if (currentStage != EnvelopeStage.ENVELOPE_STAGE_OFF &&
                currentStage != EnvelopeStage.ENVELOPE_STAGE_SUSTAIN)
            {
                if (currentSampleIndex == nextStageSampleIndex)
                {
                    EnvelopeStage newStage = (EnvelopeStage)((int)(currentStage + 1) % (int)EnvelopeStage.kNumEnvelopeStages);
                    EnterStage(newStage);
                }
                currentLevel *= multiplier;
                currentSampleIndex++;
            }
            return currentLevel;
        }

        public void SetStageValue(EnvelopeStage stage, double value)
        {
            stageValue[(int)stage] = value;
            if (stage == currentStage)
            {
                // Re-calculate the multiplier and nextStageSampleIndex
                if (currentStage == EnvelopeStage.ENVELOPE_STAGE_ATTACK ||
                   currentStage == EnvelopeStage.ENVELOPE_STAGE_DECAY ||
                   currentStage == EnvelopeStage.ENVELOPE_STAGE_RELEASE)
                {
                    double nextLevelValue = 0;
                    switch (currentStage)
                    {
                        case EnvelopeStage.ENVELOPE_STAGE_ATTACK:
                            nextLevelValue = 1.0;
                            break;
                        case EnvelopeStage.ENVELOPE_STAGE_DECAY:
                            nextLevelValue = Math.Max(stageValue[(int)EnvelopeStage.ENVELOPE_STAGE_SUSTAIN], minimumLevel);
                            break;
                        case EnvelopeStage.ENVELOPE_STAGE_RELEASE:
                            nextLevelValue = minimumLevel;
                            break;
                        default:
                            break;
                    }
                    // How far the generator is into the current stage:
                    double currentStageProcess = (currentSampleIndex + 0.0) / nextStageSampleIndex;
                    // How much of the current stage is left:
                    double remainingStageProcess = 1.0 - currentStageProcess;
                    ulong samplesUntilNextStage = (ulong)(remainingStageProcess * value * Processor.SampleRate);
                    nextStageSampleIndex = currentSampleIndex + samplesUntilNextStage;
                    calculateMultiplier(currentLevel, nextLevelValue, samplesUntilNextStage);
                }
                else if (currentStage == EnvelopeStage.ENVELOPE_STAGE_SUSTAIN)
                {
                    currentLevel = value;
                }
            }
            if (currentStage == EnvelopeStage.ENVELOPE_STAGE_DECAY &&
                stage == EnvelopeStage.ENVELOPE_STAGE_SUSTAIN)
            {
                // We have to decay to a different sustain value than before.
                // Re-calculate multiplier:
                ulong samplesUntilNextStage = nextStageSampleIndex - currentSampleIndex;
                calculateMultiplier(currentLevel,
                                    Math.Max(stageValue[(int)EnvelopeStage.ENVELOPE_STAGE_SUSTAIN], minimumLevel),
                                    samplesUntilNextStage);
            }
        }

        private void calculateMultiplier(double startLevel, double endLevel, ulong lengthInSamples)
        {
            multiplier = 1.0 + (Math.Log(endLevel) - Math.Log(startLevel)) / (lengthInSamples);
        }
    }
}
