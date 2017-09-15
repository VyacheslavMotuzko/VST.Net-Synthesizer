using System;
using Syntage.Framework.Audio;

namespace SynthNet.Logic
{
    public class Envelope : SyntageAudioProcessorComponent<AudioProcessor>
    {
        private enum EState
        {
            None,
            Attack,
            Decay,
            Sustain,
            Release
        }

        private  EnvelopeParameters parametersOwner;
        private double _time;
        private double _multiplier;
        private double _startMultiplier;
        private EState _state;
        private int _sampleNumber;

    
		public event Action OnReleaseEnd;
		
        public Envelope(EnvelopeParameters owner,AudioProcessor audioProcessor) : base(audioProcessor)
        {
            parametersOwner = owner;
        }

        public void Reset()
        {
            SetState(EState.None);
        }

        public double GetNextMultiplier(int sampleNumber)
        {
            _sampleNumber = sampleNumber;

            var startMultiplier = GetCurrentStateStartValue();
            var finishMultiplier = GetCurrentStateFinishValue();

            // время уменьшается, поэтому используем обратную величину
            var stateTime = 1 - _time / GetCurrentStateMultiplier();

            switch (_state)
            {
                case EState.None:
                    return 0;

                case EState.Attack:
                    if (_time < 0)
                        SetState(EState.Decay);
                    break;

                case EState.Decay:
                    if (_time < 0)
                        SetState(EState.Sustain);
                    break;

                case EState.Sustain:
                    break;

                case EState.Release:
                    if (_time < 0)
					{
                        SetState(EState.None);
                        OnReleaseEnd?.Invoke();
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            // интерполяция между startMultiplier и finishMultiplier 
            _multiplier = CalculateLevel(startMultiplier, finishMultiplier, stateTime);

            // вычитаем время одного семпла
            var timeDelta = 1.0 / parametersOwner.Processor.SampleRate;
            _time -= timeDelta;

            return _multiplier;
        }

        public void Press()
        {
            SetState(EState.Attack);
        }

        public void Release()
        {
            SetState(EState.Release);
        }

        private double CalculateLevel(double a, double b, double t)
        {
            switch (_state)
            {
                case EState.None:
                    return 0;
                case EState.Sustain:
                    return a;

                case EState.Attack:
                    return Math.Log(1 + t * (Math.E - 1)) * Math.Abs(b - a) + a;

                case EState.Decay:
                case EState.Release:
                    return (Math.Exp(1 - t) - 1) / (Math.E - 1) * (a - b) + b;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void SetState(EState newState)
        {
            // запомним текущее значение огибающей - это будет стартовое значение для новой фазы
            _startMultiplier = (_time > 0) ? _multiplier : GetCurrentStateFinishValue();

            _state = newState;

            // получим время новой фазы
            _time = GetCurrentStateMultiplier();
        }

        private double GetCurrentStateStartValue()
        {
            return _startMultiplier;
        }

        private double GetCurrentStateFinishValue()
        {
            switch (_state)
            {
                case EState.None:
                    return 0;

                case EState.Attack:
                    return 1;

                case EState.Decay:
                case EState.Sustain:
                    return GetSustainMultiplier();

                case EState.Release:
                    return 0;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private double GetCurrentStateMultiplier()
        {
            switch (_state)
            {
                case EState.None:
                    return 0;

                case EState.Attack:
                    return parametersOwner.Attack.ProcessedValue(_sampleNumber);

                case EState.Decay:
                    return parametersOwner.Decay.ProcessedValue(_sampleNumber);

                case EState.Sustain:
                    return GetSustainMultiplier();

                case EState.Release:
                    return parametersOwner.Release.ProcessedValue(_sampleNumber);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private double GetSustainMultiplier()
        {
            return parametersOwner.Sustain.ProcessedValue(_sampleNumber);
        }
    }
}
