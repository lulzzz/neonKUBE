#------------------------------------------------------------------------------
# Configures the environment for a script to execute against a specific cluster.
# This script is intended to be called by other scripts rather than directly
# by the system operator.
#
# usage: powershell -file env.ps1 CLUSTER-NAME [-nologin]
#
# ARGUMENTS:
#
#   CLUSTER-NAME		- Identifies the target cluster definition in the [clusters]
#	    				  subfolder.  Note that this DOES NOT include the [.json]
#						  file extension).  Example: "home-small"
#
#	-nologin			- Pass this if the script is not supposed to log into
#						  the cluster (defaults to FALSE).

param 
(
    [parameter(Mandatory=$True,Position=1)][string] $clusterName,
    [parameter(Mandatory=$False,Position=2)][switch] $nologin = $False
)

# NOTE: This script configures the following environment variables:
#	
#	CLUSTER_NODE_TEMPLATE_USERNAME	- SSH username for the neonCLUSTER node template (like: [sysadmin])
#	CLUSTER_NODE_TEMPLATE_PASSWORD  - SSH password for the neonCLUSTER node template (like: [sysadmin0000])
#	CLUSTER_LOG_FOLDER				- Path to the setup log folder
#	CLUSTER_MAX_PARALLEL			- Maximum setup steps to perform in parallel (like: 10)
#	CLUSTER_LOGIN					- neonCLUSTER login name (like: root@home-small)

# Initialize environment variables.

$env:CLUSTER              = $clusterName
$env:CLUSTER_LOGIN        = "root@$env:CLUSTER"
$env:CLUSTER_SETUP_PATH   = "$env:NF_ROOT\Devops\test"
$env:CLUSTER_MAX_PARALLEL = 10

# Cluster secrets are persisted to the Ansible compatible variables file
# called [secrets.yaml].  This file is encrypted using the [neon-git]
# Ansible password.

$env:SECRETS_PASS = "neon-git"
$env:SECRETS_VARS = "$env:CLUSTER_SETUP_PATH\clusters\$env:CLUSTER\secrets.yaml"

# Cluster secret YAML files need to have Linux-style line endings, so we're
# going to convert these here.

unix-text --recursive $env:CLUSTER_SETUP_PATH\*.yml
unix-text --recursive $env:CLUSTER_SETUP_PATH\*.yaml

# Ensure that the setup log folder exists and is cleared.

$env:CLUSTER_LOG_FOLDER = "D:\VM\cluster-logs\$env:CLUSTER"

if (Test-Path $env:CLUSTER_LOG_FOLDER)
{
	del "$env:CLUSTER_LOG_FOLDER\*.log"
}
else
{
	mkdir "$env:CLUSTER_LOG_FOLDER"
}

if (-not $nologin)
{
	neon login $env:CLUSTER_LOGIN

	if (-not $?)
	{
		exit 1
	}
}