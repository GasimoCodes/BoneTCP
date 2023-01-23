using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using BoneTCP.Logging;
using static BoneTCP.SlidingWindow;

namespace BoneTCP
{
    /// <summary>
    /// Class representing a UDP server which uses sliding window for improved stability.
    /// </summary>
    public class Server
    {
        // UDP client to send and receive messages
        private UdpClient server = null;

        bool isWorking = true;

        // Dictionary to store the sliding windows for each client
        private ConcurrentDictionary<IPEndPoint, SlidingWindow> slidingWindows;

        bool enableLogging = false;

        int maxByteSize = 1024;

        public int RunningPort
        {
            get
            {
                return ((IPEndPoint)server.Client.LocalEndPoint).Port;
            }
        }

        /// <summary>
        /// Event raised when a message is succesfully received.
        /// </summary>
        public MessageReceivedEventHandler onMessageReceived;




        /// <summary>
        /// Constructor for creating a server instance. A Start() must be called to accept new connections.
        /// </summary>
        /// <param name="PORT">Port on which will the server listen for new connections</param>
        /// <param name="enableLogging">Enable logging into console</param>
        public Server(int PORT = 6900, bool enableLogging = false, int maxByteSize = 1024)
        {
            // Create a new UDP client for sending and receiving messages
            server = new UdpClient(PORT);

            this.enableLogging = enableLogging;
            this.maxByteSize = maxByteSize;
            
            // Create a new dictionary to store the sliding windows for each client
            slidingWindows = new ConcurrentDictionary<IPEndPoint, SlidingWindow>();

        }


        /// <summary>
        /// Start listening to incoming messages
        /// </summary>
        public void Start()
        {
            if (enableLogging)
                SliderLogger.Log("Server on " + RunningPort);

            // Start listening for incoming messages
            server.BeginReceive(new AsyncCallback(PartReceivedEvent), null);

            // Keep alive
                new Thread(() =>
                {
                    while (isWorking)
                    {
                        Thread.Sleep(1000);
                    }
                }).Start();
            

        }

        
        /// <summary>
        /// When a message gets received, checks if an existing sliding window exists. If not, creates.
        /// </summary>
        /// <param name="ar">AsyncResult for the incoming conneciton</param>
        private void PartReceivedEvent(IAsyncResult ar)
        {

            // Get the client end point
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
            
            byte[] datagram = server.EndReceive(ar, ref endPoint);

            // Get the sliding window for the client, or create a new one if it doesn't exist
            if (!slidingWindows.TryGetValue(endPoint, out SlidingWindow slidingWindow))
            {
                slidingWindow = new SlidingWindow(server, endPoint, maxByteSize, enableLogging);
                slidingWindow.OnMessageReceived += MessageReceivedCall;
                slidingWindows.TryAdd(endPoint, slidingWindow);
            
            }

            slidingWindow.ReceiveRaw(datagram);

            server.BeginReceive(PartReceivedEvent, null);
        }


        /// <summary>
        /// Callback when a message gets received
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="endPoint"></param>
        private void MessageReceivedCall(Message msg, IPEndPoint endPoint)
        {
            this.onMessageReceived.Invoke(msg, endPoint);
        }



        /// <summary>
        /// Sends a message to an existing client connection.
        /// </summary>
        /// <param name="message">Message contents to be sent</param>
        public void SendMessage(string message, IPEndPoint target)
        {

            // Get the sliding window for the client, or create a new one if it doesn't exist
            if (!slidingWindows.TryGetValue(target, out SlidingWindow slidingWindow))
            {
                throw (new Exception("Target client connection does not exist."));
            }

            // Create a new message
            Message msg = new Message(message);

            // Send the message
            slidingWindow.AddMessage(msg);


        }




    }
}
