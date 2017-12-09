#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds all of the supported base [neon-log-collector] images and pushes them 
# to Docker Hub.
#
# NOTE: You must be logged into Docker Hub.
#
# Usage: powershell -file ./publish.ps1

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

$registry = "neoncluster/neon-log-collector"

function Build
{
	param
	(
		[switch]$latest = $False
	)

	$registry = "neoncluster/neon-log-collector"
	$tag      = ImageTag

	# Build the images.

	if ($latest)
	{
		./build.ps1 -registry $registry -tag $tag -latest
	}
	else
	{
		./build.ps1 -registry $registry -tag $tag
	}

    PushImage "${registry}:$tag"


	if (IsProd)
	{
		PushImage "${registry}:latest"
	}
}

Build
