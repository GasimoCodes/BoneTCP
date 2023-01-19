﻿using BoneTCP.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneTCP
{
    public class Fragment
    {

        public flagType flag = flagType.Message;

        /// <summary>
        /// Position of sliding window or slidingw
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
                    return $"Pos:{descriptor}, DATA: ({Data})";
                case flagType.Ack:
                    return "Pos:" + descriptor;
                case flagType.Set:
                    return "Set: " + descriptor;
            }

            return "";
        }

    }


}