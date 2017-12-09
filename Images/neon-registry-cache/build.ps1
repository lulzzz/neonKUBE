﻿#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds a neonCLUSTER [neon-registry-cache] image with the specified version, subversion
# and majorversion.  The image built will be a slightly modified version of the 
# Docker Registry reference.
#
# Usage: powershell -file build.ps1 VERSION [SUBVERSION] [MAJORVERSION] [-latest]

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $registry,
	[parameter(Mandatory=$True,Position=2)][string] $version,
	[parameter(Mandatory=$True,Position=3)][string] $tag,
	[switch]$latest = $False
)

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

"   "
"======================================="
"* NEON-REGISTRY-CACHE " + $version
"======================================="

# Copy the common scripts.

if (Test-Path _common)
{
	Exec { Remove-Item -Recurse _common }
}

Exec { mkdir _common }
Exec { copy ..\_common\*.* .\_common }

# Build the images.

Exec { docker build -t "${registry}:$tag" --build-arg "VERSION=$version" . }

if ($latest)
{
	Exec { docker tag "${registry}:$tag" "${registry}:latest"}
}

# Clean up

sleep 5 # Docker sometimes appears to hold references to files we need
		# to delete so wait for a bit.

Exec { Remove-Item -Recurse _common }
Exec { DeleteFile .rnd }
