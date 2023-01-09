using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneTCP
{
    internal static class MessageSerializer
    {



        // Method to serialize a Message object to a byte array
        public static byte[] Serialize(Message msg)
        {
            // Convert the data of the message to a byte array
            byte[] dataBytes = Encoding.UTF8.GetBytes(msg.Data);

            // Convert the sequence number of the message to a byte array
            byte[] sequenceNumberBytes = BitConverter.GetBytes(msg.SequenceNumber);

            // Convert the checksum of the message to a byte array
            byte[] checksumBytes = BitConverter.GetBytes(msg.Checksum);

            // Convert the isAck flag of the message to a byte array
            byte[] isAckBytes = BitConverter.GetBytes(msg.IsAck);

            // Create a new byte array to store the serialized message
            byte[] messageBytes = new byte[dataBytes.Length + sequenceNumberBytes.Length + checksumBytes.Length + isAckBytes.Length];

            // Copy the data bytes to the message bytes
            Array.Copy(dataBytes, 0, messageBytes, 0, dataBytes.Length);

            // Copy the sequence number bytes to the message bytes
            Array.Copy(sequenceNumberBytes, 0, messageBytes, dataBytes.Length, sequenceNumberBytes.Length);

            // Copy the checksum bytes to the message bytes
            Array.Copy(checksumBytes, 0, messageBytes, dataBytes.Length + sequenceNumberBytes.Length, checksumBytes.Length);

            // Copy the isAck bytes to the message bytes
            Array.Copy(isAckBytes, 0, messageBytes, dataBytes.Length + sequenceNumberBytes.Length + checksumBytes.Length, isAckBytes.Length);

            // Return the serialized message bytes
            return messageBytes;
        }



        // Method to deserialize a byte array to a Message object
        public static Message Deserialize(byte[] messageBytes)
        {
            // Get the data bytes from the message bytes
            byte[] dataBytes = new byte[messageBytes.Length - sizeof(int) - sizeof(int) - sizeof(bool)];
            Array.Copy(messageBytes, 0, dataBytes, 0, dataBytes.Length);

            // Get the sequence number bytes from the message bytes
            byte[] sequenceNumberBytes = new byte[sizeof(int)];
            Array.Copy(messageBytes, dataBytes.Length, sequenceNumberBytes, 0, sequenceNumberBytes.Length);

            // Get the checksum bytes from the message bytes
            byte[] checksumBytes = new byte[sizeof(int)];
            Array.Copy(messageBytes, dataBytes.Length + sequenceNumberBytes.Length, checksumBytes, 0, checksumBytes.Length);

            // Get the isAck bytes from the message bytes
            byte[] isAckBytes = new byte[sizeof(bool)];
            Array.Copy(messageBytes, dataBytes.Length + sequenceNumberBytes.Length + checksumBytes.Length, isAckBytes, 0, isAckBytes.Length);

            // Convert the data bytes to a string
            string data = Encoding.UTF8.GetString(dataBytes);

            // Convert the sequence number bytes to an int
            int sequenceNumber = BitConverter.ToInt32(sequenceNumberBytes, 0);

            // Convert the checksum bytes to an int
            int checksum = BitConverter.ToInt32(checksumBytes, 0);

            // Convert the isAck bytes to a bool
            bool isAck = BitConverter.ToBoolean(isAckBytes, 0);

            // Create a new Message object with the deserialized data
            Message msg = new Message(sequenceNumber, isAck)
            {
                Data = data,
                Checksum = checksum
            };

            // Return the deserialized message
            return msg;
        }


        // Method to calculate the checksum of a message
        public static int CalculateChecksum(Message msg)
        {
            // Convert the data of the message to a byte array
            byte[] dataBytes = Encoding.UTF8.GetBytes(msg.Data);

            // Initialize a checksum value
            int checksum = 0;

            // Calculate the checksum by summing the bytes of the data
            foreach (byte b in dataBytes)
            {
                checksum += b;
            }


            // Return the checksum
            return checksum;
        }





    }
}
