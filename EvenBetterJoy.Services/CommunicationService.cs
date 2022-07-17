using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Force.Crc32;
using EvenBetterJoy.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EvenBetterJoy.Services
{
    public class CommunicationService : ICommunicationService
    {
        private const ushort MAX_PROTOCOL_VERSION = 1001;

        private Socket socket;
        private uint serverId;
        private bool running;
        private readonly byte[] recvBuffer;
        private readonly Dictionary<IPEndPoint, ClientRequestTimes> clients;

        private readonly ILogger logger;
        private readonly Settings settings;

        public CommunicationService(
            ILogger<CommunicationService> logger,
            IOptions<Settings> settings)
        {
            this.logger = logger;
            this.settings = settings.Value;

            recvBuffer = new byte[1024];
            clients = new Dictionary<IPEndPoint, ClientRequestTimes>();
        }

        private int BeginPacket(byte[] packetBuf, ushort reqProtocolVersion = MAX_PROTOCOL_VERSION)
        {
            int currIdx = 0;
            packetBuf[currIdx++] = (byte)'D';
            packetBuf[currIdx++] = (byte)'S';
            packetBuf[currIdx++] = (byte)'U';
            packetBuf[currIdx++] = (byte)'S';

            Array.Copy(BitConverter.GetBytes((ushort)reqProtocolVersion), 0, packetBuf, currIdx, 2);
            currIdx += 2;

            Array.Copy(BitConverter.GetBytes((ushort)packetBuf.Length - 16), 0, packetBuf, currIdx, 2);
            currIdx += 2;

            // place for crc
            Array.Clear(packetBuf, currIdx, 4);
            currIdx += 4;

            Array.Copy(BitConverter.GetBytes((uint)serverId), 0, packetBuf, currIdx, 4);
            currIdx += 4;

            return currIdx;
        }

        private static void FinishPacket(byte[] packetBuf)
        {
            Array.Clear(packetBuf, 8, 4);

            uint crcCalc = Crc32Algorithm.Compute(packetBuf);
            Array.Copy(BitConverter.GetBytes((uint)crcCalc), 0, packetBuf, 8, 4);
        }

        private void SendPacket(IPEndPoint clientEP, byte[] usefulData, ushort reqProtocolVersion = MAX_PROTOCOL_VERSION)
        {
            byte[] packetData = new byte[usefulData.Length + 16];
            int currIdx = BeginPacket(packetData, reqProtocolVersion);
            Array.Copy(usefulData, 0, packetData, currIdx, usefulData.Length);
            FinishPacket(packetData);

            socket.SendTo(packetData, clientEP);
        }

        private void ProcessIncoming(byte[] localMsg, IPEndPoint clientEP)
        {
            try
            {
                int currIdx = 0;
                if (localMsg[0] != 'D' || localMsg[1] != 'S' || localMsg[2] != 'U' || localMsg[3] != 'C')
                    return;
                else
                    currIdx += 4;

                uint protocolVer = BitConverter.ToUInt16(localMsg, currIdx);
                currIdx += 2;

                if (protocolVer > MAX_PROTOCOL_VERSION)
                    return;

                uint packetSize = BitConverter.ToUInt16(localMsg, currIdx);
                currIdx += 2;

                if (packetSize < 0)
                    return;

                // size of header
                packetSize += 16;
                if (packetSize > localMsg.Length)
                    return;
                else if (packetSize < localMsg.Length)
                {
                    byte[] newMsg = new byte[packetSize];
                    Array.Copy(localMsg, newMsg, packetSize);
                    localMsg = newMsg;
                }

                uint crcValue = BitConverter.ToUInt32(localMsg, currIdx);
                //zero out the crc32 in the packet once we got it since that's whats needed for calculation
                localMsg[currIdx++] = 0;
                localMsg[currIdx++] = 0;
                localMsg[currIdx++] = 0;
                localMsg[currIdx++] = 0;

                uint crcCalc = Crc32Algorithm.Compute(localMsg);
                if (crcValue != crcCalc)
                    return;

                uint clientId = BitConverter.ToUInt32(localMsg, currIdx);
                currIdx += 4;

                uint messageType = BitConverter.ToUInt32(localMsg, currIdx);
                currIdx += 4;

                if (messageType == (uint)ControllerMessageType.DSUC_VersionReq)
                {
                    byte[] outputData = new byte[8];
                    int outIdx = 0;
                    Array.Copy(BitConverter.GetBytes((uint)ControllerMessageType.DSUS_VersionRsp), 0, outputData, outIdx, 4);
                    outIdx += 4;
                    Array.Copy(BitConverter.GetBytes((ushort)MAX_PROTOCOL_VERSION), 0, outputData, outIdx, 2);
                    outIdx += 2;
                    outputData[outIdx++] = 0;
                    outputData[outIdx++] = 0;

                    SendPacket(clientEP, outputData, 1001);
                }
                else if (messageType == (uint)ControllerMessageType.DSUC_ListPorts)
                {
                    // Requested information on gamepads - return MAC address
                    int numPadRequests = BitConverter.ToInt32(localMsg, currIdx);
                    currIdx += 4;
                    if (numPadRequests < 0 || numPadRequests > 4)
                        return;

                    int requestsIdx = currIdx;
                    for (int i = 0; i < numPadRequests; i++)
                    {
                        byte currRequest = localMsg[requestsIdx + i];
                        if (currRequest < 0 || currRequest > 4)
                            return;
                    }

                    byte[] outputData = new byte[16];
                    for (byte i = 0; i < numPadRequests; i++)
                    {
                        byte currRequest = localMsg[requestsIdx + i];
                        var padData = controllers[i];//controllers[currRequest];

                        int outIdx = 0;
                        Array.Copy(BitConverter.GetBytes((uint)ControllerMessageType.DSUS_PortInfo), 0, outputData, outIdx, 4);
                        outIdx += 4;

                        outputData[outIdx++] = (byte)padData.PadId;
                        outputData[outIdx++] = (byte)padData.constate;
                        outputData[outIdx++] = (byte)padData.model;
                        outputData[outIdx++] = (byte)padData.connection;

                        var addressBytes = padData.PadMacAddress.GetAddressBytes();
                        if (addressBytes.Length == 6)
                        {
                            outputData[outIdx++] = addressBytes[0];
                            outputData[outIdx++] = addressBytes[1];
                            outputData[outIdx++] = addressBytes[2];
                            outputData[outIdx++] = addressBytes[3];
                            outputData[outIdx++] = addressBytes[4];
                            outputData[outIdx++] = addressBytes[5];
                        }
                        else
                        {
                            outputData[outIdx++] = 0;
                            outputData[outIdx++] = 0;
                            outputData[outIdx++] = 0;
                            outputData[outIdx++] = 0;
                            outputData[outIdx++] = 0;
                            outputData[outIdx++] = 0;
                        }

                        outputData[outIdx++] = (byte)padData.battery;//(byte)padData.BatteryStatus;
                        outputData[outIdx++] = 0;

                        SendPacket(clientEP, outputData, 1001);
                    }
                }
                else if (messageType == (uint)ControllerMessageType.DSUC_PadDataReq)
                {
                    byte regFlags = localMsg[currIdx++];
                    byte idToReg = localMsg[currIdx++];
                    PhysicalAddress macToReg = null;
                    {
                        byte[] macBytes = new byte[6];
                        Array.Copy(localMsg, currIdx, macBytes, 0, macBytes.Length);
                        currIdx += macBytes.Length;
                        macToReg = new PhysicalAddress(macBytes);
                    }

                    lock (clients)
                    {
                        if (clients.ContainsKey(clientEP))
                            clients[clientEP].RequestPadInfo(regFlags, idToReg, macToReg);
                        else
                        {
                            var clientTimes = new ClientRequestTimes();
                            clientTimes.RequestPadInfo(regFlags, idToReg, macToReg);
                            clients[clientEP] = clientTimes;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Unable to process income BT traffic");
            }
        }

        private void ReceiveCallback(IAsyncResult iar)
        {
            byte[] localMsg = null;
            EndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);

            try
            {
                //Get the received message.
                Socket recvSock = (Socket)iar.AsyncState;
                int msgLen = recvSock.EndReceiveFrom(iar, ref clientEP);

                localMsg = new byte[msgLen];
                Array.Copy(recvBuffer, localMsg, msgLen);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Unable to receive BT traffic");
            }

            //Start another receive as soon as we copied the data
            StartReceive();

            //Process the data if its valid
            if (localMsg != null)
            {
                ProcessIncoming(localMsg, (IPEndPoint)clientEP);
            }
        }
        
        private void StartReceive()
        {
            try
            {
                if (running)
                {
                    //Start listening for a new message.
                    EndPoint newClientEP = new IPEndPoint(IPAddress.Any, 0);
                    socket.BeginReceiveFrom(recvBuffer, 0, recvBuffer.Length, SocketFlags.None, ref newClientEP, ReceiveCallback, socket);
                }
            }
            catch (SocketException ex)
            {
                uint IOC_IN = 0x80000000;
                uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                socket.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);

                StartReceive();
            }
        }

        public void Start()
        {
            var ip = settings.IpAddress;
            var port = settings.Port;

            if (!settings.MotionServer)
            {
                logger.LogInformation("Motion server is OFF.");
                return;
            }

            if (running)
            {
                if (socket != null)
                {
                    socket.Close();
                    socket = null;
                }
                running = false;
            }

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            try { socket.Bind(new IPEndPoint(ip, port)); }
            catch (SocketException ex)
            {
                socket.Close();
                socket = null;

                logger.LogError("Could not start server. Make sure that only one instance of the program is running at a time and no other CemuHook applications are running.");
                return;
            }

            byte[] randomBuf = new byte[4];
            new Random().NextBytes(randomBuf);
            serverId = BitConverter.ToUInt32(randomBuf, 0);

            running = true;
            logger.LogInformation($"Starting server on {ip}:{port}");
            StartReceive();
        }

        public void Stop()
        {
            running = false;
            if (socket != null)
            {
                socket.Close();
                socket = null;
            }
        }

        private bool ReportToBuffer(Joycon hidReport, byte[] outputData, ref int outIdx)
        {
            var ds4 = Joycon.MapToDualShock4Input(hidReport);

            outputData[outIdx] = 0;

            if (ds4.dPad == Controller.DpadDirection.West || ds4.dPad == Controller.DpadDirection.Northwest || ds4.dPad == Controller.DpadDirection.Southwest) outputData[outIdx] |= 0x80;
            if (ds4.dPad == Controller.DpadDirection.South || ds4.dPad == Controller.DpadDirection.Southwest || ds4.dPad == Controller.DpadDirection.Southeast) outputData[outIdx] |= 0x40;
            if (ds4.dPad == Controller.DpadDirection.East || ds4.dPad == Controller.DpadDirection.Northeast || ds4.dPad == Controller.DpadDirection.Southeast) outputData[outIdx] |= 0x20;
            if (ds4.dPad == Controller.DpadDirection.North || ds4.dPad == Controller.DpadDirection.Northwest || ds4.dPad == Controller.DpadDirection.Northeast) outputData[outIdx] |= 0x10;

            if (ds4.options) outputData[outIdx] |= 0x08;
            if (ds4.thumb_right) outputData[outIdx] |= 0x04;
            if (ds4.thumb_left) outputData[outIdx] |= 0x02;
            if (ds4.share) outputData[outIdx] |= 0x01;

            outputData[++outIdx] = 0;

            if (ds4.square) outputData[outIdx] |= 0x80;
            if (ds4.cross) outputData[outIdx] |= 0x40;
            if (ds4.circle) outputData[outIdx] |= 0x20;
            if (ds4.triangle) outputData[outIdx] |= 0x10;

            if (ds4.shoulder_right) outputData[outIdx] |= 0x08;
            if (ds4.shoulder_left) outputData[outIdx] |= 0x04;
            if (ds4.trigger_right_value == Byte.MaxValue) outputData[outIdx] |= 0x02;
            if (ds4.trigger_left_value == Byte.MaxValue) outputData[outIdx] |= 0x01;

            outputData[++outIdx] = ds4.ps ? (byte)1 : (byte)0;
            outputData[++outIdx] = ds4.touchpad ? (byte)1 : (byte)0;

            outputData[++outIdx] = ds4.thumb_left_x;
            outputData[++outIdx] = ds4.thumb_left_y;
            outputData[++outIdx] = ds4.thumb_right_x;
            outputData[++outIdx] = ds4.thumb_right_y;

            //we don't have analog buttons so just use the Button enums (which give either 0 or 0xFF)
            outputData[++outIdx] = (ds4.dPad == Controller.DpadDirection.West || ds4.dPad == Controller.DpadDirection.Northwest || ds4.dPad == Controller.DpadDirection.Southwest) ? (byte)0xFF : (byte)0;
            outputData[++outIdx] = (ds4.dPad == Controller.DpadDirection.South || ds4.dPad == Controller.DpadDirection.Southwest || ds4.dPad == Controller.DpadDirection.Southeast) ? (byte)0xFF : (byte)0;
            outputData[++outIdx] = (ds4.dPad == Controller.DpadDirection.East || ds4.dPad == Controller.DpadDirection.Northeast || ds4.dPad == Controller.DpadDirection.Southeast) ? (byte)0xFF : (byte)0;
            outputData[++outIdx] = (ds4.dPad == Controller.DpadDirection.North || ds4.dPad == Controller.DpadDirection.Northwest || ds4.dPad == Controller.DpadDirection.Northeast) ? (byte)0xFF : (byte)0; ;

            outputData[++outIdx] = ds4.square ? (byte)0xFF : (byte)0;
            outputData[++outIdx] = ds4.cross ? (byte)0xFF : (byte)0;
            outputData[++outIdx] = ds4.circle ? (byte)0xFF : (byte)0;
            outputData[++outIdx] = ds4.triangle ? (byte)0xFF : (byte)0;

            outputData[++outIdx] = ds4.shoulder_right ? (byte)0xFF : (byte)0;
            outputData[++outIdx] = ds4.shoulder_left ? (byte)0xFF : (byte)0;

            outputData[++outIdx] = ds4.trigger_right_value;
            outputData[++outIdx] = ds4.trigger_left_value;

            outIdx++;

            //DS4 only: touchpad points
            for (int i = 0; i < 2; i++)
            {
                outIdx += 6;
            }

            //motion timestamp
            Array.Copy(BitConverter.GetBytes(hidReport.Timestamp), 0, outputData, outIdx, 8);
            outIdx += 8;

            //accelerometer
            {
                var accel = hidReport.GetAccel();
                if (accel != null)
                {
                    Array.Copy(BitConverter.GetBytes(accel.Y), 0, outputData, outIdx, 4);
                    outIdx += 4;
                    Array.Copy(BitConverter.GetBytes(-accel.Z), 0, outputData, outIdx, 4);
                    outIdx += 4;
                    Array.Copy(BitConverter.GetBytes(accel.X), 0, outputData, outIdx, 4);
                    outIdx += 4;
                }
                else
                {
                    outIdx += 12;
                    logger.LogWarning("No accelerometer reported.");
                }
            }

            //gyroscope
            {
                var gyro = hidReport.GetGyro();
                if (gyro != null)
                {
                    Array.Copy(BitConverter.GetBytes(gyro.Y), 0, outputData, outIdx, 4);
                    outIdx += 4;
                    Array.Copy(BitConverter.GetBytes(gyro.Z), 0, outputData, outIdx, 4);
                    outIdx += 4;
                    Array.Copy(BitConverter.GetBytes(gyro.X), 0, outputData, outIdx, 4);
                    outIdx += 4;
                }
                else
                {
                    outIdx += 12;
                    logger.LogWarning("No gyroscope reported.");
                }
            }

            return true;
        }

        public void NewReportIncoming(Joycon hidReport)
        {
            if (!running)
                return;

            var clientsList = new List<IPEndPoint>();
            var now = DateTime.UtcNow;
            lock (clients)
            {
                var clientsToDelete = new List<IPEndPoint>();

                foreach (var cl in clients)
                {
                    const double TimeoutLimit = 5;

                    if ((now - cl.Value.AllPadsTime).TotalSeconds < TimeoutLimit)
                        clientsList.Add(cl.Key);
                    else if ((hidReport.PadId >= 0 && hidReport.PadId <= 3) &&
                             (now - cl.Value.PadIdsTime[(byte)hidReport.PadId]).TotalSeconds < TimeoutLimit)
                        clientsList.Add(cl.Key);
                    else if (cl.Value.PadMacsTime.ContainsKey(hidReport.PadMacAddress) &&
                             (now - cl.Value.PadMacsTime[hidReport.PadMacAddress]).TotalSeconds < TimeoutLimit)
                        clientsList.Add(cl.Key);
                    else //check if this client is totally dead, and remove it if so
                    {
                        bool clientOk = false;
                        for (int i = 0; i < cl.Value.PadIdsTime.Length; i++)
                        {
                            var dur = (now - cl.Value.PadIdsTime[i]).TotalSeconds;
                            if (dur < TimeoutLimit)
                            {
                                clientOk = true;
                                break;
                            }
                        }
                        if (!clientOk)
                        {
                            foreach (var dict in cl.Value.PadMacsTime)
                            {
                                var dur = (now - dict.Value).TotalSeconds;
                                if (dur < TimeoutLimit)
                                {
                                    clientOk = true;
                                    break;
                                }
                            }

                            if (!clientOk)
                                clientsToDelete.Add(cl.Key);
                        }
                    }
                }

                foreach (var delCl in clientsToDelete)
                {
                    clients.Remove(delCl);
                }
                clientsToDelete.Clear();
                clientsToDelete = null;
            }

            if (clientsList.Count <= 0)
                return;

            byte[] outputData = new byte[100];
            int outIdx = BeginPacket(outputData, 1001);
            Array.Copy(BitConverter.GetBytes((uint)ControllerMessageType.DSUS_PadDataRsp), 0, outputData, outIdx, 4);
            outIdx += 4;

            outputData[outIdx++] = (byte)hidReport.PadId;
            outputData[outIdx++] = (byte)hidReport.constate;
            outputData[outIdx++] = (byte)hidReport.model;
            outputData[outIdx++] = (byte)hidReport.connection;
            {
                byte[] padMac = hidReport.PadMacAddress.GetAddressBytes();
                outputData[outIdx++] = padMac[0];
                outputData[outIdx++] = padMac[1];
                outputData[outIdx++] = padMac[2];
                outputData[outIdx++] = padMac[3];
                outputData[outIdx++] = padMac[4];
                outputData[outIdx++] = padMac[5];
            }

            outputData[outIdx++] = (byte)hidReport.battery;
            outputData[outIdx++] = 1;

            Array.Copy(BitConverter.GetBytes(hidReport.packetCounter), 0, outputData, outIdx, 4);
            outIdx += 4;

            if (!ReportToBuffer(hidReport, outputData, ref outIdx))
                return;
            else
                FinishPacket(outputData);

            foreach (var cl in clientsList)
            {
                try { socket.SendTo(outputData, cl); } catch (SocketException ex) { }
            }
            clientsList.Clear();
            clientsList = null;
        }
    }
}
