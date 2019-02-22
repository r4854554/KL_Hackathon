namespace HDCircles.Hackathon.Services
{
    public sealed class Commander
    {
        private static Commander _instance;
        public static Commander Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new Commander();

                return _instance;
            }
        }

        private Commander()
        {

        }

        public void TakeOff()
        { }

        public void Landing()
        { }

        public void EmergencyLanding()
        { }

        public void SetPoint(float yawSetpoint, float altitudeSetpoint, float relativeXSetpoint, float relativeYSetpoint)
        {

        }
    }
}
