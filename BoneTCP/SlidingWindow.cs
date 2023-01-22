using BoneTCP;
using BoneTCP.Data;
using Pastel;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
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
        private const int RESEND_INTERVAL = 100;
        private const int TIMEOUT_INTERVAL = 5000; // 5 second


        // The current position of the window
        private int windowPos;
        private int windowSize;
        Fragment[] windowFragments;
        windowStatus status = windowStatus.None;

        private static readonly object windowOperationLock = new object();

        /// <summary>
        /// The queue to store the messages in before splicing into fragments and sending
        /// </summary>
        private Queue<Message> messageQueue = new Queue<Message>();
        private static readonly object messageQueueLock = new object();


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
        public SlidingWindow(UdpClient client, IPEndPoint endPoint, int maxMessageSizeBytes = 2048, bool enableLogging = true, int windowSize = 16)
        {
            // Set the window size and UDP client, and initialize the other variables
            windowFragments = new Fragment[0];
            this.windowPos = 0;
            this.windowSize = windowSize;
            this.client = client;
            this.endPoint = endPoint;
            this.enableLogging = enableLogging;
            this.maxBytesPerMessage = maxMessageSizeBytes;


            // Set Timer

            // Create a timer to resend window
            resendTimer = new System.Timers.Timer();
            resendTimer.Interval = RESEND_INTERVAL;
            resendTimer.AutoReset = true;
            resendTimer.Elapsed += RetransmitWindowFrame;
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
            if (status != windowStatus.None)
            {
                lock (messageQueueLock)
                {
                    messageQueue.Enqueue(msg);
                }
            }
            // This is the first message
            else
            {
                lock (messageQueueLock)
                {
                    messageQueue.Enqueue(msg);
                }

                SendNextMessage();
            }

        }


        /// <summary>
        /// Method to send the next message in the queue
        /// </summary>
        private void SendNextMessage()
        {
            // Check if the queue is not empty and the window is not full
            if (messageQueue.Count > 0 && status == windowStatus.None && windowFragments.Length == 0)
            {

                Message curMes;

                lock (messageQueueLock)
                {
                    curMes = messageQueue.Dequeue();
                }

                windowFragments = FragmentWorker.SerializeMessage(curMes, maxBytesPerMessage);

                if (enableLogging)
                    SliderLogger.Log($"Preparing new message... {curMes.Data.Substring(0, Math.Clamp(curMes.Data.Length, 0, 20))}");

                NegotiateWindowSize(windowFragments.Length).Wait();

                if (resendTimer.Enabled)
                {
                    ResetResendTimer();
                }


                // Begin transmit!
                RetransmitWindowFrame(null, null);

            }
            else
            {
                if (enableLogging)
                    SliderLogger.Log("All messages sent.");
            }
        }


        /// <summary>
        /// Call this to begin negotiating of windowSize on the receiver
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        private async Task NegotiateWindowSize(int size)
        {
            lock (windowOperationLock)
            {
                status = windowStatus.Negotiating;
            }

            Fragment negFrag = new Fragment();
            negFrag.flag = flagType.Set;
            negFrag.descriptor = (uint)windowFragments.Length;
            sendFragment(negFrag);


            while (status != windowStatus.Ready)
            {
                sendFragment(negFrag);
                Thread.Sleep(RESEND_INTERVAL);
                Receive();
            }

            if (enableLogging)
                SliderLogger.Log($"Negotiated window to {windowFragments.Length}".Pastel(ConsoleColor.Yellow), client, endPoint.Port.ToString(), status.ToString());

            return;
        }


        /// <summary>
        /// Call this to begin negotiating of windowSize on the receiver
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        private async Task CommitAndFlushRemote()
        {
            lock (windowOperationLock)
            {
                status = windowStatus.CommitFlush;
            }

            Fragment negFrag = new Fragment();
            negFrag.flag = flagType.CommitFlush;
            negFrag.descriptor = (uint)windowFragments.Length;
            sendFragment(negFrag);


            while (status == windowStatus.CommitFlush)
            {
                sendFragment(negFrag);
                Thread.Sleep(RESEND_INTERVAL);
                Console.WriteLine("Awaiting flush");
                Receive();
            }



            if (enableLogging)
                SliderLogger.Log($"Commited and flushed window to {windowFragments.Length}".Pastel(ConsoleColor.Yellow), client, endPoint.Port.ToString(), status.ToString());

            SendNextMessage();
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


            // Return a success message
            if (enableLogging)
                SliderLogger.Log("SND: ".Pastel(ConsoleColor.Yellow) + frg.ToString().Pastel(ConsoleColor.Gray), client, endPoint.Port.ToString(), status.ToString());

            return;
        }



        /// <summary>
        /// Method to send an ACK message
        /// </summary>
        /// <param name="descriptor"></param>
        private void SendAck(uint descriptor, flagType flag = flagType.Ack)
        {
            // Create a new ACK message with the given sequence number
            Fragment ackMessage = new Fragment(descriptor, flag);

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


        /// <summary>
        /// Confirm this fragment and move window if necessary
        /// </summary>
        private void addFragmentToFinishedSent(uint pos)
        {
            if (status != windowStatus.Transmit || windowFragments.Length == 0)
            {
                return;
            }

            if (pos == (windowPos + 1))
            {
                windowPos = (int)pos;
            }

            if (windowPos == windowFragments.Length && status == windowStatus.Transmit)
            {
                // END
                CommitAndFlushRemote().Wait();
            }
        }

        /// <summary>
        /// Confirm this fragment and move window if necessary
        /// </summary>
        private void addFragmentToFinishedReceive(Fragment frag)
        {

            windowFragments[frag.descriptor] = frag;

            lock (windowOperationLock)
            {
                if ((frag.descriptor == windowPos) && (windowPos != windowFragments.Length))
                {
                    windowPos++;
                }
            }

            // Send an ACK message for the received message
            SendAck((uint)windowPos);

        }

        #endregion


        /// <summary>
        /// Method to read incoming response
        /// </summary>
        /// <param name="ia">IAsyncResult generated by UDP Client</param>
        public void Receive(IAsyncResult ia = null)
        {
            // Receive a message over UDP
            byte[] fragBytes = client.Receive(ref endPoint);

            if (fragBytes.Length != 0)
                ReceiveRaw(fragBytes);

        }




        /// <summary>
        /// Commit bytes directly into the window
        /// </summary>
        /// <param name="fragBytes"></param>
        public void ReceiveRaw(byte[] fragBytes)
        {
            // Deserialize the message bytes to a Message object
            Fragment frag = FragmentWorker.ParseFragment(fragBytes);


            if (frag == null)
            {
                if (enableLogging)
                    SliderLogger.LogError($"DRP: BAD CHK".Pastel(ConsoleColor.Red), client, endPoint.Port.ToString(), status.ToString());
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
                        // If invalid status
                        if (status != windowStatus.Transmit)
                        {
                            if (enableLogging)
                                SliderLogger.LogError($"DRP: NO MESSAGE EXPECTED {frag.ToString()}".Pastel(ConsoleColor.Red), client, endPoint.Port.ToString(), status.ToString());

                            return;
                        }

                        if (enableLogging)
                            SliderLogger.Log($"RCV: {frag.ToString().Pastel(ConsoleColor.Blue)}", client, endPoint.Port.ToString(), status.ToString());

                        addFragmentToFinishedSent(frag.descriptor);
                        return;

                    }
                case flagType.Message:
                    {
                        // If invalid status
                        if (status != windowStatus.Receive)
                        {
                            if (enableLogging)
                                SliderLogger.LogError($"DRP: NO MESSAGE EXPECTED {frag.ToString()}".Pastel(ConsoleColor.Red), client, endPoint.Port.ToString(), status.ToString());

                            return;
                        }

                        // Notify console
                        if (enableLogging)
                            SliderLogger.Log("RCV: " + frag.ToString().Pastel(ConsoleColor.Gray), client, endPoint.Port.ToString(), status.ToString());


                        addFragmentToFinishedReceive(frag);

                        // Raise the FragmentReceived event
                        OnPartReceived?.Invoke(frag, endPoint);

                        return;/* "Received message with data: " + msg.Data;*/
                    }
                case flagType.Set:
                    {
                        // If window already set

                        if (windowFragments.Length != 0 || status != windowStatus.None)
                        {
                            if (enableLogging)
                                SliderLogger.Log("DRP: Invalid SET ".Pastel(ConsoleColor.Red) + frag.ToString().Pastel(ConsoleColor.Gray), client, endPoint.Port.ToString(), status.ToString());

                            SendAck((uint)this.windowFragments.Length);
                            return;
                        }

                        resetWindow();


                        if (enableLogging)
                            SliderLogger.Log($"RCV: {frag.ToString().Pastel(ConsoleColor.Blue)}", client, endPoint.Port.ToString(), status.ToString());

                        status = windowStatus.Ready;

                        // set
                        windowFragments = new Fragment[frag.descriptor];


                        if (enableLogging)
                            SliderLogger.Log($"Negotiated window to {windowFragments.Length}".Pastel(ConsoleColor.Yellow), client, endPoint.Port.ToString(), status.ToString());

                        SendAck(frag.descriptor, flagType.AckSet);

                        status = windowStatus.Receive;
                        return;

                    }
                case flagType.AckSet:
                    {
                        // This IS a negotiation
                        if (status == windowStatus.Negotiating)
                        {
                            if (frag.descriptor == windowFragments.Length)
                            {
                                // Negotiation done
                                lock (windowOperationLock)
                                {
                                    status = windowStatus.Ready;
                                }

                                if (enableLogging)
                                    SliderLogger.Log($"RCV: {frag.ToString().Pastel(ConsoleColor.Blue)}", client, endPoint.Port.ToString(), status.ToString());
                            }
                            else
                            {

                                if (enableLogging)
                                    SliderLogger.LogError($"DRP: INCORRECT NEGOTIATION VALUE {frag.descriptor} EXPECTED {windowFragments.Length}".Pastel(ConsoleColor.Red), client, endPoint.Port.ToString(), status.ToString());

                            }
                        }

                        return;
                    }
                // COMMITS AND RESETS THIS ENDPOINT
                case flagType.CommitFlush:
                    {
                        if (status != windowStatus.Receive)
                        {
                            if (enableLogging)
                                SliderLogger.Log("DRP: Invalid COMMIT FLUSH ".Pastel(ConsoleColor.Red) + frag.ToString().Pastel(ConsoleColor.Gray), client, endPoint.Port.ToString(), status.ToString());

                            SendAck(0, flagType.AckCF);
                            return;

                        }

                        // Check for raising events
                        checkReceivedListForEvent();
                        resetWindow();


                        SendAck(0, flagType.AckCF);


                        if (enableLogging)
                            SliderLogger.Log($"Remote called flush on this instance.  {frag.ToString()}".Pastel(ConsoleColor.Yellow), client, endPoint.Port.ToString(), status.ToString());

                        return;
                    }


                case flagType.AckCF:
                    {

                        resetWindow();
                    
                        if (enableLogging)
                            SliderLogger.Log($"Remote flushed. Sending new message. {frag.ToString()}".Pastel(ConsoleColor.Yellow), client, endPoint.Port.ToString(), status.ToString());

                        return;
                    }


            }
        }



        /// <summary>
        /// Method to resend unacknowledged messages
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RetransmitWindowFrame(object sender, ElapsedEventArgs e)
        {

            if (status == windowStatus.Ready)
            {
                status = windowStatus.Transmit;
            }


            if (status != windowStatus.Transmit || windowFragments.Length == 0)
            {
                ResetResendTimer();
                return;
            }


            // Resend all frame messages
            for (int i = 0; i < windowSize; i++)
            {
                if (i + windowPos >= windowFragments.Length)
                {
                    // END OF ARRAY
                    break;
                }

                sendFragment(windowFragments[windowPos + i]);

            }


            // Restart the timer
            ResetResendTimer();
        }

        private void resetWindow()
        {
            lock (windowOperationLock)
            {
                windowPos = 0;
                windowFragments = new Fragment[0];
                status = windowStatus.None;
                ResetResendTimer();
            }
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
        private void checkReceivedListForEvent()
        {
            Message result;

            lock (windowOperationLock)
            {
                result = FragmentWorker.ParseMessage(windowFragments);
            }

            OnMessageReceived.Invoke(result, endPoint);
        }

        #endregion


    }
}
