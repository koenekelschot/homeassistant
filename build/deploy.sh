#!/bin/bash

hass_folder=$SSH_FOLDER_DOCKER/volumes/homeassistant/config

rsync -athv .HA_VERSION $SSH_USER@$SSH_HOST:$SSH_FOLDER_DOCKER/.HA_VERSION

echo "Cleaning folder"
ssh $SSH_USER@$SSH_HOST "find $hass_folder -maxdepth 1 -type f -name *.yaml -not -name secrets.yaml -exec rm {} \;"
echo "Cleaning packages folder"
ssh $SSH_USER@$SSH_HOST "find $hass_folder/packages -type f -name *.yaml -exec rm {} \;"
echo "Cleaning custom_components folder"
ssh $SSH_USER@$SSH_HOST "find $hass_folder/custom_components -type f -exec rm {} \;"

echo "Copying yaml files"
for i in `find . -maxdepth 2 -type f -name "*.yaml" -not -name "fake_secrets.yaml" 2>/dev/null`
do
    echo "Copying file $i"
    rsync -athv $i $SSH_USER@$SSH_HOST:$hass_folder/$i
done

echo "Copying custom_components"
for i in `find ./custom_components -type f 2>/dev/null`
do
    echo "Copying file $i"
    rsync -athv $i $SSH_USER@$SSH_HOST:$hass_folder/$i
done