﻿/* meadowWiFiCom Firmware
 * 
 * Anthony Biccum
 * ECET 230 Object Oriented Programming 
 * Camosun College
 * 
 * This is the firmware for the meadow board to communicate packets with my WPF UDP server. 
 * 
 * Read analog pins and create packets to be sent.
 * Connect to WiFi and then to UDP server. 
 * Setup meadow board as UDP client to send and receive packets.
 * Parse received packets to turn on led's and graphics data.
 * 
 * 
 * 
 */

using Meadow;
using Meadow.Devices;
using Meadow.Foundation;
using Meadow.Foundation.Displays.TftSpi;
using Meadow.Foundation.Graphics;
using Meadow.Foundation.Leds;
using Meadow.Gateway.WiFi;
using Meadow.Hardware;
using Meadow.Units;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace meadowWiFiCom
{
    public class MeadowApp : App<F7Micro, MeadowApp>
    {
        St7735 st7735;
        GraphicsLibrary graphics;
        UdpClient udpclient;

        //WiFi Information
        private static string SSID = "TELUS0108";
        private static string PASSWORD = "kz9s7yhs3v";

        //UDP Server Information
        private const int PORT_NO = 0;
        private const string SERVER_IP = "";

        /**********************Packet Variables*********************/
        string milliVolts00, milliVolts01, milliVolts02, milliVolts03, milliVolts04, milliVolts05;

        IAnalogInputPort analogIn00;
        IAnalogInputPort analogIn01;
        IAnalogInputPort analogIn02;
        IAnalogInputPort analogIn03;
        IAnalogInputPort analogIn04;
        IAnalogInputPort analogIn05;

        IDigitalInputPort inputPortD02;
        IDigitalInputPort inputPortD03;
        IDigitalInputPort inputPortD04;
        IDigitalInputPort inputPortD05;
        IDigitalOutputPort outputPortD06;
        IDigitalOutputPort outputPortD07;
        IDigitalOutputPort outputPortD08;
        IDigitalOutputPort outputPortD09;

        private int packetNumber;
        private int milliVolts;
        private int analogPin;
        private int digitalPin;
        private int packetLen;
        private int analogReq = 6;// set to the Max number of analog pins required
        private int digitalInReq = 8;// set to the Max number of digital input pins required
        private int checkSum = 0;
        private int index = 0;
        private int TXstate = 0;
        private int RXstate = 0;
        private int RXindex = 0;
        private String outPacket = "###P##AN00AN01AN02AN03AN04AN05bbbbCHKrn";
        private String inPacket;
        private int packetTime = 100;
        /**************************************************/

        public MeadowApp()
        {
            //Initialize screen, wifi, and udp connection to server.
            Initialize();

            SendLoop();

        }

        private void Initialize()
        {
            RgbLed led = new RgbLed(Device,
                                    Device.Pins.OnboardLedRed,
                                    Device.Pins.OnboardLedGreen,
                                    Device.Pins.OnboardLedBlue);
            led.SetColor(RgbLed.Colors.Red);

            InitializeDisplay();

            InitializePackets();

            InitializeWiFi().Wait();

            InitializeUDP();


            led.StartBlink(RgbLed.Colors.Green);
        }

        private void InitializeDisplay()
        {
            var config = new SpiClockConfiguration(6000, SpiClockConfiguration.Mode.Mode3);
            st7735 = new St7735
            (
                device: Device,
                spiBus: Device.CreateSpiBus(Device.Pins.SCK,
                                            Device.Pins.MOSI,
                                            Device.Pins.MISO,
                                            config),
                chipSelectPin: Device.Pins.D02,
                dcPin: Device.Pins.D01,
                resetPin: Device.Pins.D00,
                width: 128,
                height: 160,
                St7735.DisplayType.ST7735R_BlackTab
            );
            graphics = new GraphicsLibrary(st7735);

            graphics.Clear(true);
            graphics.CurrentFont = new Font8x12();
            graphics.DrawText(5, 20, "Welcome To");
            graphics.DrawText(5, 20, "Meadow WiFi!");
            graphics.Show();
        }

        async Task InitializeWiFi()
        {
            //Display debug message on display and change board LED to blue
            graphics.Clear(true);
            graphics.DrawText(5, 5, "Connecting...");
            graphics.Show();

            Device.WiFiAdapter.WiFiConnected += WiFiAdapter_ConnectionCompleted;     //Do this event once we are connected to WiFi

            var connectionResult = await Device.WiFiAdapter.Connect(SSID, PASSWORD); //Connect to WiFi, and save result

            if (connectionResult.ConnectionStatus != ConnectionStatus.Success)       //If not connected will throw error
            {
                graphics.Clear(true);
                graphics.DrawText(5, 5, "Problem Connecting.");
                graphics.Show();

                throw new Exception($"Cannot connect to network: {connectionResult.ConnectionStatus}");
            }
        }

        private void WiFiAdapter_ConnectionCompleted(object sender, EventArgs e)
        {
            graphics.Clear(true);
            graphics.DrawText(5, 5, "Connected.");
            graphics.Show();
        }

        private void InitializeUDP()
        {
            //https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.udpclient?view=net-6.0
            udpclient = new UdpClient(PORT_NO);
            try
            {
                //Connect to our WPF server
                udpclient.Connect(SERVER_IP, PORT_NO);
                //Send a message that we have connected
                Byte[] sendBytes = Encoding.ASCII.GetBytes("Meadow has connected!");
                udpclient.Send(sendBytes, sendBytes.Length);

                graphics.Clear(true);
                graphics.DrawText(5, 5, "Connected To");
                graphics.DrawText(5, 5, "UDP Server");
                graphics.Show();

                //// Sends a message to the host to which you have connected.
                //Byte[] sendBytes = Encoding.ASCII.GetBytes("Meadow has connected!");

                //udpclient.Send(sendBytes, sendBytes.Length);

                ////IPEndPoint object will allow us to read datagrams sent from any source.
                //IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

                //// Blocks until a message returns on this socket from a remote host.
                //Byte[] receiveBytes = udpclient.Receive(ref RemoteIpEndPoint);
                //string returnData = Encoding.ASCII.GetString(receiveBytes);

                //// Uses the IPEndPoint object to determine which of these two hosts responded.
                //Console.WriteLine("This is the message you received " +
                //                             returnData.ToString());

                //udpclient.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void InitializePackets()
        {
            inPacket = "";
            analogIn00 = Device.CreateAnalogInputPort(Device.Pins.A00);
            analogIn01 = Device.CreateAnalogInputPort(Device.Pins.A01);
            analogIn02 = Device.CreateAnalogInputPort(Device.Pins.A02);
            analogIn03 = Device.CreateAnalogInputPort(Device.Pins.A03);
            analogIn04 = Device.CreateAnalogInputPort(Device.Pins.A04);
            analogIn05 = Device.CreateAnalogInputPort(Device.Pins.A05);
            inputPortD02 = Device.CreateDigitalInputPort(Device.Pins.D02);
            inputPortD03 = Device.CreateDigitalInputPort(Device.Pins.D03);
            inputPortD04 = Device.CreateDigitalInputPort(Device.Pins.D04);
            inputPortD05 = Device.CreateDigitalInputPort(Device.Pins.D05);
            outputPortD06 = Device.CreateDigitalOutputPort(Device.Pins.D06, false);
            outputPortD07 = Device.CreateDigitalOutputPort(Device.Pins.D07, false);
            outputPortD08 = Device.CreateDigitalOutputPort(Device.Pins.D08, false);
            outputPortD09 = Device.CreateDigitalOutputPort(Device.Pins.D09, false);
            Thread.Sleep(200);
            outputPortD06.State = true;
            Thread.Sleep(200);
            outputPortD08.State = true;
            Thread.Sleep(200);
            outputPortD07.State = true;
            Thread.Sleep(200);
            outputPortD09.State = true;
            //serialPort = Device.CreateSerialPort(Device.SerialPortNames.Com1, 115200);
            //serialPort.Open();
            //serialPort.DataReceived += SerialPort_DataReceived;
            analogIn00.Updated += AnalogIn00_Updated;
            analogIn01.Updated += AnalogIn01_Updated;
            analogIn02.Updated += AnalogIn02_Updated;
            analogIn03.Updated += AnalogIn03_Updated;
            analogIn04.Updated += AnalogIn04_Updated;
            analogIn05.Updated += AnalogIn05_Updated;
            int timeSpanInMilliSec = 100;
            analogIn00.StartUpdating(TimeSpan.FromMilliseconds(timeSpanInMilliSec));
            analogIn01.StartUpdating(TimeSpan.FromMilliseconds(timeSpanInMilliSec));
            analogIn02.StartUpdating(TimeSpan.FromMilliseconds(timeSpanInMilliSec));
            analogIn03.StartUpdating(TimeSpan.FromMilliseconds(timeSpanInMilliSec));
            analogIn04.StartUpdating(TimeSpan.FromMilliseconds(timeSpanInMilliSec));
            analogIn05.StartUpdating(TimeSpan.FromMilliseconds(timeSpanInMilliSec));
            analogIn05.StartUpdating(TimeSpan.FromMilliseconds(timeSpanInMilliSec));
        }

        private void AnalogIn05_Updated(object sender, IChangeResult<Voltage> e)
        {
            int miliVolts = Convert.ToInt32(e.New.Millivolts);
            milliVolts05 = miliVolts.ToString("D4");
        }

        private void AnalogIn04_Updated(object sender, IChangeResult<Voltage> e)
        {
            int miliVolts = Convert.ToInt32(e.New.Millivolts);
            milliVolts04 = miliVolts.ToString("D4");
        }

        private void AnalogIn03_Updated(object sender, IChangeResult<Voltage> e)
        {
            int miliVolts = Convert.ToInt32(e.New.Millivolts);
            milliVolts03 = miliVolts.ToString("D4");
        }

        private void AnalogIn02_Updated(object sender, IChangeResult<Voltage> e)
        {
            int miliVolts = Convert.ToInt32(e.New.Millivolts);
            milliVolts02 = miliVolts.ToString("D4");
        }

        private void AnalogIn01_Updated(object sender, IChangeResult<Voltage> e)
        {
            int miliVolts = Convert.ToInt32(e.New.Millivolts);
            milliVolts01 = miliVolts.ToString("D4");
        }

        private void AnalogIn00_Updated(object sender, IChangeResult<Voltage> e)
        {
            int milliVolts = Convert.ToInt32(e.New.Millivolts);
            milliVolts00 = milliVolts.ToString("D4");
        }

        //private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        //{
        //    int calChkSum = 0;
        //    int recChkSum = 0;

        //    //Console.WriteLine(serialPort.BytesToRead);
        //    byte[] response = new byte[12];
        //    this.serialPort.Read(response, 0, 12);
        //    //serialPort.Write(response);
        //    if (response[0] == 0x23 &&
        //        response[1] == 0x23 &&
        //        response[2] == 0x23)
        //    {
        //        for (int i = 3; i < 7; i++)
        //        {
        //            calChkSum += response[i];
        //        }
        //        //Console.WriteLine(calChkSum);
        //        recChkSum = (response[7] - 0x30) * 100 + (response[8] - 0x30) * 10 + (response[9] - 0x30);
        //        //Console.WriteLine(recChkSum);
        //        if (calChkSum == recChkSum)
        //        {
        //            outputPortD06.State = response[3] == 0x31 ? true : false;
        //            outputPortD07.State = response[4] == 0x31 ? true : false;
        //            outputPortD08.State = response[5] == 0x31 ? true : false;
        //            outputPortD09.State = response[6] == 0x31 ? true : false;
        //            //Console.WriteLine(outputPortD09.State);
        //        }
        //    }
        //}

        private void SendLoop()
        {
            while (true)
            {
                switch (TXstate)
                {
                case 0: // begin making output packet
                    {
                        outPacket = "###"; //header
                        checkSum = 0;
                        index = 0;
                        analogPin = 0;
                        //digitalPin=0;
                        digitalPin = digitalInReq - 1;//start with Most significant pin 
                        outPacket += packetNumber++.ToString("D3"); //inc packet number add to outPacket string
                        packetNumber %= 1000;   //packetnumber rollover code 
                        TXstate = 1;  //move to next state
                        
                    }
                    break;
                case 1: // continue making output packet and do analog at the same time
                    {
                        outPacket += milliVolts00 + milliVolts01 + milliVolts02 + milliVolts03 + milliVolts04 + milliVolts05;

                        TXstate = 2;// move to next state when all analog complete
                        
                    }
                    break;
                case 2:
                    {
                        bool[] currentState = new bool[4];
                        currentState[0] = inputPortD02.State;
                        currentState[1] = inputPortD03.State;
                        currentState[2] = inputPortD04.State;
                        currentState[3] = inputPortD05.State;

                        foreach (bool state in currentState)
                        {
                            string outString = " ";
                            if (state)
                            {
                                outString = "1";
                            }
                            else
                            {
                                outString = "0";
                            }
                            outPacket += outString;
                        }
                        //Console.WriteLine(outPacket);
                        //Thread.Sleep(1000);
                        TXstate = 3;//move to next state when all input complete
                        
                    }
                    break;
                case 3:
                    {
                        for (int i = 3; i < outPacket.Length; i++)
                        {
                            checkSum += (byte)outPacket[i];//calculate check sum
                        }
                        checkSum %= 1000; //trucate check sum to 3 digits
                        outPacket += checkSum.ToString("D3");
                        outPacket += "\r\n";// add carriage return, line feed
                        packetLen = outPacket.Length;//set packet length to send
                                                        //Console.WriteLine(outPacket);
                                                        //Thread.Sleep(1000);
                        TXstate = 4; //move to next state
                        
                    }
                    break;
                case 4: // stay in case 4 until entire packet is sent 
                    //if (index == packetLen)// when entire packet is sent check interval
                    {
                        var buffer = new byte[35];
                        buffer = Encoding.UTF8.GetBytes(outPacket);
                        serialPort.Write(buffer);
                        Thread.Sleep(packetTime);

                        TXstate = 0; //reset the state when the whole packet is sent
                    }
                    break;
                }
            }  
        }
    }
}
