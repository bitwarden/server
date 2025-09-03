# Bitwarden Database Seeder

A class library for generating and inserting test data.

## Project Structure

The project is organized into these main components:

### Factories

Factories are helper classes for creating domain entities and populating them with realistic data. This assist in
decreasing the amount of boilerplate code needed to create test data in recipes.

### Recipes

Recipes are pre-defined data sets which can be run to generate and load data into the database. They often allow a allow
for a few arguments to customize the data slightly. Recipes should be kept simple and focused on a single task. Default
to creating more recipes rather than adding complexity to existing ones.
