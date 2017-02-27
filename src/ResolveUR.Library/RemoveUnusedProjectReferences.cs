﻿namespace ResolveUR.Library
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Xml;

    public class RemoveUnusedProjectReferences : IResolveUR
    {
        private const string Include = "Include";
        private const string Reference = "Reference";
        private const string Project = "Project";
        private string _filePath;
        private bool _isCancel;
        private PackageConfig _packageConfig;

        public event HasBuildErrorsEventHandler HasBuildErrorsEvent;
        public event ProgressMessageEventHandler ProgressMessageEvent;
        public event ReferenceCountEventHandler ReferenceCountEvent;
        public event EventHandler ItemGroupResolvedEvent;
        public event PackageResolveProgressEventHandler PackageResolveProgressEvent;

        public string FilePath
        {
            get
            {
                return _filePath;
            }
            set
            {
                _filePath = value;
                var dirPath = Path.GetDirectoryName(_filePath);
                if (!IsResolvePackage)
                    return;
                _packageConfig = new PackageConfig
                {
                    FilePath = value,
                    PackageConfigPath = dirPath + "\\packages.config"
                };
            }
        }

        public string BuildLogPath { get; set; }

        public string BuilderPath { get; set; }

        public bool IsResolvePackage { get; set; }

        // returns false if there were build errors
        public void Resolve()
        {
            // do nothing if project already has build errors
            if (ProjectHasBuildErrors())
            {
                RaiseBuildErrorsEvent();
                return;
            }
            if (IsResolvePackage)
                _packageConfig.LoadPackagesIfAny();

            Resolve(Reference);

            if (_isCancel)
                return;

            Resolve(Project + Reference);
        }

        public void Cancel()
        {
            _isCancel = true;
        }

        private void Resolve(
            string referenceType)
        {
            RaiseProgressMessageEvent($"Resolving {referenceType}s in {Path.GetFileName(FilePath)}");

            var projectXmlDocument = GetXmlDocument();
            var projectXmlDocumentToRestore = GetXmlDocument();

            var itemIndex = 0;
            var item = GetReferenceGroupItemIn(projectXmlDocument, referenceType, itemIndex);
            while (item != null)
            {
                if (_isCancel)
                    break;

                // use string names to match up references, using nodes themselves will mess references
                if (item.ChildNodes.Count == 0)
                    continue;

                var referenceNodeNames = getReferenceNodeNamesIn(item);

                var nodeNames = referenceNodeNames as IList<string> ?? referenceNodeNames.ToList();
                ReferenceCountEvent?.Invoke(nodeNames.Count());

                foreach (var referenceNodeName in nodeNames)
                {
                    if (_isCancel)
                        break;

                    var nodeToRemove = findNodeByAttributeName(item, referenceNodeName);

                    if (nodeToRemove?.ParentNode == null)
                        continue;

                    nodeToRemove.ParentNode.RemoveChild(nodeToRemove);

                    projectXmlDocument.Save(FilePath);

                    if (ProjectHasBuildErrors())
                    {
                        RaiseProgressMessageEvent($"\tKept: {referenceNodeName}");
                        if (IsResolvePackage)
                            _packageConfig.CopyPackageToKeep(nodeToRemove);

                        // restore original
                        projectXmlDocumentToRestore.Save(FilePath);
                        // reload item group from doc
                        projectXmlDocument = GetXmlDocument();
                        item = GetReferenceGroupItemIn(projectXmlDocument, referenceType, itemIndex);
                        // prevents from using yield return?
                        if (item == null)
                            break;
                    }
                    else
                    {
                        RaiseProgressMessageEvent($"\tRemoved: {referenceNodeName}");
                        projectXmlDocumentToRestore = GetXmlDocument();
                        if (IsResolvePackage)
                            _packageConfig.RemoveUnusedPackage(nodeToRemove);
                    }
                }

                RaiseItemGroupResolvedEvent();

                item = GetReferenceGroupItemIn(projectXmlDocument, referenceType, ++itemIndex);
            }

            if (_isCancel)
                return;

            if (IsResolvePackage)
                _packageConfig.UpdatePackageConfig();
            RaisePackageResolveProgressEvent("Packages resolved!");

            RaiseProgressMessageEvent("Done with: " + Path.GetFileName(FilePath));
        }

        private XmlDocument GetXmlDocument()
        {
            var doc = new XmlDocument();
            doc.Load(FilePath);
            return doc;
        }

        private XmlNodeList getItemGroupNodesIn(
            XmlDocument document)
        {
            const string itemGroupXPath = @"//*[local-name()='ItemGroup']";
            return document.SelectNodes(itemGroupXPath);
        }

        private XmlNode GetReferenceGroupItemIn(
            XmlDocument document,
            string referenceNodeName,
            int startIndex)
        {
            var itemGroups = getItemGroupNodesIn(document);

            if (itemGroups == null || itemGroups.Count == 0)
                return null;

            for (var i = startIndex; i < itemGroups.Count; i++)
            {
                if (itemGroups[i].ChildNodes.Count == 0)
                    return null;

                if (itemGroups[i].ChildNodes[0].Name == referenceNodeName)
                    return itemGroups[i];
            }

            return null;
        }

        private IEnumerable<string> getReferenceNodeNamesIn(
            XmlNode itemGroup)
        {
            return
                itemGroup.ChildNodes.OfType<XmlNode>()
                    .Select(x => x.Attributes?[Include].Value)
                    .ToList();
        }

        private XmlNode findNodeByAttributeName(
            XmlNode itemGroup,
            string attributeName)
        {
            return
                itemGroup.ChildNodes.Cast<XmlNode>()
                    .FirstOrDefault(item => item.Attributes != null && item.Attributes[Include].Value == attributeName);
        }

        private bool ProjectHasBuildErrors()
        {
            const string logFileName = "buildlog.txt";
            const string argumentsFormat = "\"{0}\" /clp:ErrorsOnly /nologo /m /flp:logfile={1};Verbosity=Quiet";
            const string error = "error";

            var tempPath = Path.GetTempPath();
            BuildLogPath = tempPath + @"\" + logFileName;

            var startInfo = new ProcessStartInfo
            {
                FileName = BuilderPath,
                Arguments = string.Format(CultureInfo.CurrentCulture, argumentsFormat, FilePath, BuildLogPath),
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = tempPath
            };

            // clear build log file if it was left out for some reason
            if (File.Exists(BuildLogPath))
                File.Delete(BuildLogPath);

            var status = 0;
            using (var exeProcess = Process.Start(startInfo))
            {
                exeProcess?.WaitForExit();
                if (exeProcess != null)
                    status = exeProcess.ExitCode;
            }

            // open build log to check for errors
            if (!File.Exists(BuildLogPath))
                return true;

            var s = File.ReadAllText(BuildLogPath);
            //File.Delete(BuildLogPath); Don't delete the log file

            // if build error, the reference cannot be removed
            return status != 0 && (s.Contains(error) || s == string.Empty);
        }

        private void RaiseProgressMessageEvent(
            string message)
        {
            ProgressMessageEvent?.Invoke(message);
        }

        private void RaiseBuildErrorsEvent()
        {
            HasBuildErrorsEvent?.Invoke(Path.GetFileNameWithoutExtension(FilePath), BuildLogPath);
        }

        private void RaiseItemGroupResolvedEvent()
        {
            ItemGroupResolvedEvent?.Invoke(null, null);
        }

        private void RaisePackageResolveProgressEvent(
            string message)
        {
            PackageResolveProgressEvent?.Invoke(message);
        }
    }
}