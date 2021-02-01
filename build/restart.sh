#!/bin/bash

curl -X POST -H "Authorization: Bearer $HASS_TOKEN" -H "Content-Type: application/json" http://$CI_SERVER_HOST:8123/api/services/homeassistant/restart