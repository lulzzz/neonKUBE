﻿//-----------------------------------------------------------------------------
// FILE:	    MainForm.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Neon;
using Neon.Common;
using Neon.Kube;

namespace WinDesktop
{
    /// <summary>
    /// The main application form.  Note that this form will always be hidden.
    /// </summary>
    public partial class MainForm : Form
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the current (and only) main form instance so that other
        /// parts of the app can manipulate the UI.
        /// </summary>
        public static MainForm Current { get; private set; }

        //---------------------------------------------------------------------
        // Instance members

        private const double animationFrameRate = 2;
        private const string headendError       = "Unable to contact the neonKUBE headend service.";

        private Icon            disconnectedIcon;
        private Icon            connectedIcon;
        private AnimatedIcon    connectingAnimation;
        private AnimatedIcon    workingAnimation;
        private int             animationNesting;
        private ContextMenu     menu;

        /// <summary>
        /// Constructor.
        /// </summary>
        public MainForm()
        {
            MainForm.Current = this;

            InitializeComponent();

            Load  += MainForm_Load;
            Shown += (s, a) => Visible = false; // The main form should always be hidden

            // Preload the notification icons and animations for better performance.

            connectedIcon       = new Icon(@"Images\connected.ico");
            disconnectedIcon    = new Icon(@"Images\disconnected.ico");
            connectingAnimation = AnimatedIcon.Load("Images", "connecting", animationFrameRate);
            workingAnimation    = AnimatedIcon.Load("Images", "working", animationFrameRate);

            IsConnected = false;

            // Initialize the client state.

            Headend = new HeadendClient();
            KubeHelper.LoadClientConfig();
        }

        /// <summary>
        /// Indicates whether the application is connected to a cluster.
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// Returns the neonKUBE head client to be used to query the headend services.
        /// </summary>
        public HeadendClient Headend { get; private set; }

        /// <summary>
        /// Handles form initialization.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void MainForm_Load(object sender, EventArgs args)
        {
            // Set the text labels on the main form.  Nobody should ever see
            // this because the form should remain hidden but we'll put something
            // here just in case.

            productNameLabel.Text  = $"{Build.ProductName}  v{Build.ProductVersion}";
            copyrightLabel.Text    = Build.Copyright;
            licenseLinkLabel.Text  = Build.ProductLicense;

            // Initialize the notify icon and its context memu.
            
            notifyIcon.Text        = Build.ProductName;
            notifyIcon.Icon        = disconnectedIcon;
            notifyIcon.ContextMenu = menu = new ContextMenu();
            notifyIcon.Visible     = true;

            menu.Popup            += Menu_Popup;
        }

        /// <summary>
        /// Handles license link clicks.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void licenseLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs args)
        {
            NeonHelper.OpenBrowser(Build.ProductLicenseUrl);
        }

        /// <summary>
        /// Intercept the window close event and minimize it instead.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void MainForm_FormClosing(object sender, FormClosingEventArgs args)
        {
            // The main form should always be hidden but we'll 
            // implement this just in case.

            args.Cancel  = true;
            this.Visible = false;
        }

        /// <summary>
        /// Starts a notify icon animation.
        /// </summary>
        /// <param name="animatedIcon">The icon animation.</param>
        /// <remarks>
        /// Calls to this method may be recursed and should be matched 
        /// with a call to <see cref="StopWorkingAnimnation"/>.  The
        /// amimation will actually stop when the last matching
        /// <see cref="StartWorkingAnimation"/> call was matched with
        /// the last <see cref="StopWorkingAnimnation"/>.
        /// </remarks>
        private void StartNotifyAnimation(AnimatedIcon animatedIcon)
        {
            Covenant.Requires<ArgumentNullException>(animatedIcon != null);

            if (animationNesting == 0)
            {
                animationTimer.Interval = (int)TimeSpan.FromSeconds(1 / animatedIcon.FrameRate).TotalMilliseconds;
                animationTimer.Tick    +=
                    (s, a) =>
                    {
                        notifyIcon.Icon = animatedIcon.GetNextFrame();
                    };

                animationTimer.Start();
            }

            animationNesting++;
        }

        /// <summary>
        /// Stops the notify icon animation.
        /// </summary>
        /// <param name="force">Optionally force the animation to stop regardless of the nesting level.</param>
        private void StopNotifyAnimation(bool force = false)
        {
            if (force)
            {
                if (animationNesting > 0)
                {
                    animationTimer.Stop();
                    notifyIcon.Icon = IsConnected ? connectedIcon : disconnectedIcon;
                    animationNesting = 0;
                }

                return;
            }

            if (animationNesting == 0)
            {
                throw new InvalidOperationException("StopNotifyAnimation: Stack underflow.");
            }

            if (--animationNesting == 0)
            {
                animationTimer.Stop();
                notifyIcon.Icon = IsConnected ? connectedIcon : disconnectedIcon;
            }
        }

        //---------------------------------------------------------------------
        // Menu commands

        /// <summary>
        /// Poulates the context menu when it is clicked, based on the current
        /// application state.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void Menu_Popup(object sender, EventArgs args)
        {
            menu.MenuItems.Clear();

            // Append submenus for each of the cluster contexts that have
            // neonKUBE extensions.  We're not going to try to manage 
            // non-neonKUBE clusters.
            //
            // Put a check mark next to the logged in cluster (if there
            // is one) and also enable [Logout] if we're logged in.

            var contexts = KubeHelper.Config.Contexts
                .Where(c => c.Extensions != null)
                .OrderBy(c => c.Name)
                .ToArray();

            var currentContextName = (string)KubeHelper.CurrentContextName;
            var loggedIn           = !string.IsNullOrEmpty(currentContextName);;

            if (contexts.Length > 0)
            {
                var contextsMenu = new MenuItem(loggedIn ? currentContextName : "login to cluster") { Checked = loggedIn };

                contextsMenu.RadioCheck = loggedIn;

                if (loggedIn)
                {
                    contextsMenu.MenuItems.Add(new MenuItem(currentContextName) { Checked = true });
                }

                var addedContextsSeparator = false;

                foreach (var context in contexts.Where(c => c.Name != currentContextName))
                {
                    if (!addedContextsSeparator)
                    {
                        contextsMenu.MenuItems.Add("-");
                        addedContextsSeparator = true;
                    }

                    contextsMenu.MenuItems.Add(new MenuItem(context.Name, OnClusterContext));
                }

                contextsMenu.MenuItems.Add("-");
                contextsMenu.MenuItems.Add(new MenuItem("Logout", OnLogoutCommand) { Enabled = loggedIn });

                menu.MenuItems.Add(contextsMenu);
            }

            // Append cluster-specific menus.

            menu.MenuItems.Add("-");

            var dashboardsMenu = new MenuItem("Dashboard") { Enabled = loggedIn };

            dashboardsMenu.MenuItems.Add(new MenuItem("Kubernetes", OnKubernetesDashboardCommand) { Enabled = loggedIn });

            var addedDashboardSeparator = false;

            if (KubeHelper.CurrentContext.Extensions.ClusterDefinition.Ceph.Enabled)
            {
                if (!addedDashboardSeparator)
                {
                    dashboardsMenu.MenuItems.Add(new MenuItem("-"));
                    addedDashboardSeparator = true;
                }

                dashboardsMenu.MenuItems.Add(new MenuItem("Ceph", OnCephDashboardCommand) { Enabled = loggedIn });
            }

            if (KubeHelper.CurrentContext.Extensions.ClusterDefinition.EFK.Enabled)
            {
                if (!addedDashboardSeparator)
                {
                    dashboardsMenu.MenuItems.Add(new MenuItem("-"));
                    addedDashboardSeparator = true;
                }

                dashboardsMenu.MenuItems.Add(new MenuItem("Kibana", OnKibanaDashboardCommand) { Enabled = loggedIn });
            }

            if (KubeHelper.CurrentContext.Extensions.ClusterDefinition.Prometheus.Enabled)
            {
                if (!addedDashboardSeparator)
                {
                    dashboardsMenu.MenuItems.Add(new MenuItem("-"));
                    addedDashboardSeparator = true;
                }

                dashboardsMenu.MenuItems.Add(new MenuItem("Prometheus", OnPrometheusDashboardCommand) { Enabled = loggedIn });
            }

            menu.MenuItems.Add(dashboardsMenu);

            // Append the static commands.

            menu.MenuItems.Add("-");
            menu.MenuItems.Add(new MenuItem("GitHub", OnGitHubCommand));
            menu.MenuItems.Add(new MenuItem("Help", OnHelpCommand));
            menu.MenuItems.Add(new MenuItem("About", OnAboutCommand));
            menu.MenuItems.Add("-");
            menu.MenuItems.Add(new MenuItem("Settings", OnSettingsCommand));
            menu.MenuItems.Add(new MenuItem("Check for Updates", OnCheckForUpdatesCommand));
            menu.MenuItems.Add("-");
            menu.MenuItems.Add(new MenuItem("Exit", OnExitCommand));
        }

        /// <summary>
        /// Handles the <b>Github</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private async void OnGitHubCommand(object sender, EventArgs args)
        {
            StartNotifyAnimation(workingAnimation);

            try
            {
                var clientInfo = await Headend.GetClientInfoAsync();

                NeonHelper.OpenBrowser(clientInfo.GitHubUrl);
            }
            catch
            {
                MessageBox.Show(headendError, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                StopNotifyAnimation();
            }
        }

        /// <summary>
        /// Handles the <b>Help</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private async void OnHelpCommand(object sender, EventArgs args)
        {
            StartNotifyAnimation(workingAnimation);

            try
            {
                var clientInfo = await Headend.GetClientInfoAsync();

                NeonHelper.OpenBrowser(clientInfo.HelpUrl);
            }
            catch
            {
                MessageBox.Show(headendError, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                StopNotifyAnimation();
            }
        }

        /// <summary>
        /// Handles the <b>About</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnAboutCommand(object sender, EventArgs args)
        {
            var aboutBox = new AboutBox();

            aboutBox.ShowDialog();
        }

        /// <summary>
        /// Handles the <b>Settings</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnSettingsCommand(object sender, EventArgs args)
        {
        }

        /// <summary>
        /// Handles the <b>Settings</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private async void OnCheckForUpdatesCommand(object sender, EventArgs args)
        {
            StartNotifyAnimation(workingAnimation);

            try
            {
                var clientInfo = await Headend.GetClientInfoAsync();

                if (clientInfo.UpdateVersion == null)
                {
                    MessageBox.Show("The latest version of neonKUBE is installed.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("$todo(jeff.lill): Not implemented yet.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch
            {
                MessageBox.Show("Update check failed", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                StopNotifyAnimation();
            }
        }

        /// <summary>
        /// Handles cluster context commands.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnClusterContext(object sender, EventArgs args)
        {
        }

        /// <summary>
        /// Handles the <b>Logout</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnLogoutCommand(object sender, EventArgs args)
        {
        }

        /// <summary>
        /// Handles the <b>Kubernetes Dashboard</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnKubernetesDashboardCommand(object sender, EventArgs args)
        {
            MessageBox.Show("$todo(jeff.lill): Not implemented yet.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>
        /// Handles the <b>Ceph Dashboard</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnCephDashboardCommand(object sender, EventArgs args)
        {
            MessageBox.Show("$todo(jeff.lill): Not implemented yet.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>
        /// Handles the <b>Kibana Dashboard</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnKibanaDashboardCommand(object sender, EventArgs args)
        {
            MessageBox.Show("$todo(jeff.lill): Not implemented yet.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>
        /// Handles the <b>Prometheus Dashboard</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnPrometheusDashboardCommand(object sender, EventArgs args)
        {
            MessageBox.Show("$todo(jeff.lill): Not implemented yet.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>
        /// Handles the <b>Exit</b> command.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The arguments.</param>
        private void OnExitCommand(object sender, EventArgs args)
        {
            StopNotifyAnimation(force: true);
            notifyIcon.Visible = false;
            Environment.Exit(0);
        }
    }
}
