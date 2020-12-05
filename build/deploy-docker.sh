#!/bin/bash

# https://unix.stackexchange.com/a/417661
apk update && apk add curl openssh sshpass

# upload .HA_VERSION to server and trigger build for Docker project
export SSHPASS=$SSH_PASS
sshpass -e scp -o StrictHostKeyChecking=no -r .HA_VERSION $SSH_USER:$SSH_FOLDER/.HA_VERSION
curl -X POST -F token=f76dd8f0fb13505899a73ffea8b858 -F ref=master $CI_SERVER_URL/api/v4/projects/2/trigger/pipeline