using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneTCP.Data
{
    /// <summary>
    /// Used to represent fragment type
    /// </summary>
    public  enum flagType : byte
    {
        Message,
        Ack,
        Set
    }
}
