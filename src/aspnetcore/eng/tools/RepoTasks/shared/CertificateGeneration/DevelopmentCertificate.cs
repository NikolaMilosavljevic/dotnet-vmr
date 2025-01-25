// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#if NETCOREAPP
using Microsoft.AspNetCore.Certificates.Generation;
#else
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
#endif

#pragma warning disable IDE0161 // Convert to file-scoped namespace (not supported on .NET FX)
namespace RepoTasks
{

    public readonly struct DevelopmentCertificate
    {
        public DevelopmentCertificate(string certificatePath, string certificatePassword, string certificateThumbprint)
        {
            CertificatePath = certificatePath;
            CertificatePassword = certificatePassword;
            CertificateThumbprint = certificateThumbprint;
        }

        public string CertificatePath { get; }
        public string CertificatePassword { get; }
        public string CertificateThumbprint { get; }

        public static DevelopmentCertificate Create(string certificatePath)
        {
            var certificatePassword = "";
            var certificateThumbprint = EnsureDevelopmentCertificates(certificatePath, certificatePassword);

            return new DevelopmentCertificate(certificatePath, certificatePassword, certificateThumbprint);
        }

#if NETCOREAPP
        private static string EnsureDevelopmentCertificates(string certificatePath, string certificatePassword)
        {
            var now = DateTimeOffset.Now;
            var manager = CertificateManager.Instance;
            var certificate = manager.CreateAspNetCoreHttpsDevelopmentCertificate(now, now.AddYears(1));
            var certificateThumbprint = certificate.Thumbprint;
            manager.ExportCertificate(certificate, path: certificatePath, includePrivateKey: true, certificatePassword, CertificateKeyExportFormat.Pfx);

            return certificateThumbprint;
        }
#else
        private static string EnsureDevelopmentCertificates(string certificatePath, string certificatePassword)
        {
            var now = DateTimeOffset.Now;

            var certificate = CreateAspNetCoreHttpsDevelopmentCertificate(now, now.AddYears(1));
            var certificateThumbprint = certificate.Thumbprint;

            // Create PFX (PKCS #12) with private key
            File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pfx, certificatePassword));

            return certificateThumbprint;
        }

        private static X509Certificate2 CreateAspNetCoreHttpsDevelopmentCertificate(DateTimeOffset notBefore, DateTimeOffset notAfter)
        {
            const int CurrentAspNetCoreCertificateVersion = 2;

            // OID and other constants used for HTTPS certs
            const string AspNetHttpsOid = "1.3.6.1.4.1.311.84.1.1";
            const string AspNetHttpsOidFriendlyName = "ASP.NET Core HTTPS development certificate";
            const string ServerAuthenticationEnhancedKeyUsageOid = "1.3.6.1.5.5.7.3.1";
            const string ServerAuthenticationEnhancedKeyUsageOidFriendlyName = "Server Authentication";
            const string LocalhostHttpsDnsName = "localhost";
            const string LocalhostHttpsDistinguishedName = "CN=" + LocalhostHttpsDnsName;
            int AspNetHttpsCertificateVersion = CurrentAspNetCoreCertificateVersion;

            var subject = new X500DistinguishedName(LocalhostHttpsDistinguishedName);
            var extensions = new List<X509Extension>();
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(LocalhostHttpsDnsName);

            var keyUsage = new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, critical: true);
            var enhancedKeyUsage = new X509EnhancedKeyUsageExtension(
                new OidCollection() {
                    new Oid(
                        ServerAuthenticationEnhancedKeyUsageOid,
                        ServerAuthenticationEnhancedKeyUsageOidFriendlyName)
                },
                critical: true);

            var basicConstraints = new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true);

            byte[] bytePayload;

            if (AspNetHttpsCertificateVersion != 0)
            {
                bytePayload = new byte[1];
                bytePayload[0] = (byte)AspNetHttpsCertificateVersion;
            }
            else
            {
                bytePayload = Encoding.ASCII.GetBytes(AspNetHttpsOidFriendlyName);
            }

            var aspNetHttpsExtension = new X509Extension(
                new AsnEncodedData(
                    new Oid(AspNetHttpsOid, AspNetHttpsOidFriendlyName),
                    bytePayload),
                critical: false);

            extensions.Add(basicConstraints);
            extensions.Add(keyUsage);
            extensions.Add(enhancedKeyUsage);
            extensions.Add(sanBuilder.Build(critical: true));
            extensions.Add(aspNetHttpsExtension);

            var certificate = CreateSelfSignedCertificate(subject, extensions, notBefore, notAfter);
            return certificate;
        }

        private static X509Certificate2 CreateSelfSignedCertificate(
            X500DistinguishedName subject,
            IEnumerable<X509Extension> extensions,
            DateTimeOffset notBefore,
            DateTimeOffset notAfter)
        {
            const int RSAMinimumKeySizeInBits = 2048;

            using (var key = CreateKeyMaterial(RSAMinimumKeySizeInBits))
            {
                var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                foreach (var extension in extensions)
                {
                    request.CertificateExtensions.Add(extension);
                }

                var result = request.CreateSelfSigned(notBefore, notAfter);
                return result;
            }
        }

        private static RSA CreateKeyMaterial(int minimumKeySize)
        {
            var rsa = RSA.Create(minimumKeySize);
            if (rsa.KeySize < minimumKeySize)
            {
                throw new InvalidOperationException($"Failed to create a key with a size of {minimumKeySize} bits");
            }

            return rsa;
        }
#endif
    }
}
#pragma warning restore IDE0161 // Convert to file-scoped namespace

