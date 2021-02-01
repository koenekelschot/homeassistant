#!/bin/bash

copy_folder() {
    sshpass -e ssh -o StrictHostKeyChecking=no $SSH_USER "test -d $SSH_FOLDER_HASS/$1 && echo $SSH_PASS | sudo -S rm -r $SSH_FOLDER_HASS/$1"
    echo "Copying folder $1"
    sshpass -e scp -o StrictHostKeyChecking=no -r $1 $SSH_USER:$SSH_FOLDER_HASS/$1
}

copy_file() {
    echo "Copying file $1"
    sshpass -e scp -o StrictHostKeyChecking=no $1 $SSH_USER:$SSH_FOLDER_HASS/$1
}

export SSHPASS=$SSH_PASS

#copy .HA_VERSION to main Docker folder
sshpass -e scp -o StrictHostKeyChecking=no .HA_VERSION $SSH_USER:$SSH_FOLDER_DOCKER/.HA_VERSION

#install hacs
test -d "custom_components/hacs" || mkdir -p "custom_components/hacs"
wget "https://github.com/hacs/integration/releases/latest/download/hacs.zip"
unzip "hacs.zip" -d "custom_components/hacs"
rm "hacs.zip"

copy_folder "custom_components"
copy_folder "packages"
copy_file "automations.yaml"
copy_file "configuration.yaml"
copy_file "groups.yaml"
copy_file "lovelace.yaml"
copy_file "scenes.yaml"
copy_file "scripts.yaml"