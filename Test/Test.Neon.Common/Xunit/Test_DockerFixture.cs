﻿//-----------------------------------------------------------------------------
// FILE:	    Test_DockerFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_DockerFixture : IClassFixture<DockerFixture>
    {
        private DockerFixture docker;

        public Test_DockerFixture(DockerFixture fixture)
        {
            this.docker = fixture;

            docker.Initialize(
                () =>
                {
                    docker.CreateSecret("secret_text", "hello");
                    docker.CreateSecret("secret_data", new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                    docker.CreateConfig("config_text", "hello");
                    docker.CreateConfig("config_data", new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

                    docker.CreateService("test-service", "neoncluster/test");

                    var composeText =
@"version: '3'

services:
  sleeper:
    image: neoncluster/test
    deploy:
      replicas: 2
";
                    docker.DeployStack("test-stack", composeText);
                    docker.RunContainer("test-container", "neoncluster/test");
                    docker.CreateNetwork("test-network");
                });
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Basics()
        {
            // Verify that the secrets, configs, networks, container, service, and stack were created.

            Assert.Single(docker.ListSecrets().Where(item => item.Name == "secret_text"));
            Assert.Single(docker.ListSecrets().Where(item => item.Name == "secret_data"));

            Assert.Single(docker.ListConfigs().Where(item => item.Name == "config_text"));
            Assert.Single(docker.ListConfigs().Where(item => item.Name == "config_data"));

            Assert.Single(docker.ListServices().Where(item => item.Name == "test-service"));

            Assert.Single(docker.ListStacks().Where(item => item.Name == "test-stack"));
            Assert.Equal(1, docker.ListStacks().First().ServiceCount);

            Assert.Single(docker.ListContainers().Where(item => item.Name == "test-container"));
            Assert.Single(docker.ListContainers().Where(item => item.Name.StartsWith("test-stack_sleeper.1.")));

            // Verify that restarting a service doesn't barf.

            docker.RestartService("test-service");
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCluster)]
        public void ClearVolumes()
        {
            //-----------------------------------------------------------------
            // Create a test volume on the cluster node and then verify
            // that ClearVolumes() removes them.

            docker.DockerExecute("docker volume create test-volume");
            docker.ClearVolumes();

            var response = docker.DockerExecute("volume ls --format \"{{.Name}}\"");

            Assert.Equal(0, response.ExitCode);
            Assert.DoesNotContain("test-volume", response.OutputText);
        }
    }
}