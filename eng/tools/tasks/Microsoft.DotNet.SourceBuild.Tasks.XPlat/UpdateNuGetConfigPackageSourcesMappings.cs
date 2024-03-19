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
            List<string> currentPackages = new List<string>();
            List<string> referencePackages = new List<string>();
            List<string> previouslyBuiltPackages = new List<string>();
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
                    string id = GetNupkgId(package).ToLower();
                    if (packageSource.StartsWith("source-built-"))
                    {
                        if (!currentPackages.Contains(id))
                        {
                            currentPackages.Add(id);
                        }
                    }
                    else if (packageSource.Equals("reference-packages"))
                    {
                        if (!referencePackages.Contains(id))
                        {
                            referencePackages.Add(id);
                        }
                    }
                    else // previously build packages
                    {
                        if (!previouslyBuiltPackages.Contains(id))
                        {
                            previouslyBuiltPackages.Add(id);
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

            // If there is a source-build-reference-package-cache source,
            // add all packages from it to the reference-packages source mappings

            if (allSourcesPackages.ContainsKey("reference-packages"))
            {
                XElement sbrpSourceElement = pkgSourcesElement.Descendants().FirstOrDefault(e => e.Name == "add" && e.Attribute("key").Value == sbrpCacheName);
                if (sbrpSourceElement != null)
                {
                    if (!allSourcesPackages.ContainsKey(sbrpCacheName))
                    {
                        allSourcesPackages.Add(sbrpCacheName, new List<string>());
                    }

                    List<string> sbrpcPackages = (List<string>)allSourcesPackages[sbrpCacheName];
                    foreach (string packagePattern in (List<string>)allSourcesPackages["reference-packages"])
                    {
                        if (!sbrpcPackages.Contains(packagePattern))
                        {
                            sbrpcPackages.Add(packagePattern);
                        }
                    }
                }
            }

            // Enumerate any existing package source mappings and filter to remove
            // those that are present in any source-built source
            Hashtable oldSourceMappingPatterns = new Hashtable();
            if (pkgSrcMappingElement != null)
            {
                foreach (XElement packageSource in pkgSrcMappingElement.Descendants().Where(e => e.Name == "packageSource"))
                {
                    string key = packageSource.Attribute("key").Value;
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
                        oldSourceMappingPatterns.Add(key, patterns);
                    }
                }
            }

            if (pkgSrcMappingElement == null)
            {
                pkgSrcMappingElement = new XElement("packageSourceMapping");
                document.Root.Add(pkgSrcMappingElement);
            }

            // Remove all packageSourceMappings.
            // When building online, we will add filtered original mappings
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

            foreach (string packageSource in allSourcesPackages.Keys)
            {
                if (allSourcesPackages[packageSource] == null)
                {
                    continue;
                }

                XElement pkgSrc = new XElement("packageSource", new XAttribute("key", packageSource));

                if (allSourcesPackages.ContainsKey(packageSource))
                {
                    if (packageSource.StartsWith("source-built-") ||
                        packageSource.Equals(sbrpCacheName) ||
                        packageSource.Equals("reference-packages"))
                    {
                        // add all packages from current source-build source
                        foreach (string packagePattern in (List<string>)allSourcesPackages[packageSource])
                        {
                            pkgSrc.Add(new XElement("package", new XAttribute("pattern", packagePattern)));
                        }
                    }
                    else // previously source-built and prebuilt sources
                    {
                        // add only packages not present in current source-build sources
                        foreach (string packagePattern in (List<string>)allSourcesPackages[packageSource])
                        {
                            if (!currentPackages.Contains(packagePattern))
                            {
                                pkgSrc.Add(new XElement("package", new XAttribute("pattern", packagePattern)));
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

        static string GetNupkgId(string path)
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
                        return metadataElement.Descendants().First(c => c.Name.LocalName.ToString() == "id").Value;
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
}
