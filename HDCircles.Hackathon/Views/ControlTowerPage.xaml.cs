﻿namespace HDCircles.Hackathon.Views
{
    using HDCircles.Hackathon.Services;
    using System;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;

    public sealed partial class ControlTowerPage : Page
    {
        struct InputArgs
        {
            public float Yaw { get; }
            public float Altitude { get; }
            public float RelativeX { get; }
            public float RelativeY { get; }

            public InputArgs(float yaw, float altitude, float relativeX, float relativeY)
            {
                Yaw = yaw;
                Altitude = altitude;
                RelativeX = relativeX;
                RelativeY = relativeY;
            }
        }

        private object _emergencyLock = new object();

        private object _cmdLock = new object();

        private bool _isLanding;
        
        private bool _isTakingOff;

        private bool _isAutoPilot;

        private Commander Commander => Commander.Instance;

        public ControlTowerPage()
        {
            this.InitializeComponent();
        }

        private void DisableControls(bool isEmergency)
        {
            DisableInputs();
        }

        private void EnableControls(bool isEmergency)
        {
            EnableInputs();
        }

        private void DisableInputs()
        { }

        private void EnableInputs()
        { }

        private InputArgs GetInputArgs()
        {
            var yawText = YawSetpointBox.Text;
            var altitudeText = AltitudeSetpointBox.Text;
            var relativeXText = RelativeXSetpointBox.Text;
            var relativeYText = RelativeYSetpointBox.Text;

            var yawValue = float.Parse(yawText);
            var altitudeValue = float.Parse(altitudeText);
            var relativeXValue = float.Parse(relativeXText);
            var relativeYValue = float.Parse(relativeYText);

            // TODO: Check input bound.

            var args = new InputArgs(yawValue, altitudeValue, relativeXValue, relativeYValue);
            
            return args;
        }

        private void EmergencyButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: disable all ui element
            // TODO: stop autonomous flight

            try
            {
                lock (_emergencyLock)
                {
                    if (_isLanding) return;

                    DisableControls(true);

                    _isLanding = true;

                    Drone.Instance.EmergencyLanding();

                    _isLanding = false;

                    EnableControls(true);
                }
            }
            catch (Exception ex)
            {
                if (_isLanding)
                {
                    // exception when landing command executing
                    // TODO: set the ui to error state, disable all ui control.
                }

                EnableControls(true);

                Logger.Instance.Log(ex.ToString());
            }

        }

        private void TakeOffButton_Click(object sender, RoutedEventArgs e)
        {
            // return if it is landing or taking off
            if (_isLanding || _isTakingOff) return;

            // return if it is flying
            // TODO: 

            try
            {
                _isTakingOff = true;
                DisableControls(false);
                Commander.TakeOff();
                _isTakingOff = false;
                EnableControls(false);
            }
            catch (Exception ex)
            {
                var oldValue = _isTakingOff;

                _isTakingOff = false;
                EnableControls(false);

                Logger.Instance.Log(ex.ToString());
            }

        }

        private void LandingButton_Click(object sender, RoutedEventArgs e)
        {
            // return if it is landing or taking off
            if (_isLanding || _isTakingOff) return;

            // return if it is flying
            // TODO: 
            try
            {
                _isLanding = true;
                DisableControls(false);
                Commander.Landing();
                _isLanding = false;
                EnableControls(false);
            }
            catch (Exception ex)
            {
                var oldValue = _isLanding;

                _isLanding = false;
                EnableControls(false);

                Logger.Instance.Log(ex.ToString());
            }
        }

        private void ManualAutoToggle_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SetPointButton_Click(object sender, RoutedEventArgs e)
        {
            lock (_cmdLock)
            {
                DisableInputs();

                var args = GetInputArgs();

                Commander.SetPoint(args.Yaw, args.Altitude, args.RelativeX, args.RelativeY);

                EnableInputs();
            }
        }
    }
}