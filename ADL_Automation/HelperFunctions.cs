using ADL_Automation;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using tt_net_sdk;

namespace ADL_Automation
{
    public class HelperFunctions
    {
        public static void ShutEverythingDown(string errorText)
        {
            try
            {
                PriceSimulator.Stop();
                LoadingLabel.ShowErrorLabel(errorText);
                Thread.Sleep(10000);
                //MainForm.Dispose();
            }
            catch(Exception ex)
            {
                Logger.Log($"There was an error while shutting everything down: {ex.Message}");
            }

        }


    }
}
