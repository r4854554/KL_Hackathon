using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace HDCircles.Hackathon.Services
{
    class UdpDebug
    {
        private static UdpDebug _instance;

        private const int _statePort = 11000;
        static UdpClient sendingUdpClient;

        public static UdpDebug Instance
        {
            get
            {
                if (null == _instance)
                    _instance = new UdpDebug();

                return _instance;
            }
        }

        private UdpDebug()
        {
            sendingUdpClient = new UdpClient("127.0.0.1", _statePort);
        }
        // udp port for debugging 


        public void SendUdpDebug(double[] data)
        {
            double[] udpState = new double[10];


            //Byte[] sendBytes = Encoding.ASCII.GetBytes("Is anybody there");
            udpState[0] = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            udpState[1] = data[0];
            udpState[2] = data[1];
            udpState[3] = data[2];
            udpState[4] = data[3];
            udpState[9] = (double)_statePort;

            Byte[] sendBytes = udpState.SelectMany(value => BitConverter.GetBytes(value)).ToArray();

            try
            {
                sendingUdpClient.Send(sendBytes, sendBytes.Length);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
        }

        // spare code for udp send
        //try
        //{
        //    //Debug.WriteLine($"This is the message you received {receivingUdpClient.Available}");
        //    if (receivingUdpClient.Available > 0)
        //    {

        //        // Blocks until a message returns on this socket from a remote host.
        //        Byte[] receiveBytes = receivingUdpClient.Receive(ref RemoteIpEndPoint);

        //        double[] values = new double[receiveBytes.Length / 8];

        //        Debug.WriteLine("This is the message you received 1");
        //        Buffer.BlockCopy(receiveBytes, 0, values, 0, values.Length * 8);

        //        //string returnData = Encoding.ASCII.GetString(receiveBytes);


        //        Debug.WriteLine("This is the message you received " +
        //                                     values[0].ToString());

        //        Debug.WriteLine("This is the message you received " +
        //                                     values[4].ToString());
        //        Debug.WriteLine("This message was sent from " +
        //                                    RemoteIpEndPoint.Address.ToString() +
        //                                    " on their port number " +
        //                                    RemoteIpEndPoint.Port.ToString());
        //    }
        //}
        //catch (Exception e)
        //{
        //    Debug.WriteLine(e.ToString());
        //}
    }

}
