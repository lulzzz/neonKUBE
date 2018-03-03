#!/bin/sh
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

. log-info.sh "Starting [neon-registry]"

# Handle the environment variables. 

if [ "${HOSTNAME}" == "" ] ; then
    . log-error.sh "HOSTNAME environment variable is required."
    exit 1
fi

if [ "${PASSWORD}" == "" ] ; then
    . log-error.sh "PASSWORD environment variable is required."
    exit 1
fi

if [ "${READ_ONLY}" == "" ] ; then
    export READ_ONLY=false
fi

if [ "${LOG_LEVEL}" == "" ] ; then
    export LOG_LEVEL=info
fi

. log-info.sh "USERNAME=${USERNAME}"
. log-info.sh "PASSWORD=** REDACTED **"
. log-info.sh "SECRET=** REDACTED **"
. log-info.sh "READ_ONLY=${READ_ONLY}"
. log-info.sh "LOG_LEVEL=${LOG_LEVEL}"

# Add the root directory to the PATH.

PATH=${PATH}:/

# Warn if no external data volume is mounted.

if [ ! -d /var/lib/neon-registry ] ; then
    . log-warn.sh "Expected the registry data volume to mounted at [/var/lib/neon-registry].  Production deployments should not persist images within the container."
    mkdir -p /var/lib/neon-registry
fi

# Generate the registry configuration.

. registry.yml.sh

if [ "${USERNAME}" != "" ] ; then

    # Append the proxy config including the upstream credentials.

    cat <<EOF >> registry.yml
proxy:
    remoteurl: ${REGISTRY}
    username: ${USERNAME}
    password: ${PASSWORD}
EOF

else

    # Append the proxy config.

    cat <<EOF >> registry.yml
proxy:
    remoteurl: ${REGISTRY}
EOF

fi

# Garbage collect if requested.

if [ "${1}" == "garbage-collect" ] ; then    
    registry garbage-collect /registry.yml
    exit $?
fi

# Start the registry.

. log-info.sh "Starting: [registry]"
registry serve registry.yml