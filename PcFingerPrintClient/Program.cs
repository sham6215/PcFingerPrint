using SystemUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PcFingerPrintClient
{
    class Program
    {
        static void Main(string[] args)
        {
            var res = SystemInfoService.Instance.FetchMotherboardIdInternal();
            res = SystemInfoService.Instance.FetchCpuIdInternal();
            res = SystemInfoService.Instance.FecthMACAddressInternal();
            res = SystemInfoService.Instance.FetchVolumeSerial();
            System.Diagnostics.Trace.WriteLine(res);
            res = SystemInfoService.Instance.FetchVolumeSerial('d');
            System.Diagnostics.Trace.WriteLine(res);
            res = SystemInfoService.Instance.FetchVolumeSerial('f');
            System.Diagnostics.Trace.WriteLine(res);

            var hdds = SystemInfoService.Instance.FetchHddDrivesInfo();
        }
    }
}
