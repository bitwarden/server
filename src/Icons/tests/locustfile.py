import csv
import random
import time

from locust import HttpUser, task, between

class IconLoadTest(HttpUser):
    wait_time = between(1, 2.5)
    host = "http://localhost:50024"

    def __init__(self, environment):
        type(self).icon_sites = type(self).load_icon_sites_from_file("top500domains.csv")
        super().__init__(environment)

    @staticmethod
    def load_icon_sites_from_file(filename):
        icon_sites = []
        with open(filename) as csvfile:
            fieldnames = ["Rank","Root Domain","Linking Root Domains","Domain Authority"]
            csvreader = csv.DictReader(csvfile, fieldnames=fieldnames)
            icon_sites = [ f"www.{row['Root Domain']}" for row in csvreader ]

        return icon_sites

    @task
    def test_icons(self):
        # Having the URL parameter in there at all will prevent caching
        random_site = IconLoadTest.icon_sites[
            random.randint(0, len(IconLoadTest.icon_sites) - 1)
        ]
        self.client.get(
            f"{IconLoadTest.host}/{random_site}/icon.png?cache=false"
        )
