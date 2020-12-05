#!/bin/bash

copy_folder() {
    sshpass -e ssh -o StrictHostKeyChecking=no $SSH_USER "test -d $SSH_FOLDER/$1 && rm -r -v $SSH_FOLDER/$1"
    echo "Copying folder $1"
    sshpass -e scp -o StrictHostKeyChecking=no -r $1 $SSH_USER:$SSH_FOLDER/$1
}

copy_file() {
    echo "Copying file $1"
    sshpass -e scp -o StrictHostKeyChecking=no $1 $SSH_USER:$SSH_FOLDER/$1
}

# https://unix.stackexchange.com/a/417661
apk update && apk add curl openssh sshpass

export SSHPASS=$SSH_PASS

copy_folder "packages"
copy_file "automations.yaml"
copy_file "configuration.yaml"
copy_file "groups.yaml"
copy_file "scenes.yaml"
copy_file "scripts.yaml"

#restart HA
curl -X POST -H "Authorization: Bearer $HA_TOKEN" -H "Content-Type: application/json" http://$CI_SERVER_HOST:8123/api/services/homeassistant/restart