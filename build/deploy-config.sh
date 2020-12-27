#!/bin/bash

copy_folder() {
    sshpass -e ssh -o StrictHostKeyChecking=no $SSH_USER "test -d $SSH_FOLDER_HASS/$1 && rm -r $SSH_FOLDER_HASS/$1"
    echo "Copying folder $1"
    sshpass -e scp -o StrictHostKeyChecking=no -r $1 $SSH_USER:$SSH_FOLDER_HASS/$1
}

copy_file() {
    echo "Copying file $1"
    sshpass -e scp -o StrictHostKeyChecking=no $1 $SSH_USER:$SSH_FOLDER_HASS/$1
}

install_hacs() {
    test -d $1 || mkdir -p $1
    wget "https://github.com/hacs/integration/releases/latest/download/hacs.zip"
    unzip "hacs.zip" -d $1
    rm "hacs.zip"
    copy_folder $1
}

# https://unix.stackexchange.com/a/417661
apk update && apk add curl openssh sshpass

export SSHPASS=$SSH_PASS

install_hacs "custom_components/hacs"
copy_folder "custom_components/denonavr"
copy_folder "packages"
copy_file "automations.yaml"
copy_file "configuration.yaml"
copy_file "groups.yaml"
copy_file "scenes.yaml"
copy_file "scripts.yaml"

#restart HA
curl -X POST -H "Authorization: Bearer $HASS_TOKEN" -H "Content-Type: application/json" http://$CI_SERVER_HOST:8123/api/services/homeassistant/restart