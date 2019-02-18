namespace AprilTagsSharp
{
    public class Tag25h9 : TagFamily
    {
        public string name;
        public int tagNumber;
        public static long[] codes = {
            0x155cbf1L,
            0x1e4d1b6L,
            0x17b0b68L,
            0x1eac9cdL,
            0x12e14ceL,
            0x3548bbL,
            0x7757e6L,
            0x1065dabL,
            0x1baa2e7L,
            0xdea688L,
            0x81d927L,
            0x51b241L,
            0xdbc8aeL,
            0x1e50e19L,
            0x15819d2L,
            0x16d8282L,
            0x163e035L,
            0x9d9b81L,
            0x173eec4L,
            0xae3a09L,
            0x5f7c51L,
            0x1a137fcL,
            0xdc9562L,
            0x1802e45L,
            0x1c3542cL,
            0x870fa4L,
            0x914709L,
            0x16684f0L,
            0xc8f2a5L,
            0x833ebbL,
            0x59717fL,
            0x13cd050L,
            0xfa0ad1L,
            0x1b763b0L,
            0xb991ceL
        };

        public Tag25h9(bool debug = false, int minhamming = 3) : base(5, debug, minhamming)
        {
            this.name = "tag25h9";
            this.tagNumber = codes.Length;
        }

        public override long[] GetCodes()
        {
            return codes;
        }
    }
}
