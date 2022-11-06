#!/bin/bash

hass_folder=$SSH_FOLDER_DOCKER/volumes/homeassistant/config

copy_folder() {
    sshpass -e ssh -o StrictHostKeyChecking=no $SSH_USER@$SSH_HOST "test -d $hass_folder/$1 && echo $SSH_PASS | sudo -S rm -r $hass_folder/$1"
    echo "Copying folder $1"
    sshpass -e scp -O -o StrictHostKeyChecking=no -r $1 $SSH_USER@$SSH_HOST:$hass_folder/$1
}

copy_file() {
    echo "Copying file $1"
    sshpass -e scp -O -o StrictHostKeyChecking=no $1 $SSH_USER@$SSH_HOST:$hass_folder/$1
}

export SSHPASS=$SSH_PASS

#copy .HA_VERSION to main Docker folder
sshpass -e scp -O -o StrictHostKeyChecking=no .HA_VERSION $SSH_USER@$SSH_HOST:$SSH_FOLDER_DOCKER/.HA_VERSION

copy_folder "custom_components"
copy_folder "packages"
copy_file "automations.yaml"
copy_file "configuration.yaml"
copy_file "groups.yaml"
copy_file "scenes.yaml"
copy_file "scripts.yaml"