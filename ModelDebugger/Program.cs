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
            string jsonFilePath = "";

            // If no args provided, show menu or file dialog
            if (args.Length == 0)
            {
                Console.WriteLine("Choose an option:");
                Console.WriteLine("1. Full Model Debugger");
                Console.WriteLine("2. Quick JSON Inspector");
                int choice = int.Parse(Console.ReadLine() ?? "0");

                if (choice == 1)
                    ModelDebugger.RunModelDebugger(args);
                else if (choice == 2)
                    QuickJsonInspector.RunQuickInspector(args);
            }
            else if (args[0] == "--quick")
                QuickJsonInspector.RunQuickInspector(args.Skip(1).ToArray());
            else
                ModelDebugger.RunModelDebugger(args);
        }
    }
}