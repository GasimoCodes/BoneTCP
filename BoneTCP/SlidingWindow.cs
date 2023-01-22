using BoneTCP;
using BoneTCP.Data;
using Pastel;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
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
        private const int RESEND_INTERVAL = 2000;
        private const int TIMEOUT_INTERVAL = 5000; // 5 second


        // The current position of the window
        private int windowPos;
        Fragment[] windowFragments;

        windowStatus status = windowStatus.None;

        /// <summary>
        /// The queue to store the messages in before splicing into fragments and sending
        /// </summary>
        private Queue<Message> messageQueue = new Queue<Message>();

        /// <summary>
        /// List to store received messages into
        /// </summary>
        private List<Message> receivedMessages;
        private readonly object receivedMessagesLock = new object();


        // The UDP client to use for sending and receiving messages
        private UdpClient client;


        // The IP address and port to send and receive messages from
        private IPEndPoint endPoint;


        // The timer for resending unacknowledged messages
        private System.Timers.Timer resendTimer;


        bool SIM_FAIL = false;

        Random RAND = new Random();


        // Event for the PART RECEIVED event
        public delegate void PartReceivedEventHandler(Fragment msg, IPEndPoint endPoint);
        public event PartReceivedEventHandler OnPartReceived;


        // Event for the MessageReceived event
        public delegate void MessageReceivedEventHandler(Message msg, IPEndPoint endPoint);
        public event MessageReceivedEventHandler OnMessageReceived;

        private bool enableLogging = false;

        private int maxBytesPerMessage;

        /// <summary>
        /// Creates an slidingWindow instance
        /// </summary>
        /// <param name="client">An UDP Client instance used for network communication</param>
        /// <param name="endPoint">Target to communicate with</param>
        /// <param name="maxMessageSizeBytes">Max size of 1 transmitted packet. Window size is calculated for this limit dynamically.</param>
        /// <param name="enableLogging">Enables extensice logging to console</param>
        public SlidingWindow(UdpClient client, IPEndPoint endPoint, int maxMessageSizeBytes = 2048, bool enableLogging = true)
        {
            // Set the window size and UDP client, and initialize the other variables
            windowFragments = new Fragment[0];
            windowPos = 0;

            this.client = client;
            this.endPoint = endPoint;
            this.enableLogging = enableLogging;
            this.maxBytesPerMessage = maxMessageSizeBytes;

            // Create a timer to resend window
            resendTimer = new System.Timers.Timer();
            resendTimer.Interval = RESEND_INTERVAL;
            resendTimer.AutoReset = true;
            resendTimer.Elapsed += ResendFragments;
            // Start the timer
            resendTimer.Start();
        }




        /// <summary>
        /// Schedules an message to be sent once the window is available
        /// </summary>
        /// <param name="msg">Message contents</param>
        public void AddMessage(Message msg)
        {
            // Window is not ready to take messages
            if (windowPos != 0)
            {
                messageQueue.Enqueue(msg);
            }
            // This is the first message
            else
            {
                messageQueue.Enqueue(msg);
                SendNextMessage();
            }
        }


        /// <summary>
        /// Method to send the next message in the queue
        /// </summary>
        private void SendNextMessage()
        {

            // Check if the queue is not empty and the window is not full
            if (messageQueue.Count > 0 && windowPos == 0)
            {
                Message curMes = messageQueue.Dequeue();
                windowFragments = FragmentWorker.SerializeMessage(curMes, maxBytesPerMessage);
                
                NegotiateWindowSize(windowFragments.Length).Wait();

                // Begin transmit!

            }
        }

        /// <summary>
        /// Call this to begin negotiating of windowSize on the receiver
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        private async Task NegotiateWindowSize(int size)
        {
            status = windowStatus.Set;

            Fragment negFrag = new Fragment();
            negFrag.flag = flagType.Set;
            negFrag.descriptor = (uint)windowFragments.Length;
            sendFragment(negFrag);

            while (status == windowStatus.Set)
            {
                sendFragment(negFrag);
                Thread.Sleep(RESEND_INTERVAL);
                Receive();
            }

            if(enableLogging)
            SliderLogger.Log($"Negotiated window to {windowFragments.Length}", client, endPoint.Port.ToString());

            return;
        }

        #region sendMethods
        

        /// <summary>
        /// Sends fragment over UDP
        /// </summary>
        /// <param name="frg"></param>
        private void sendFragment(Fragment frg)
        {

            byte[] fragBytes = FragmentWorker.SerializeFragment(frg);

            // Simulate failure
            if (SIM_FAIL)
            {
                if (RAND.Next(10) < 7)
                    fragBytes[fragBytes.Length - 1] = 0;
            }

            // Send the message over UDP
            client.Send(fragBytes, fragBytes.Length, endPoint);


            // Start timer to resend timed out messages (if not running)
            if (!resendTimer.Enabled)
            {
                ResetResendTimer();
            }

            // Return a success message
            if (enableLogging)
                SliderLogger.Log("Send: " + frg.ToString().Pastel(ConsoleColor.Gray), client, endPoint.Port.ToString(), windowPos);

            return;

        }



        /// <summary>
        /// Method to send an ACK message
        /// </summary>
        /// <param name="descriptor"></param>
        private void SendAck(uint descriptor)
        {
            // Create a new ACK message with the given sequence number
            Fragment ackMessage = new Fragment(descriptor, Data.flagType.Ack);

            // Simulate failure
            if (SIM_FAIL)
            {
                if (RAND.Next(10) > 7)
                {
                    return;
                }
            }

            sendFragment(ackMessage);
        }

        #endregion


        /// <summary>
        /// Method to listen for incoming responses
        /// </summary>
        /// <param name="ia">IAsyncResult generated by UDP Client</param>
        public void Receive(IAsyncResult ia = null)
        {

            // Receive a message over UDP
            byte[] fragBytes = client.Receive(ref endPoint);

            // Deserialize the message bytes to a Message object
            Fragment frag = FragmentWorker.ParseFragment(fragBytes);

            if (frag == null)
            {
                if (enableLogging)
                    SliderLogger.LogError($"DROP: BAD CHK", client, endPoint.Port.ToString(), windowPos);
                return;
            }

            /*
            if (endPoint.Port == 6900)
                Console.WriteLine("ReceivedStuff from " + endPoint.Port);
            */

            switch (frag.flag)
            {
                case flagType.Ack:
                    {

                        if (enableLogging)
                            SliderLogger.Log($"RECV: {frag.ToString().Pastel(ConsoleColor.Blue)}", client, endPoint.Port.ToString(), windowPos);


                        // This might be a negotiation
                        if(status == windowStatus.Set)
                        {
                            if(frag.descriptor == windowFragments.Length)
                            {
                                // Negotiation done
                                status = windowStatus.Transmit;
                                return;
                            }
                        }


                        windowPos++;
                        sendNextFragment();
                        return;
                    }
                case flagType.Message:
                    {
                        // Notify console
                        if (enableLogging)
                            SliderLogger.Log("RECV: " + frag.ToString().Pastel(ConsoleColor.Gray), client, endPoint.Port.ToString(), windowPos);


                        // IF NOT DUPE, Increment the window position and register message

                        windowPos++;

                        // Check for raising events
                        checkReceivedListForSend();

                        // Send an ACK message for the received message
                        SendAck(frag.descriptor);

                        // Check if there are any more messages to send
                        SendNextMessage();


                        // Raise the MessageReceived event
                        OnPartReceived?.Invoke(frag, endPoint);

                        return;/* "Received message with data: " + msg.Data;*/
                    }
                case flagType.Set:
                    {
                        // If window already set
                        if (windowFragments.Length != 0 || status != windowStatus.None)
                        {
                            if (enableLogging)
                                SliderLogger.Log("DROP: " + frag.ToString().Pastel(ConsoleColor.Gray), client, endPoint.Port.ToString(), windowPos);

                            SendAck(frag.descriptor);
                            return;
                        }

                        if (enableLogging)
                            SliderLogger.Log($"RECV: {frag.ToString().Pastel(ConsoleColor.Blue)}", client, endPoint.Port.ToString(), windowPos);

                        status = windowStatus.Transmit;

                        // set
                        windowFragments = new Fragment[frag.descriptor];

                        if (enableLogging)
                            SliderLogger.Log($"Negotiated window to {windowFragments.Length}", client, endPoint.Port.ToString());

                        SendAck(frag.descriptor);

                        return;

                    }
            }

        }




        


        /// <summary>
        /// Method to resend unacknowledged messages
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResendFragments(object sender, ElapsedEventArgs e)
        {
            // Resend entirety of slidingWindow
            /*
            lock (unacknowledgedMessagesLock)
            {

                int i = unacknowledgedMessages.Count;

                if (enableLogging && unacknowledgedMessages.Count > 0)
                    SliderLogger.LogError($"ACK TIMEOUT, {i} messages will be resent.", client, endPoint.Port.ToString(), windowPos);

                // Resend all unacknowledged messages
                foreach (Fragment msg in unacknowledgedMessages.Values)
                {
                    Send(msg);
                }
            }
            */


            // Restart the timer
            ResetResendTimer();
        }

        private void sendNextFragment()
        {

        }

        #region events
        
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
        private void checkReceivedListForSend()
        {
            if (receivedMessages.Count == 0)
                return;


            lock (receivedMessagesLock)
            {
                for (int i = 0; i < receivedMessages.Count; i++)
                {
                    // Console.WriteLine("Comp: " + receivedMessages[i].SequenceNumber + " to " + nextReadSeq + " total: " + receivedMessages.Count() + " ran " + i);

                    /*
                    if (receivedMessages[i].SequenceNumber == (nextReadSeq))
                    {
                        OnMessageReceived.Invoke(receivedMessages[i], endPoint);
                        nextReadSeq++;

                        // Reset counter
                        checkMessageQueueForSend();
                        return;
                    }
                    */
                }
            }
        }

        #endregion


    }
}
