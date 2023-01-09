using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneTCP
{
    public class Message
    {
        // The data of the message
        public string Data = "";

        // The sequence number of the message
        public int SequenceNumber { get; set; }

        // The checksum of the message
        public int Checksum { get; set; }

        // A flag to indicate if the message is an ACK message
        public bool IsAck { get; set; }

        public Message(string data)
        {
            Data = data;
        }

        public Message(int sequenceNumber, bool isAck)
        {
            SequenceNumber = sequenceNumber;
            IsAck = isAck;
        }


        public override string ToString()
        {
            if(IsAck)
            {
                return "ACK SEQ:" + SequenceNumber + " CHK:" + Checksum;
            }

            return $"SEQ:{SequenceNumber}, CHK:{Checksum}, DATA: ({Data})";

        }

    }


}
