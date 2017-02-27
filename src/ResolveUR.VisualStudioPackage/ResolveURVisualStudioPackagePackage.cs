﻿namespace ResolveURVisualStudioPackage
{
    using System;
    using System.ComponentModel.Design;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Runtime.InteropServices;
    using EnvDTE;
    using EnvDTE80;
    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using Properties;
    using ResolveUR.Library;
    using Thread = System.Threading.Thread;

    /// <summary>
    ///     is the class that implements the package exposed by assembly.
    ///     The minimum requirement for a class to be considered a valid package for Visual Studio
    ///     is to implement the IVsPackage interface and register itself with the shell.
    ///     package uses the helper classes defined inside the Managed Package Framework (MPF)
    ///     to do it: it derives from the Package class that provides the implementation of the
    ///     IVsPackage interface and uses the registration attributes defined in the framework to
    ///     register itself and its components with the shell.
    /// </summary>
    // attribute tells the PkgDef creation utility (CreatePkgDef.exe) that class is
    // a package.
    [PackageRegistration(UseManagedResourcesOnly = true)]
    // attribute is used to register the information needed to show package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    // attribute is needed to let the shell know that package exposes some menus.
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(GuidList.GuidResolveUrVisualStudioPackagePkgString)]
    // ReSharper disable once InconsistentNaming - Product Name
    public sealed class ResolveURVisualStudioPackagePackage : Package
    {
        /// <summary>
        ///     Default constructor of the package.
        ///     Inside method you can place any initialization code that does not require
        ///     any Visual Studio service because at point the package object is created but
        ///     not sited yet inside Visual Studio environment. The place to do all the other
        ///     initialization is the Initialize method.
        /// </summary>
        public ResolveURVisualStudioPackagePackage()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", ToString()));
        }

        /////////////////////////////////////////////////////////////////////////////
        // Overridden Package Implementation

        /// <summary>
        ///     function is the callback used to execute a command when the a menu item is clicked.
        ///     See the Initialize method to see how the menu item is associated to function using
        ///     the OleMenuCommandService service and the MenuCommand class.
        /// </summary>
        private void ProjectMenuItemCallback(
            object sender,
            EventArgs e)
        {
            HandleCallBack(GetProjectName);
        }

        private void SolutionMenuItemCallback(
            object sender,
            EventArgs e)
        {
            HandleCallBack(GetSolutionName);
        }

        private void HandleCallBack(
            Func<string> activeFileNameGetter)
        {
            CreateOutputWindow();
            CreateProgressDialog();
            CreateUiShell();

            var builderPath = FindMsBuildPath();
            if (string.IsNullOrWhiteSpace(builderPath))
            {
                _helper.ShowMessageBox(
                    "MsBuild Exe not found",
                    "MsBuild Executable required to compile project was not found on machine. Aborting...");
                _helper.EndWaitDialog();
                return;
            }

            var filePath = activeFileNameGetter();
            if (string.IsNullOrEmpty(filePath))
            {
                resolveur_ProgressMessageEvent("Invalid file");
                return;
            }

            _resolveur = createResolver(filePath);
            if (_resolveur == null)
                resolveur_ProgressMessageEvent("Unrecognized project or solution type");
            else
            {
                _helper.ResolveurCanceled += helper_ResolveurCanceled;
                _resolveur.IsResolvePackage = packageOption();
                _resolveur.BuilderPath = builderPath;
                _resolveur.FilePath = filePath;
                _resolveur.HasBuildErrorsEvent += resolveur_HasBuildErrorsEvent;
                _resolveur.ProgressMessageEvent += resolveur_ProgressMessageEvent;
                _resolveur.ReferenceCountEvent += resolveur_ReferenceCountEvent;
                _resolveur.ItemGroupResolvedEvent += resolveur_ItemGroupResolvedEvent;
                _resolveur.PackageResolveProgressEvent += _resolveur_PackageResolveProgressEvent;
                _resolveur.Resolve();
            }

            _helper.EndWaitDialog();
        }

        private bool packageOption()
        {
            var packageResolveOptionDialog = new PackageDialog();
            packageResolveOptionDialog.ShowModal();
            return packageResolveOptionDialog.IsResolvePackage;
        }

        private string GetSolutionName()
        {
            var dte2 = GetService(typeof (SDTE)) as DTE2;

            var solutionObject = dte2?.Solution;
            if (solutionObject == null)
                return string.Empty;

            var solution = solutionObject.Properties.Item(5).Value.ToString();
            if (!File.Exists(solution))
                return string.Empty;

            return solution;
        }

        private string GetProjectName()
        {
            var dte2 = GetService(typeof (SDTE)) as DTE2;

            var activeProjects = (Array) dte2?.ActiveSolutionProjects;
            if (activeProjects == null || activeProjects.Length == 0)
                return string.Empty;

            var project = (Project) activeProjects.GetValue(0);

            return project.FileName;
        }

        private static string FindMsBuildPath()
        {
            if (File.Exists(Settings.Default.msbuildx86v14))
                return Settings.Default.msbuildx86v14;
            if (File.Exists(Settings.Default.msbuildx64v14))
                return Settings.Default.msbuildx64v14;
            if (File.Exists(Settings.Default.msbuildx86v12))
                return Settings.Default.msbuildx86v12;
            if (File.Exists(Settings.Default.msbuildx64v12))
                return Settings.Default.msbuildx64v12;
            if (File.Exists(Settings.Default.msbuildx6440))
                return Settings.Default.msbuildx6440;
            if (File.Exists(Settings.Default.msbuildx6440))
                return Settings.Default.msbuildx6440;
            if (File.Exists(Settings.Default.msbuildx6440))
                return Settings.Default.msbuildx6440;
            if (File.Exists(Settings.Default.msbuildx8640))
                return Settings.Default.msbuildx8640;
            if (File.Exists(Settings.Default.msbuildx6435))
                return Settings.Default.msbuildx6435;
            if (File.Exists(Settings.Default.msbuildx8635))
                return Settings.Default.msbuildx8635;
            if (File.Exists(Settings.Default.msbuildx6420))
                return Settings.Default.msbuildx6420;
            if (File.Exists(Settings.Default.msbuildx8620))
                return Settings.Default.msbuildx8620;

            return string.Empty;
        }

        #region Package Members

        private Helper _helper;
        private IResolveUR _resolveur;

        /// <summary>
        ///     Initialization of the package; method is called right after the package is sited, so is the place
        ///     where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Debug.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", ToString()));
            base.Initialize();

            // Add our command handlers for menu (commands must exist in the .vsct file)
            var mcs = GetService(typeof (IMenuCommandService)) as OleMenuCommandService;
            if (null != mcs)
            {
                // Create the command for the menu item.
                var menuCommandId = new CommandID(
                    GuidList.GuidResolveUrVisualStudioPackageCmdSet,
                    (int) PkgCmdIdList.CmdRemoveUnusedProjectReferences);
                var menuItem = new MenuCommand(ProjectMenuItemCallback, menuCommandId);
                mcs.AddCommand(menuItem);
                menuCommandId = new CommandID(
                    GuidList.GuidResolveUrVisualStudioPackageCmdSet,
                    (int) PkgCmdIdList.CmdRemoveUnusedSolutionReferences);
                menuItem = new MenuCommand(SolutionMenuItemCallback, menuCommandId);
                mcs.AddCommand(menuItem);
                _helper = new Helper();
            }
        }

        #endregion

        #region Create members

        private void CreateOutputWindow()
        {
            var dte2 = GetService(typeof (SDTE)) as DTE2;
            if (dte2 != null)
            {
                var window = dte2.Windows.Item(EnvDTE.Constants.vsWindowKindOutput);
                var outputWindow = (OutputWindow) window.Object;
                OutputWindowPane outputWindowPane = null;

                const string outputWindowName = "Output";
                for (uint i = 1; i <= outputWindow.OutputWindowPanes.Count; i++)
                {
                    if (outputWindow.OutputWindowPanes.Item(i)
                        .Name.Equals(outputWindowName, StringComparison.CurrentCultureIgnoreCase))
                    {
                        outputWindowPane = outputWindow.OutputWindowPanes.Item(i);
                        break;
                    }
                }

                if (outputWindowPane == null)
                {
                    outputWindowPane = outputWindow.OutputWindowPanes.Add(outputWindowName);
                    if (outputWindowPane != null)
                        _helper.OutputWindow = outputWindow;
                }
            }
        }

        private void CreateProgressDialog()
        {
            var dialogFactory = GetService(typeof (SVsThreadedWaitDialogFactory)) as IVsThreadedWaitDialogFactory;
            IVsThreadedWaitDialog2 progressDialog = null;
            if (dialogFactory != null)
                dialogFactory.CreateInstance(out progressDialog);

            if (progressDialog != null &&
                progressDialog.StartWaitDialog(
                    Constants.AppName + " Working...",
                    "Visual Studio is busy. Cancel ResolveUR by clicking Cancel button",
                    string.Empty,
                    null,
                    string.Empty,
                    0,
                    true,
                    true) == VSConstants.S_OK)
                Thread.Sleep(1000);

            _helper.ProgressDialog = progressDialog;

            var dialogCanceled = false;
            if (progressDialog != null)
                progressDialog.HasCanceled(out dialogCanceled);
            if (dialogCanceled)
            {
                _resolveur.Cancel();
                _helper.ShowMessageBox(Constants.AppName + " Status", "Canceled");
            }
        }

        private IResolveUR createResolver(
            string filePath)
        {
            if (filePath.EndsWith("proj"))
                return new RemoveUnusedProjectReferences();

            return filePath.EndsWith(".sln") ? new RemoveUnusedSolutionReferences() : null;
        }

        private void CreateUiShell()
        {
            _helper.UiShell = (IVsUIShell) GetService(typeof (SVsUIShell));
        }

        #endregion

        #region Resolveur Events

        private void resolveur_HasBuildErrorsEvent(
            string projectName, string buildLogFile)
        {
            _helper.ShowMessageBox(
                "Resolve Unused References",
                "Project " + projectName +
                " already has compile errors. Please ensure it has no build errors and retry removing references."+
                " See the log file: "+ buildLogFile);
            _helper.EndWaitDialog();
        }

        private void _resolveur_PackageResolveProgressEvent(
            string message)
        {
            _helper.SetMessage(message);
        }

        private void helper_ResolveurCanceled(
            object sender,
            EventArgs e)
        {
            _resolveur.Cancel();
        }

        private void resolveur_ProgressMessageEvent(
            string message)
        {
            if (message.Contains("Resolving"))
            {
                _helper.ItemGroupCount = 1;
                _helper.CurrentProject = message;
            }
            _helper.SetMessage(message);
        }

        private void resolveur_ItemGroupResolvedEvent(
            object sender,
            EventArgs e)
        {
            _helper.CurrentReferenceCountInItemGroup = 0;
            _helper.ItemGroupCount++;
        }

        private void resolveur_ReferenceCountEvent(
            int count)
        {
            _helper.TotalReferenceCount = count;
        }

        #endregion
    }
}