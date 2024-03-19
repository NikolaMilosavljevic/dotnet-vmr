// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks
{
    /*
     * This task updates the package source mappings in the NuGet.Config.
     * If package source mappings are used, source-build packages sources will be added with the cumulative package patterns
     * for all of the existing package sources. When building offline, the existing package source mappings will be removed;
     * otherwise they will be preserved after the source-build sources.
     */
    public class UpdateNuGetConfigPackageSourcesMappings : Task
    {
        [Required]
        public string NuGetConfigFile { get; set; }

        /// <summary>
        /// Whether to work in offline mode (remove all internet sources) or online mode (remove only authenticated sources)
        /// </summary>
        public bool BuildWithOnlineFeeds { get; set; }

        /// <summary>
        /// A list of all source-build specific NuGet sources.
        /// </summary>
        public string[] SourceBuildSources { get; set; }

        public string VmrRoot { get; set; }

        public override bool Execute()
        {
            string xml = File.ReadAllText(NuGetConfigFile);
            string newLineChars = FileUtilities.DetectNewLineChars(xml);
            string sbrpCacheName = "source-build-reference-package-cache";
            XDocument document = XDocument.Parse(xml);
            XElement pkgSrcMappingElement = document.Root.Descendants().FirstOrDefault(e => e.Name == "packageSourceMapping");
            XElement pkgSourcesElement = document.Root.Descendants().FirstOrDefault(e => e.Name == "packageSources");
            if (pkgSourcesElement == null)
            {
                return true;
            }

            Hashtable allSourcesPackages = new Hashtable();
            Hashtable currentPackages = new Hashtable();
            Hashtable referencePackages = new Hashtable();
            Hashtable previouslyBuiltPackages = new Hashtable();
            foreach (string packageSource in SourceBuildSources)
            {
                XElement sourceElement = pkgSourcesElement.Descendants().FirstOrDefault(e => e.Name == "add" && e.Attribute("key").Value == packageSource);
                if (sourceElement == null)
                {
                    continue;
                }

                string path = sourceElement.Attribute("value").Value;
                if (!Directory.Exists(path))
                {
                    continue;
                }

                string[] packages = Directory.GetFiles(path, "*.nupkg", SearchOption.AllDirectories);
                foreach (string package in packages)
                {
                    NupkgInfo info = GetNupkgId(package);
                    string id = info.Id.ToLower();
                    string version = info.Version.ToLower();
                    if (packageSource.StartsWith("source-built-"))
                    {
                        if (currentPackages.ContainsKey(id))
                        {
                            List<string> versions = (List<string>)currentPackages[id];
                            if (!versions.Contains(version))
                            {
                                versions.Add(version);
                            }
                        }
                        else
                        {
                            currentPackages.Add(id, new List<string> { version });
                        }
                    }
                    else if (packageSource.Equals("reference-packages"))
                    {
                        if (referencePackages.ContainsKey(id))
                        {
                            List<string> versions = (List<string>)referencePackages[id];
                            if (!versions.Contains(version))
                            {
                                versions.Add(version);
                            }
                        }
                        else
                        {
                            referencePackages.Add(id, new List<string> { version });
                        }
                    }
                    else // previously build packages
                    {
                        if (previouslyBuiltPackages.ContainsKey(id))
                        {
                            List<string> versions = (List<string>)previouslyBuiltPackages[id];
                            if (!versions.Contains(version))
                            {
                                versions.Add(version);
                            }
                        }
                        else
                        {
                            previouslyBuiltPackages.Add(id, new List<string> { version });
                        }
                    }

                    if (allSourcesPackages.ContainsKey(packageSource))
                    {
                        List<string> sourcePackages = (List<string>)allSourcesPackages[packageSource];
                        if (!sourcePackages.Contains(id))
                        {
                            sourcePackages.Add(id);
                        }
                    }
                    else
                    {
                        allSourcesPackages.Add(packageSource, new List<string> { id });
                    }
                }
            }

            // If there is a source-build-reference-package-cache source, we are building SBRP repo.
            // source-build-reference-package-cache is a dynamic source, populated by SBRP build,
            // Discover all SBRP packages from checked in nuspec files.

            XElement sbrpSourceElement = pkgSourcesElement.Descendants().FirstOrDefault(e => e.Name == "add" && e.Attribute("key").Value == sbrpCacheName);
            if (sbrpSourceElement != null)
            {
                if (!allSourcesPackages.ContainsKey(sbrpCacheName))
                {
                    allSourcesPackages.Add(sbrpCacheName, new List<string>());
                }

                if (string.IsNullOrEmpty(VmrRoot))
                {
                    throw new InvalidDataException(string.Format(CultureInfo.CurrentCulture, "VmrRoot is not set - cannot determine SBRP packages."));
                }

                List<string> sbrpcPackages = (List<string>)allSourcesPackages[sbrpCacheName];
                string sbrpRepoRoot = Path.Combine(VmrRoot, "src", "source-build-reference-packages");
                if (Directory.Exists(sbrpRepoRoot))
                {
                    string[] nuspecFiles = Directory.GetFiles(sbrpRepoRoot, "*.nuspec", SearchOption.AllDirectories);
                    foreach (string nuspecFile in nuspecFiles)
                    {
                        // SBRP nuspec file names do not contain version number, so we can get package id without parsing the nuspec file.
                        string id = Path.GetFileNameWithoutExtension(nuspecFile).ToLower();
                        if (!sbrpcPackages.Contains(id))
                        {
                            sbrpcPackages.Add(id);
                        }
                    }
                }
                else
                {
                    throw new InvalidDataException(string.Format(CultureInfo.CurrentCulture, "SBRP repo root does not exist in expected path: {0}", sbrpRepoRoot));
                }
            }


            // Enumerate any existing package source mappings and filter to remove
            // those that are present in any source-build source
            Hashtable oldSourceMappingPatterns = new Hashtable();
            if (pkgSrcMappingElement != null)
            {
                foreach (XElement packageSource in pkgSrcMappingElement.Descendants().Where(e => e.Name == "packageSource"))
                {
                    List<string> patterns = new List<string>();
                    foreach (XElement package in packageSource.Descendants().Where(e => e.Name == "package"))
                    {
                        string pattern = package.Attribute("pattern").Value.ToLower();
                        if (!currentPackages.Contains(pattern) &&
                            !referencePackages.Contains(pattern) &&
                            !previouslyBuiltPackages.Contains(pattern))
                        {
                            patterns.Add(pattern);
                        }
                    }

                    if (patterns.Count > 0)
                    {
                        oldSourceMappingPatterns.Add(packageSource.Attribute("key").Value, patterns);
                    }
                }
            }

            if (pkgSrcMappingElement == null)
            {
                pkgSrcMappingElement = new XElement("packageSourceMapping");
                document.Root.Add(pkgSrcMappingElement);
            }

            // Remove all packageSourceMappings.
            pkgSrcMappingElement.ReplaceNodes(new XElement("clear"));

            XElement pkgSrcMappingClearElement = pkgSrcMappingElement.Descendants().FirstOrDefault(e => e.Name == "clear");

            if (BuildWithOnlineFeeds)
            {
                // When building online add the original, filtered, mappings back in
                foreach (DictionaryEntry entry in oldSourceMappingPatterns)
                {
                    if (entry.Value == null)
                    {
                        continue;
                    }

                    XElement pkgSrc = new XElement("packageSource", new XAttribute("key", entry.Key));
                    foreach (string pattern in (List<string>)entry.Value)
                    {
                        pkgSrc.Add(new XElement("package", new XAttribute("pattern", pattern)));
                    }

                    pkgSrcMappingClearElement.AddAfterSelf(pkgSrc);
                }
            }

            // Add all new package source mappings
            foreach (string packageSource in allSourcesPackages.Keys)
            {
                // skip sources with zero mappings
                if (allSourcesPackages[packageSource] == null)
                {
                    continue;
                }

                XElement pkgSrc = new XElement("packageSource", new XAttribute("key", packageSource));

                if (packageSource.StartsWith("source-built-") ||
                    packageSource.Equals(sbrpCacheName) ||
                    packageSource.Equals("reference-packages"))
                {
                    // Add all packages from current source-built source
                    foreach (string packagePattern in (List<string>)allSourcesPackages[packageSource])
                    {
                        pkgSrc.Add(new XElement("package", new XAttribute("pattern", packagePattern)));
                    }
                }
                else // previously source-built and prebuilt sources
                {
                    foreach (string packagePattern in (List<string>)allSourcesPackages[packageSource])
                    {
                        // Add only packages where version does not exist in current source-built sources
                        if (!currentPackages.Contains(packagePattern))
                        {
                            pkgSrc.Add(new XElement("package", new XAttribute("pattern", packagePattern)));
                        }
                        else
                        {
                            // Matching pattern/id - check if any version is different
                            foreach (string version in (List<string>)previouslyBuiltPackages[packagePattern])
                            {
                                if (!((List<string>)currentPackages[packagePattern]).Contains(version))
                                {
                                    pkgSrc.Add(new XElement("package", new XAttribute("pattern", packagePattern)));
                                    break;
                                }
                            }
                        }
                    }
                }

                pkgSrcMappingClearElement.AddAfterSelf(pkgSrc);
            }

            using (var writer = XmlWriter.Create(NuGetConfigFile, new XmlWriterSettings { NewLineChars = newLineChars, Indent = true }))
            {
                document.Save(writer);
            }

            return true;
        }

        private NupkgInfo GetNupkgId(string path)
        {
            try
            {
                using Stream stream = File.OpenRead(path);
                ZipArchive zipArchive = new(stream, ZipArchiveMode.Read);
                foreach (ZipArchiveEntry entry in zipArchive.Entries)
                {
                    if (entry.Name.EndsWith(".nuspec"))
                    {
                        using Stream nuspecFileStream = entry.Open();
                        XDocument doc = XDocument.Load(nuspecFileStream, LoadOptions.PreserveWhitespace);
                        XElement metadataElement = doc.Descendants().First(c => c.Name.LocalName.ToString() == "metadata");
                        return new NupkgInfo(
                                metadataElement.Descendants().First(c => c.Name.LocalName.ToString() == "id").Value,
                                metadataElement.Descendants().First(c => c.Name.LocalName.ToString() == "version").Value);
                    }
                }

                throw new InvalidDataException(string.Format(CultureInfo.CurrentCulture, "Did not extract nuspec file from package: {0}", path));
            }
            catch (Exception ex)
            {
                throw new InvalidDataException(string.Format(CultureInfo.CurrentCulture, "Invalid package", path), ex);
            }
        }
    }

    public class NupkgInfo
    {
        public NupkgInfo(string id, string version)
        {
            Id = id;
            Version = version;
        }

        public string Id { get; }
        public string Version { get; }
    }
}
