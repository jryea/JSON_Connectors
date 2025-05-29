using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ModelDebugger
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // If arguments provided, run directly
            if (args.Length > 0)
            {
                ModelDebugger.RunModelDebugger(args);
                return;
            }
            // Otherwise run the model debugger with file dialog
            ModelDebugger.RunModelDebugger(args);
        }
    }
}