using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AprilTagsSharp
{
    public class Tag16h5 : TagFamily
    {
        public string name;
        public int tagNumber;
        public static long[] codes = {
            0x000000000000231bL,
            0x0000000000002ea5L,
            0x000000000000346aL,
            0x00000000000045b9L,
            0x00000000000079a6L,
            0x0000000000007f6bL,
            0x000000000000b358L,
            0x000000000000e745L,
            0x000000000000fe59L,
            0x000000000000156dL,
            0x000000000000380bL,
            0x000000000000f0abL,
            0x0000000000000d84L,
            0x0000000000004736L,
            0x0000000000008c72L,
            0x000000000000af10L,
            0x000000000000093cL,
            0x00000000000093b4L,
            0x000000000000a503L,
            0x000000000000468fL,
            0x000000000000e137L,
            0x0000000000005795L,
            0x000000000000df42L,
            0x0000000000001c1dL,
            0x000000000000e9dcL,
            0x00000000000073adL,
            0x000000000000ad5fL,
            0x000000000000d530L,
            0x00000000000007caL,
            0x000000000000af2eL
        };

        public Tag16h5(bool debug = false, int minhamming = 3) : base(4, debug, minhamming)
        {
            this.name = "tag16h5";
            this.tagNumber = codes.Length;
        }

        public override long[] GetCodes()
        {
            return codes;
        }
    }
}
