﻿namespace ResolveUR.Library
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml;

    class PackageConfig
    {
        readonly HashSet<XmlNode> _packagesToKeep = new HashSet<XmlNode>();
        XmlDocument _packageConfigDocument;
        IDictionary<string, XmlNode> _packages;

        public string PackageConfigPath { get; set; }
        public string FilePath { get; set; }

        public void LoadPackagesIfAny()
        {
            // an entry in package config maps to hint path under project reference node as follows:
            // Entry : <package id="CsvHelper" version="2.7.0" targetFramework="net45" />
            // Hint path : <HintPath>..\packages\CsvHelper.2.7.0\lib\net40-client\CsvHelper.dll</HintPath>

            // Folder in HintPath CsvHelper.2.7.0 is derieved by concatenation of Id and Version attributes of Entry

            // map versioned library CsvHelper.2.7.0 to package entries in method
            if (!File.Exists(PackageConfigPath) || _packageConfigDocument != null)
                return;

            _packageConfigDocument = new XmlDocument();
            _packageConfigDocument.Load(PackageConfigPath);

            const string packageNode = @"//*[local-name()='package']";
            var packageNodes = _packageConfigDocument.SelectNodes(packageNode);
            if (packageNodes != null && packageNodes.Count > 0)
                _packages = new Dictionary<string, XmlNode>();
            if (packageNodes == null)
                return;

            foreach (XmlNode node in packageNodes)
            {
                if (node.Attributes != null)
                    _packages.Add(node.Attributes["id"].Value + "." + node.Attributes["version"].Value, node);
            }

            // when references are cleaned up later, store package nodes that match hint paths for references that are ok to keep
            // at the conclusion of cleanup, rewrite packages config with saved package nodes to keep
        }

        public void CopyPackageToKeep(XmlNode referenceNode)
        {
            if (referenceNode.ChildNodes.Count == 0)
                return;

            var hintPath = getHintPath(referenceNode);
            if (string.IsNullOrWhiteSpace(hintPath))
                return;

            foreach (var package in _packages)
            {
                if (hintPath.Contains(package.Key) && !_packagesToKeep.Contains(package.Value))
                {
                    _packagesToKeep.Add(package.Value);
                    break;
                }
            }
        }

        public void RemoveUnusedPackage(XmlNode referenceNode)
        {
            if (referenceNode.ChildNodes.Count == 0)
                return;

            var hintPath = getHintPath(referenceNode);
            foreach (var package in _packages)
            {
                if (!hintPath.Contains(package.Key))
                    continue;

                var packagePath = hintPath.Substring(0, hintPath.IndexOf(package.Key, StringComparison.Ordinal)) +
                                  package.Key;
                var folderName = Path.GetDirectoryName(FilePath);
                if (folderName != null)
                    packagePath = Path.Combine(folderName, packagePath);

                try
                {
                    Directory.Delete(packagePath, true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                break;
            }
        }

        string getHintPath(XmlNode referenceNode)
        {
            var node = referenceNode.ChildNodes.OfType<XmlNode>().FirstOrDefault(x => x.Name == "HintPath");
            if (node == null)
                return string.Empty;

            return node.InnerXml;
        }

        public void UpdatePackageConfig()
        {
            if (_packagesToKeep.Count == 0)
                return;

            if (_packageConfigDocument.DocumentElement != null)
            {
                _packageConfigDocument.DocumentElement.RemoveAll();
                foreach (var package in _packagesToKeep)
                    _packageConfigDocument.DocumentElement.AppendChild(package);
            }

            _packageConfigDocument.Save(PackageConfigPath);
            _packagesToKeep.Clear();
        }
    }
}