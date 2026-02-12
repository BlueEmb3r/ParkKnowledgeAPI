# NPS Park Scraper

Fetches all US national park data from the [NPS API](https://developer.nps.gov/) and saves a `.txt` file per park into `Data/parks/`.

## Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [dotnet-script](https://github.com/dotnet-script/dotnet-script) global tool

## Setup

```bash
dotnet tool install -g dotnet-script
```

## Run

```bash
dotnet script ParkKnowledgeAPI/Scripts/ScrapeNpsParks.csx
```

Output goes to `ParkKnowledgeAPI/Data/parks/` (474 park files).

## What it does

1. Calls `GET /api/v1/parks` with pagination (50 per page)
2. Extracts these fields from each park: full name, state(s), description, directions, operating hours, and weather
3. Saves each park as `{parkCode}.txt` (e.g., `yell.txt`, `acad.txt`)

## API Key

The NPS API key is hardcoded in the script. If it expires, get a new one at https://developer.nps.gov/signup/ and replace the `apiKey` variable.
