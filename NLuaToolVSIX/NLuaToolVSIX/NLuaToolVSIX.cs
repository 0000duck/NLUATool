//------------------------------------------------------------------------------
// <copyright file="NLuaToolVSIX.cs" company="Company">
//     Copyright (c) Company.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;

namespace NLuaToolVSIX
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [Guid(GuidList.guidProjectLauncherPkgString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
   // [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class NLuaToolVSIX : Package
    {
        

        /// <summary>
        /// Initializes a new instance of the <see cref="NLuaToolVSIX"/> class.
        /// </summary>
        public NLuaToolVSIX()
        {
            // Inside this method you can place any initialization code that does not require
            // any Visual Studio service because at this point the package object is created but
            // not sited yet inside Visual Studio environment. The place to do all the other
            // initialization is the Initialize method.
        }

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs)
            {
                // Create the command for the menu item.
                CommandID menuCommandID = new CommandID(GuidList.guidProjectLauncherCmdSet, (int)PkgCmdIDList.cmdidProjectLauncher);
                MenuCommand menuItem = new MenuCommand(DisplayLaunchForm, menuCommandID);
                mcs.AddCommand(menuItem);
            }
        }

        /// <summary>
        /// This function is the callback used to execute a command when the a menu item is clicked.
        /// See the Initialize method to see how the menu item is associated to this function using
        /// the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        private void DisplayLaunchForm(object sender, EventArgs e)
        {
            // Show a Message Box to prove we were here
            var dte = (EnvDTE80.DTE2)GetService(typeof(EnvDTE.DTE));
            LaunchFrom launchForm = new LaunchFrom(dte);
            if (launchForm.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // The user clicked on Ok in the form, so launch the file using the sample debug engine.
                string filePath = launchForm.FilePath;

                LaunchDebugTarget(filePath, launchForm.SelectLuaFile);
            }
        }

        private void LaunchDebugTarget(string filePath,string codefile)
        {
            var debugger = (IVsDebugger4)GetService(typeof(IVsDebugger));
            VsDebugTargetInfo4[] debugTargets = new VsDebugTargetInfo4[1];
            debugTargets[0].dlo = (uint)DEBUG_LAUNCH_OPERATION.DLO_CreateProcess;
            debugTargets[0].bstrExe = filePath;
            debugTargets[0].bstrArg = codefile;
            debugTargets[0].guidLaunchDebugEngine = new Guid(Microsoft.VisualStudio.Debugger.Lua.EngineConstants.EngineId);
            VsDebugTargetProcessInfo[] processInfo = new VsDebugTargetProcessInfo[debugTargets.Length];
            

            debugger.LaunchDebugTargets4(1, debugTargets, processInfo);
            
        }

        #endregion
    }
}
