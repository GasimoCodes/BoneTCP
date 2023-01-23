using BoneTCP.Data;
using BoneTCP.Logging;
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


        // The current position of the window
        private int windowIndex;
        private int windowSize;
        Fragment[] windowFragments;
        windowStatus status = windowStatus.INIT;

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

        bool receiveThreadActive = false;

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
        public SlidingWindow(UdpClient client, IPEndPoint endPoint, int maxMessageSizeBytes = 2048, bool enableLogging = true, int windowSize = 8)
        {
            // Set the window size and UDP client, and initialize the other variables
            windowFragments = new Fragment[0];
            windowIndex = 0;
            this.windowSize = windowSize;
            this.client = client;
            this.endPoint = endPoint;
            this.enableLogging = enableLogging;
            maxBytesPerMessage = maxMessageSizeBytes;


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
            if (status != windowStatus.INIT)
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
            if (messageQueue.Count > 0 && status == windowStatus.INIT && windowFragments.Length == 0)
            {

                Message curMes;
                curMes = messageQueue.Dequeue();

                windowFragments = FragmentWorker.SerializeMessage(curMes, maxBytesPerMessage);

                if (enableLogging)
                    SliderLogger.Log($"Preparing new message... {curMes.Data.Substring(0, Math.Clamp(curMes.Data.Length, 0, 20))}");

                NegotiateWindowSize(windowFragments.Length).Wait();


                // Begin timer
                if (resendTimer.Enabled)
                {
                    ResetResendTimer();
                }
            }
        }




        /// <summary>
        /// Call this to begin negotiating of windowSize on the receiver
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        private async Task NegotiateWindowSize(int size)
        {

            status = windowStatus.WAIT_NEG;
            Fragment negFrag = new Fragment((uint)windowFragments.Length, flagType.Set);

            while (status == windowStatus.WAIT_NEG)
            {
                sendFragment(negFrag);
                Thread.Sleep(RESEND_INTERVAL);
                beginReceiveThread();
            }

            if (enableLogging)
                SliderLogger.Log($"Finished negotiating with REMOTE on {windowFragments.Length}".Pastel(ConsoleColor.Yellow), client, endPoint.Port.ToString(), status.ToString());

            return;
        }

        private void beginReceiveThread()
        {
            if (!receiveThreadActive)
            {
                receiveThreadActive = true;
                new Thread(() =>
                {
                    while (receiveThreadActive)
                    {
                        Thread.Sleep(RESEND_INTERVAL);
                        Receive();
                    }
                }).Start();
            }
        }


        /// <summary>
        /// Call this to begin negotiating of windowSize on the receiver
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        private async Task CommitFlushRemoteWindow()
        {

            status = windowStatus.WAIT_FLUSH;
            Fragment negFrag = new Fragment((uint)windowFragments.Length, flagType.CommitFlush);

            while (status == windowStatus.WAIT_FLUSH)
            {
                sendFragment(negFrag);
                Thread.Sleep(RESEND_INTERVAL);
                //beginReceiveThread();
            }

            if (enableLogging)
                SliderLogger.Log($"Finished commit and flush on REMOTE".Pastel(ConsoleColor.Yellow), client, endPoint.Port.ToString(), status.ToString());

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




        #endregion







        /// <summary>
        /// Method to resend unacknowledged messages
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RetransmitWindowFrame(object sender, ElapsedEventArgs e)
        {
            if (status != windowStatus.TRANSMIT || windowFragments.Length == 0 || windowIndex >= windowFragments.Length)
            {
                ResetResendTimer();
                return;
            }

            if (enableLogging)
                Console.WriteLine("\n\n");

            // Resend all frame messages
            for (int i = 0; i < windowSize; i++)
            {
                if (i + windowIndex >= windowFragments.Length)
                {
                    break;
                }

                sendFragment(windowFragments[windowIndex + i]);
            }

            // Restart the timer
            ResetResendTimer();
        }


        /// <summary>
        /// Resets state of slidingWindow
        /// </summary>
        private void resetWindow()
        {
            lock (windowOperationLock)
            {
                windowIndex = 0;
                windowFragments = new Fragment[0];
                status = windowStatus.INIT;
                ResetResendTimer();
            }
        }











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

            packetHandler(frag);

        }

        /// <summary>
        /// Handles packet logic tree
        /// </summary>
        /// <param name="frag"></param>
        private void packetHandler(Fragment frag)
        {

            if (enableLogging)
                SliderLogger.Log("REC: ".Pastel(ConsoleColor.Green) + frag.ToString().Pastel(ConsoleColor.Gray), client, endPoint.Port.ToString(), status.ToString());


            switch (frag.flag)
            {
                // Message received
                case flagType.Message:
                    {
                        if (status != windowStatus.RECEIVE)
                        {
                            dropForReason(frag, "Unexpected");
                            return;
                        }

                        registerMessageReceived(frag);
                        SendAck((uint)windowIndex, flagType.Ack);
                        break;
                    }

                // Negotiation SET
                case flagType.Set:
                    {
                        if (status != windowStatus.INIT)
                        {
                            dropForReason(frag, "Unexpected");
                            SendAck((uint)windowFragments.Length, flagType.AckSet);
                            return;
                        }

                        register_SetWindowLegth(frag);
                        SendAck((uint)windowFragments.Length, flagType.AckSet);
                        break;
                    }

                // Commit existing fragments and clean window for next negotiation
                case flagType.CommitFlush:
                    {
                        if (status != windowStatus.RECEIVE)
                        {
                            dropForReason(frag, "Unexpected");
                            SendAck((uint)windowIndex, flagType.AckCF);
                            return;
                        }

                        register_CommitFlush(frag);
                        SendAck((uint)windowIndex, flagType.AckCF);
                        break;
                    }

                // Acknowledgment for message received
                case flagType.Ack:
                    {
                        if (status != windowStatus.TRANSMIT)
                        {
                            dropForReason(frag, "Unexpected");
                            return;
                        }

                        register_ACK_Receive(frag);
                        break;
                    }
                // Acknowledgment for SET on remote
                case flagType.AckSet:
                    {
                        if (status != windowStatus.WAIT_NEG)
                        {
                            dropForReason(frag, "Unexpected");
                            return;
                        }

                        register_ACK_Set(frag);
                        break;
                    }
                // Acknowledgment for FLUSH on remote
                case flagType.AckCF:
                    {
                        if (status != windowStatus.WAIT_FLUSH)
                        {
                            dropForReason(frag, "Unexpected");
                            return;
                        }

                        register_ACK_Commit(frag);
                        break;
                    }
            }
        }



        /// <summary>
        /// Called when you received a message, if its on index it raises the windowPosition
        /// </summary>
        /// <param name="frag"></param>
        private void registerMessageReceived(Fragment frag)
        {

            // Check if we arent already full
            if (windowIndex > windowFragments.Length - 1)
            {
                return;
            }

            // Console.WriteLine("RECEIVER: POS " + windowIndex + "/" + (windowFragments.Length - 1) + " FRAG: " + frag.descriptor);

            // Insert fragment
            windowFragments[frag.descriptor] = frag;

            // Raise position
            if (frag.descriptor == windowIndex)
            {
                windowIndex++;
            }

        }


        /// <summary>
        /// Sets local windowLength
        /// </summary>
        /// <param name="frag"></param>
        private void register_SetWindowLegth(Fragment frag)
        {
            windowFragments = new Fragment[frag.descriptor];
            windowIndex = 0;

            if (enableLogging)
                SliderLogger.Log($"Receiver size now at {windowFragments.Length}".Pastel(ConsoleColor.Yellow), client, endPoint.Port.ToString(), status.ToString());

            status = windowStatus.RECEIVE;

        }

        /// <summary>
        /// Clears local window and commits it
        /// </summary>
        /// <param name="frag"></param>
        private void register_CommitFlush(Fragment frag)
        {

            if (enableLogging)
                SliderLogger.Log($"Receiver Commiting... {windowFragments.Length}".Pastel(ConsoleColor.Yellow), client, endPoint.Port.ToString(), status.ToString());

            checkReceivedListForEvent();

            if (enableLogging)
                SliderLogger.Log($"Receiver Reset... {windowFragments.Length}".Pastel(ConsoleColor.Yellow), client, endPoint.Port.ToString(), status.ToString());

            resetWindow();

        }



        /// <summary>
        /// Remote successfuly configured its windowSize
        /// </summary>
        /// <param name="frag"></param>
        private void register_ACK_Set(Fragment frag)
        {
            status = windowStatus.TRANSMIT;
        }

        /// <summary>
        /// Remote commited all messages and cleared its contents
        /// </summary>
        /// <param name="frag"></param>
        private void register_ACK_Commit(Fragment frag)
        {
            resetWindow();
        }


        /// <summary>
        /// Called when you receive an ACK for a message you have sent
        /// </summary>
        /// <param name="frag"></param>
        private void register_ACK_Receive(Fragment frag)
        {
            if (windowIndex >= windowFragments.Length)
            {
                return;
            }

            if (frag.descriptor >= windowIndex + 1)
            {
                windowIndex++;
            }

            // Console.WriteLine("SENDER: POS " + windowIndex + "/" + (windowFragments.Length - 1) + " FRAG: " + frag.descriptor);

            if (windowIndex >= windowFragments.Length)
            {
                // All messages received on the other side
                CommitFlushRemoteWindow();
                return;
            }

        }




        /// <summary>
        /// Logs a fancy reason for which a packet was dropped
        /// </summary>
        /// <param name="frag"></param>
        /// <param name="reason"></param>
        private void dropForReason(Fragment frag, string reason = "")
        {
            if (enableLogging)
                SliderLogger.LogError($"DRP: {reason} ".Pastel(ConsoleColor.Red) + $"{frag.ToString()}".Pastel(ConsoleColor.Gray), client, endPoint.Port.ToString(), status.ToString());
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
