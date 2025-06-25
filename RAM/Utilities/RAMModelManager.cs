// RAMModelManager.cs
using System;
using System.IO;
using System.Runtime.InteropServices;
using Core.Models;
using RAMDATAACCESSLib;

namespace RAM.Utilities
{
    public class RAMModelManager : IDisposable
    {
        public RamDataAccess1 RamDataAccess { get; private set; }
        public IDBIO1 Database { get; private set; }
        public IModel Model { get; private set; }

        // Property to get the last error message for enhanced error reporting
        public string LastErrorMessage { get; private set; }

        public RAMModelManager()
        {
            RamDataAccess = new RamDataAccess1();
            Database = RamDataAccess.GetInterfacePointerByEnum(EINTERFACES.IDBIO1_INT) as IDBIO1;
            Model = RamDataAccess.GetInterfacePointerByEnum(EINTERFACES.IModel_INT) as IModel;
        }

        public bool CreateNewModel(string filePath, EUnits units = EUnits.eUnitsEnglish)
        {
            try
            {
                // Check if file is accessible before attempting to create
                if (!CheckFileAccessibility(filePath, true))
                {
                    return false; // LastErrorMessage already set by CheckFileAccessibility
                }

                // Additional check for RAM-specific issues
                if (IsRAMApplicationRunning())
                {
                    LastErrorMessage = "The RAM application appears to be running. Please close all instances of RAM (including the RAM Manager) and try again.";
                    return false;
                }

                Database.CreateNewDatabase2(filePath, units, "1");
                LastErrorMessage = null; // Clear any previous error
                return true;
            }
            catch (COMException comEx)
            {
                LastErrorMessage = GetUserFriendlyErrorMessage(comEx, filePath, "create");
                Console.WriteLine($"COM Error creating RAM model: 0x{comEx.HResult:X8} - {comEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LastErrorMessage = GetUserFriendlyErrorMessage(ex, filePath, "create");
                Console.WriteLine($"Error creating new RAM model: {ex.Message}");
                return false;
            }
        }

        public bool OpenModel(string filePath)
        {
            try
            {
                // Check if file exists and is accessible
                if (!CheckFileAccessibility(filePath, false))
                {
                    return false; // LastErrorMessage already set by CheckFileAccessibility
                }

                // Additional check for RAM-specific issues
                if (IsRAMApplicationRunning())
                {
                    LastErrorMessage = "The RAM application appears to be running. Please close all instances of RAM (including the RAM Manager) and try again.";
                    return false;
                }

                Database.LoadDataBase2(filePath, "1");
                LastErrorMessage = null; // Clear any previous error
                return true;
            }
            catch (COMException comEx)
            {
                LastErrorMessage = GetUserFriendlyErrorMessage(comEx, filePath, "open");
                Console.WriteLine($"COM Error opening RAM model: 0x{comEx.HResult:X8} - {comEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LastErrorMessage = GetUserFriendlyErrorMessage(ex, filePath, "open");
                Console.WriteLine($"Error opening RAM model: {ex.Message}");
                return false;
            }
        }

        public bool SaveModel()
        {
            try
            {
                Database.SaveDatabase();
                LastErrorMessage = null; // Clear any previous error
                return true;
            }
            catch (COMException comEx)
            {
                LastErrorMessage = GetUserFriendlyErrorMessage(comEx, null, "save");
                Console.WriteLine($"COM Error saving RAM model: 0x{comEx.HResult:X8} - {comEx.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LastErrorMessage = GetUserFriendlyErrorMessage(ex, null, "save");
                Console.WriteLine($"Error saving RAM model: {ex.Message}");
                return false;
            }
        }

        private bool IsRAMApplicationRunning()
        {
            try
            {
                // Check for common RAM process names
                string[] ramProcessNames = { "RAM Structural System", "RAMSBeam", "RAMSColumn", "RAMManager", "RAM" };

                foreach (string processName in ramProcessNames)
                {
                    var processes = System.Diagnostics.Process.GetProcessesByName(processName);
                    if (processes.Length > 0)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                // If we can't check processes, assume it's not running
                return false;
            }
        }

        private bool CheckFileAccessibility(string filePath, bool isCreateOperation)
        {
            try
            {
                if (!isCreateOperation)
                {
                    // For open operations, check if file exists
                    if (!File.Exists(filePath))
                    {
                        LastErrorMessage = $"RAM file not found: {filePath}";
                        return false;
                    }
                }

                // Try to access the file to see if it's locked
                string directoryPath = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directoryPath))
                {
                    LastErrorMessage = $"Directory does not exist: {directoryPath}";
                    return false;
                }

                // For existing files, try to open with FileShare.None to detect if file is in use
                if (File.Exists(filePath))
                {
                    try
                    {
                        using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            // File is accessible
                        }
                    }
                    catch (IOException ioEx) when (ioEx.Message.Contains("being used by another process") ||
                                                   ioEx.Message.Contains("cannot access the file"))
                    {
                        LastErrorMessage = "The RAM file appears to be open in another application. Please close all instances of RAM and try again.";
                        return false;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        LastErrorMessage = "Access denied to RAM file. Please check file permissions.";
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LastErrorMessage = "Please close all instances of RAM and try again.";
                return false;
            }
        }

        private string GetUserFriendlyErrorMessage(Exception ex, string filePath, string operation)
        {
            // Check for COM errors first (most common with RAM)
            if (ex is System.Runtime.InteropServices.COMException comEx)
            {
                uint hresult = (uint)comEx.HResult;

                // RPC_E_SERVERFAULT - The server threw an exception
                if (hresult == 0x80010105)
                {
                    return "The RAM application appears to be running. Please close all instances of RAM (including the RAM Manager) and try again.";
                }

                // Other COM errors
                return "The RAM application appears to be running or inaccessible. Please close all instances of RAM and try again.";
            }

            // Check for file access issues
            string exceptionMessage = ex.Message.ToLower();
            if (exceptionMessage.Contains("access is denied") ||
                exceptionMessage.Contains("being used by another process") ||
                exceptionMessage.Contains("cannot access the file") ||
                exceptionMessage.Contains("sharing violation") ||
                exceptionMessage.Contains("file is locked"))
            {
                return "The RAM file appears to be open in another application. Please close all instances of RAM and try again.";
            }

            // Check for server threw an exception (can also appear in regular exceptions)
            if (exceptionMessage.Contains("server threw an exception") ||
                exceptionMessage.Contains("rpc_e_serverfault"))
            {
                return "The RAM application appears to be running. Please close all instances of RAM (including the RAM Manager) and try again.";
            }

            // Simple fallback
            return "Please close all instances of RAM and try again.";
        }

        public void Dispose()
        {
            try
            {
                if (Database != null)
                {
                    Database.CloseDatabase();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing RAM database: {ex.Message}");
            }
        }
    }
}