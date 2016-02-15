﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Remoting.Contexts;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Xml;
using ENTM.TuringMachine;
using SharpNeat.Core;
using SharpNeat.Domains;
using SharpNeat.Phenomes;

namespace ENTM.Experiments.CopyTask
{
    public class CopyTaskEvaluator : BaseEvaluator<CopyTaskEnvironment>
    {

        private readonly Stopwatch _stopWatch = new Stopwatch();

        private CopyTaskProperties _properties;

        public override void Initialize(XmlElement xmlConfig)
        {
            _properties = new CopyTaskProperties(xmlConfig);
        }

        protected override CopyTaskEnvironment NewEnvironment()
        {
            // This is called from the ThreadLocal Environment, so a new environment is instantiated for each thread
            return new CopyTaskEnvironment(_properties);
        }

        private int _maxScore = -1;
        public override int MaxScore
        {
            get
            {
                if (_maxScore < 0)
                {
                    _maxScore = (int) (_properties.Iterations * _properties.MaxSequenceLength * _properties.FitnessFactor);
                }
                return _maxScore;
            }
        }

        public int TuringMachineInputCount
        {
            get
            {
                int shifts = 0;
                switch (_properties.ShiftMode)
                {
                    case ShiftMode.Multiple:
                        shifts = _properties.ShiftLength;
                        break;
                    case ShiftMode.Single:
                        shifts = 1;
                        break;
                }

                return (_properties.M + 2 + shifts) * _properties.Heads;
            }
        }

        public int TuringMachineOutputCount => _properties.M * _properties.Heads;

        public int EnvironmentInputCount => Environment.InputCount;

        public int EnvironmentOutputCount => Environment.OutputCount;

        public override FitnessInfo Evaluate(IBlackBox phenome)
        {         
            
            double totalScore = 0;
            int steps = 0;

            long nnTime = 0;
            long contTime = 0;
            long simTime = 0;

            TuringController controller = new TuringController(phenome, _properties);

            int turingMachineInputCount = controller.TuringMachine.InputCount;
            int environmentInputCount = Environment.InputCount;

            // For each iteration
            for (int i = 0; i < _properties.Iterations; i++)
            {
                Reset();
                Environment.Restart();

                double[] turingMachineOutput = controller.InitialInput;
                double[] enviromentOutput = Environment.InitialObservation;

                while (!Environment.IsTerminated)
                {
                    _stopWatch.Start();

                    double[] nnOutput = controller.ActivateNeuralNetwork(enviromentOutput, turingMachineOutput);

                    nnTime += _stopWatch.ElapsedMilliseconds;
                    _stopWatch.Restart();

                    // CopyTask can rely on the TM acting first
                    turingMachineOutput = controller.ProcessNNOutputs(Utilities.ArrayCopyOfRange(nnOutput, environmentInputCount, turingMachineInputCount));

                    contTime += _stopWatch.ElapsedMilliseconds;
                    _stopWatch.Restart();

                    enviromentOutput = Environment.PerformAction(Utilities.ArrayCopyOfRange(nnOutput, 0, environmentInputCount));

                    simTime += _stopWatch.ElapsedMilliseconds;

                    steps++;

                    _stopWatch.Stop();
                    _stopWatch.Reset();
                }

                totalScore += Environment.CurrentScore;
            }

            double result = Math.Max(0.0, totalScore);

            _evaluationCount++;

            if (result >= MaxScore) _stopConditionSatisfied = true;

            return new FitnessInfo(result, 0);
        }

        public override void Reset()
        {
            Environment.Reset();
        }
    }
}
