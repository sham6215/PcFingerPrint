using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management;
using System.Runtime.InteropServices;
using System.IO;

namespace SystemUtil
{
    /// <summary>
    /// https://docs.microsoft.com/ru-ru/windows/desktop/CIMWin32Prov/computer-system-hardware-classes
    /// </summary>
    public sealed class SystemInfoService
    {
        public class HardDrive
        {
            public string Model { get; internal set; }
            public string SerialNo { get; internal set; }
            public string Type { get; internal set; }
        }

        #region Singleton
        private static SystemInfoService _instance = new SystemInfoService();
        public static SystemInfoService Instance => _instance;
        private SystemInfoService()
        { }
        #endregion

        public void GetSystemFingerPrint()
        {
            var mb = FetchMotherboardIdInternal();
        }

        public string FetchMotherboardIdInternal()
        {
            string res = string.Empty;
            ManagementScope scope = new ManagementScope(@"\\" + Environment.MachineName + @"\root\cimv2");
            scope.Connect();

            using (ManagementObject wmiClass = new ManagementObject(scope, new ManagementPath("Win32_BaseBoard.Tag=\"Base Board\""), new ObjectGetOptions()))
            {
                object motherboardIDObj = wmiClass["SerialNumber"];
                if (motherboardIDObj != null)
                {
                    string motherboardID = motherboardIDObj.ToString().Trim();
                    if (IsValidMotherBoardID(motherboardID))
                    {
                        res = motherboardID;
                    }
                }
            }

            return res;
        }
        private bool IsValidMotherBoardID(string value)
        {
            if (value == null)
                return false;
            string motherboardID = value.Trim();
            return !(motherboardID.Replace(".", "").Replace(" ", "").Replace("\t", "").Trim().
                Length < 5 ||
                motherboardID.ToUpper().Contains("BASE") ||
                motherboardID.Contains("2345") ||
                motherboardID.ToUpper().StartsWith("TO BE") ||
                motherboardID.ToUpper().StartsWith("NONE") ||
                motherboardID.ToUpper().StartsWith("N/A") ||
                motherboardID.ToUpper().Contains("SERIAL") ||
                motherboardID.ToUpper().Contains("OEM") ||
                motherboardID.ToUpper().Contains("AAAAA") ||
                motherboardID.ToUpper().Contains("ABCDE") ||
                motherboardID.ToUpper().Contains("XXXXX") ||
                motherboardID.ToUpper().Contains("NOT") ||
                motherboardID.ToUpper().StartsWith("00000"));
        }

        public string FetchCpuIdInternal()
        {
            string res = string.Empty;
            using (ManagementClass mc = new ManagementClass("Win32_Processor"))
            using (ManagementObjectCollection moc = mc.GetInstances())
            {
                foreach (ManagementObject mo in moc)
                {
                    if (mo.Properties["UniqueId"] != null && mo.Properties["UniqueId"].Value != null)
                    {
                        // only return cpuInfo from first CPU
                        res = mo.Properties["UniqueId"].Value.ToString();
                    }
                    mo.Dispose();
                }
            }
            return res;
        }

        public string FecthMACAddressInternal()
        {
            string res = string.Empty;
            try
            {
                using (ManagementClass mc = new ManagementClass("Win32_NetworkAdapterConfiguration"))
                using (ManagementObjectCollection moc = mc.GetInstances())
                {
                    if (moc != null)
                    {
                        foreach (ManagementObject mo in moc)
                        {
                            using (mo)
                            {

                                //System.Diagnostics.Trace.WriteLine(mo["Index"] + " Mac " + mo["Caption"] + " : " + mo["MacAddress"] + " Enabled " + (bool)mo["IPEnabled"]);
                                /// Only return MAC Address from first card
                                if (string.IsNullOrEmpty(res))
                                {
                                    if (mo["MacAddress"] != null && mo["IPEnabled"] != null && (bool)mo["IPEnabled"] == true)
                                    {
                                        res = mo["MacAddress"].ToString();
                                    }
                                }
                            }
                        }
                    }
                }
            } catch (Exception)
            {
                //Trace.TraceWarning("Failed to read DiskID\r\n" + ex.Message);
            }
            return res;
        }


        public string FetchVolumeSerial()
        {
            var res = string.Empty;
            try
            {
                char? drive = Path.GetPathRoot(Environment.SystemDirectory)?.ElementAt(0);
                if (drive != null)
                {
                    res = FetchVolumeSerial((char)drive);
                }
            }
            catch (Exception)
            {
            }

            return res;
        }

        /// <summary>
        /// return Volume Serial Number from hard drive
        /// </summary>
        /// <param name="strDriveLetter">[optional] Drive letter</param>
        /// <returns>[string] VolumeSerialNumber</returns>
        public string FetchVolumeSerial(char driveLetter)
        {
            string res = FetchVolumeSerialDotNet(driveLetter);
            if (string.IsNullOrEmpty(res))
            {
                res = FetchVolumeSerialSysDll(driveLetter);
            }
            return res;
        }

        private string FetchVolumeSerialDotNet(char driveLetter)
        {
            string res = string.Empty;
            try
            {
                using (ManagementObject disk = new ManagementObject("win32_logicaldisk.deviceid=\"" + driveLetter + ":\""))
                {
                    if (disk == null)
                        return null;
                    disk.Get();
                    object diskObj = disk["VolumeSerialNumber"];
                    if (diskObj != null)
                        res = diskObj.ToString();
                }
            }
            catch (Exception)
            {
                //Trace.TraceWarning("Failed to read DiskID\r\n" + ex.Message);
            }
            return res;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetVolumeInformation(string Volume, StringBuilder VolumeName, uint VolumeNameSize, out uint SerialNumber, out uint SerialNumberLength, out uint flags, StringBuilder fs, uint fs_size);
        private string FetchVolumeSerialSysDll(char driveLetter)
        {
            string res = string.Empty;
            try
            {
                uint serialNum, serialNumLength, flags;
                StringBuilder volumename = new StringBuilder(256);
                StringBuilder fstype = new StringBuilder(256);

                bool ok = GetVolumeInformation(driveLetter.ToString() + ":\\", volumename, (uint)volumename.Capacity - 1, out serialNum, out serialNumLength, out flags, fstype, (uint)fstype.Capacity - 1);
                if (ok)
                {
                    res = string.Format("{0:X4}{1:X4}", serialNum >> 16, serialNum & 0xFFFF);
                }
            }
            catch(Exception)
            {
                //Trace.TraceWarning("Failed to read DiskID\r\n" + ex.Message);
            }
            return res;
        }

        public List<HardDrive> FetchHddDrivesInfo()
        {
            List<HardDrive> res = new List<HardDrive>();
            ManagementObjectSearcher searcher = 
                new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");

            foreach (ManagementObject wmi_HD in searcher.Get())
            {
                HardDrive hd = new HardDrive();
                hd.Model = wmi_HD["Model"].ToString();
                hd.Type = wmi_HD["Signature"].ToString();
                hd.Type = wmi_HD["InterfaceType"].ToString();
                res.Add(hd);
            }

            searcher = 
                new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMedia");
            
            int i = 0;
            foreach (ManagementObject wmi_HD in searcher.Get())
            {
                HardDrive hd = res[i];
                // get the hardware serial no.
                if (wmi_HD["SerialNumber"] == null)
                    hd.SerialNo = "None";
                else
                    hd.SerialNo = wmi_HD["SerialNumber"].ToString();

                ++i;
            }
            return res;
        }

        private void TraceManagementObject(ManagementObject obj)
        {
            System.Diagnostics.Trace.WriteLine($"{obj}");
            foreach (var pr in obj.Properties)
            {
                System.Diagnostics.Trace.WriteLine($"\t{pr.Name}:{pr.Value}");
            }
            System.Diagnostics.Trace.WriteLine($"============");
        }
    }
}
