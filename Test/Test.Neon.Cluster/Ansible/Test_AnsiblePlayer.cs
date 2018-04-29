﻿//-----------------------------------------------------------------------------
// FILE:	    Test_AnsiblePlayer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

using Xunit;
using Xunit.Neon;

namespace TestNeonCluster
{
    public class Test_AnsiblePlayer : IClassFixture<ClusterFixture>
    {
        private ClusterFixture cluster;

        public Test_AnsiblePlayer(ClusterFixture cluster)
        {
            this.cluster = cluster;

            // NOTE: These tests do not require a cluster reset every time.

            cluster.Initialize(login: null);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Change()
        {
            var name     = "secret-" + Guid.NewGuid().ToString("D");
            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: create secret
      neon_docker_secret:
        name: {name}
        state: present
        text: password
";
            var results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("create secret");

            Assert.NotNull(taskResult);
            Assert.Equal("create secret", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void NoChange()
        {
            var name = "secret-" + Guid.NewGuid().ToString("D");
            var playbook =
$@"
- name: test
  hosts: localhost
  tasks:
    - name: create secret
      neon_docker_secret:
        name: {name}
        state: present
        text: password
";
            var results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            var taskResult = results.GetTaskResult("create secret");

            Assert.NotNull(taskResult);
            Assert.Equal("create secret", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.True(taskResult.Changed);

            // Run the playbook again.  This time it shouldn't 
            // change anything because the secret already exists.

            results = AnsiblePlayer.NeonPlay(playbook);

            Assert.NotNull(results);

            taskResult = results.GetTaskResult("create secret");

            Assert.NotNull(taskResult);
            Assert.Equal("create secret", taskResult.TaskName);
            Assert.True(taskResult.Success);
            Assert.False(taskResult.Changed);
        }
    }
}