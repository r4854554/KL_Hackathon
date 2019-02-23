using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HDCircles.Hackathon
{
    public sealed class LateralController
    {
        public LateralController()
        {
            CurrentIndex = 0;
            TargetIndex = 0;
            counter = 0;
        }

        public void Start()
        {
            CurrentIndex = 0;
            TargetIndex = 0;
            counter = 0;
        }

        public double Update(double currentIndex, double targetIndex, bool rightSide)
        {
            double output = 0;
            double error = targetIndex - currentIndex;

            CurrentIndex = currentIndex;
            TargetIndex = targetIndex;

            if (targetIndex < 1)
            {
                return 0;
            }

            if (counter < 5)
            {
                if (error < -0.5)
                    output = -StepCmd;
                else if (error < 1)
                    output = 0;
                else
                    output = StepCmd;
            }
            else
            {
                output = 0;
            }

            if (!rightSide)
                output = -output;

            counter++;
            if (counter >= 10)
                counter = 0;

            return output;
        }

        public double CurrentIndex { get; set; } = 0;
        public double TargetIndex { get; set; } = 0;
        public double StepCmd { get; set; } = 0.1;

        private int counter = 0;
    }
}
