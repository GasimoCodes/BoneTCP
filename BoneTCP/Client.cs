using BoneTCP;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using static BoneTCP.SlidingWindow;

namespace BoneTCP
{
    /// <summary>
    /// Class to represent a client that uses the sliding window protocol to send and receive messages
    /// </summary>
    public class Client
    {

        // UDP client to send and receive messages
        private UdpClient client;

        // Sliding window to use for sending and receiving messages
        private SlidingWindow slidingWindow;

        IPEndPoint SERVER_ENDPOINT = null;


        /// <summary>
        /// Event raised when a message is succesfully received.
        /// </summary>
        public MessageReceivedEventHandler onMessageReceived;


        public int RunningPort
        {
            get
            {
                return ((IPEndPoint)client.Client.LocalEndPoint).Port;
            }
        }

        /// <summary>
        /// Constructor for creating a client instance.
        /// </summary>
        /// <param name="SERVER_IP">Target IP Address</param>
        /// <param name="PORT">Target PORT</param>
        /// <param name="enableLogging">Enable logging to console</param>
        public Client(string SERVER_IP = "127.0.0.1", int PORT = 6900, bool enableLogging = false, int maxByteSize = 1024)
        {
            // Create a new UDP client for sending and receiving messages
            client = new UdpClient();

            SERVER_ENDPOINT = new IPEndPoint(IPAddress.Parse(SERVER_IP), PORT);

            // Create a new sliding window for the client
            slidingWindow = new SlidingWindow(client, SERVER_ENDPOINT, maxByteSize , enableLogging);

            // Set the message received event handler
            slidingWindow.OnMessageReceived += MessageReceivedCall;

        }


        /// <summary>
        /// Sends an message to server specified in the constructor.
        /// </summary>
        /// <param name="message">Message contents to be sent</param>
        public void SendMessage(string message)
        {
            // Create a new message
            Message msg = new Message(message);

            // Send the message
            slidingWindow.AddMessage(msg);

            new Thread(() =>
            {
                while (true)
                    slidingWindow.Receive();

            }).Start();
        }


        /// <summary>
        /// Callback when a message gets received
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="endPoint"></param>
        private void MessageReceivedCall(Message msg, IPEndPoint endPoint)
        {
            // TO-DO: Fix this line.
            if(this.onMessageReceived != null)
            this.onMessageReceived.Invoke(msg, endPoint);
        }


    }


}
