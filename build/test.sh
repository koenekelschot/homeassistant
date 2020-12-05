#!/bin/bash

mv $CI_PROJECT_DIR/fake_secrets.yaml $CI_PROJECT_DIR/secrets.yaml
python -m homeassistant --config $CI_PROJECT_DIR --script check_config