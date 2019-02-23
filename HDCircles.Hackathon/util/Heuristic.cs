using Dynamsoft.Barcode;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HDCircles.Hackathon.util
{
    public class Heuristic
    {
        public Tuple<double, double> coordinateL,
            coordinateR,
            coordinateC;

        public bool hasL, hasR, hasC;

        public Heuristic() { hasL = hasR = hasC = false; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="barcodeL">Left Tag</param>
        /// <param name="barcodeR">Right Tag</param>
        /// <param name="barcodeC">Center Tag</param>
        /// <returns>Item 1: should go left. Item 2: should go right. Item 3: center average coordinate</returns>
        public void LRHeuristic(
            string barcodeL,
            string barcodeR,
            string barcodeC)
        {

            #region calculate the average
            var pc = PosController.Instance;
            pc.PoseUpdated += new PoseEstimationHandler((pose) =>
            {
                foreach (TextResult dr in pose.DetectResults)
                {
                    var points = dr.LocalizationResult.ResultPoints;
                    if (points != null && points.Length > 0)
                    {
                        int tmpX = 0, tmpY = 0, count = 0;
                        foreach (Point point in points) { tmpX += point.X; tmpY += point.Y; ++count; }
                        double avgX = tmpX / count, avgY = tmpY / count;
                        if (dr.BarcodeText == barcodeL)
                        {
                            coordinateL = new Tuple<double, double>(avgX, avgY);
                            hasL = true;
                            Trace.Assert(dr.BarcodeText == barcodeL, string.Format("Left Barcode Detected. Coordinate: {0}, {1}", avgX.ToString(), avgY.ToString()));
                        }
                        else if (dr.BarcodeText == barcodeR)
                        {
                            coordinateR = new Tuple<double, double>(avgX, avgY);
                            hasR = true;
                            Trace.Assert(dr.BarcodeText == barcodeL, string.Format("Right Barcode Detected. Coordinate: {0}, {1}", avgX.ToString(), avgY.ToString()));
                        }
                        else if (dr.BarcodeText == barcodeC)
                        {
                            coordinateC = new Tuple<double, double>(avgX, avgY);
                            hasC = true;
                            Trace.Assert(dr.BarcodeText == barcodeL, string.Format("Center Barcode Detected. Coordinate: {0}, {1}", avgX.ToString(), avgY.ToString()));
                        }
                        else
                        {
                            Trace.Assert(true, "Unknown barcode: " + dr.BarcodeText);
                        }
                    }
                }
            });
            #endregion
        }


    }
}
