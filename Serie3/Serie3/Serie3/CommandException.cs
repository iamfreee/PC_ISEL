﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serie3 {

    class CommandException : Exception {

        public CommandException(string msg)
            : base(msg) {}

    }

}