using BoneTCP;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

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
        public Client(string SERVER_IP = "127.0.0.1", int PORT = 6900, bool enableLogging = false)
        {
            // Create a new UDP client for sending and receiving messages
            client = new UdpClient();

            SERVER_ENDPOINT = new IPEndPoint(IPAddress.Parse(SERVER_IP), PORT);

            // Create a new sliding window for the client
            slidingWindow = new SlidingWindow(64, client, SERVER_ENDPOINT, enableLogging);

            // Set the message received event handler
            slidingWindow.MessageReceived += OnMessageReceived;

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
            slidingWindow.Send(msg);

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
        private void OnMessageReceived(Message msg, IPEndPoint endPoint)
        {
            // Print the received message to the console
            // Console.WriteLine($"[CL] Received message from {endPoint}: {msg.Data}");
        }


    }


}
