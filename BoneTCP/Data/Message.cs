using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneTCP.Data
{
    /// <summary>
    /// Represents a whole message to be sent using SlidingWindow
    /// </summary>
    public class Message
    {
        public string Data = "";
        public uint SeqID = 0;

        public Message(string data)
        {
            this.Data = data;
        }


    }
}
