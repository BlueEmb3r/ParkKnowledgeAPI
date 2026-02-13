using ParkKnowledgeAPI.Services;

namespace ParkKnowledgeAPI.Tests.Services;

public class QdrantVectorStoreServiceTests
{
    [Fact]
    public void GenerateDeterministicGuid_SameInput_ReturnsSameGuid()
    {
        var guid1 = QdrantVectorStoreService.GenerateDeterministicGuid("acad");
        var guid2 = QdrantVectorStoreService.GenerateDeterministicGuid("acad");
        Assert.Equal(guid1, guid2);
    }

    [Fact]
    public void GenerateDeterministicGuid_DifferentInputs_ReturnDifferentGuids()
    {
        var guid1 = QdrantVectorStoreService.GenerateDeterministicGuid("acad");
        var guid2 = QdrantVectorStoreService.GenerateDeterministicGuid("yell");
        Assert.NotEqual(guid1, guid2);
    }
}
