# Craigslist Search Result Tracking
Azure Function that periodically scrapes a Craigslist Search Results URL, and writes messages to a Discord Server channel any time a change is detected.

## Settings Configuration

For logging of results to work correctly, you'll need to set up some configuration values in settings.json (And in Application Settings if deploying on Azure):

"ADDED_LISTINGS_DISCORD_WEBHOOK": "{Discord Webhook URL For New Postings}",

"MONITOR_HEALTH_DISCORD_WEBHOOK": "{Discord Webhook URL For Warning/Error Logging}",

"CRAIGSLIST_SEARCH_URL": "{Search URL For Craigslist}" e.g "https://vancouver.craigslist.org/search/sss?query=honda%20civic"
