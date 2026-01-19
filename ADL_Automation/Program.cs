using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using tt_net_sdk;

namespace ADL_Automation
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                using (ApiKeyForm keyForm = new ApiKeyForm())
                {
                    if (keyForm.ShowDialog() != DialogResult.OK)
                    {
                        return;
                    }

                    string appSecretKey = keyForm.SecretKey;
                    tt_net_sdk.ServiceEnvironment environment;
                    switch (keyForm.SelectedEnvironment)
                    {
                        case "ProdSim":
                            environment = tt_net_sdk.ServiceEnvironment.ProdSim;
                            break;
                        case "ProdLive":
                            environment = tt_net_sdk.ServiceEnvironment.ProdLive;
                            break;
                        case "UatCert":
                        default:
                            environment = tt_net_sdk.ServiceEnvironment.UatCert;
                            break;
                    }

                    tt_net_sdk.TTAPIOptions.SDKMode sdkMode;
                    switch (keyForm.SelectedUser)
                    {
                        case "Client":
                            sdkMode = tt_net_sdk.TTAPIOptions.SDKMode.Client;
                            break;
                        case "Server":
                            sdkMode = tt_net_sdk.TTAPIOptions.SDKMode.Server;
                            break;
                        default:
                            sdkMode = tt_net_sdk.TTAPIOptions.SDKMode.Client;
                            break;
                    }
                    tt_net_sdk.TTAPIOptions apiConfig = new tt_net_sdk.TTAPIOptions(
                            sdkMode,
                            environment,
                            appSecretKey,
                            5000);

                    Logger.Init("TT_logs");

                    // Setting only for server side 
                    apiConfig.EnableOrderExecution = true;
                    apiConfig.EnableFillFeeds = false;
                    apiConfig.OrderTimeoutSeconds = 500;
                    apiConfig.EnabledServerSideSynthetics = true;
                    apiConfig.EnablePriceLogging = false;
                    apiConfig.LogDebugMessages = false;
                    apiConfig.LogToConsole = false;

                    using (Dispatcher disp = Dispatcher.AttachUIDispatcher())
                    {
                        Application.EnableVisualStyles();

                        // Create an instance of the API
                        MainForm frm = new MainForm(appSecretKey);
                        //apiConfig.EnableAccountFiltering = true;
                        ApiInitializeHandler handler = new ApiInitializeHandler(frm.ttNetApiInitHandler);
                        TTAPI.CreateTTAPI(disp, apiConfig, handler);

                        Application.Run(frm);
                    }
                }
            }
            catch (Exception ex)
            {
                HelperFunctions.ShutEverythingDown($"Error occured while starting the application. \nMessage:{ex.Message}");
            }
        }
    }
}
