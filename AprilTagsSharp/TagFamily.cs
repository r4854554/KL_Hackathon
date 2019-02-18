namespace AprilTagsSharp
{
    using System;
    using System.Text.RegularExpressions;

    public class TagFamily
    {
        int edge; //内部边长如tag36 就是6 tag25就是5; tag`s black block `s length
        int blackBorder; //黑边长度，默认都是1; usuall is one
        public bool debug; //是否debug; debug or not
        int minhamming; //可接受匹配的最小汉明距离; whether the detection is good depend on it`s hamming compare with this minhamming

        protected TagFamily(int edge, bool debug, int minhamming)
        {
            this.debug = false;
            this.edge = edge;
            this.blackBorder = 1;
            this.minhamming = minhamming;
            this.debug = debug;
        }
        /// <summary>
        /// 得到黑边界长度 get blackborder
        /// </summary>
        /// <returns>黑边长度一般为1</returns>
        public int getBlackBorder()
        {
            return this.blackBorder;
        }
        /// <summary>
        /// 得到tag的边长
        /// </summary>
        /// <returns>Tag的边长</returns>
        public int getEdge()
        {
            return this.edge;
        }
        /// <summary>
        /// 得到对应的TagFamily·codes
        /// </summary>
        /// <returns>Codes</returns>
        public virtual long[] GetCodes()
        {
            long[] codes = new long[0];
            return codes;
        }

        /// <summary>
        /// 将当前二进制码旋转九十度 rotate the code 90°
        /// </summary>
        /// <param name="code">long类型的二进制码</param>
        /// <returns>旋转九十度的结果</returns>
        protected long _rotate90(long code)
        {
            long wr = 0;
            long one = 1;
            for (int r = this.edge - 1; r >= 0; r--)
            {
                for (int c = 0; c < this.edge; c++)
                {
                    int b = r + this.edge * c;
                    wr = wr << 1;
                    if ((code & (one << b)) != 0)
                    {
                        wr |= 1;
                    }
                }
            }
            return wr;
        }

        /// <summary>
        /// 进行code匹配 match tagcode with array you have
        /// </summary>
        /// <param name="tagcode">二进制code(long)</param>
        /// <returns></returns>
        public Detector _decode(long tagcode)
        {
            int bestid = -1;
            int besthamming = 255;
            int bestrotation = -1;
            long bestcode = -1;
            long rcodes = tagcode;

            for (int r = 0; r < 4; r++)
            {
                int index = 0;
                foreach (long tag in GetCodes())
                {
                    byte[] byValue = BitConverter.GetBytes(tag ^ rcodes);
                    //Array.Reverse(byValue);
                    string str = string.Empty;
                    string strTemp;
                    foreach (var item in byValue)
                    {
                        strTemp = System.Convert.ToString(item, 2);
                        strTemp = strTemp.Insert(0, new string('0', 8 - strTemp.Length));
                        str += strTemp;
                    }

                    //var str = Convert.ToString(unint,2);
                    int dis = Regex.Matches(str, @"1").Count;
                    if (dis < besthamming)
                    {
                        besthamming = dis;
                        bestid = index;
                        bestcode = tag;
                        bestrotation = r;
                    }
                    index += 1;
                }
                rcodes = _rotate90(rcodes);
            }
            Detector tagdection = new Detector();
            tagdection.id = bestid;
            tagdection.hammingDistance = besthamming;
            tagdection.obsCode = tagcode;
            tagdection.matchCode = bestcode;
            tagdection.rotation = bestrotation;
            if (besthamming < this.minhamming)
            {
                tagdection.good = true;
            }
            return tagdection;
        }
    }
}
