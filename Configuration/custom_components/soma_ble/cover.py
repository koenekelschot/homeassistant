"""
Support for command line covers.

For more details about this platform, please refer to the documentation at
https://home-assistant.io/components/cover.command_line/
"""
import logging
import asyncio
import re

import voluptuous as vol

from homeassistant.components.cover import (CoverDevice, PLATFORM_SCHEMA,
    ATTR_POSITION)
from homeassistant.const import (
    CONF_COVERS, CONF_FRIENDLY_NAME, CONF_MAC
)
import homeassistant.helpers.config_validation as cv

_LOGGER = logging.getLogger(__name__)

COVER_SCHEMA = vol.Schema({
    vol.Required(CONF_MAC): cv.string,
    vol.Optional(CONF_FRIENDLY_NAME): cv.string,
})

PLATFORM_SCHEMA = PLATFORM_SCHEMA.extend({
    vol.Required(CONF_COVERS): vol.Schema({cv.slug: COVER_SCHEMA}),
})

# Soma Shade BLE Characteristics
# https://github.com/paolotremadio/SOMA-Smart-Shades-HTTP-API/blob/master/control.py
#
# BATTERY_SERVICE_UUID = "0000180f-0000-1000-8000-00805f9b34fb"
# BATTERY_CHARACTERISTIC_UUID = "00002a19-0000-1000-8000-00805f9b34fb"
# MOTOR_SERVICE_UUID = "00001861-b87f-490c-92cb-11ba5ea5167c"
# MOTOR_STATE_CHARACTERISTIC_UUID = "00001525-b87f-490c-92cb-11ba5ea5167c"
# MOTOR_CONTROL_CHARACTERISTIC_UUID = "00001530-b87f-490c-92cb-11ba5ea5167c"
# MOTOR_TARGET_CHARACTERISTIC_UUID = "00001526-b87f-490c-92cb-11ba5ea5167c"
#
# Handles found with "gatttool -b %s -t random --characteristics " %(self._mac)
#
# handle = 0x0002, char properties = 0x0a, char value handle = 0x0003, uuid = 00002a00-0000-1000-8000-00805f9b34fb
# handle = 0x0004, char properties = 0x02, char value handle = 0x0005, uuid = 00002a01-0000-1000-8000-00805f9b34fb
# handle = 0x0006, char properties = 0x02, char value handle = 0x0007, uuid = 00002a04-0000-1000-8000-00805f9b34fb
# handle = 0x0009, char properties = 0x20, char value handle = 0x000a, uuid = 00002a05-0000-1000-8000-00805f9b34fb
# handle = 0x000d, char properties = 0x02, char value handle = 0x000e, uuid = 00002a29-0000-1000-8000-00805f9b34fb
# handle = 0x000f, char properties = 0x02, char value handle = 0x0010, uuid = 00002a25-0000-1000-8000-00805f9b34fb
# handle = 0x0011, char properties = 0x02, char value handle = 0x0012, uuid = 00002a27-0000-1000-8000-00805f9b34fb
# handle = 0x0013, char properties = 0x02, char value handle = 0x0014, uuid = 00002a26-0000-1000-8000-00805f9b34fb
# handle = 0x0015, char properties = 0x02, char value handle = 0x0016, uuid = 00002a28-0000-1000-8000-00805f9b34fb
# handle = 0x0018, char properties = 0x12, char value handle = 0x0019, uuid = 00002a19-0000-1000-8000-00805f9b34fb --> battery
# handle = 0x001c, char properties = 0x1a, char value handle = 0x001d, uuid = 00001555-b87f-490c-92cb-11ba5ea5167c
# handle = 0x0020, char properties = 0x12, char value handle = 0x0021, uuid = 00001525-b87f-490c-92cb-11ba5ea5167c --> motor state
# handle = 0x0023, char properties = 0x0a, char value handle = 0x0024, uuid = 00001526-b87f-490c-92cb-11ba5ea5167c --> motor target
# handle = 0x0025, char properties = 0x0a, char value handle = 0x0026, uuid = 00001527-b87f-490c-92cb-11ba5ea5167c
# handle = 0x0027, char properties = 0x12, char value handle = 0x0028, uuid = 00001528-b87f-490c-92cb-11ba5ea5167c
# handle = 0x002a, char properties = 0x1a, char value handle = 0x002b, uuid = 00001529-b87f-490c-92cb-11ba5ea5167c
# handle = 0x002d, char properties = 0x0a, char value handle = 0x002e, uuid = 00001530-b87f-490c-92cb-11ba5ea5167c --> motor control
# handle = 0x002f, char properties = 0x0a, char value handle = 0x0030, uuid = 00001531-b87f-490c-92cb-11ba5ea5167c
# handle = 0x0031, char properties = 0x0a, char value handle = 0x0032, uuid = 00001533-b87f-490c-92cb-11ba5ea5167c
# handle = 0x0033, char properties = 0x12, char value handle = 0x0034, uuid = 0000ba71-b87f-490c-92cb-11ba5ea5167c
# handle = 0x0037, char properties = 0x12, char value handle = 0x0038, uuid = 0000ba72-b87f-490c-92cb-11ba5ea5167c
# handle = 0x003b, char properties = 0x2a, char value handle = 0x003c, uuid = 00001891-b87f-490c-92cb-11ba5ea5167c
# handle = 0x003e, char properties = 0x0a, char value handle = 0x003f, uuid = 00001892-b87f-490c-92cb-11ba5ea5167c
# handle = 0x0040, char properties = 0x0a, char value handle = 0x0041, uuid = 00001893-b87f-490c-92cb-11ba5ea5167c
# handle = 0x0042, char properties = 0x0a, char value handle = 0x0043, uuid = 00001894-b87f-490c-92cb-11ba5ea5167c

MOTOR_STATE_CHARACTERISTIC_HANDLE = "0x0021"
MOTOR_TARGET_CHARACTERISTIC_HANDLE = "0x0024"
MOTOR_CONTROL_CHARACTERISTIC_HANDLE = "0x002e"
MOTOR_CONTROL_UP = 69
MOTOR_CONTROL_DOWN = 96
MOTOR_CONTROL_STOP = 0

CHARACTERISTIC_READ_REGEX = re.compile(b"(?<=(value/descriptor\:\s))([^\s\,]+)")

def setup_platform(hass, config, add_entities, discovery_info=None):
    """Set up Soma shades controlled by Bluetooth LE."""
    devices = config.get(CONF_COVERS, {})
    covers = []

    for device_name, device_config in devices.items():
        covers.append(
            SomaCover(
                hass,
                device_config.get(CONF_FRIENDLY_NAME, device_name),
                device_config.get(CONF_MAC)
            )
        )

    if not covers:
        _LOGGER.error("No covers added")
        return False

    add_entities(covers)


class SomaCover(CoverDevice):
    """Representation a Soma BLE cover."""

    def __init__(self, hass, name, mac):
        """Initialize the cover."""
        self._hass = hass
        self._name = name
        self._mac = mac
        self._state = None

    async def _async_restart_bluetooth(self):
        """Restart bluetooth"""
        await self._async_execute_command("hciconfig hci0 down", False)
        await self._async_execute_command("hciconfig hci0 up", False)

    async def _async_execute_command(self, command, should_log=True):
        """Execute the actual commands."""
        if should_log:
            _LOGGER.info("Running command: %s", command)

        create_process = asyncio.subprocess.create_subprocess_shell(
                command,
                loop=self._hass.loop,
                stdin=None,
                stdout=asyncio.subprocess.PIPE,
                stderr=asyncio.subprocess.PIPE,
        )
        process = await create_process
        stdout_data, stderr_data = await process.communicate()

        if stdout_data:
            _LOGGER.debug(
                "Stdout of command: `%s`, return code: %s:\n%s",
                command,
                process.returncode,
                stdout_data,
            )
        
        if process.returncode != 0 or stderr_data:
            _LOGGER.exception(
                "Error running command: `%s`, return code: %s, stderr:\n%s", 
                command, 
                process.returncode, 
                stderr_data
            )
        
        return stdout_data

    async def _async_query_state_value(self):
        """Execute state command for return value."""
        await self._async_restart_bluetooth()
        command_result = await self._async_execute_command(
            "gatttool -b %s -t random --char-read --handle=%s" %(self._mac, MOTOR_STATE_CHARACTERISTIC_HANDLE)
        )
        
        if command_result:
            out_val = CHARACTERISTIC_READ_REGEX.findall(command_result)
            _LOGGER.debug(out_val)
            position = self._invert_position(int(out_val[0][1], 16))
            _LOGGER.debug("position = %s", position)
            return position
        else:
            return None
    
    async def _async_control_cover(self, control_value):
        """Execute open / close / stop commands."""
        await self._async_restart_bluetooth()
        await self._async_execute_command(
            "gatttool -b %s -t random --char-write-req --handle=%s --value=%s" %(self._mac, MOTOR_CONTROL_CHARACTERISTIC_HANDLE, control_value)
        )
    
    async def _async_position_cover(self, target_position):
        """Execute command to move cover to requested position"""
        inverted_position = self._invert_position(target_position)
        hex_position = format(inverted_position, "02x")
        #_LOGGER.debug("position target %s = hex %s", target_position, hex_position)
        await self._async_restart_bluetooth()
        await self._async_execute_command(
            "gatttool -b %s -t random --char-write-req --handle=%s --value=%s" %(self._mac, MOTOR_TARGET_CHARACTERISTIC_HANDLE, hex_position)
        )

    def _invert_position(self, position):
        """Soma reports 100 as closed and 0 as opened, whereas homeassistant expects the opposite"""
        return 100 - position

    @property
    def should_poll(self):
        return True

    @property
    def name(self):
        """Return the name of the cover."""
        return self._name

    @property
    def is_closed(self):
        """Return if the cover is closed."""
        if self.current_cover_position is not None:
            return self.current_cover_position == 0

    @property
    def current_cover_position(self):
        """Return current position of cover.
        None is unknown, 0 is closed, 100 is fully open.
        """
        return self._state

    async def async_update(self):
        """Update device state."""
        self._state = await self._async_query_state_value()

    async def async_open_cover(self, **kwargs):
        """Open the cover."""
        await self._async_control_cover(MOTOR_CONTROL_UP)

    async def async_close_cover(self, **kwargs):
        """Close the cover."""
        await self._async_control_cover(MOTOR_CONTROL_DOWN)

    async def async_stop_cover(self, **kwargs):
        """Stop the cover."""
        await self._async_control_cover(MOTOR_CONTROL_STOP)

    async def async_set_cover_position(self, **kwargs):
        """Move the cover to a specific position."""
        if ATTR_POSITION in kwargs:
            position = int(kwargs[ATTR_POSITION])
            if 0 <= position <= 100:
                await self._async_position_cover(position)
            else:
                _LOGGER.warning(
                    "Position is no integer (0-100): %s",
                    position)
                return