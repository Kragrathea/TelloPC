using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Tello
{
    public struct Received
    {
        public IPEndPoint Sender;
        public string Message;
        public byte[] bytes;
    }

    abstract class UdpBase
    {
        protected UdpClient Client;

        protected UdpBase()
        {
            Client = new UdpClient();
        }

        public async Task<Received> Receive()
        {
            var result = await Client.ReceiveAsync();
            return new Received()
            {
                bytes = result.Buffer.ToArray(),
                Message = Encoding.ASCII.GetString(result.Buffer, 0, result.Buffer.Length),
                Sender = result.RemoteEndPoint
            };
        }
    }

    //Server
    class UdpListener : UdpBase
    {
        private IPEndPoint _listenOn;

        public UdpListener(int port) : this(new IPEndPoint(IPAddress.Any, port))
        {
        }

        public UdpListener(IPEndPoint endpoint)
        {
            _listenOn = endpoint;
            Client = new UdpClient(_listenOn);
        }

        public void Reply(string message, IPEndPoint endpoint)
        {
            var datagram = Encoding.ASCII.GetBytes(message);
            Client.Send(datagram, datagram.Length, endpoint);
        }

    }

    //Client
    class UdpUser : UdpBase
    {
        private UdpUser() { }

        public static UdpUser ConnectTo(string hostname, int port)
        {
            var connection = new UdpUser();
            connection.Client.Connect(hostname, port);
            return connection;
        }

        public void Send(string message)
        {
            var datagram = Encoding.ASCII.GetBytes(message);
            Client.Send(datagram, datagram.Length);
        }
        public void Send(byte[] message)
        {
            Client.Send(message, message.Length);
        }
    }

    class Program
    {
        //FCS crc
        public static int poly = 13970;
        public static int[] fcstab = { 0, 4489, 8978, 12955, 17956, 22445, 25910, 29887, 35912, 40385, 44890, 48851, 51820, 56293, 59774, 63735, 4225, 264, 13203, 8730, 22181, 18220, 30135, 25662, 40137, 36160, 49115, 44626, 56045, 52068, 63999, 59510, 8450, 12427, 528, 5017, 26406, 30383, 17460, 21949, 44362, 48323, 36440, 40913, 60270, 64231, 51324, 55797, 12675, 8202, 4753, 792, 30631, 26158, 21685, 17724, 48587, 44098, 40665, 36688, 64495, 60006, 55549, 51572, 16900, 21389, 24854, 28831, 1056, 5545, 10034, 14011, 52812, 57285, 60766, 64727, 34920, 39393, 43898, 47859, 21125, 17164, 29079, 24606, 5281, 1320, 14259, 9786, 57037, 53060, 64991, 60502, 39145, 35168, 48123, 43634, 25350, 29327, 16404, 20893, 9506, 13483, 1584, 6073, 61262, 65223, 52316, 56789, 43370, 47331, 35448, 39921, 29575, 25102, 20629, 16668, 13731, 9258, 5809, 1848, 65487, 60998, 56541, 52564, 47595, 43106, 39673, 35696, 33800, 38273, 42778, 46739, 49708, 54181, 57662, 61623, 2112, 6601, 11090, 15067, 20068, 24557, 28022, 31999, 38025, 34048, 47003, 42514, 53933, 49956, 61887, 57398, 6337, 2376, 15315, 10842, 24293, 20332, 32247, 27774, 42250, 46211, 34328, 38801, 58158, 62119, 49212, 53685, 10562, 14539, 2640, 7129, 28518, 32495, 19572, 24061, 46475, 41986, 38553, 34576, 62383, 57894, 53437, 49460, 14787, 10314, 6865, 2904, 32743, 28270, 23797, 19836, 50700, 55173, 58654, 62615, 32808, 37281, 41786, 45747, 19012, 23501, 26966, 30943, 3168, 7657, 12146, 16123, 54925, 50948, 62879, 58390, 37033, 33056, 46011, 41522, 23237, 19276, 31191, 26718, 7393, 3432, 16371, 11898, 59150, 63111, 50204, 54677, 41258, 45219, 33336, 37809, 27462, 31439, 18516, 23005, 11618, 15595, 3696, 8185, 63375, 58886, 54429, 50452, 45483, 40994, 37561, 33584, 31687, 27214, 22741, 18780, 15843, 11370, 7921, 3960 };

        public static int fsc16(byte[] bytes, int len, int poly)
        {
            if (bytes == null)
            {
                return 65535;
            }
            int i = 0;
            int j = poly;
            poly = len;
            len = j;
            while (true)
            {
                j = len;
                if (poly == 0)
                {
                    break;
                }
                j = bytes[i];
                len = fcstab[((len ^ j) & 0xFF)] ^ len >> 8;
                i += 1;
                poly -= 1;
            }
            return j;
        }
        //write fsc16 crc into the last 2 bytes of the array.
        public static void calcCrc(byte[] bytes, int len)
        {
            if ((bytes == null) || (len <= 2))
            {
                return;
            }
            int i = fsc16(bytes, len - 2, poly);
            bytes[(len - 2)] = ((byte)(i & 0xFF));
            bytes[(len - 1)] = ((byte)(i >> 8 & 0xFF));
        }

        //uCRC
        public static short[] uCRCTable = { 0, 94, 188, 226, 97, 63, 221, 131, 194, 156, 126, 32, 163, 253, 31, 65, 157, 195, 33, 127, 252, 162, 64, 30, 95, 1, 227, 189, 62, 96, 130, 220, 35, 125, 159, 193, 66, 28, 254, 160, 225, 191, 93, 3, 128, 222, 60, 98, 190, 224, 2, 92, 223, 129, 99, 61, 124, 34, 192, 158, 29, 67, 161, 255, 70, 24, 250, 164, 39, 121, 155, 197, 132, 218, 56, 102, 229, 187, 89, 7, 219, 133, 103, 57, 186, 228, 6, 88, 25, 71, 165, 251, 120, 38, 196, 154, 101, 59, 217, 135, 4, 90, 184, 230, 167, 249, 27, 69, 198, 152, 122, 36, 248, 166, 68, 26, 153, 199, 37, 123, 58, 100, 134, 216, 91, 5, 231, 185, 140, 210, 48, 110, 237, 179, 81, 15, 78, 16, 242, 172, 47, 113, 147, 205, 17, 79, 173, 243, 112, 46, 204, 146, 211, 141, 111, 49, 178, 236, 14, 80, 175, 241, 19, 77, 206, 144, 114, 44, 109, 51, 209, 143, 12, 82, 176, 238, 50, 108, 142, 208, 83, 13, 239, 177, 240, 174, 76, 18, 145, 207, 45, 115, 202, 148, 118, 40, 171, 245, 23, 73, 8, 86, 180, 234, 105, 55, 213, 139, 87, 9, 235, 181, 54, 104, 138, 212, 149, 203, 41, 119, 244, 170, 72, 22, 233, 183, 85, 11, 136, 214, 52, 106, 43, 117, 151, 201, 74, 20, 246, 168, 116, 42, 200, 150, 21, 75, 169, 247, 182, 232, 10, 84, 215, 137, 107, 53 };
        public static int uCRC(byte[] bytes, int len, int poly)
        {
            int j = 0;
            int i = poly;
            poly = j;
            while (len != 0)
            {
                j = bytes[poly] ^ i;
                i = j;
                if (j < 0)
                {
                    i = j + 256;
                }
                i = uCRCTable[i];
                poly += 1;
                len -= 1;
            }
            return i;
        }

        //write uCRC to bytes[len-1]
        public static void calcUCRC(byte[] bytes, int len)
        {
            if ((bytes.Length == 0) || (len <= 2))
            {
                return;
            }
            int i = uCRC(bytes, len - 1, 119);
            bytes[(len - 1)] = ((byte)(i & 0xFF));
        }
        //Create joystick packet from floating point axis.
        //Center = 0.0. 
        //Up/Right =1.0. 
        //Down/Left=-1.0. 
        public static byte[] createJoyPacket(float fRx, float fRy, float fLx, float fLy, float unk)
        {
            //template joy packet.
            var packet = new byte[] { 0xcc, 0xb0, 0x00, 0x7f, 0x60, 0x50, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x12, 0x16, 0x01, 0x0e, 0x00, 0x25, 0x54 };

            short axis1 = (short)(660.0F * fRx + 1024.0F);//RightX center=1024 left =364 right =-364
            short axis2 = (short)(660.0F * fRy + 1024.0F);//RightY down =364 up =-364
            short axis3 = (short)(660.0F * fLy + 1024.0F);//LeftY down =364 up =-364
            short axis4 = (short)(660.0F * fLx + 1024.0F);//LeftX left =364 right =-364
            short axis5 = (short)(660.0F * unk + 1024.0F);//Unknown. Possibly camera controls. 

            long packedAxis = ((long)axis1 & 0x7FF) | (((long)axis2 & 0x7FF) << 11) | ((0x7FF & (long)axis3) << 22) | ((0x7FF & (long)axis4) << 33) | ((long)axis5 << 44);
            packet[9] = ((byte)(int)(0xFF & packedAxis));
            packet[10] = ((byte)(int)(packedAxis >> 8 & 0xFF));
            packet[11] = ((byte)(int)(packedAxis >> 16 & 0xFF));
            packet[12] = ((byte)(int)(packedAxis >> 24 & 0xFF));
            packet[13] = ((byte)(int)(packedAxis >> 32 & 0xFF));
            packet[14] = ((byte)(int)(packedAxis >> 40 & 0xFF));

            //Add time info.		
            var now = DateTime.Now;
            packet[15] = (byte)now.Hour;
            packet[16] = (byte)now.Minute;
            packet[17] = (byte)now.Second;
            packet[18] = (byte)(now.Millisecond & 0xff);
            packet[19] = (byte)(now.Millisecond >> 8);

            calcUCRC(packet, 4);//Not really needed.

            //calc crc for packet. 
            calcCrc(packet, packet.Length);

            return packet;
        }
        static JoystickState joyState = new JoystickState();
        static void initJoystick()
        {
            // Initialize DirectInput
            var directInput = new DirectInput();

            // Find a Joystick Guid
            var joystickGuid = Guid.Empty;

            foreach (var deviceInstance in directInput.GetDevices(DeviceType.Gamepad,
                        DeviceEnumerationFlags.AllDevices))
                joystickGuid = deviceInstance.InstanceGuid;

            // If Gamepad not found, look for a Joystick
            if (joystickGuid == Guid.Empty)
                foreach (var deviceInstance in directInput.GetDevices(DeviceType.Joystick,
                        DeviceEnumerationFlags.AllDevices))
                    joystickGuid = deviceInstance.InstanceGuid;

            // If Joystick not found, throws an error
            if (joystickGuid == Guid.Empty)
            {
                Console.WriteLine("No joystick/Gamepad found.");
                Console.ReadKey();
                Environment.Exit(1);
            }

            // Instantiate the joystick
            var joystick = new Joystick(directInput, joystickGuid);

            Console.WriteLine("Found Joystick/Gamepad with GUID: {0}", joystickGuid);

            // Query all suported ForceFeedback effects
            var allEffects = joystick.GetEffects();
            foreach (var effectInfo in allEffects)
                Console.WriteLine("Effect available {0}", effectInfo.Name);

            // Set BufferSize in order to use buffered data.
            joystick.Properties.BufferSize = 128;

            // Acquire the joystick
            joystick.Acquire();

            while (true)
            {

                joystick.Poll();
                joystick.GetCurrentState(ref joyState);
                //var x = state.X / 5000.0f;
                //var y = state.Y / 5000.0f;
                Thread.Sleep(10);

            }
        }
        static void Main(string[] args)
        {

            //command server
            var commandServer = new UdpListener(9000);
            Task.Factory.StartNew(async () => {
                while (true)
                {
                    var received = await commandServer.Receive();
                    Console.WriteLine("Server:"+received.Message);
                }
            });

            //video server
            var videoServer = new UdpListener(6138);
            Task.Factory.StartNew(async () => {
                while (true)
                {
                    var received = await videoServer.Receive();
                    Console.WriteLine("video:" + received.Message);
                }
            });

            //messages server
            var server = new UdpListener(6525);
            Task.Factory.StartNew(async () => {
                while (true)
                {
                    var received = await server.Receive();
                    int cmdId = ((int)received.bytes[5] | ((int)received.bytes[6] << 8));
                    Console.WriteLine("cmdId:" + cmdId);
                }
            });

            //create a new client
            var client = UdpUser.ConnectTo("192.168.10.1", 8889);
            //            var client = UdpUser.ConnectTo("127.0.0.1", 9000);

            Dictionary<int, string> cmdIdLookup = new Dictionary<int, string>
            {
                { 26, "Wifi" },//2 bytes. Strength, Disturb.
                { 53, "Light" },//1 byte?
                { 86, "FlyData" },
                { 4176, "Data" },//wtf?
            };

            //wait for reply messages from server and send them to console 
            Task.Factory.StartNew(async () => {
                while (true)
                {
                    try
                    {
                        var received = await client.Receive();

                        //var received = await server.Receive();
                        int cmdId = ((int)received.bytes[5] | ((int)received.bytes[6] << 8));

                        var cmdName = "unknown";
                        if (cmdIdLookup.ContainsKey(cmdId))
                            cmdName = cmdIdLookup[cmdId];

                        var dataStr = BitConverter.ToString(received.bytes.Skip(9).Take(30).ToArray()).Replace("-", " ");

                        if(cmdId!=26 && cmdId != 86 && cmdId != 53 && cmdId != 4177 && cmdId != 4178)
                        //    if (cmdId == 86)
                                Console.WriteLine("cmdId:"+cmdId + "(0x"+cmdId.ToString("X2")+")"+cmdName + " "+dataStr  );


                        //Console.WriteLine(received.Message);
                        if (received.Message.Contains("quit"))
                            break;
                    }
                    catch (Exception ex)
                    {
                        Debug.Write(ex);
                    }
                }
            });



            byte[] sendbuf = Encoding.UTF8.GetBytes("conn_req:\x00\x00");
            sendbuf[sendbuf.Length - 2] = 0x96;
            sendbuf[sendbuf.Length - 1] = 0x17;
            client.Send(sendbuf);

            //var iframePacket = new byte[] { 0xcc, 0x58, 0x00, 0x7c, 0x60, 0x25, 0x00, 0x00, 0x00, 0x6c, 0x95 };
            //client.Send(iframePacket);

            //Start polling joystick
            Task.Factory.StartNew(async () => {
                initJoystick();
            });

            //Send joystick updates.
            Task.Factory.StartNew(async () => {
            while (true)
            {
                try
                {
                        var rx = ((float)joyState.RotationX / 0x8000)-1;
                        var ry = -(((float)joyState.RotationY / 0x8000) - 1);
                        var lx = ((float)joyState.X / 0x8000) - 1;
                        var ly = -(((float)joyState.Y / 0x8000) - 1);
                        var deadBand = 0.15f;
                        rx = Math.Abs(rx) < deadBand ? 0.0f : rx;
                        ry = Math.Abs(ry) < deadBand ? 0.0f : ry;
                        lx = Math.Abs(lx) < deadBand ? 0.0f : lx;
                        ly = Math.Abs(ly) < deadBand ? 0.0f : ly;

                        rx = rx * 0.5f;
                        ry = ry * 0.5f;
                        lx = lx * 0.5f;
                        //ly = ly * 0.5f;

                        var packet = createJoyPacket(rx, ry, lx, ly, 0.0f);
                        //Console.WriteLine(rx + " " + ry + " " + lx + " " + ly);
                        client.Send(packet);
                        Thread.Sleep(20);

                        if(joyState.Buttons[3])
                        {
                            var takeOffPacket = new byte[] { 0xcc, 0x58, 0x00, 0x7c, 0x68, 0x54, 0x00, 0xe4, 0x01, 0xc2, 0x16 };
                            client.Send(takeOffPacket);
                            Thread.Sleep(250);

                        }
                        if (joyState.Buttons[0])
                        {
                            var landPacket = new byte[] { 0xcc ,0x60, 0x00, 0x27, 0x68, 0x55, 0x00, 0xe5, 0x01, 0x00, 0xba, 0xc7 };
                            client.Send(landPacket);
                            Thread.Sleep(250);

                        }

                    }
                    catch (Exception ex)
                    {
                        Debug.Write(ex);
                    }
                }
            });

            //type ahead :-)
            string read;
            do
            {
                read = Console.ReadLine();
                client.Send(read);
            } while (read != "quit");
        }
    }
    //s conn_req port
    //r conn_ack port
    
    //r msg 0x46=70 Ask return of Date? Code indicates other.
    //s msg 46. Return of Date. Code indicates other. 

    //r telemetry starts.

    //r 1a 16. Wifi updates.
    
    //s 0x25 37. No payload? wth?
    
    //r 0x35 53. Light?


}
