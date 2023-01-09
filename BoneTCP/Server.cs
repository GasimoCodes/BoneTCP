using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace BoneTCP
{
    /// <summary>
    /// Class representing a UDP server which uses sliding window for improved stability.
    /// </summary>
    internal class Server
    {
        // UDP client to send and receive messages
        private UdpClient server;

        // Dictionary to store the sliding windows for each client
        private ConcurrentDictionary<IPEndPoint, SlidingWindow> slidingWindows;

        bool enableLogging = false;

        public int RunningPort
        {
            get
            {
                return ((IPEndPoint)server.Client.LocalEndPoint).Port;
            }
        }


        /// <summary>
        /// Constructor for creating a server instance. A Start() must be called to accept new connections.
        /// </summary>
        /// <param name="PORT">Port on which will the server listen for new connections</param>
        /// <param name="enableLogging">Enable logging into console</param>
        public Server(int PORT = 6900, bool enableLogging = false)
        {
            // Create a new UDP client for sending and receiving messages
            server = new UdpClient(PORT);

            this.enableLogging = enableLogging;

            // Create a new dictionary to store the sliding windows for each client
            slidingWindows = new ConcurrentDictionary<IPEndPoint, SlidingWindow>();

            Console.WriteLine("Server set to " + RunningPort);
        }


        /// <summary>
        /// Start listening to incoming messages
        /// </summary>
        public void Start()
        {
            // Start listening for incoming messages
            server.BeginReceive(new AsyncCallback(OnMessageReceived), null);
            Console.WriteLine("Server on " + RunningPort);
        }

        
        /// <summary>
        /// When a message gets received, checks if an existing sliding window exists. If not, creates.
        /// </summary>
        /// <param name="ar">AsyncResult for the incoming conneciton</param>
        private void OnMessageReceived(IAsyncResult ar)
        {

            // Get the client end point
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);

            // Get the sliding window for the client, or create a new one if it doesn't exist
            if (!slidingWindows.TryGetValue(endPoint, out SlidingWindow slidingWindow))
            {
                slidingWindow = new SlidingWindow(64, server, endPoint, enableLogging);
                slidingWindows.TryAdd(endPoint, slidingWindow);
            }

            slidingWindow.Receive();


            server.BeginReceive(OnMessageReceived, null);
        }

    }
}
