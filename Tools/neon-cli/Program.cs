﻿//-----------------------------------------------------------------------------
// FILE:	    Program.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon;
using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.IO;
using Neon.Windows;
using Neon.Xunit.Kube;

namespace NeonCli
{
    /// <summary>
    /// This tool is used to configure the nodes of a cluster.
    /// See <b>$/Doc/Ubuntu-18.04 cluster Deploy.docx</b> for more information.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The program version.
        /// </summary>
        public const string Version = Build.ProductVersion;
    
        /// <summary>
        /// CURL command common options.
        /// </summary>
        public const string CurlOptions = "-4fsSLv --retry 10 --retry-delay 30"; 

        /// <summary>
        /// Program entry point.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The exit code.</returns>
        public static int Main(params string[] args)
        {
            string usage = $@"
neonKUBE Management Tool: neon [v{Program.Version}]
{Build.Copyright}

USAGE:

    neon [OPTIONS] COMMAND [ARG...]

COMMAND SUMMARY:

    neon help               COMMAND
    neon cluster prepare    [CLUSTER-DEF]
    neon cluster setup      [CLUSTER-DEF]
    neon couchbase          COMMNAND
    neon generate models    [OPTIONS] ASSEMBLY-PATH [OUTPUT-PATH]
    neon login              COMMAND
    neon logout
    neon password           COMMAND
    neon prepare            COMMAND
    neon run                -- COMMAND
    neon scp                [NODE]
    neon ssh                [NODE]
    neon vault              COMMAND
    neon version            [-n] [--git] [--minimum=VERSION]

ARGUMENTS:

    CLUSTER-DEF         - Path to a cluster definition file.  This is
                          optional for some commands when logged in
    COMMAND             - Subcommand and arguments.
    NODE                - A node name.

OPTIONS:

    --help                              - Display help
    --log-folder=LOG-FOLDER             - Optional log folder path

    -m=COUNT, --max-parallel=COUNT      - Maximum number of nodes to be 
                                          configured in parallel [default=6]

    --machine-password=PASSWORD         - Overrides default initial machine
                                          password: sysadmin0000

    --machine-username=USERNAME         - Overrides default initial machine
                                          username: sysadmin
    -q, --quiet                         - Disables operation progress

    -w=SECONDS, --wait=SECONDS          - Seconds to delay for cluster stablization 
                                          (defaults to 60s).
";
            // Disable any logging that might be performed by library classes.

            LogManager.Default.LogLevel = LogLevel.None;

            // Ensure that temporary files are written to the users temporary folder because
            // there's a decent chance that this folder will be encrypted at rest.

            if (KubeTestManager.Current == null)
            {
                TempFile.Root   = KubeHelper.TempFolder;
                TempFolder.Root = KubeHelper.TempFolder;
            }

            // Use the version of Powershell Core installed with the application,
            // if present.

            PowerShell.PwshPath = KubeHelper.PwshPath;

            // We need to verify that we're running with elevated permissions if we're not
            // shimmed into a Docker container.

            // $todo(jeff.lill):
            //
            // We're currently requiring elevated permissions for all commands, even those
            // that don't actually require elevated permissions.  We may wish to relax this
            // in the future.

            if (!KubeHelper.InToolContainer)
            {
                if (NeonHelper.IsWindows)
                {
                    var identity  = WindowsIdentity.GetCurrent();
                    var principal = new WindowsPrincipal(identity);

                    if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                    {
                        Console.Error.WriteLine("*** ERROR: This command requires elevated administrator permissions.");
                        Program.Exit(1);
                    }
                }
                else if (NeonHelper.IsOSX)
                {
                    throw new NotImplementedException("$todo(jeff.lill): Implement OSX elevated permissions check.");
                }
            }

            // Ensure that all of the cluster hosting manager implementations are loaded.

            new HostingManagerFactory(() => HostingLoader.Initialize());

            // Process the command line.

            try
            {
                ICommand command;

                CommandLine     = new CommandLine(args);
                LeftCommandLine = CommandLine.Split("--").Left;

                foreach (var cmdLine in new CommandLine[] { CommandLine, LeftCommandLine })
                {
                    cmdLine.DefineOption("--machine-username");
                    cmdLine.DefineOption("--machine-password");
                    cmdLine.DefineOption("-os").Default = "Ubuntu-18.04";
                    cmdLine.DefineOption("-q", "--quiet");
                    cmdLine.DefineOption("-m", "--max-parallel").Default = "6";
                    cmdLine.DefineOption("-w", "--wait").Default = "60";
                    cmdLine.DefineOption("--log-folder").Default = string.Empty;
                }

                var validOptions = new HashSet<string>();

                validOptions.Add("--debug");
                validOptions.Add("--help");
                validOptions.Add("--log-folder");
                validOptions.Add("--machine-username");
                validOptions.Add("--machine-password");
                validOptions.Add("-m");
                validOptions.Add("--max-parallel");
                validOptions.Add("-q");
                validOptions.Add("--quiet");
                validOptions.Add("-w");
                validOptions.Add("--wait");

                if (CommandLine.Arguments.Length == 0)
                {
                    Console.WriteLine(usage);
                    Program.Exit(0);
                }

                var commands = new List<ICommand>()
                {
                    new ClusterCommand(),
                    new ClusterPrepareCommand(),
                    new ClusterSetupCommand(),
                    new ClusterVerifyCommand(),
                    new CouchbaseCommand(),
                    new CouchbaseQueryCommand(),
                    new CouchbaseUpsertCommand(),
                    new GenerateCommand(),
                    new GenerateModelsCommand(),
                    new LoginCommand(),
                    new LoginExportCommand(),
                    new LoginImportCommand(),
                    new LoginListCommand(),
                    new LoginRemoveCommand(),
                    new LogoutCommand(),
                    new PasswordCommand(),
                    new PasswordExportCommand(),
                    new PasswordGenerateCommand(),
                    new PasswordGetCommand(),
                    new PasswordImportCommand(),
                    new PasswordListCommand(),
                    new PasswordRemoveCommand(),
                    new PasswordSetCommand(),
                    new PrepareCommand(),
                    new PrepareNodeTemplateCommand(),
                    new RunCommand(),
                    new ScpCommand(),
                    new SshCommand(),
                    new VaultCommand(),
                    new VaultCreateCommand(),
                    new VaultDecryptCommand(),
                    new VaultEditCommand(),
                    new VaultEncryptCommand(),
                    new VaultPasswordNameCommand(),
                    new VersionCommand()
                };

                // Short-circuit the help command.

                if (CommandLine.Arguments[0] == "help")
                {
                    if (CommandLine.Arguments.Length == 1)
                    {
                        Console.WriteLine(usage);
                        Program.Exit(0);
                    }

                    CommandLine = CommandLine.Shift(1);

                    command = GetCommand(CommandLine, commands);

                    if (command == null)
                    {
                        Console.Error.WriteLine($"*** ERROR: Unexpected [{CommandLine.Arguments[0]}] command.");
                        Program.Exit(1);
                    }

                    command.Help();
                    Program.Exit(0);
                }

                // Lookup the command.

                command = GetCommand(CommandLine, commands);

                if (command == null)
                {
                    Console.Error.WriteLine($"*** ERROR: Unexpected [{CommandLine.Arguments[0]}] command.");
                    Program.Exit(1);
                }

                // Handle the logging options.

                LogPath = LeftCommandLine.GetOption("--log-folder");
                Quiet   = LeftCommandLine.GetFlag("--quiet");

                if (KubeHelper.InToolContainer)
                {
                    // We hardcode logging to [/log] inside [neon-cli] containers.

                    LogPath = "/log";
                }
                else if (!string.IsNullOrEmpty(LogPath))
                {
                    LogPath = Path.GetFullPath(LogPath);

                    Directory.CreateDirectory(LogPath);
                }
                else
                {
                    LogPath = KubeHelper.LogFolder;

                    // We can clear this folder because we know that there shouldn't be
                    // any other files in here.

                    NeonHelper.DeleteFolderContents(LogPath);
                }

                //-------------------------------------------------------------
                // Process the standard command line options.

                // Load the user name and password from the command line options, if present.

                MachineUsername = LeftCommandLine.GetOption("--machine-username", "sysadmin");
                MachinePassword = LeftCommandLine.GetOption("--machine-password", "sysadmin0000");

                // Handle the other options.

                var maxParallelOption = LeftCommandLine.GetOption("--max-parallel");
                int maxParallel;

                if (!int.TryParse(maxParallelOption, out maxParallel) || maxParallel < 1)
                {
                    Console.Error.WriteLine($"*** ERROR: [--max-parallel={maxParallelOption}] option is not valid.");
                    Program.Exit(1);
                }

                Program.MaxParallel = maxParallel;

                var     waitSecondsOption = LeftCommandLine.GetOption("--wait");
                double  waitSeconds;

                if (!double.TryParse(waitSecondsOption, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out waitSeconds) || waitSeconds < 0)
                {
                    Console.Error.WriteLine($"*** ERROR: [--wait={waitSecondsOption}] option is not valid.");
                    Program.Exit(1);
                }

                Program.WaitSeconds = waitSeconds;

                Debug = LeftCommandLine.HasOption("--debug");

                if (command.CheckOptions)
                {
                    // Ensure that there are no unexpected command line options.

                    foreach (var optionName in command.ExtendedOptions)
                    {
                        validOptions.Add(optionName);
                    }

                    foreach (var option in LeftCommandLine.Options)
                    {
                        if (!validOptions.Contains(option.Key))
                        {
                            var commandWords = string.Empty;

                            foreach (var word in command.Words)
                            {
                                if (commandWords.Length > 0)
                                {
                                    commandWords += " ";
                                }

                                commandWords += word;
                            }

                            Console.Error.WriteLine($"*** ERROR: [{commandWords}] command does not support [{option.Key}].");
                            Program.Exit(1);
                        }
                    }
                }

                // Run the command.

                if (command.NeedsSshCredentials(CommandLine))
                {
                    if (string.IsNullOrWhiteSpace(MachineUsername) || string.IsNullOrEmpty(MachinePassword))
                    {
                        Console.WriteLine();
                        Console.WriteLine("    Enter cluster SSH credentials:");
                        Console.WriteLine("    -------------------------------");
                    }

                    while (string.IsNullOrWhiteSpace(MachineUsername))
                    {
                        Console.Write("    username: ");
                        MachineUsername = Console.ReadLine();
                    }

                    while (string.IsNullOrEmpty(MachinePassword))
                    {
                        MachinePassword = NeonHelper.ReadConsolePassword("    password: ");
                    }
                }
                else
                {
                    // $hack(jeff.lill):
                    //
                    // Only the [neon cluster prepare ...] command recognizes the [--machine-username] and
                    // [--machine-password] options.  These can cause problems for other commands
                    // so we're going to set both to NULL here.
                    //
                    // It would be cleaner to enable these only for the prepare command but the SSH proxy
                    // authentication code is already a bit twisted and I don't want to mess with it.

                    MachineUsername = null;
                    MachinePassword = null;
                }

                if (command.SplitItem != null)
                {
                    // We don't shift the command line for pass-thru commands 
                    // because we don't want to change the order of any options.

                    command.Run(CommandLine);
                }
                else
                {
                    command.Run(CommandLine.Shift(command.Words.Length));
                }
            }
            catch (ProgramExitException e)
            {
                return e.ExitCode;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"*** ERROR: {NeonHelper.ExceptionError(e)}");
                Console.Error.WriteLine(string.Empty);
                return 1;
            }

            return 0;
        }

        /// <summary>
        /// Message written then a user is not logged into a cluster.
        /// </summary>
        public const string MustLoginMessage = "*** ERROR: You must first log into a cluster.";

        /// <summary>
        /// Returns the Git source code branch.
        /// </summary>
        public static string GitBranch => ThisAssembly.Git.Branch;

        /// <summary>
        /// Path to the WinSCP program executable.
        /// </summary>
        public static string WinScpPath
        {
            get { return Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)"), @"WinSCP\WinSCP.exe"); }
        }

        /// <summary>
        /// Path to the PuTTY program executable.
        /// </summary>
        public static string PuttyPath
        {
            get
            {
                // Look for a x64 or x86 version of PuTTY at their default install locations.

                var path = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles"), @"PuTTY\putty.exe");

                if (File.Exists(path))
                {
                    return path;
                }

                return Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)"), @"PuTTY\putty.exe");
            }
        }

        /// <summary>
        /// Attempts to match the command line to the <see cref="ICommand"/> to be used
        /// to implement the command.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        /// <param name="commands">The commands.</param>
        /// <returns>The command instance or <c>null</c>.</returns>
        private static ICommand GetCommand(CommandLine commandLine, List<ICommand> commands)
        {
            // Sort the commands in decending order by number of words in the
            // command (we want to match the longest sequence).

            foreach (var command in commands.OrderByDescending(c => c.Words.Length))
            {
                if (command.Words.Length > commandLine.Arguments.Length)
                {
                    // Not enough arguments to match the command.

                    continue;
                }

                var matches = true;

                for (int i = 0; i < command.Words.Length; i++)
                {
                    if (!string.Equals(command.Words[i], commandLine.Arguments[i]))
                    {
                        matches = false;
                        break;
                    }
                }

                if (!matches && command.AltWords != null)
                {
                    matches = true;

                    for (int i = 0; i < command.AltWords.Length; i++)
                    {
                        if (!string.Equals(command.AltWords[i], commandLine.Arguments[i]))
                        {
                            matches = false;
                            break;
                        }
                    }
                }

                if (matches)
                {
                    return command;
                }
            }

            // No match.

            return null;
        }

        /// <summary>
        /// Returns the program version as the Git branch and commit and an optional
        /// indication of whether the program was build from a dirty branch.
        /// </summary>
        public static string GitVersion
        {
            get
            {
                var version = $"{ThisAssembly.Git.Branch}-{ThisAssembly.Git.Commit}";

#pragma warning disable 162 // Unreachable code

                //if (ThisAssembly.Git.IsDirty)
                //{
                //    version += "-DIRTY";
                //}

#pragma warning restore 162 // Unreachable code

                return version;
            }
        }

        /// <summary>
        /// Exits the program returning the specified process exit code.
        /// </summary>
        /// <param name="exitCode">The exit code.</param>
        public static void Exit(int exitCode)
        {
            // Ensure that all sensitive files and folders are encrypted at rest.  We're 
            // running this after every command just to be super safe.

            KubeHelper.EncryptSensitiveFiles();
            throw new ProgramExitException(exitCode);
        }

        /// <summary>
        /// Returns the orignal program <see cref="CommandLine"/>.
        /// </summary>
        public static CommandLine CommandLine { get; private set; }

        /// <summary>
        /// Returns the part of the command line to the left of the [--] splitter
        /// or the entire command line if there is no splitter.
        /// </summary>
        public static CommandLine LeftCommandLine { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if the program was built from the production <b>PROD</b> 
        /// source code branch.
        /// </summary>
        public static bool IsRelease => ThisAssembly.Git.Branch.StartsWith("release-", StringComparison.InvariantCultureIgnoreCase);

        /// <summary>
        /// Returns the username used to secure the cluster nodes before they are setup.  This
        /// defaults to <b>sysadmin</b> which is used for the cluster machine templates.
        /// </summary>
        public static string MachineUsername { get; set; }

        /// <summary>
        /// The password used to secure the cluster nodes before they are setup.  This defaults
        /// to <b>sysadmin0000</b> which is used for the cluster machine templates.
        /// </summary>
        public static string MachinePassword { get; set; }

        /// <summary>
        /// Returns the log folder path or a <c>null</c> or empty string 
        /// to disable logging.
        /// </summary>
        public static string LogPath { get; set; }

        /// <summary>
        /// The maximum number of nodes to be configured in parallel.
        /// </summary>
        public static int MaxParallel { get; set; }

        /// <summary>
        /// The seconds to wait after operations that may need a stablization period.
        /// </summary>
        public static double WaitSeconds { get; set; }

        /// <summary>
        /// Indicates whether operation progress output is to be suppressed.
        /// </summary>
        public static bool Quiet { get; set; }

        /// <summary>
        /// Runs the command in DEBUG mode.
        /// </summary>
        public static bool Debug { get; set; }

        /// <summary>
        /// Creates a <see cref="SshProxy{TMetadata}"/> for the specified host and server name,
        /// configuring logging and the credentials as specified by the global command
        /// line options.
        /// </summary>
        /// <param name="name">The node name.</param>
        /// <param name="publicAddress">The node's public IP address or FQDN.</param>
        /// <param name="privateAddress">The node's private IP address.</param>
        /// <param name="appendToLog">
        /// Pass <c>true</c> to append to an existing log file (or create one if necessary)
        /// or <c>false</c> to replace any existing log file with a new one.
        /// </param>
        /// 
        /// <typeparam name="TMetadata">Defines the metadata type the command wishes to associate with the server.</typeparam>
        /// <returns>The <see cref="SshProxy{TMetadata}"/>.</returns>
        public static SshProxy<TMetadata> CreateNodeProxy<TMetadata>(string name, string publicAddress, IPAddress privateAddress, bool appendToLog)
            where TMetadata : class
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            var logWriter = (TextWriter)null;

            if (!string.IsNullOrEmpty(LogPath))
            {
                var path = Path.Combine(LogPath, name + ".log");

                logWriter = new StreamWriter(new FileStream(path, appendToLog ? FileMode.Append : FileMode.Create, appendToLog ? FileAccess.Write : FileAccess.ReadWrite));
            }

            SshCredentials sshCredentials;

            if (!string.IsNullOrEmpty(Program.MachineUsername) && !string.IsNullOrEmpty(Program.MachinePassword))
            {
                sshCredentials = SshCredentials.FromUserPassword(Program.MachineUsername, Program.MachinePassword);
            }
            else if (KubeHelper.CurrentContext != null)
            {
                sshCredentials = KubeHelper.CurrentContext.Extension.SshCredentials;
            }
            else
            {
                Console.Error.WriteLine("*** ERROR: Expected some node credentials.");
                Program.Exit(1);

                return null;
            }

            return new SshProxy<TMetadata>(name, publicAddress, privateAddress, sshCredentials, logWriter);
        }

        /// <summary>
        /// Returns a <see cref="ClusterProxy"/> for the current Kubernetes context.
        /// </summary>
        /// <returns>The <see cref="ClusterProxy"/>.</returns>
        /// <remarks>
        /// <note>
        /// This method will terminate the program with an error message when not logged
        /// into a neonKUBE cluster.
        /// </note>
        /// </remarks>
        public static ClusterProxy GetCluster()
        {
            if (KubeHelper.CurrentContext == null)
            {
                Console.Error.WriteLine("*** ERROR: You are not logged into a cluster.");
                Program.Exit(1);
            }

            return new ClusterProxy(KubeHelper.CurrentContext, Program.CreateNodeProxy<NodeDefinition>);
        }

        /// <summary>
        /// Returns the folder holding the Linux resource files for the target operating system.
        /// </summary>
        public static ResourceFiles.Folder LinuxFolder => ResourceFiles.Root.GetFolder("Ubuntu-18.04");

        /// <summary>
        /// Presents the user with a yes/no question and waits for a response.
        /// </summary>
        /// <param name="prompt">The question prompt.</param>
        /// <returns><c>true</c> if the answer is yes, <b>false</b> for no.</returns>
        public static bool PromptYesNo(string prompt)
        {
            try
            {
                while (true)
                {
                    Console.WriteLine();
                    Console.Write($"{prompt} [y/n]: ");

                    var key = Console.ReadKey().KeyChar;

                    Console.WriteLine();

                    if (key == 'y' || key == 'Y')
                    {
                        return true;
                    }
                    else if (key == 'n' || key == 'N')
                    {
                        return false;
                    }
                }
            }
            finally
            {
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Uses WinSCP to convert an OpenSSH PEM formatted key to the PPK format
        /// required by PuTTY/WinSCP.  This works only on Windows.
        /// </summary>
        /// <param name="kubeLogin">The related cluster login information.</param>
        /// <param name="pemKey">The OpenSSH PEM key.</param>
        /// <returns>The converted PPPK key.</returns>
        /// <exception cref="NotImplementedException">Thrown when not running on Windows.</exception>
        /// <exception cref="Win32Exception">Thrown if WinSCP could not be executed.</exception>
        public static string ConvertPUBtoPPK(KubeContextExtension kubeLogin, string pemKey)
        {
            if (!NeonHelper.IsWindows)
            {
                throw new NotImplementedException("Not implemented for non-Windows platforms.");
            }

            var programPath = "winscp.com";
            var pemKeyPath  = Path.Combine(KubeHelper.TempFolder, Guid.NewGuid().ToString("D"));
            var ppkKeyPath  = Path.Combine(KubeHelper.TempFolder, Guid.NewGuid().ToString("D"));

            try
            {
                File.WriteAllText(pemKeyPath, pemKey);

                var result = NeonHelper.ExecuteCapture(programPath, $@"/keygen ""{pemKeyPath}"" /comment=""{kubeLogin.ClusterDefinition.Name} Key"" /output=""{ppkKeyPath}""");

                if (result.ExitCode != 0)
                {
                    Console.WriteLine(result.OutputText);
                    Console.Error.WriteLine(result.ErrorText);
                    Program.Exit(result.ExitCode);
                }

                return File.ReadAllText(ppkKeyPath);
            }
            catch (Win32Exception)
            {
                Console.Error.WriteLine($"*** ERROR: Cannot launch [{programPath}].");
                throw;
            }
            finally
            {
                if (File.Exists(pemKeyPath))
                {
                    File.Delete(pemKeyPath);
                }

                if (File.Exists(ppkKeyPath))
                {
                    File.Delete(ppkKeyPath);
                }
            }
        }

        /// <summary>
        /// Executes a command on the local operating system, writing an error and
        /// existing the program if the command fails.
        /// </summary>
        /// <param name="programPath">The program.</param>
        /// <param name="args">The arguments.</param>
        public static void Execute(string programPath, params object[] args)
        {
            var sbArgs = new StringBuilder();

            foreach (var arg in args)
            {
                var argString = arg.ToString();

                if (argString.Contains(" "))
                {
                    argString = "\"" + argString + "\"";
                }

                sbArgs.AppendWithSeparator(argString);
            }

            try
            {
                var result = NeonHelper.ExecuteCapture(programPath, sbArgs.ToString());

                if (result.ExitCode != 0)
                {
                    Console.Error.Write(result.AllText);
                    Program.Exit(result.ExitCode);
                }
            }
            catch (Win32Exception)
            {
                Console.Error.WriteLine($"*** ERROR: Cannot launch [{programPath}].");
                Program.Exit(1);
            }
        }

        /// <summary>
        /// <para>
        /// Recursively executes a <b>neon-cli</b> command by launching a new
        /// instance of the tool with the arguments passed and capturing the
        /// process output streams.
        /// </para>
        /// <note>
        /// This does not recurse into  a container, it simply launches a new
        /// process instance of the program in the current environment with
        /// the arguments passed.
        /// </note>
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns>The process response.</returns>
        public static ExecuteResponse ExecuteRecurseCaptureStreams(params object[] args)
        {
            // We need to prepend the program assembly path to the arguments.

            var argList = new List<object>(args);

            argList.Insert(0, NeonHelper.GetEntryAssemblyPath());

            return NeonHelper.ExecuteCapture("dotnet", argList.ToArray());
        }

        /// <summary>
        /// Verify that the current user has administrator privileges, exiting
        /// the application if this is not the case.
        /// </summary>
        /// <param name="message">Optional message.</param>
        public static void VerifyAdminPrivileges(string message = null)
        {
            if (message == null)
            {
                message = "*** ERROR: This command requires elevated administrator privileges.";
            }
            else
            {
                if (!message.StartsWith("*** ERROR: "))
                {
                    message = $"** ERROR: {message}";
                }
            }

            if (!KubeHelper.InToolContainer)
            {
                if (NeonHelper.IsWindows)
                {
                    var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());

                    if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                    {
                        Console.Error.WriteLine(message);
                        Program.Exit(1);
                    }
                }
                else if (NeonHelper.IsOSX)
                {
                    // $todo(jeff.lill): Implement this
                }
            }
        }

        /// <summary>
        /// Optionally set to the registry to be used to override any explicit or implicit <b>nkubeio</b>
        /// or <b>nkubedev</b> organizations specified when deploying or updating a neonKUBE.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is <c>null</c> by default but may be specified using the <b>--image-reg=REGISTRY</b>
        /// command line option.  The main purpose of this is support development and testing scenarios.
        /// </para>
        /// </remarks>
        public static string DockerImageReg { get; private set; } = null;

        /// <summary>
        /// Optionally set to the tag to be used to override any explicit or implicit <b>:latest</b>
        /// image tags specified when deploying or updating a neonKUBE.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is <c>null</c> by default but may be specified using the <b>--image-tag=TAG</b>
        /// command line option.  The main purpose of this is support development and testing by specifying
        /// something like <b>--image-tag=BRANCH-latest</b>, where <b>BRANCH</b> is the current development
        /// branch.
        /// </para>
        /// <para>
        /// This will direct <b>neon-cli</b> to use images built from the branch rather than the default
        /// production images without needing to modify cluster configuration files.  All the developer
        /// needs to do is ensure that all of the required images were built from that branch first and
        /// then published to Docker Hub.
        /// </para>
        /// </remarks>
        public static string DockerImageTag { get; private set; } = null;

        /// <summary>
        /// Resolves a Docker Image name/tag into the image specification to be actually deployed, taking
        /// the <see cref="DockerImageReg"/> and <see cref="DockerImageTag"/> properties into account.
        /// </summary>
        /// <param name="image">The input image specification.</param>
        /// <returns>The output specification.</returns>
        /// <remarks>
        /// <para>
        /// If <see cref="DockerImageReg"/> is empty and <paramref name="image"/> specifies the 
        /// <see cref="KubeConst.NeonProdRegistry"/> and the Git branch used to build <b>neon-cli</b>
        /// is not <b>PROD</b>, then the image registry will be set to <see cref="KubeConst.NeonDevRegistry"/>.
        /// This ensures that non-production <b>neon-cli </b> builds will use the development Docker
        /// images by default.
        /// </para>
        /// <para>
        /// If <see cref="DockerImageReg"/> is not empty  and <paramref name="image"/> specifies the 
        /// <see cref="KubeConst.NeonProdRegistry"/> then <see cref="DockerImageReg"/> will
        /// replace the registry in the image.
        /// </para>
        /// <para>
        /// If <see cref="DockerImageTag"/> is empty, then this method simply returns the <paramref name="image"/>
        /// argument as passed.  Otherwise, if the image argument implicitly or explicitly specifies the 
        /// <b>:latest</b> tag, then the image returned will be tagged with <see cref="DockerImageTag"/>
        /// when that's not empty or <b>:latest</b> for the <b>PROD</b> branch or <b>:BRANCH-latest</b> 
        /// for non-<b>PROD</b> branches.
        /// </para>
        /// <para>
        /// In all cases where <paramref name="image"/> specifies a non-latest tag, then the argument
        /// will be returned unchanged.
        /// </para>
        /// </remarks>
        public static string ResolveDockerImage(string image)
        {
            if (string.IsNullOrEmpty(image))
            {
                return image;
            }

            // Extract the registry from the image.  Note that this will
            // be empty for official images on Docker Hub.

            var registry = (string)null;
            var p        = image.IndexOf('/');

            if (p != -1)
            {
                registry = image.Substring(0, p);
            }

            if (!string.IsNullOrEmpty(registry) && registry == KubeConst.NeonProdRegistry)
            {
                var imageWithoutRegistry = image.Substring(registry.Length);

                if (!string.IsNullOrEmpty(DockerImageReg))
                {
                    image = DockerImageReg + imageWithoutRegistry;
                }
                else if (!IsRelease)
                {
                    image = KubeConst.NeonDevRegistry + imageWithoutRegistry;
                }
            }

            if (string.IsNullOrEmpty(image))
            {
                return image;
            }

            var normalized = image;

            if (normalized.IndexOf(':') == -1)
            {
                // The image implicitly specifies [:latest].

                normalized += ":latest";
            }

            if (normalized.EndsWith(":latest"))
            {
                if (!string.IsNullOrEmpty(DockerImageTag))
                {
                    return normalized.Replace(":latest", $":{DockerImageTag}");
                }
                else if (IsRelease)
                {
                    return normalized;
                }
                else
                {
                    return normalized.Replace(":latest", $":{ThisAssembly.Git.Branch.ToLowerInvariant()}-latest");
                }
            }
            else
            {
                return image;
            }
        }

        /// <summary>
        /// Searches the directory holding a file as well as any ancestor directories
        /// for the first <b>.password-name</b> file specifying a default password name.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <returns>The default password name if one was found or <c>null</c>.</returns>
        public static string GetDefaultPasswordName(string filePath)
        {
            var folderPath = Path.GetDirectoryName(Path.GetFullPath(filePath));

            try
            {
                while (true)
                {
                    var passwordNamePath = Path.Combine(folderPath, ".password-name");

                    if (File.Exists(passwordNamePath))
                    {
                        var passwordName = File.ReadLines(passwordNamePath).First().Trim();

                        if (passwordName == string.Empty)
                        {
                            // An empty [.password-name] file will block further searching.

                            return null;
                        }

                        return passwordName;
                    }

                    if (Path.GetPathRoot(folderPath) == folderPath)
                    {
                        // We're at the file system root.

                        return null;
                    }

                    // Advance to the parent folder.

                    folderPath = Path.GetFullPath(Path.Combine(folderPath, ".."));
                }
            }
            catch (UnauthorizedAccessException)
            {
                // We will see this if the current user doesn't have permissions to
                // walk the file directories all the way up to the root of the
                // file system.  We'll just return NULL in this case.

                return null;
            }
        }

        /// <summary>
        /// Returns a password based on its name.
        /// </summary>
        /// <param name="passwordName">The password name.</param>
        /// <returns>The password or <c>null</c> if the named password doesn't exist.</returns>
        public static string LookupPassword(string passwordName)
        {
            var passwordPath = Path.Combine(KubeHelper.PasswordsFolder, passwordName);

            if (File.Exists(passwordPath))
            {
                return File.ReadLines(passwordPath).First().Trim();
            }
            else
            {
                return null;
            }
        }
    }
}
