// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Arcade.Common;
using Microsoft.Build.Framework;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public class PushToAzureDevOpsArtifacts : MSBuildTaskBase
    {
        [Required]
        public ITaskItem[] ItemsToPush { get; set; }

        public string AssetsTemporaryDirectory { get; set; }

        public bool PublishFlatContainer { get; set; }

        public string ManifestRepoName { get; set; }

        public string ManifestRepoUri { get; set; }

        public string ManifestBuildId { get; set; } = "no build id provided";

        public string ManifestBranch { get; set; }

        public string ManifestCommit { get; set; }

        public string[] ManifestBuildData { get; set; }

        public string AzureDevOpsCollectionUri { get; set; }

        public string AzureDevOpsProject { get; set; }

        public int AzureDevOpsBuildId { get; set; }

        public ITaskItem[] ItemsToSign { get; set; }

        public ITaskItem[] StrongNameSignInfo { get; set; }

        public ITaskItem[] FileSignInfo { get; set; }

        public ITaskItem[] FileExtensionSignInfo { get; set; }

        public ITaskItem[] CertificatesSignInfo { get; set; }

        public string AssetManifestPath { get; set; }

        public bool IsStableBuild { get; set; }

        public bool IsReleaseOnlyPackageVersion { get; set; }

        public string AssetsLocalStoragePath { get; set; }

        public string ShippingPackagesLocalStoragePath { get; set; }

        public string NonShippingPackagesLocalStoragePath { get; set; }

        public string AssetManifestLocalStoragePath { get; set; }

        public bool PushToAzDO { get; set; }

        public bool PushToLocalStorage { get; set; }

        /// <summary>
        /// Which version should the build manifest be tagged with.
        /// By default he latest version is used.
        /// </summary>
        public string PublishingVersion { get; set; }

        public enum ItemType
        {
            AssetManifest = 0,
            PackageArtifact,
            BlobArtifact
        }

        public override void ConfigureServices(IServiceCollection collection)
        {
            collection.TryAddSingleton<ISigningInformationModelFactory, SigningInformationModelFactory>();
            collection.TryAddSingleton<IBlobArtifactModelFactory, BlobArtifactModelFactory>();
            collection.TryAddSingleton<IPackageArtifactModelFactory, PackageArtifactModelFactory>();
            collection.TryAddSingleton<IBuildModelFactory, BuildModelFactory>();
            collection.TryAddSingleton<IFileSystem, FileSystem>();
            collection.TryAddSingleton<IPackageArchiveReaderFactory, PackageArchiveReaderFactory>();
            collection.TryAddSingleton<INupkgInfoFactory, NupkgInfoFactory>();
            collection.TryAddSingleton(Log);
        }

        public bool ExecuteTask(IFileSystem fileSystem,
            ISigningInformationModelFactory signingInformationModelFactory,
            IBlobArtifactModelFactory blobArtifactModelFactory,
            IPackageArtifactModelFactory packageArtifactModelFactory,
            IBuildModelFactory buildModelFactory)
        {
            try
            {
                /*
                if (PushToAzDO == false && PushToLocalStorage == false)
                {
                    // Default to pushing to AzDO - ensures this task works everywhere
                    PushToAzDO = true;
                }
                */

                if (PushToLocalStorage)
                {
                    if (string.IsNullOrEmpty(AssetsLocalStoragePath) ||
                        string.IsNullOrEmpty(ShippingPackagesLocalStoragePath) ||
                        string.IsNullOrEmpty(NonShippingPackagesLocalStoragePath) ||
                        string.IsNullOrEmpty(AssetManifestLocalStoragePath))
                    {
                        throw new Exception($"AssetsLocalStoragePath, ShippingPackagesLocalStoragePath, NonShippingPackagesLocalStoragePath and AssetManifestLocalStoragePath need to be specified if PublishToLocalStorage is set to true");
                    }

                    Directory.CreateDirectory(AssetManifestLocalStoragePath);
                    Directory.CreateDirectory(AssetsLocalStoragePath);
                    Directory.CreateDirectory(ShippingPackagesLocalStoragePath);
                    Directory.CreateDirectory(NonShippingPackagesLocalStoragePath);
                    Log.LogMessage(MessageImportance.High, "Performing push to local artifacts storage.");
                }

                if (PushToAzDO)
                {
                    Log.LogMessage(MessageImportance.High, "Performing push to Azure DevOps artifacts storage.");
                }

                if (!string.IsNullOrWhiteSpace(AssetsTemporaryDirectory))
                {
                    Log.LogMessage(MessageImportance.High, $"It's no longer necessary to specify a value for the {nameof(AssetsTemporaryDirectory)} property. " +
                        $"Please consider patching your code to not use it.");
                }

                if (ItemsToPush == null)
                {
                    Log.LogError($"No items to push. Please check ItemGroup ItemsToPush.");
                }
                else
                {
                    IEnumerable<BlobArtifactModel> blobArtifacts = Enumerable.Empty<BlobArtifactModel>();
                    IEnumerable<PackageArtifactModel> packageArtifacts = Enumerable.Empty<PackageArtifactModel>();

                    var itemsToPushNoExcludes = ItemsToPush.
                        Where(i => !string.Equals(i.GetMetadata("ExcludeFromManifest"), "true", StringComparison.OrdinalIgnoreCase));

                    if (PublishFlatContainer)
                    {
                        // Act as if %(PublishFlatContainer) were true for all items.
                        blobArtifacts = itemsToPushNoExcludes
                            .Select(i => blobArtifactModelFactory.CreateBlobArtifactModel(i));
                        foreach (var blobItem in itemsToPushNoExcludes)
                        {
                            if (!fileSystem.FileExists(blobItem.ItemSpec))
                            {
                                Log.LogError($"Could not find file {blobItem.ItemSpec}.");
                                continue;
                            }

                            // test
                            Log.LogMessage(MessageImportance.High, $"PublishFlatContainer is true - publishing blob: {blobItem.ItemSpec}");
                            PushToLocalStorageOrAzDO(ItemType.BlobArtifact, blobItem.ItemSpec);
                        }
                    }
                    else
                    {
                        ITaskItem[] symbolItems = itemsToPushNoExcludes
                            .Where(i => i.ItemSpec.EndsWith("symbols.nupkg"))
                            .Select(i =>
                            {
                                string fileName = Path.GetFileName(i.ItemSpec);
                                i.SetMetadata("RelativeBlobPath", $"{AssetsVirtualDir}symbols/{fileName}");
                                return i;
                            })
                            .ToArray();

                        // test
                        foreach (ITaskItem item in symbolItems)
                        {
                            Log.LogMessage(MessageImportance.High, $"Symbol: {item.ItemSpec}");
                        }

                        var blobItems = itemsToPushNoExcludes
                            .Where(i =>
                            {
                                var isFlatString = i.GetMetadata("PublishFlatContainer");
                                if (!string.IsNullOrEmpty(isFlatString) &&
                                    bool.TryParse(isFlatString, out var isFlat))
                                {
                                    return isFlat;
                                }

                                return false;
                            })
                            .Union(symbolItems)
                            .ToArray();

                        // test
                        foreach (var b in blobItems)
                        {
                            Log.LogMessage(MessageImportance.High, $"BlobItem: {b.ItemSpec}");
                        }

                        ITaskItem[] packageItems = itemsToPushNoExcludes
                            .Except(blobItems)
                            .ToArray();

                        foreach (var packagePath in packageItems)
                        {
                            if (!fileSystem.FileExists(packagePath.ItemSpec))
                            {
                                Log.LogError($"Could not find file {packagePath.ItemSpec}.");
                                continue;
                            }

                            // test
                            Log.LogMessage(MessageImportance.High, $"Publish package artifact: {packagePath.ItemSpec}");
                            PushToLocalStorageOrAzDO(ItemType.PackageArtifact, packagePath.ItemSpec);
                        }

                        foreach (var blobItem in blobItems)
                        {
                            if (!fileSystem.FileExists(blobItem.ItemSpec))
                            {
                                Log.LogError($"Could not find file {blobItem.ItemSpec}.");
                                continue;
                            }

                            // test
                            Log.LogMessage(MessageImportance.High, $"Publish blob artifact: {blobItem.ItemSpec}");
                            PushToLocalStorageOrAzDO(ItemType.BlobArtifact, blobItem.ItemSpec);
                        }

                        packageArtifacts = packageItems.Select(packageArtifactModelFactory.CreatePackageArtifactModel);
                        blobArtifacts = blobItems.Select(i => blobArtifactModelFactory.CreateBlobArtifactModel(i)).Where(blob => blob != null);
                    }

                    PublishingInfraVersion targetPublishingVersion = PublishingInfraVersion.Latest;

                    if (!string.IsNullOrEmpty(PublishingVersion))
                    {
                        if (!Enum.TryParse(PublishingVersion, ignoreCase: true, out targetPublishingVersion))
                        {
                            Log.LogError($"Could not parse publishing infra version '{PublishingVersion}'");
                        }
                    }
                    
                    SigningInformationModel signingInformationModel = signingInformationModelFactory.CreateSigningInformationModelFromItems(
                        ItemsToSign, StrongNameSignInfo, FileSignInfo, FileExtensionSignInfo, CertificatesSignInfo, blobArtifacts, packageArtifacts);

                    buildModelFactory.CreateBuildManifest(
                        blobArtifacts,
                        packageArtifacts,
                        AssetManifestPath,
                        !string.IsNullOrEmpty(ManifestRepoName) ? ManifestRepoName : ManifestRepoUri,
                        ManifestBuildId,
                        ManifestBranch,
                        ManifestCommit,
                        ManifestBuildData,
                        IsStableBuild,
                        targetPublishingVersion,
                        IsReleaseOnlyPackageVersion,
                        signingInformationModel: signingInformationModel);

                    PushToLocalStorageOrAzDO(ItemType.AssetManifest, AssetManifestPath);
                }
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }

            return !Log.HasLoggedErrors;
        }

        private void PushToLocalStorageOrAzDO(ItemType itemType, string itemSpec)
        {
            if (PushToAzDO)
            {
                switch (itemType)
                {
                    case ItemType.AssetManifest:
                        Log.LogMessage(MessageImportance.High,
                            $"##vso[artifact.upload containerfolder=AssetManifests;artifactname=AssetManifests]{itemSpec}");
                        break;

                    case ItemType.PackageArtifact:
                        Log.LogMessage(MessageImportance.High,
                            $"##vso[artifact.upload containerfolder=PackageArtifacts;artifactname=PackageArtifacts]{itemSpec}");
                        break;

                    case ItemType.BlobArtifact:
                        Log.LogMessage(MessageImportance.High,
                            $"##vso[artifact.upload containerfolder=BlobArtifacts;artifactname=BlobArtifacts]{itemSpec}");
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(itemType));
                }
            }

            if (PushToLocalStorage)
            {
                string filename = Path.GetFileName(itemSpec);
                switch (itemType)
                {
                    case ItemType.AssetManifest:
                        File.Copy(itemSpec, Path.Combine(AssetManifestLocalStoragePath, filename), true);
                        break;

                    case ItemType.PackageArtifact:
                        if (itemSpec.Contains("\\NonShipping\\"))
                        {
                            File.Copy(itemSpec, Path.Combine(NonShippingPackagesLocalStoragePath, filename), true);
                        }
                        else
                        {
                            File.Copy(itemSpec, Path.Combine(ShippingPackagesLocalStoragePath, filename), true);
                        }
                        break;

                    case ItemType.BlobArtifact:
                        File.Copy(itemSpec, Path.Combine(AssetsLocalStoragePath, filename), true);
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(itemType));
                }
            }
        }
    }
}
