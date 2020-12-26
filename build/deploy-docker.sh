#!/bin/bash

# https://unix.stackexchange.com/a/417661
apk update && apk add curl openssh sshpass

# upload .HA_VERSION to server and trigger build for Docker project
export SSHPASS=$SSH_PASS
sshpass -e scp -o StrictHostKeyChecking=no -r .HA_VERSION $SSH_USER:$SSH_FOLDER_HASS/.HA_VERSION
curl -X POST -F token=$DOCKER_PIPELINE_TOKEN -F ref=master $CI_SERVER_URL/api/v4/projects/$DOCKER_PROJECT_ID/trigger/pipeline