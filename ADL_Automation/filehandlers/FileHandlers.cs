using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace ADL_Automation
{
    public class FileHandlers
    {
        public void SaveApiKey(string path, string key)
        {
            try
            {
                File.WriteAllText(path, key);
            }
            catch (Exception fileEx)
            {
                MessageBox.Show($"Connected, but failed to save key: {fileEx.Message}", "File Save Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public string FetchApiKey(string keyFilePath)
        {
            try
            {
                if (File.Exists(keyFilePath))
                {
                    return File.ReadAllText(keyFilePath);
                }
                return null;

            }
            catch
            {
                MessageBox.Show("Error occured while fetching API key. Shutting down.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return String.Empty;
            }

        } 

        public string GetAccountName(string filePath)
        {
            try
            {
                //throw new Exception("This is a dummy exception");
                return File.ReadAllText(filePath).Trim();
            }
            catch (Exception ex)
            {
                HelperFunctions.ShutEverythingDown(
                    $"Error reading name from file.\nMessage: {ex.Message}"
                );
                return string.Empty;
            }
        }
    }
}
