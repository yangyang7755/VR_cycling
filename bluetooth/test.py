from bleak import BleakClient
from pycycling.tacx_trainer_control import TacxTrainerControl
# fe_c_reader_fixed.py
import asyncio
import time
import json
from pathlib import Path

address = "B0871262-D228-5DCB-797F-9F6B9D6E7779"

LAST_MEAS = None  # will be assigned by the handler

measurements = []

def fe_c_handler(meas):
    #global LAST_MEAS
    #LAST_MEAS = meas
    measurements.append(meas)


async def main():
    async with BleakClient(address) as client:
        connected = await client.is_connected()
        print("Connected:", connected)
        if not connected:
            print("Failed to connect.")
            return

    services = await client.get_services()
    print("Services discovered:", len(services.services) if hasattr(services, "services") else len(services))

    ttc = TacxTrainerControl(client)
    ttc.set_specific_trainer_data_page_handler(fe_c_handler)
    ttc.set_general_fe_data_page_handler(fe_c_handler)
    ttc.set_command_status_data_page_handler(fe_c_handler)

    # Enable notifications (this will cause TacxTrainerControl._fec_notification_handler to call our handlers)
    await ttc.enable_fec_notifications()
    print("Notifications are enabled")

if __name__ == "__main__":
    asyncio.run(main())