using BoneTCP;
using Pastel;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Timers;


namespace BoneTCP
{
    /// <summary>
    /// SlidingWindow implementation using UDP Connection
    /// </summary>
    public class SlidingWindow
    {
        private const int RESEND_INTERVAL = 100; // 1 second
        private const int TIMEOUT_INTERVAL = 20000; // 1 second


        // The window size
        private int windowSize;

        // The current position of the window
        private int windowPos;

        // The queue to store the messages in
        private Queue<Message> messageQueue;

        // The list to store the received messages in
        private List<Message> receivedMessages;
        private readonly object receivedMessagesLock = new object();

        // The UDP client to use for sending and receiving messages
        private UdpClient client;

        // The IP address and port to send and receive messages from
        private IPEndPoint endPoint;

        // The timer for resending unacknowledged messages
        private System.Timers.Timer resendTimer;

        // The dictionary to store the unacknowledged messages in
        private Dictionary<int, Message> unacknowledgedMessages;

        // The lock object to synchronize access to the unacknowledged messages dictionary
        private readonly object unacknowledgedMessagesLock = new object();

        bool SIM_FAIL = false;

        Random RAND = new Random();


        // Event for the PART RECEIVED event
        public delegate void PartReceivedEventHandler(Message msg, IPEndPoint endPoint);
        public event PartReceivedEventHandler OnPartReceived;


        // Event for the MessageReceived event
        public delegate void MessageReceivedEventHandler(Message msg, IPEndPoint endPoint);
        public event MessageReceivedEventHandler OnMessageReceived;




        private bool enableLogging = false;

        private int seq = 0;
        private int nextReadSeq = 1;


        /// <summary>
        /// Creates an slidingWindow instance
        /// </summary>
        /// <param name="windowSize">Size of the slidingWindow dictating how many frames can be sent and expected at once before queueing</param>
        /// <param name="client">An UDP Client instance used for network communication</param>
        /// <param name="endPoint">Target to communicate with</param>
        /// <param name="enableLogging">Enables extensice logging to console</param>
        public SlidingWindow(int windowSize, UdpClient client, IPEndPoint endPoint, bool enableLogging = true)
        {
            // Set the window size and UDP client, and initialize the other variables
            this.windowSize = 32;
            this.client = client;
            this.endPoint = endPoint;
            windowPos = 0;
            messageQueue = new Queue<Message>();
            receivedMessages = new List<Message>();
            unacknowledgedMessages = new Dictionary<int, Message>();

            this.enableLogging = enableLogging;

            // Create a timer to resend unacknowledged messages
            resendTimer = new System.Timers.Timer();

            // Set the interval to the resend interval and set the timer to auto-reset
            resendTimer.Interval = RESEND_INTERVAL;
            resendTimer.AutoReset = true;

            // Set the elapsed event handler
            resendTimer.Elapsed += ResendUnacknowledgedMessages;

            // Start the timer
            resendTimer.Start();

        }

        /// <summary>
        /// Sends a message using the SlidingWindow
        /// </summary>
        /// <param name="msg">Message contents</param>
        public void Send(Message msg)
        {
            // Check if the window is full
            if (messageQueue.Count == windowSize)
            {
                // The window is full, cannot send the message
                if (enableLogging)
                    SliderLogger.Log("Queued message.", client, endPoint.Port.ToString());

                seq++;
                msg.SequenceNumber = seq;
                messageQueue.Enqueue(msg);

                return;
            }
            else
            {
                bool isResend;

                // If this message has already been sent
                lock (unacknowledgedMessagesLock)
                {
                    isResend = unacknowledgedMessages.ContainsKey(msg.SequenceNumber) || msg.SequenceNumber != 0;
                }

                // If this is a new message
                if (!isResend)
                {
                    seq++;
                    msg.SequenceNumber = seq;
                }


                msg.Checksum = MessageSerializer.CalculateChecksum(msg);

                // Simulate failure
                if (SIM_FAIL)
                {
                    if (RAND.Next(10) < 7)
                        msg.Checksum++;
                }

                byte[] messageBytes = MessageSerializer.Serialize(msg);


                // Send the message over UDP
                client.Send(messageBytes, messageBytes.Length, endPoint);


                // Add the message to the unacknowledged messages dictionary
                lock (unacknowledgedMessagesLock)
                {
                    unacknowledgedMessages[msg.SequenceNumber] = msg;
                }


                // Start timer to resend timed out messages (if not running)
                if (!resendTimer.Enabled)
                {
                    ResetResendTimer();
                }


                // Return a success message
                if (enableLogging)
                    SliderLogger.Log("S: " + msg.ToString().Pastel(ConsoleColor.Gray), client, endPoint.Port.ToString(), windowPos);

                return;
            }
        }

        /// <summary>
        /// Method to listen for incoming responses
        /// </summary>
        /// <param name="ia">IAsyncResult generated by UDP Client</param>
        public void Receive(IAsyncResult ia = null)
        {

            // Receive a message over UDP
            byte[] messageBytes = client.Receive(ref endPoint);

            /*
            if(endPoint.Port == 6900)
            Console.WriteLine("ReceivedStuff from " + endPoint.Port);*/


            // Deserialize the message bytes to a Message object
            Message msg = MessageSerializer.Deserialize(messageBytes);
            int checksumReceived = MessageSerializer.CalculateChecksum(msg);


            // Check the checksum of the message
            if (checksumReceived == msg.Checksum)
            {
                // The message is valid, remove it from the unacknowledged messages dictionary
                lock (unacknowledgedMessagesLock)
                {
                    unacknowledgedMessages.Remove(msg.SequenceNumber);
                }

                // Check if the message is an ACK message
                if (msg.IsAck)
                {
                    // The message is an ACK message, send the next message in the queue
                    if (enableLogging)
                        SliderLogger.Log($"R: {msg.ToString().Pastel(ConsoleColor.Blue)}", client, endPoint.Port.ToString(), windowPos);

                    windowPos++;

                    SendNextMessage();
                }
                else
                {
                    // The message is not an ACK message, add it to the received messages list

                    
                    // Notify console
                    if (enableLogging)
                        SliderLogger.Log("R: " + msg.ToString().Pastel(ConsoleColor.Gray), client, endPoint.Port.ToString(), windowPos);

                    
                    // Increment the window position
                    lock (receivedMessagesLock)
                    {

                        if (!receivedMessages.Contains(msg))
                        {
                            Console.WriteLine("ADD MESSAG! (" + receivedMessages.Count);
                            windowPos++;
                            receivedMessages.Add(msg);
                        }
                    }




                    // Check for raising events
                    checkMessageQueueForSend();

                    // Send an ACK message for the received message
                    SendAck(msg.SequenceNumber);

                    // Check if there are any more messages to send
                    SendNextMessage();


                    // Raise the MessageReceived event
                    OnPartReceived?.Invoke(msg, endPoint);

                    return;/* "Received message with data: " + msg.Data;*/
                }
            }
            else
            {
                // The message is invalid, send an ACK message with the expected sequence number
                if (enableLogging)
                    SliderLogger.LogError($"Bad Checksum {checksumReceived}, expected {msg.Checksum}", client, endPoint.Port.ToString(), windowPos);
            }

            // Return an empty string
            return;
        }

        /// <summary>
        /// Method to send the next message in the queue
        /// </summary>
        private void SendNextMessage()
        {

            // Check if the queue is not empty and the window is not full
            if (messageQueue.Count > 0 && messageQueue.Count < windowSize)
            {

                // Get the next message in the queue
                Message msg = messageQueue.Dequeue();

                Console.WriteLine($"Sending next message SEQ:{msg.SequenceNumber} in queue, queue size is " + messageQueue.Count);

                // Send the message
                Send(msg);
            }
        }

        /// <summary>
        /// Method to send an ACK message
        /// </summary>
        /// <param name="sequenceNumber"></param>
        private void SendAck(int sequenceNumber)
        {
            // Create a new ACK message with the given sequence number
            Message ackMessage = new Message(sequenceNumber, true);

            // Generate checksum
            ackMessage.Checksum = MessageSerializer.CalculateChecksum(ackMessage);

            // Simulate failure
            if (SIM_FAIL)
            {
                if (RAND.Next(10) > 7)
                {
                    ackMessage.Checksum++;
                }
            }

            // Serialize the message object to a byte array
            byte[] messageBytes = MessageSerializer.Serialize(ackMessage);


            if (enableLogging)
                SliderLogger.Log($"S: {ackMessage.ToString().Pastel(ConsoleColor.Blue)}", client, endPoint.Port.ToString(), windowPos);

            // Send the message over UDP

            // Simulate failure
            if (SIM_FAIL)
            {
                if (RAND.Next(10) > 7)
                {
                    return;
                }
            }

            client.Send(messageBytes, messageBytes.Length, endPoint);
        }


        /// <summary>
        /// Method to resend unacknowledged messages
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResendUnacknowledgedMessages(object sender, ElapsedEventArgs e)
        {
            lock (unacknowledgedMessagesLock)
            {

                int i = unacknowledgedMessages.Count;

                if (enableLogging && unacknowledgedMessages.Count > 0)
                    SliderLogger.LogError($"ACK TIMEOUT, {i} messages will be resent.", client, endPoint.Port.ToString(), windowPos);

                // Resend all unacknowledged messages
                foreach (Message msg in unacknowledgedMessages.Values)
                {
                    Send(msg);
                }
            }

            // Restart the timer
            ResetResendTimer();
        }


        /// <summary>
        /// Method to reset the resend timer
        /// </summary>
        private void ResetResendTimer()
        {
            // Set the interval of the resend timer to the resend interval
            resendTimer.Interval = RESEND_INTERVAL;

            // If the resend timer is not already running, start it
            if (!resendTimer.Enabled)
            {
                resendTimer.Start();
            }
        }


        /// <summary>
        /// Raises an OnMessageReceived event if any unread messages exist.
        /// </summary>
        private void checkMessageQueueForSend()
        {
            if (receivedMessages.Count == 0)
                return;


            lock (receivedMessagesLock)
            {
                for (int i = 0; i < receivedMessages.Count; i++)
                {
                    // Console.WriteLine("Comp: " + receivedMessages[i].SequenceNumber + " to " + nextReadSeq + " total: " + receivedMessages.Count() + " ran " + i);
                    
                    if (receivedMessages[i].SequenceNumber == (nextReadSeq))
                    {
                        OnMessageReceived.Invoke(receivedMessages[i], endPoint);
                        nextReadSeq++;

                        // Reset counter
                        checkMessageQueueForSend();
                        return;
                    }

                }
            }

            

        }


    }
}
