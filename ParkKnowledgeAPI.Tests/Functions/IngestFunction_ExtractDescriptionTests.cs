using ParkKnowledgeAPI.Functions;

namespace ParkKnowledgeAPI.Tests.Functions;

public class IngestFunction_ExtractDescriptionTests
{
    [Fact]
    public void WithDescriptionSection_ReturnsDescription()
    {
        var content = "Park Name\nState(s): CA\n\nDescription:\nA beautiful park with mountains.";
        var result = IngestFunction.ExtractDescription(content);
        Assert.Contains("A beautiful park with mountains.", result);
    }

    [Fact]
    public void NoDescriptionHeader_ReturnsEntireContent()
    {
        var content = "Park Name\nState(s): CA\nSome info about the park.";
        var result = IngestFunction.ExtractDescription(content);
        Assert.Equal(content, result);
    }

    [Fact]
    public void StopsAtDirections()
    {
        var content = "Park Name\n\nDescription:\nGreat park.\n\nDirections:\nTake I-95 north.";
        var result = IngestFunction.ExtractDescription(content);
        Assert.Contains("Great park.", result);
        Assert.DoesNotContain("Take I-95", result);
    }

    [Fact]
    public void StopsAtOperatingHours()
    {
        var content = "Park Name\n\nDescription:\nWonderful views.\n\nOperating Hours:\nOpen year-round.";
        var result = IngestFunction.ExtractDescription(content);
        Assert.Contains("Wonderful views.", result);
        Assert.DoesNotContain("Open year-round", result);
    }

    [Fact]
    public void StopsAtWeather()
    {
        var content = "Park Name\n\nDescription:\nHistoric site.\n\nWeather:\nMild climate.";
        var result = IngestFunction.ExtractDescription(content);
        Assert.Contains("Historic site.", result);
        Assert.DoesNotContain("Mild climate", result);
    }

    [Fact]
    public void StopsAtSingleWordHeader()
    {
        var content = "Park Name\n\nDescription:\nLovely scenery.\n\nFees:\n$30 per vehicle.";
        var result = IngestFunction.ExtractDescription(content);
        Assert.Contains("Lovely scenery.", result);
        Assert.DoesNotContain("$30 per vehicle", result);
    }

    [Fact]
    public void MultilineDescription_JoinedWithSpaces()
    {
        var content = "Park\n\nDescription:\nLine one.\nLine two.\nLine three.\n\nDirections:\nGo north.";
        var result = IngestFunction.ExtractDescription(content);
        Assert.Contains("Line one.", result);
        Assert.Contains("Line two.", result);
        Assert.Contains("Line three.", result);
        Assert.DoesNotContain("Go north.", result);
    }

    [Fact]
    public void CaseInsensitiveHeader()
    {
        var content = "Park Name\n\ndescription:\nFound via case-insensitive match.\n\nDirections:\nNorth.";
        var result = IngestFunction.ExtractDescription(content);
        Assert.Contains("Found via case-insensitive match.", result);
    }

    [Fact]
    public void EmptyDescriptionSection_ReturnsEntireContent()
    {
        var content = "Park Name\n\nDescription:\n\nDirections:\nTake I-95.";
        var result = IngestFunction.ExtractDescription(content);
        // Empty description between Description: and Directions: yields empty string after trim,
        // so fallback returns entire content
        Assert.Equal(content, result);
    }

    [Fact]
    public void RealParkFile_AcadiaContent()
    {
        var content = """
            Acadia National Park
            State(s): ME

            Description:
            Acadia National Park protects the natural beauty of the highest rocky headlands along the Atlantic coastline of the United States, an abundance of habitats, and a rich cultural heritage. At 4 million visits a year, it's one of the top 10 most-visited national parks in the United States. Visitors enjoy 27 miles of historic motor roads, 158 miles of hiking trails, and 45 miles of carriage roads.

            Directions:
            From Boston take I-95 north to Augusta, Maine, then Route 3 east to Ellsworth, and on to Mount Desert Island.

            Operating Hours:
            Open year-round.
            """;

        var result = IngestFunction.ExtractDescription(content);
        Assert.Contains("Acadia National Park protects the natural beauty", result);
        Assert.Contains("45 miles of carriage roads.", result);
        Assert.DoesNotContain("From Boston", result);
        Assert.DoesNotContain("Open year-round", result);
    }

    [Fact]
    public void ColonInText_DoesNotStopPrematurely()
    {
        var content = "Park\n\nDescription:\nThe park hours are 9:00 AM to 5:00 PM daily.\n\nDirections:\nDrive north.";
        var result = IngestFunction.ExtractDescription(content);
        Assert.Contains("9:00 AM to 5:00 PM daily.", result);
        Assert.DoesNotContain("Drive north.", result);
    }
}
