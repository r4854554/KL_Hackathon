using System;
using System.Collections.Generic;
using System.Diagnostics;
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

            if(currentIndex < 1)
                return 0;

            CurrentIndex = currentIndex;
            TargetIndex = targetIndex;
            Debug.WriteLine($"Info:Lateral: Target: {TargetIndex},  Current: {CurrentIndex}");
            if (targetIndex < 1)
            {
                return 0;
            }

            if (counter < 10)
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
            if (counter >= 15)
                counter = 0;

            Debug.WriteLine($"Info:Lateral: Target: {TargetIndex},  Current: {CurrentIndex}, Output: { output}");
            return output;
        }

        public double CurrentIndex { get; set; } = 0;
        public double TargetIndex { get; set; } = 0;
        public double StepCmd { get; set; } = 0.5;

        private int counter = 0;
    }
}
