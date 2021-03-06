﻿using DJI.WindowsSDK;
using HDCircles.Hackathon.Services;
using System;
using System.Diagnostics;
using System.Threading;

// this implement a simple controller for the yaw

namespace HDCircles.Hackathon
{
    public sealed class YawController
    {
         public YawController(double GainProportional, double GainIntegration, double GainDerivative)
        {
            // call when the object is constructed
            Init(GainProportional, GainIntegration, GainDerivative);
        }

        public void Init(double GainProportional, double GainIntegration, double GainDerivative)
        {
            // call when the object is constructed, 
            this.GainDerivative = GainDerivative;
            this.GainProportional = GainProportional;
            this.GainIntegration = GainIntegration;
            this.OutputMax = 1f;
            this.OutputMin = -1f;
            this.StateIntegration = 0;
        }

        public void Start(double currentSetpoint, double currentProcessVariable, double currentProcessVariableRate)
        {
            // call when the controller is started
            this.SetPoint = currentSetpoint;
            this.ProcessVariable = currentProcessVariable;
            this.ProcessVariableRate = currentProcessVariableRate;
            this.StateIntegration = 0;
        }
        /// <summary>
        /// The controller output
        /// </summary>
        /// <param name="timeSinceLastUpdate">timespan of the elapsed time
        /// since the previous time that ControlVariable was called</param>
        /// <returns>Value of the variable that needs to be controlled</returns>
        public double Update(double currentProcessVariable, double currentProcessVariableRate)
        {
            // update the property with current feedback
            ProcessVariable = currentProcessVariable;
            ProcessVariableRate = currentProcessVariableRate;
            
            // work out the control
            double error = SetPoint - ProcessVariable;
            if (error > 180)
                error -= 360;
            else if (error < -180)
                error += 360;
            double errorToIntegration = Clamp(error, 10f, -10f);
            
            this.StateIntegration += errorToIntegration;
            this.StateIntegration = Clamp(this.StateIntegration, 60, -60);

            double output = GainProportional * errorToIntegration + GainIntegration * this.StateIntegration; // it add the derivative term becasue the zv is the other sign
            output = Clamp(output,OutputMax, OutputMin);

            var udpData = new double[]{ SetPoint, ProcessVariable, ProcessVariableRate, output };
            UdpDebug.Instance.SendUdpDebug(udpData);

            Debug.WriteLine($"Info:YawController: Setpoint: {SetPoint},  ProcessVar: {ProcessVariable} , Output: {output}");
            return output;
        }

        private double processVariableRate;
        /// <summary>
        /// The current value
        /// </summary>
        public double ProcessVariableRate
        {
            get { return processVariableRate; }
            set
            {

                processVariableRate = value;
            }
        }

        object lockNew = new object();
        /// <summary>
        /// The current value
        /// </summary>
        public double ProcessVariable
        {
            get { return processVariable; }
            set
            {
                //processVariable = value;
                lock (lockNew)
                {//ProcessVariableLast = processVariable;
                    processVariable = value;
                }

            }
        }
        /// <summary>
        /// The derivative term is proportional to the rate of
        /// change of the error
        /// </summary>
        public double GainDerivative { get; set; } = 0f;

        /// <summary>
        /// The proportional term produces an output value that
        /// is proportional to the current error value
        /// </summary>
        /// <remarks>
        /// Tuning theory and industrial practice indicate that the
        /// proportional term should contribute the bulk of the output change.
        /// </remarks>
        public double GainProportional { get; set; } = 0;

        /// <summary>
        /// The integeration term produces an output value that
        /// is proportional to the integeration of error value
        /// </summary>
        public double GainIntegration { get; set; } = 0;

        /// <summary>
        /// the integration of error during control process
        /// </summary>
        private double StateIntegration = 0;

        /// <summary>
        /// The max output value the control device can accept.
        /// </summary>
        public double OutputMax { get; private set; } = 0;

        /// <summary>
        /// The minimum ouput value the control device can accept.
        /// </summary>
        public double OutputMin { get; private set; } = 0;

        /// <summary>
        /// The last reported value (used to calculate the rate of change)
        /// </summary>
        public double ProcessVariableLast { get; private set; } = 0;


        private double _setPoint;
        /// <summary>
        /// The desired value
        /// </summary>
        public double SetPoint {
            get => _setPoint;
            set {

                _setPoint = value;

                while(_setPoint > 180)
                {
                    _setPoint -= 360;
                }

                while(_setPoint < -180)
                {
                    _setPoint += 360;
                }

                //_setPoint = Clamp(value, SetPointMax, SetPointMin);
             
            }
        }

        /// <summary>
        /// Limit a variable to the set OutputMax and OutputMin properties
        /// </summary>
        /// <returns>
        /// A value that is between the OutputMax and OutputMin properties
        /// </returns>
        /// <remarks>
        /// Inspiration from http://stackoverflow.com/questions/3176602/how-to-force-a-number-to-be-in-a-range-in-c
        /// </remarks>
        private double Clamp(double variableToClamp, double max, double min)
        {
            if (variableToClamp <= min) { return min; }
            if (variableToClamp >= max) { return max; }
            return variableToClamp;
        }

        private double processVariable = 0;
        private double SetPointMax = 180;
        private double SetPointMin = -180;


    }
}
