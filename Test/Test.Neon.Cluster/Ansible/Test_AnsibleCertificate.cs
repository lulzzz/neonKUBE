﻿//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleCertificate.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Xunit;
using Xunit.Neon;

namespace TestNeonCluster
{
    public class Test_AnsibleCertificate : IClassFixture<ClusterFixture>
    {
        private ClusterFixture cluster;

        public Test_AnsibleCertificate(ClusterFixture cluster)
        {
            this.cluster = cluster;

            // We're going to use unique certificate names for each
            // test so we only need to reset the test fixture once 
            // for all tests implemented by this class.

            cluster.LoginAndInitialize(login: null);
        }

        /// <summary>
        /// Returns the certificate and private key as text indented 
        /// by 10 spaces so that it can be included within a playbook
        /// beneath the <b>values:</b> argument.  This is sensitive
        /// to how the playbooks are formatted in the tests below.
        /// </summary>
        /// <param name="certificate">The certificate.</param>
        /// <returns>The indented PEM text.</returns>
        private string GetIndentedPem(TlsCertificate certificate)
        {
            var indent = new string(' ', 10);
            var sb     = new StringBuilder();

            using (var reader = new StringReader(certificate.CombinedPem))
            {
                foreach (var line in reader.Lines())
                {
                    sb.AppendLine($"{indent}{line}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Ensures that lines in the input string are normalized to
        /// Linux-style endings.
        /// </summary>
        /// <param name="value">Input value.</param>
        /// <returns>The value with normalized line endings.</returns>
        private string NormalizeLineEndings(string value)
        {
            var sb = new StringBuilder();

            using (var reader = new StringReader(value))
            {
                foreach (var line in reader.Lines())
                {
                    sb.AppendLineLinux(line);
                }
            }

            return sb.ToString();
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Create()
        {
            var name        = "cert-" + Guid.NewGuid().ToString("D");
            var certificate = TlsCertificate.CreateSelfSigned("test.com");
            var certPem     = GetIndentedPem(certificate);
            var playbook    =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: create cert
      neon_certificate:
        name: {name}
        value: |
{certPem}
        state: present
";
            // Create a new certificate.

            var results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("create cert");

            Assert.NotNull(taskResult);
            Assert.Equal("create cert", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListCertificates().Where(c => c == name));

            // Verify that the certificate was persisted correctly.

            var response = cluster.NeonExecute("cert", "get", name);

            Assert.Equal(0, response.ExitCode);

            var savedPem = response.OutputText;

            Assert.Equal(NormalizeLineEndings(certificate.CombinedPem), NormalizeLineEndings(savedPem));

            // Run the playbook again but this time nothing should
            // be changed because the certificate already exists
            // and is unchanged.

            results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("create cert");

            Assert.NotNull(taskResult);
            Assert.Equal("create cert", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.Single(cluster.ListCertificates().Where(c => c == name));

            // Generate a new certificate and verify that we can update
            // an existing cert.

            certificate = TlsCertificate.CreateSelfSigned("test.com");
            certPem     = GetIndentedPem(certificate);
            playbook    =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: create cert
      neon_certificate:
        name: {name}
        value: |
{certPem}
        state: present
";
            results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("create cert");

            Assert.NotNull(taskResult);
            Assert.Equal("create cert", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListCertificates().Where(c => c == name));

            // Verify that the certificate was persisted correctly.

            response = cluster.NeonExecute("cert", "get", name);

            Assert.Equal(0, response.ExitCode);

            savedPem = response.OutputText;

            Assert.Equal(NormalizeLineEndings(certificate.CombinedPem), NormalizeLineEndings(savedPem));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Remove()
        {
            var name        = "cert-" + Guid.NewGuid().ToString("D");
            var certificate = TlsCertificate.CreateSelfSigned("test.com");
            var certPem     = GetIndentedPem(certificate);
            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: create cert
      neon_certificate:
        name: {name}
        value: |
{certPem}
        state: present
";
            // Create a new cert.

            var results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("create cert");

            Assert.NotNull(taskResult);
            Assert.Equal("create cert", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Single(cluster.ListCertificates().Where(c => c == name));

            // Now remove it.

            playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: remove cert
      neon_certificate:
        name: {name}
        state: absent
";
            results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("remove cert");

            Assert.NotNull(taskResult);
            Assert.Equal("remove cert", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
            Assert.Empty(cluster.ListCertificates().Where(c => c == name));

            // Remove it again to verify that nothing changes.

            results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("remove cert");

            Assert.NotNull(taskResult);
            Assert.Equal("remove cert", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.Empty(cluster.ListCertificates().Where(c => c == name));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void ErrorNoName()
        {
            // Verify that the module ensures that the [name] argument is present.

            var name        = "cert-" + Guid.NewGuid().ToString("D");
            var certificate = TlsCertificate.CreateSelfSigned("test.com");
            var certPem     = GetIndentedPem(certificate);
            var playbook    =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: create cert
      neon_certificate:
        value: |
{certPem}
        state: absent
";
            var results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("create cert");

            Assert.NotNull(taskResult);
            Assert.Equal("create cert", taskResult.TaskName);
            Assert.False(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.Empty(cluster.ListCertificates().Where(c => c == name));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void ErrorNoValue()
        {
            // Verify that the module ensures that the [value] argument is present.

            var name        = "cert-" + Guid.NewGuid().ToString("D");
            var certificate = TlsCertificate.CreateSelfSigned("test.com");
            var certPem     = GetIndentedPem(certificate);
            var playbook    =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: create cert
      neon_certificate:
        name: {name}
        value: |
{certPem}
        state: absent
";
            var results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("create cert");

            Assert.NotNull(taskResult);
            Assert.Equal("create cert", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
            Assert.Empty(cluster.ListCertificates().Where(c => c == name));
        }
    }
}