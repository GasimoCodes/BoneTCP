using BoneTCP.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneTCP.Data
{
    /// <summary>
    /// Represents part of a message. Includes flags and descriptor
    /// </summary>
    public class Fragment
    {
        /// <summary>
        /// Fragment flag
        /// </summary>
        public flagType flag = flagType.Message;

        /// <summary>
        /// Universal descriptor, may be position of sliding window
        /// </summary>
        public uint descriptor = 0;

        /// <summary>
        /// Content of fragment
        /// </summary>
        public byte[] Data;


        public Fragment() { }

        public Fragment(byte[] data)
        {
            Data = data;
        }

        public Fragment(uint windowPos, flagType flag)
        {
            this.descriptor = windowPos;
            this.flag = flag;
        }


        public override string ToString()
        {
            switch(flag)
            {
                case flagType.Message:
                    return $"POS:{descriptor}, DATA: ({Data.Length})";
                case flagType.Ack:
                    return "ACK\tPOS:" + descriptor;
                case flagType.Set:
                    return "NEG_POS: " + descriptor;
                case flagType.AckSet:
                    return "ACK\tNEG_POS " + descriptor;
                case flagType.CommitFlush:
                    return "COM_FLUSH";
                case flagType.AckCF:
                    return "ACK\tCOM_FLUSH";
            }

            return "";
        }

    }


}
