using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using BoneTCP.Data;
using Force.Crc32;

namespace BoneTCP
{
    internal static class FragmentWorker
    {

        /// <summary>
        /// Serializes fragment to a byteArray
        /// </summary>
        /// <param name="fragment"></param>
        /// <returns></returns>
        public static byte[] SerializeFragment(Fragment fragment)
        {

            using (var memoryStream = new MemoryStream())
            {
                Byte[] bytes;

                using (var binaryWriter = new BinaryWriter(memoryStream))
                {
                    binaryWriter.Write(((byte)fragment.flag));
                    binaryWriter.Write(fragment.descriptor);

                    if(fragment.Data!= null)
                    binaryWriter.Write(fragment.Data);

                    byte[] toCpy = memoryStream.ToArray();

                    bytes = new Byte[toCpy.Length + 4];

                    Array.Copy(toCpy, 0, bytes, 0, toCpy.Length);
                }

                Crc32Algorithm.ComputeAndWriteToEnd(bytes);


                return bytes;
            }
        }



        /// <summary>
        /// Serializes fragment to a byteArray
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns>Fragment object, null if invalid</returns>
        public static Fragment ParseFragment(byte[] bytes)
        {
            // Check validity
            if (!Crc32Algorithm.IsValidWithCrcAtEnd(bytes))
                return null;


            // Get flag
            flagType flag = (flagType)bytes[0];
            Fragment f = new Fragment();
            f.flag = flag;

            if (f.flag == flagType.Message)
            {

                // 1 byte flag, Descriptor UInt, 4 bytes CRC 
                int dataLength = bytes.Length - (1 + sizeof(uint) + 4);
                Byte[] content = new Byte[dataLength];

                // Save data
                Array.Copy(bytes, 1 + sizeof(uint), content, 0, dataLength);

                f.Data = content;

            }


            // Save descriptor
            Byte[] descriptor = new Byte[sizeof(uint)];
            Array.Copy(bytes, 1, descriptor, 0, sizeof(uint));
            f.descriptor = BitConverter.ToUInt32(descriptor, 0);


            return f;
        }




        /// <summary>
        /// Splices the message into Fragments
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="fragmentBytesize"></param>
        /// <returns></returns>
        static public Fragment[] SerializeMessage(Message msg, int fragmentBytesize)
        {

            byte[] sequenceNumberBytes = BitConverter.GetBytes(msg.SeqID);
            byte[] dataBytes = Encoding.UTF8.GetBytes(msg.Data);


            // Create a new byte array to store the serialized message
            byte[] messageBytes = new byte[sequenceNumberBytes.Length + dataBytes.Length];

            Array.Copy(sequenceNumberBytes, 0, messageBytes, 0, sequenceNumberBytes.Length);
            Array.Copy(dataBytes, 0, messageBytes, sequenceNumberBytes.Length, dataBytes.Length);



            int fragmentAmount = (int)Math.Ceiling((float)(messageBytes.Length / (float)fragmentBytesize));
            Fragment[] fragments = new Fragment[fragmentAmount];

            uint frgIndex = 0;

            for (int i = 0; i < messageBytes.Length; i += fragmentBytesize)
            {
                byte[] chunk;

                if (frgIndex != fragmentAmount-1)
                {
                    chunk = new byte[fragmentBytesize];
                    Array.Copy(messageBytes, i, chunk, 0, fragmentBytesize);
                }
                else
                {
                    chunk = new byte[messageBytes.Length - i];
                    Array.Copy(messageBytes, i, chunk, 0, messageBytes.Length - i);
                }


                Fragment fragment = new Fragment(chunk);
                fragment.descriptor = frgIndex;

                fragments[frgIndex] = fragment;
                frgIndex++;

            }


            // Return the serialized message bytes
            return fragments;
        }

        /// <summary>
        /// Parses fragments of variable size into a message object
        /// </summary>
        /// <param name="fragments"></param>
        /// <returns></returns>
        public static Message ParseMessage(Fragment[] fragments)
        {
            if(fragments == null || fragments.Length == 0)
            {
                throw (new ArgumentException("Cannot parse message from empty fragment array"));
            }

            List<Byte> bytes = new List<Byte>();

            fragments.OrderBy(x => x.descriptor);

            foreach (Fragment fragment in fragments)
            {
                if (fragment == null)
                    throw (new NullReferenceException("Attempted to parse a message object from a null fragment."));


                bytes.AddRange(fragment.Data);
            }

            Message m = new Message(Encoding.UTF8.GetString(bytes.GetRange(sizeof(uint), bytes.Count - sizeof(uint)).ToArray()));
            m.SeqID = BitConverter.ToUInt32(bytes.GetRange(0, sizeof(uint)).ToArray());
            

            return m;
        }



    }


    public class FragmentWorkerException : Exception
    {

    }

}
