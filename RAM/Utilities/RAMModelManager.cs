// RAMModelManager.cs
using System;
using RAMDATAACCESSLib;

namespace RAM.Utilities
{
    public class RAMModelManager : IDisposable
    {
        public RamDataAccess1 RamDataAccess { get; private set; }
        public IDBIO1 Database { get; private set; }
        public IModel Model { get; private set; }

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
                Database.CreateNewDatabase2(filePath, units, "1");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating new RAM model: {ex.Message}");
                return false;
            }
        }

        public bool OpenModel(string filePath)
        {
            try
            {
                Database.LoadDataBase2(filePath, "1");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening RAM model: {ex.Message}");
                return false;
            }
        }

        public bool SaveModel()
        {
            try
            {
                Database.SaveDatabase();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving RAM model: {ex.Message}");
                return false;
            }
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